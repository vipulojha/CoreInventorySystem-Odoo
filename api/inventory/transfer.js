const { asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

// POST /api/inventory/transfer
// Body: { from_location_id, to_location_id, product_id, quantity, note }
module.exports = asyncHandler(async (req, res) => {
  if (req.method !== "POST") { res.statusCode = 405; return res.end(); }

  const body = typeof req.body === "object" ? req.body : JSON.parse(req.body || "{}");
  const { from_location_id, to_location_id, product_id, quantity, note } = body;

  if (!from_location_id || !to_location_id || !product_id || !quantity || quantity < 1) {
    return sendJson(res, 400, { ok: false, message: "from_location_id, to_location_id, product_id, and quantity are all required." });
  }
  if (from_location_id === to_location_id) {
    return sendJson(res, 400, { ok: false, message: "Source and destination must be different." });
  }

  await withClient(async (client) => {
    // Get warehouse for from location
    const locRes = await client.query(
      `select warehouse_id from inventory_location where id=$1`, [from_location_id]
    );
    if (locRes.rows.length === 0) throw new Error("Source location not found.");
    const warehouse_id = locRes.rows[0].warehouse_id;

    // Check available stock
    const stockRes = await client.query(
      `select on_hand from stock_balance where warehouse_id=$1 and location_id=$2 and product_id=$3`,
      [warehouse_id, from_location_id, product_id]
    );
    const available = stockRes.rows.length > 0 ? stockRes.rows[0].on_hand : 0;
    if (available < quantity) throw new Error(`Insufficient stock. Available: ${available}, requested: ${quantity}.`);

    // Sequence for reference
    const seqRes = await client.query("select nextval('operation_reference_seq') as seq");
    const ref = `WH/INT/${String(seqRes.rows[0].seq).padStart(4, "0")}`;

    // Create the operation as Done immediately (internal transfer = instant)
    const opRes = await client.query(`
      insert into inventory_operation
        (reference, operation_type, status, warehouse_id, from_location_id, to_location_id, contact_name, delivery_address, schedule_date, notes, created_by_user_id)
      values ($1, 'Adjustment', 'Done', $2, $3, $4, 'Internal Transfer', '', current_date, $5, 1)
      returning id
    `, [ref, warehouse_id, from_location_id, to_location_id, note || "Internal stock transfer"]);
    const opId = opRes.rows[0].id;

    // Create line item
    const lineRes = await client.query(`
      insert into inventory_operation_line (operation_id, product_id, quantity, unit_cost, note)
      select $1, $2, $3, unit_cost, 'Transfer' from product where id=$2
      returning id
    `, [opId, product_id, quantity]);

    // OUT move from source
    await client.query(`
      insert into stock_move
        (operation_id, operation_line_id, reference, product_id, warehouse_id, from_location_id, to_location_id, quantity, move_kind, note, performed_by_user_id)
      values ($1,$2,$3,$4,$5,$6,$7,$8,'OUT','Internal transfer out',1)
    `, [opId, lineRes.rows[0].id, ref, product_id, warehouse_id, from_location_id, to_location_id, quantity]);

    // IN move to destination
    await client.query(`
      insert into stock_move
        (operation_id, operation_line_id, reference, product_id, warehouse_id, from_location_id, to_location_id, quantity, move_kind, note, performed_by_user_id)
      values ($1,$2,$3,$4,$5,$6,$7,$8,'IN','Internal transfer in',1)
    `, [opId, lineRes.rows[0].id, ref, product_id, warehouse_id, from_location_id, to_location_id, quantity]);

    // Update stock balances
    await client.query(`
      update stock_balance set on_hand = on_hand - $1, updated_at=now()
      where warehouse_id=$2 and location_id=$3 and product_id=$4
    `, [quantity, warehouse_id, from_location_id, product_id]);

    await client.query(`
      insert into stock_balance (warehouse_id, location_id, product_id, on_hand, allocated)
      values ($1,$2,$3,$4,0)
      on conflict (warehouse_id, location_id, product_id)
      do update set on_hand = stock_balance.on_hand + $4, updated_at=now()
    `, [warehouse_id, to_location_id, product_id, quantity]);

    return ref;
  }).then(ref => sendJson(res, 200, { ok: true, message: `Internal transfer ${ref} completed.`, ref }));
});
