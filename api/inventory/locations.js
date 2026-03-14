const { allowMethod, asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

module.exports = asyncHandler(async (req, res) => {
  if (req.method === "GET") {
    const rows = await withClient(async (client) => {
      const r = await client.query(`
        select l.id, l.code, l.name, l.kind, w.name as warehouse_name, l.warehouse_id
        from inventory_location l
        join warehouse w on w.id = l.warehouse_id
        order by w.name, l.code asc
      `);
      return r.rows;
    });
    return sendJson(res, 200, rows);
  }

  if (req.method === "POST") {
    const body = typeof req.body === "object" ? req.body : JSON.parse(req.body || "{}");
    const { code, name, warehouse_id, kind } = body;
    if (!code || !name || !warehouse_id) return sendJson(res, 400, { ok: false, message: "Code, name, and warehouse are required." });

    await withClient(async (client) => {
      await client.query(
        `insert into inventory_location (code, name, warehouse_id, kind) values ($1, $2, $3, $4)`,
        [code.toUpperCase(), name, warehouse_id, kind || "Stock"]
      );
    });
    return sendJson(res, 201, { ok: true, message: "Location created." });
  }

  res.statusCode = 405;
  res.end();
});
