const { asyncHandler, sendJson, allowMethod } = require("../../lib/api");
const { withClient } = require("../../lib/db");
const { sendOtpEmail } = require("../../lib/email");

module.exports = asyncHandler(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  const pathname = url.pathname;

  // GET /api/inventory/stock -> List stock
  if (req.method === "GET") {
    const stock = await withClient(async (client) => {
      const result = await client.query(`
        select
          w.name as warehouse_name, l.code as location_code,
          p.sku as product_sku, p.name as product_name,
          b.on_hand, b.allocated
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
  }

  // POST /api/inventory/adjust OR /api/inventory/alert-low-stock
  if (req.method === "POST") {
    if (pathname.includes("alert-low-stock")) {
      // ALERT LOGIC
      const THRESHOLD = 10;
      const lowItems = await withClient(async (client) => {
        const r = await client.query(`
          select p.sku, p.name as product_name, w.name as warehouse_name, l.code as location_code, sb.on_hand
          from stock_balance sb
          join product p on p.id = sb.product_id
          join warehouse w on w.id = sb.warehouse_id
          join inventory_location l on l.id = sb.location_id
          where sb.on_hand <= $1 order by sb.on_hand asc
        `, [THRESHOLD]);
        return r.rows;
      });
      if (lowItems.length === 0) return sendJson(res, 200, { ok: true, message: "No low-stock items found." });
      
      const adminEmail = process.env.SMTP_USER;
      if (!adminEmail) return sendJson(res, 500, { ok: false, message: "SMTP_USER not configured." });
      const rows = lowItems.map(i => `  • [${i.sku}] ${i.product_name} — ${i.warehouse_name}/${i.location_code}: ${i.on_hand} units`).join("\n");
      const body = `Low Stock Alert\n\n${rows}`;
      
      const nodemailer = require("nodemailer");
      const transporter = nodemailer.createTransport({
        host: process.env.SMTP_HOST, port: parseInt(process.env.SMTP_PORT || "587"),
        secure: process.env.SMTP_SECURE === "true",
        auth: { user: process.env.SMTP_USER, pass: process.env.SMTP_PASS }
      });
      await transporter.sendMail({ from: process.env.SMTP_FROM || adminEmail, to: adminEmail, subject: "⚠️ Low Stock Alert", text: body });
      return sendJson(res, 200, { ok: true, message: `Alert sent for ${lowItems.length} items.` });
    } else {
      // ADJUST LOGIC
      const body = typeof req.body === "object" ? req.body : JSON.parse(req.body || "{}");
      const { product_id, location_id, actual_qty, note } = body;
      if (!product_id || !location_id || actual_qty === undefined) return sendJson(res, 400, { ok: false, message: "Missing fields" });

      const result = await withClient(async (client) => {
        const locRes = await client.query(`select warehouse_id from inventory_location where id=$1`, [location_id]);
        if (locRes.rows.length === 0) throw new Error("Location not found.");
        const warehouse_id = locRes.rows[0].warehouse_id;

        const currentRes = await client.query(`select on_hand from stock_balance where warehouse_id=$1 and location_id=$2 and product_id=$3`, [warehouse_id, location_id, product_id]);
        const current = currentRes.rows.length > 0 ? currentRes.rows[0].on_hand : 0;
        const diff = actual_qty - current;
        if (diff === 0) throw new Error("No adjustment needed.");

        const seqRes = await client.query("select nextval('operation_reference_seq') as seq");
        const ref = `WH/ADJ/${String(seqRes.rows[0].seq).padStart(4, "0")}`;
        const moveKind = diff > 0 ? "IN" : "OUT";

        await client.query(`insert into stock_move (reference, product_id, warehouse_id, from_location_id, to_location_id, quantity, move_kind, note, performed_by_user_id) values ($1,$2,$3,$4,$5,$6,$7,$8,1)`, 
          [ref, product_id, warehouse_id, diff < 0 ? location_id : null, diff > 0 ? location_id : null, Math.abs(diff), moveKind, note || `Manual adjustment`]);

        await client.query(`insert into stock_balance (warehouse_id, location_id, product_id, on_hand, allocated) values ($1,$2,$3,$4,0) on conflict (warehouse_id, location_id, product_id) do update set on_hand=$4, updated_at=now()`, [warehouse_id, location_id, product_id, actual_qty]);
        return { ref, diff, moveKind };
      });
      return sendJson(res, 200, { ok: true, message: `Adjustment ${result.ref} recorded.` });
    }
  }

  res.statusCode = 405;
  res.end();
});
