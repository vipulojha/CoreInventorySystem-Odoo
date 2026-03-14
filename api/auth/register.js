const { ApiError, allowMethod, asyncHandler, parseJsonBody, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");
const { sendOtpEmail } = require("../../lib/email");
const { generateOtp, getOtpExpiryDate, hashOtp, verifyOtp } = require("../../lib/otp");
const { hashPassword, meetsPasswordPolicy } = require("../../lib/password");
const { validateSignupInput, normalizeEmail } = require("../../lib/validators");

module.exports = asyncHandler(async (req, res) => {
  if (!allowMethod(req, res, "POST")) return;
  const url = new URL(req.url, `http://${req.headers.host}`);
  const pathname = url.pathname;
  const body = parseJsonBody(req);

  if (pathname.includes("request-otp")) {
    const { loginId, displayName, email } = validateSignupInput(body);
    const password = String(body.password || "");
    if (!meetsPasswordPolicy(password)) throw new ApiError(400, "Password policy failed.");

    const otp = generateOtp(), otpHash = await hashOtp(otp), passwordHash = hashPassword(password), expires = getOtpExpiryDate();
    await withClient(async (client) => {
      const existing = await client.query("select 1 from app_user where lower(login_id)=lower($1) or lower(email)=lower($2)", [loginId, email]);
      if (existing.rowCount > 0) throw new ApiError(409, "Exists.");
      await client.query("delete from pending_signup where lower(email)=lower($1)", [email]);
      await client.query("insert into pending_signup (login_id, display_name, email, password_hash, otp_hash, otp_expires_at) values ($1,$2,$3,$4,$5,$6)", [loginId, displayName, email, passwordHash, otpHash, expires]);
    });
    await sendOtpEmail({ email, displayName, otp });
    return sendJson(res, 200, { ok: true, email });
  }

  if (pathname.includes("verify-otp")) {
    const email = normalizeEmail(body.email), otp = String(body.otp || "").trim();
    await withClient(async (client) => {
      const pending = await client.query("select * from pending_signup where lower(email)=lower($1)", [email]);
      if (pending.rowCount === 0) throw new ApiError(404, "No signup.");
      const row = pending.rows[0];
      if (new Date(row.otp_expires_at) < new Date()) throw new ApiError(410, "Expired.");
      if (!(await verifyOtp(otp, row.otp_hash))) throw new ApiError(400, "Invalid OTP.");
      await client.query("insert into app_user (login_id, display_name, email, password_hash, is_active, email_verified_at) values ($1,$2,$3,$4,true,now())", [row.login_id, row.display_name, row.email, row.password_hash]);
      await client.query("delete from pending_signup where email=$1", [email]);
    });
    return sendJson(res, 200, { ok: true });
  }

  res.statusCode = 405; res.end();
});
