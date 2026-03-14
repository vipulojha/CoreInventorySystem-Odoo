const { allowMethod, asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

module.exports = asyncHandler(async (req, res) => {
  if (!allowMethod(req, res, "GET")) return;

  const moves = await withClient(async (client) => {
    const result = await client.query(`
      select
        sm.reference,
        sm.move_kind,
        sm.quantity,
        p.sku as product_sku,
        p.name as product_name,
        fl.code as from_loc,
        tl.code as to_loc,
        w.name as warehouse_name,
        sm.event_at,
        sm.note
      from stock_move sm
      join product p on p.id = sm.product_id
      join warehouse w on w.id = sm.warehouse_id
      left join inventory_location fl on fl.id = sm.from_location_id
      left join inventory_location tl on tl.id = sm.to_location_id
      order by sm.event_at desc
      limit 200
    `);
    return result.rows;
  });

  return sendJson(res, 200, moves);
});
