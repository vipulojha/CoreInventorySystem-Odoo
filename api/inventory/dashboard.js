const { allowMethod, asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

module.exports = asyncHandler(async (req, res) => {
  if (!allowMethod(req, res, "GET")) {
    return;
  }

  const data = await withClient(async (client) => {
    const products = await client.query("select count(*) as count from product");
    const lowStock = await client.query(`
      select count(*) as count from stock_balance
      where on_hand <= 10
    `);
    const receipts = await client.query("select count(*) as count from inventory_operation where operation_type = 'Receipt' and status != 'Done'");
    const deliveries = await client.query("select count(*) as count from inventory_operation where operation_type = 'Delivery' and status != 'Done'");

    return {
      products: parseInt(products.rows[0].count, 10),
      lowStock: parseInt(lowStock.rows[0].count, 10),
      receipts: parseInt(receipts.rows[0].count, 10),
      deliveries: parseInt(deliveries.rows[0].count, 10)
    };
  });

  return sendJson(res, 200, data);
});
