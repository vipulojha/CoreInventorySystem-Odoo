const { asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

// POST /api/inventory/adjust
// Body: { product_id, location_id, actual_qty, note }
module.exports = asyncHandler(async (req, res) => {
  if (req.method !== "POST") { res.statusCode = 405; return res.end(); }

  const body = typeof req.body === "object" ? req.body : JSON.parse(req.body || "{}");
  const { product_id, location_id, actual_qty, note } = body;

  if (!product_id || !location_id || actual_qty === undefined || actual_qty < 0) {
    return sendJson(res, 400, { ok: false, message: "product_id, location_id, and actual_qty are required." });
  }

  await withClient(async (client) => {
    const locRes = await client.query(`select warehouse_id from inventory_location where id=$1`, [location_id]);
    if (locRes.rows.length === 0) throw new Error("Location not found.");
    const warehouse_id = locRes.rows[0].warehouse_id;

    // Get current on_hand
    const currentRes = await client.query(
      `select on_hand from stock_balance where warehouse_id=$1 and location_id=$2 and product_id=$3`,
      [warehouse_id, location_id, product_id]
    );
    const current = currentRes.rows.length > 0 ? currentRes.rows[0].on_hand : 0;
    const diff = actual_qty - current;
    if (diff === 0) throw new Error("Actual quantity matches system quantity. No adjustment needed.");

    const seqRes = await client.query("select nextval('operation_reference_seq') as seq");
    const ref = `WH/ADJ/${String(seqRes.rows[0].seq).padStart(4, "0")}`;
    const moveKind = diff > 0 ? "IN" : "OUT";

    // Log the adjustment move
    await client.query(`
      insert into stock_move
        (reference, product_id, warehouse_id, from_location_id, to_location_id, quantity, move_kind, note, performed_by_user_id)
      values ($1,$2,$3,$4,$5,$6,$7,$8,1)
    `, [ref, product_id, warehouse_id,
        diff < 0 ? location_id : null,
        diff > 0 ? location_id : null,
        Math.abs(diff), moveKind,
        note || `Manual count adjustment. Was: ${current}, Set to: ${actual_qty}`]);

    // Update balance
    await client.query(`
      insert into stock_balance (warehouse_id, location_id, product_id, on_hand, allocated)
      values ($1,$2,$3,$4,0)
      on conflict (warehouse_id, location_id, product_id)
      do update set on_hand=$4, updated_at=now()
    `, [warehouse_id, location_id, product_id, actual_qty]);

    return { ref, diff, moveKind };
  }).then(r => sendJson(res, 200, { ok: true, message: `Adjustment ${r.ref} recorded (${r.moveKind} ${Math.abs(r.diff)} units).` }));
});
