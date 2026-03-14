const { asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

module.exports = asyncHandler(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  const pathname = url.pathname;

  // GET Metadata
  if (req.method === "GET") {
    const data = await withClient(async (client) => {
      if (pathname.includes("warehouses")) {
        return (await client.query(`select id, code, name, address from warehouse order by name asc`)).rows;
      } else {
        return (await client.query(`select l.id, l.code, l.name, l.kind, w.name as warehouse_name, l.warehouse_id from inventory_location l join warehouse w on w.id = l.warehouse_id order by w.name, l.code asc`)).rows;
      }
    });
    return sendJson(res, 200, data);
  }

  // POST Metadata
  if (req.method === "POST") {
    const body = typeof req.body === "object" ? req.body : JSON.parse(req.body || "{}");
    if (pathname.includes("warehouses")) {
      await withClient(async client => client.query(`insert into warehouse (code, name, address) values ($1, $2, $3)`, [body.code.toUpperCase(), body.name, body.address || ""]));
    } else {
      await withClient(async client => client.query(`insert into inventory_location (code, name, warehouse_id, kind) values ($1, $2, $3, $4)`, [body.code.toUpperCase(), body.name, body.warehouse_id, body.kind || "Stock"]));
    }
    return sendJson(res, 201, { ok: true });
  }

  res.statusCode = 405; res.end();
});
