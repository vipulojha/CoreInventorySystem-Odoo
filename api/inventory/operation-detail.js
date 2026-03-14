const { asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

// GET /api/inventory/operation-detail?ref=WH/IN/0001
// Returns full operation with its line items
module.exports = asyncHandler(async (req, res) => {
  const url = new URL(req.url, "http://localhost");
  const ref = url.searchParams.get("ref");

  if (!ref) return sendJson(res, 400, { ok: false, message: "ref query param required." });

  const result = await withClient(async (client) => {
    const opRes = await client.query(`
      select
        op.id, op.reference, op.operation_type, op.status,
        op.contact_name, op.delivery_address, op.schedule_date, op.notes,
        w.name as warehouse_name,
        fl.code as from_location, tl.code as to_location
      from inventory_operation op
      join warehouse w on w.id = op.warehouse_id
      left join inventory_location fl on fl.id = op.from_location_id
      left join inventory_location tl on tl.id = op.to_location_id
      where op.reference = $1
    `, [ref]);

    if (opRes.rows.length === 0) return null;
    const op = opRes.rows[0];

    const linesRes = await client.query(`
      select
        ol.id, ol.quantity, ol.unit_cost, ol.note,
        p.sku, p.name as product_name,
        coalesce(sb.on_hand, 0) as on_hand
      from inventory_operation_line ol
      join product p on p.id = ol.product_id
      left join stock_balance sb on sb.product_id = ol.product_id and sb.warehouse_id = (
        select warehouse_id from inventory_operation where id = ol.operation_id
      )
      where ol.operation_id = $1
    `, [op.id]);

    return { ...op, lines: linesRes.rows };
  });

  if (!result) return sendJson(res, 404, { ok: false, message: "Operation not found." });
  return sendJson(res, 200, result);
});
