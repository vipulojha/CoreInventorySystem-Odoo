const { asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");
const jwt = require("jsonwebtoken");

function getUserIdFromReq(req) {
  const authHeader = req.headers && req.headers["authorization"];
  if (!authHeader) return null;
  const token = authHeader.replace("Bearer ", "").trim();
  try {
    const payload = jwt.verify(token, process.env.AUTH_JWT_SECRET);
    return payload.sub;
  } catch {
    return null;
  }
}

// GET /api/inventory/my-orders
module.exports = asyncHandler(async (req, res) => {
  if (req.method !== "GET") { res.statusCode = 405; return res.end(); }

  const userId = getUserIdFromReq(req);
  if (!userId) return sendJson(res, 401, { ok: false, message: "Authentication required." });

  const rows = await withClient(async (client) => {
    const r = await client.query(`
      select
        op.reference, op.operation_type, op.status, op.contact_name,
        op.schedule_date, op.notes, op.created_at,
        count(ol.id) as line_count,
        sum(ol.quantity * ol.unit_cost) as total_value
      from inventory_operation op
      left join inventory_operation_line ol on ol.operation_id = op.id
      where op.created_by_user_id = $1
      group by op.id
      order by op.created_at desc
      limit 100
    `, [userId]);
    return r.rows;
  });

  return sendJson(res, 200, rows);
});
