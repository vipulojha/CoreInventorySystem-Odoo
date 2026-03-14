const { allowMethod, asyncHandler, parseJsonBody, sendJson } = require("../../../lib/api");
const { withClient } = require("../../../lib/db");
const { verifyOtp } = require("../../../lib/otp");
const { normalizeEmail } = require("../../../lib/validators");

const MAX_OTP_ATTEMPTS = 5;

module.exports = asyncHandler(async (req, res) => {
  if (!allowMethod(req, res, "POST")) {
    return;
  }

  const body = parseJsonBody(req);
  const email = normalizeEmail(body.email);
  const otp = String(body.otp || "").trim();

  if (!email || otp.length !== 6) {
    return sendJson(res, 400, {
      ok: false,
      message: "Email and 6-digit OTP are required."
    });
  }

  let failureResponse = null;

  await withClient(async (client) => {
    await client.query("begin");

    try {
      const pending = await client.query(
        `
        select id, login_id, display_name, email, password_hash, otp_hash, otp_expires_at, attempts
        from pending_signup
        where lower(email) = lower($1)
        limit 1;
        `,
        [email]
      );

      if (pending.rowCount === 0) {
        await client.query("rollback");
        failureResponse = { statusCode: 404, message: "No pending signup found for that email." };
        return;
      }

      const row = pending.rows[0];
      if (row.attempts >= MAX_OTP_ATTEMPTS) {
        await client.query("delete from pending_signup where id = $1;", [row.id]);
        await client.query("commit");
        failureResponse = { statusCode: 429, message: "Too many invalid OTP attempts. Request a new OTP." };
        return;
      }

      if (new Date(row.otp_expires_at).getTime() < Date.now()) {
        await client.query("delete from pending_signup where id = $1;", [row.id]);
        await client.query("commit");
        failureResponse = { statusCode: 410, message: "OTP expired. Request a new one." };
        return;
      }

      const isValidOtp = await verifyOtp(otp, row.otp_hash);
      if (!isValidOtp) {
        const nextAttempts = row.attempts + 1;

        if (nextAttempts >= MAX_OTP_ATTEMPTS) {
          await client.query("delete from pending_signup where id = $1;", [row.id]);
          await client.query("commit");
          failureResponse = { statusCode: 429, message: "Too many invalid OTP attempts. Request a new OTP." };
          return;
        }

        await client.query(
          "update pending_signup set attempts = $2, updated_at = now() where id = $1;",
          [row.id, nextAttempts]
        );
        await client.query("commit");
        failureResponse = { statusCode: 400, message: "Invalid OTP." };
        return;
      }

      const existingUser = await client.query(
        `
        select 1
        from app_user
        where lower(login_id) = lower($1)
           or lower(email) = lower($2)
        limit 1;
        `,
        [row.login_id, row.email]
      );

      if (existingUser.rowCount > 0) {
        await client.query("delete from pending_signup where id = $1;", [row.id]);
        await client.query("commit");
        failureResponse = { statusCode: 409, message: "Login ID or email already exists." };
        return;
      }

      await client.query(
        `
        insert into app_user (login_id, display_name, email, password_hash, is_active, email_verified_at, created_at, updated_at)
        values ($1, $2, $3, $4, true, now(), now(), now());
        `,
        [row.login_id, row.display_name, row.email, row.password_hash]
      );

      await client.query("delete from pending_signup where id = $1;", [row.id]);
      await client.query("commit");
    } catch (error) {
      await client.query("rollback");
      throw error;
    }
  });

  if (failureResponse) {
    return sendJson(res, failureResponse.statusCode, {
      ok: false,
      message: failureResponse.message
    });
  }

  return sendJson(res, 200, {
    ok: true,
    message: "Account created successfully.",
    redirectTo: "/account/login/"
  });
});
