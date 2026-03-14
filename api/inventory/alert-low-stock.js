const { asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");
const { sendOtpEmail } = require("../../lib/email");

// POST /api/inventory/alert-low-stock
// Triggered manually or via cron. Sends admin an email of low-stock items.
module.exports = asyncHandler(async (req, res) => {
  if (req.method !== "POST") { res.statusCode = 405; return res.end(); }

  const THRESHOLD = 10;

  const lowItems = await withClient(async (client) => {
    const r = await client.query(`
      select
        p.sku, p.name as product_name,
        w.name as warehouse_name, l.code as location_code,
        sb.on_hand
      from stock_balance sb
      join product p on p.id = sb.product_id
      join warehouse w on w.id = sb.warehouse_id
      join inventory_location l on l.id = sb.location_id
      where sb.on_hand <= $1
      order by sb.on_hand asc
    `, [THRESHOLD]);
    return r.rows;
  });

  if (lowItems.length === 0) {
    return sendJson(res, 200, { ok: true, message: "No low-stock items found. No email sent." });
  }

  const adminEmail = process.env.SMTP_USER;
  if (!adminEmail) return sendJson(res, 500, { ok: false, message: "SMTP_USER not configured." });

  const rows = lowItems.map(i =>
    `  • [${i.sku}] ${i.product_name} — ${i.warehouse_name}/${i.location_code}: ${i.on_hand} units remaining`
  ).join("\n");

  const body = `CoreInventory Low Stock Alert\n\nThe following items are at or below ${THRESHOLD} units:\n\n${rows}\n\nPlease raise purchase orders to replenish stock.\n\n— CoreInventory System`;

  // Reuse nodemailer transport from email.js via a direct call
  const nodemailer = require("nodemailer");
  const transporter = nodemailer.createTransport({
    host: process.env.SMTP_HOST,
    port: parseInt(process.env.SMTP_PORT || "587"),
    secure: process.env.SMTP_SECURE === "true",
    auth: { user: process.env.SMTP_USER, pass: process.env.SMTP_PASS }
  });

  await transporter.sendMail({
    from: process.env.SMTP_FROM || adminEmail,
    to: adminEmail,
    subject: `⚠️ Low Stock Alert — ${lowItems.length} item(s) need restocking`,
    text: body
  });

  return sendJson(res, 200, { ok: true, message: `Alert email sent for ${lowItems.length} low-stock items.` });
});
