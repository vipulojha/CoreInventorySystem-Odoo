const { allowMethod, asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

module.exports = asyncHandler(async (req, res) => {
  if (!allowMethod(req, res, "GET")) {
    return;
  }

  const products = await withClient(async (client) => {
    const result = await client.query(`
      select sku, name, unit_cost
      from product
      order by name asc
      limit 100
    `);
    return result.rows;
  });

  return sendJson(res, 200, products);
});
