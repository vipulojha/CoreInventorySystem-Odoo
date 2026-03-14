const { asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

module.exports = asyncHandler(async (req, res) => {
  // GET /api/inventory/products
  if (req.method === "GET") {
    const rows = await withClient(async (client) => {
      const r = await client.query(
        `select id, sku, name, unit_cost from product order by name asc limit 500`
      );
      return r.rows;
    });
    return sendJson(res, 200, rows);
  }

  // POST /api/inventory/products  → create
  if (req.method === "POST") {
    const body = typeof req.body === "object" ? req.body : JSON.parse(req.body || "{}");
    const { sku, name, unit_cost } = body;
    if (!sku || !name) return sendJson(res, 400, { ok: false, message: "SKU and name are required." });

    await withClient(async (client) => {
      await client.query(
        `insert into product (sku, name, unit_cost) values ($1, $2, $3)`,
        [sku.toUpperCase(), name, parseFloat(unit_cost) || 0]
      );
    });
    return sendJson(res, 201, { ok: true, message: "Product created." });
  }

  // PUT /api/inventory/products  → update (expects {id, sku, name, unit_cost})
  if (req.method === "PUT") {
    const body = typeof req.body === "object" ? req.body : JSON.parse(req.body || "{}");
    const { id, sku, name, unit_cost } = body;
    if (!id || !sku || !name) return sendJson(res, 400, { ok: false, message: "ID, SKU and name are required." });

    await withClient(async (client) => {
      await client.query(
        `update product set sku=$1, name=$2, unit_cost=$3, updated_at=now() where id=$4`,
        [sku.toUpperCase(), name, parseFloat(unit_cost) || 0, id]
      );
    });
    return sendJson(res, 200, { ok: true, message: "Product updated." });
  }

  // DELETE /api/inventory/products?id=X
  if (req.method === "DELETE") {
    const url = new URL(req.url, `http://localhost`);
    const id = url.searchParams.get("id");
    if (!id) return sendJson(res, 400, { ok: false, message: "Product ID is required." });

    await withClient(async (client) => {
      await client.query(`delete from product where id=$1`, [id]);
    });
    return sendJson(res, 200, { ok: true, message: "Product deleted." });
  }

  res.statusCode = 405;
  res.end();
});
