const { asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

// POST /api/inventory/buy
// Body: { product_id, quantity, note }
// Creates a pending Delivery receipt request for the user
module.exports = asyncHandler(async (req, res) => {
  if (req.method !== "POST") {
    res.statusCode = 405;
    return res.end();
  }

  const body = typeof req.body === "object" ? req.body : JSON.parse(req.body || "{}");
  const { product_id, quantity, action, note } = body;

  if (!product_id || !quantity || quantity < 1) {
    return sendJson(res, 400, { ok: false, message: "Product and quantity are required." });
  }

  if (!["buy", "discard"].includes(action)) {
    return sendJson(res, 400, { ok: false, message: "Action must be 'buy' or 'discard'." });
  }

  await withClient(async (client) => {
    // Get product info
    const prodRes = await client.query("select id, sku, name from product where id=$1", [product_id]);
    if (prodRes.rows.length === 0) throw new Error("Product not found.");
    const product = prodRes.rows[0];

    // Get default warehouse and location
    const whRes = await client.query("select id from warehouse order by id asc limit 1");
    const locRes = await client.query("select id from inventory_location where warehouse_id=$1 and kind='Stock' order by id asc limit 1", [whRes.rows[0].id]);
    const warehouseId = whRes.rows[0].id;
    const locationId = locRes.rows[0].id;

    // Auto-generate reference
    const seqRes = await client.query("select nextval('operation_reference_seq') as seq");
    const seq = String(seqRes.rows[0].seq).padStart(4, "0");

    if (action === "buy") {
      // Create a Receipt operation in Draft state
      const ref = `WH/IN/${seq}`;
      const opRes = await client.query(`
        insert into inventory_operation
          (reference, operation_type, status, warehouse_id, to_location_id, contact_name, delivery_address, schedule_date, notes, created_by_user_id)
        values ($1, 'Receipt', 'Draft', $2, $3, 'User Order', '', current_date, $4, 1)
        returning id
      `, [ref, warehouseId, locationId, note || `Buy request for ${product.name}`]);

      await client.query(`
        insert into inventory_operation_line (operation_id, product_id, quantity, unit_cost, note)
        select $1, $2, $3, unit_cost, 'User buy request'
        from product where id=$2
      `, [opRes.rows[0].id, product_id, quantity]);

      return { ref, type: "Receipt" };
    } else {
      // Discard: create Adjustment out-move directly
      const ref = `WH/DISC/${seq}`;
      await client.query(`
        insert into stock_move
          (reference, product_id, warehouse_id, from_location_id, quantity, move_kind, note, performed_by_user_id)
        values ($1, $2, $3, $4, $5, 'OUT', $6, 1)
      `, [ref, product_id, warehouseId, locationId, quantity, note || `Discard of ${product.name}`]);

      // Reduce stock balance
      await client.query(`
        update stock_balance
        set on_hand = greatest(0, on_hand - $1), updated_at = now()
        where warehouse_id=$2 and location_id=$3 and product_id=$4
      `, [quantity, warehouseId, locationId, product_id]);

      return { ref, type: "Discard" };
    }
  }).then(result => {
    return sendJson(res, 200, { ok: true, message: `${result.type} created: ${result.ref}`, ref: result.ref });
  });
});
