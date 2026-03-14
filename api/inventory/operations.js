const { allowMethod, asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

module.exports = asyncHandler(async (req, res) => {
  if (!allowMethod(req, res, "GET")) {
    return;
  }

  const operations = await withClient(async (client) => {
    const result = await client.query(`
      select reference, operation_type, contact_name, schedule_date, status
      from inventory_operation
      order by schedule_date desc
      limit 100
    `);
    return result.rows;
  });

  return sendJson(res, 200, operations);
});
