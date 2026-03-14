const { allowMethod, asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

module.exports = asyncHandler(async (req, res) => {
  if (req.method === "GET") {
    const rows = await withClient(async (client) => {
      const r = await client.query(
        `select id, code, name, address from warehouse order by name asc`
      );
      return r.rows;
    });
    return sendJson(res, 200, rows);
  }

  if (req.method === "POST") {
    const body = typeof req.body === "object" ? req.body : JSON.parse(req.body || "{}");
    const { code, name, address } = body;
    if (!code || !name) return sendJson(res, 400, { ok: false, message: "Code and name are required." });

    await withClient(async (client) => {
      await client.query(
        `insert into warehouse (code, name, address) values ($1, $2, $3)`,
        [code.toUpperCase(), name, address || ""]
      );
    });
    return sendJson(res, 201, { ok: true, message: "Warehouse created." });
  }

  res.statusCode = 405;
  res.end();
});
