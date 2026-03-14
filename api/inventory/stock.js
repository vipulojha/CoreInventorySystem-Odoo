const { allowMethod, asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

module.exports = asyncHandler(async (req, res) => {
  if (!allowMethod(req, res, "GET")) {
    return;
  }

  const stock = await withClient(async (client) => {
    const result = await client.query(`
      select
        w.name as warehouse_name,
        l.code as location_code,
        p.sku as product_sku,
        p.name as product_name,
        b.on_hand,
        b.allocated
      from stock_balance b
      join warehouse w on w.id = b.warehouse_id
      join inventory_location l on l.id = b.location_id
      join product p on p.id = b.product_id
      order by w.name, l.code, p.name
      limit 500
    `);
    return result.rows;
  });

  return sendJson(res, 200, stock);
});
