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
  } catch { return null; }
}

module.exports = asyncHandler(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  const pathname = url.pathname;

  // GET /api/inventory/my-orders
  if (req.method === "GET" && pathname.includes("my-orders")) {
    const userId = getUserIdFromReq(req);
    if (!userId) return sendJson(res, 401, { ok: false, message: "Authentication required." });
    const rows = await withClient(async (client) => {
      const r = await client.query(`select op.reference, op.operation_type, op.status, op.contact_name, op.schedule_date, op.notes, op.created_at, count(ol.id) as line_count, sum(ol.quantity * ol.unit_cost) as total_value from inventory_operation op left join inventory_operation_line ol on ol.operation_id = op.id where op.created_by_user_id = $1 group by op.id order by op.created_at desc limit 100`, [userId]);
      return r.rows;
    });
    return sendJson(res, 200, rows);
  }

  // GET /api/inventory/operations -> List
  if (req.method === "GET" && !pathname.includes("operation-detail")) {
    const operations = await withClient(async (client) => {
      const result = await client.query(`select reference, operation_type, contact_name, schedule_date, status from inventory_operation order by schedule_date desc limit 100`);
      return result.rows;
    });
    return sendJson(res, 200, operations);
  }

  // GET /api/inventory/operation-detail?ref=...
  if (req.method === "GET" && pathname.includes("operation-detail")) {
    const ref = url.searchParams.get("ref");
    if (!ref) return sendJson(res, 400, { ok: false, message: "ref required" });
    const result = await withClient(async (client) => {
      const opRes = await client.query(`select op.id, op.reference, op.operation_type, op.status, op.contact_name, op.delivery_address, op.schedule_date, op.notes, w.name as warehouse_name, fl.code as from_location, tl.code as to_location from inventory_operation op join warehouse w on w.id = op.warehouse_id left join inventory_location fl on fl.id = op.from_location_id left join inventory_location tl on tl.id = op.to_location_id where op.reference = $1`, [ref]);
      if (opRes.rows.length === 0) return null;
      const op = opRes.rows[0];
      const linesRes = await client.query(`select ol.id, ol.quantity, ol.unit_cost, ol.note, p.sku, p.name as product_name, coalesce(sb.on_hand, 0) as on_hand from inventory_operation_line ol join product p on p.id = ol.product_id left join stock_balance sb on sb.product_id = ol.product_id and sb.warehouse_id = (select warehouse_id from inventory_operation where id = ol.operation_id) where ol.operation_id = $1`, [op.id]);
      return { ...op, lines: linesRes.rows };
    });
    if (!result) return sendJson(res, 404, { ok: false, message: "Not found" });
    return sendJson(res, 200, result);
  }

  // POST Logic (Transition, Buy, Transfer)
  if (req.method === "POST") {
    const body = typeof req.body === "object" ? req.body : JSON.parse(req.body || "{}");
    
    if (pathname.includes("transition")) {
      const { ref, action } = body;
      await withClient(async (client) => {
        const opRes = await client.query(`select id, status, operation_type, warehouse_id, from_location_id, to_location_id from inventory_operation where reference = $1`, [ref]);
        if (opRes.rows.length === 0) throw new Error("Op not found");
        const op = opRes.rows[0];
        let newStatus = action === "cancel" ? "Cancelled" : (op.status === "Draft" || op.status === "Waiting" ? "Ready" : (op.status==="Ready" ? "Done" : null));
        if (!newStatus) throw new Error("Invalid transition");
        if (newStatus === "Done") {
          const lines = await client.query(`select ol.id, ol.product_id, ol.quantity from inventory_operation_line ol where ol.operation_id = $1`, [op.id]);
          for (const line of lines.rows) {
            const userId = getUserIdFromReq(req) || 1;
            await client.query(`insert into stock_move (operation_id, operation_line_id, reference, product_id, warehouse_id, from_location_id, to_location_id, quantity, move_kind, performed_by_user_id, note) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'Validated')`, [op.id, line.id, ref, line.product_id, op.warehouse_id, op.from_location_id, op.to_location_id, line.quantity, mk, userId]);
            if (mk === "IN") await client.query(`insert into stock_balance (warehouse_id, location_id, product_id, on_hand, allocated) values ($1, $2, $3, $4, 0) on conflict (warehouse_id, location_id, product_id) do update set on_hand = stock_balance.on_hand + $4, updated_at = now()`, [op.warehouse_id, op.to_location_id, line.product_id, line.quantity]);
            else if (mk === "OUT") await client.query(`update stock_balance set on_hand = greatest(0, on_hand - $1), updated_at = now() where warehouse_id=$2 and location_id=$3 and product_id=$4`, [line.quantity, op.warehouse_id, op.from_location_id, line.product_id]);
          }
        }
        await client.query(`update inventory_operation set status=$1, updated_at=now() where id=$2`, [newStatus, op.id]);
      });
      return sendJson(res, 200, { ok: true, message: "Transitioned" });
    }

    if (pathname.includes("buy")) {
      const { product_id, quantity, action, note } = body;
      const result = await withClient(async (client) => {
        const prodRes = await client.query("select id, sku, name from product where id=$1", [product_id]);
        if (prodRes.rows.length === 0) throw new Error("Product not found");
        const product = prodRes.rows[0];
        
        const whRes = await client.query("select id from warehouse order by id asc limit 1");
        const warehouseId = whRes.rows[0].id;
        const locRes = await client.query("select id from inventory_location where warehouse_id=$1 and kind='Stock' order by id asc limit 1", [warehouseId]);
        const locationId = locRes.rows[0].id;

        const seqRes = await client.query("select nextval('operation_reference_seq') as seq");
        const seq = String(seqRes.rows[0].seq).padStart(4, "0");
        const userId = getUserIdFromReq(req) || 1; // Fallback for emergency, but should be JWT

        if (action === "buy") {
           const ref = `WH/IN/${seq}`;
           const opId = (await client.query(`insert into inventory_operation (reference, operation_type, status, warehouse_id, to_location_id, contact_name, schedule_date, notes, created_by_user_id) values ($1, 'Receipt', 'Draft', $2, $3, 'User Order', current_date, $4, $5) returning id`, [ref, warehouseId, locationId, note, userId])).rows[0].id;
           await client.query(`insert into inventory_operation_line (operation_id, product_id, quantity, unit_cost, note) select $1, $2, $3, unit_cost, 'User buy' from product where id=$2`, [opId, product_id, quantity]);
           return { ref, type: "Receipt" };
        } else {
           const ref = `WH/DISC/${seq}`;
           await client.query(`insert into stock_move (reference, product_id, warehouse_id, from_location_id, quantity, move_kind, note, performed_by_user_id) values ($1, $2, $3, $4, $5, 'OUT', $6, $7)`, [ref, product_id, warehouseId, locationId, quantity, note, userId]);
           await client.query(`update stock_balance set on_hand = greatest(0, on_hand - $1), updated_at = now() where warehouse_id=$2 and location_id=$3 and product_id=$4`, [quantity, warehouseId, locationId, product_id]);
           return { ref, type: "Discard" };
        }
      });
      return sendJson(res, 200, { ok: true, ref: result.ref });
    }

    if (pathname.includes("transfer")) {
      const { from_location_id, to_location_id, product_id, quantity, note } = body;
      const ref = await withClient(async (client) => {
        const warehouse_id = (await client.query(`select warehouse_id from inventory_location where id=$1`, [from_location_id])).rows[0].warehouse_id;
        const seqRes = await client.query("select nextval('operation_reference_seq') as seq");
        const userId = getUserIdFromReq(req) || 1;
        const ref = `WH/INT/${String(seqRes.rows[0].seq).padStart(4, "0")}`;
        const opId = (await client.query(`insert into inventory_operation (reference, operation_type, status, warehouse_id, from_location_id, to_location_id, contact_name, schedule_date, notes, created_by_user_id) values ($1, 'Adjustment', 'Done', $2, $3, $4, 'Internal', current_date, $5, $6) returning id`, [ref, warehouse_id, from_location_id, to_location_id, note, userId])).rows[0].id;
        const lineId = (await client.query(`insert into inventory_operation_line (operation_id, product_id, quantity, unit_cost, note) select $1, $2, $3, unit_cost, 'Transfer' from product where id=$2 returning id`, [opId, product_id, quantity])).rows[0].id;
        await client.query(`insert into stock_move (operation_id, operation_line_id, reference, product_id, warehouse_id, from_location_id, to_location_id, quantity, move_kind, note, performed_by_user_id) values ($1,$2,$3,$4,$5,$6,$7,$8,'OUT','TR-OUT',$9), ($1,$2,$3,$4,$5,$6,$7,$8,'IN','TR-IN',$9)`, [opId, lineId, ref, product_id, warehouse_id, from_location_id, to_location_id, quantity, userId]);
        await client.query(`update stock_balance set on_hand = on_hand - $1 where warehouse_id=$2 and location_id=$3 and product_id=$4`, [quantity, warehouse_id, from_location_id, product_id]);
        await client.query(`insert into stock_balance (warehouse_id, location_id, product_id, on_hand) values ($1,$2,$3,$4) on conflict (warehouse_id, location_id, product_id) do update set on_hand = stock_balance.on_hand + $4`, [warehouse_id, to_location_id, product_id, quantity]);
        return ref;
      });
      return sendJson(res, 200, { ok: true, ref });
    }
  }

  res.statusCode = 405; res.end();
});
