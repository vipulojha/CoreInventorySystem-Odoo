const { ApiError, allowMethod, asyncHandler, parseJsonBody, sendJson } = require("../../../lib/api");
const { withClient } = require("../../../lib/db");
const { sendOtpEmail } = require("../../../lib/email");
const { generateOtp, getOtpExpiryDate, hashOtp } = require("../../../lib/otp");
const { hashPassword, meetsPasswordPolicy } = require("../../../lib/password");
const { validateSignupInput } = require("../../../lib/validators");

module.exports = asyncHandler(async (req, res) => {
  if (!allowMethod(req, res, "POST")) {
    return;
  }

  const body = parseJsonBody(req);
  const { loginId, displayName, email } = validateSignupInput(body);
  const password = String(body.password || "");

  if (!meetsPasswordPolicy(password)) {
    return sendJson(res, 400, {
      ok: false,
      message: "Password must contain upper, lower, special characters and be at least 8 characters long."
    });
  }

  const otp = generateOtp();
  const otpHash = await hashOtp(otp);
  const passwordHash = hashPassword(password);
  const otpExpiresAt = getOtpExpiryDate();

  await withClient(async (client) => {
    await client.query("begin");

    try {
      const existingUser = await client.query(
        `
        select 1
        from app_user
        where lower(login_id) = lower($1)
           or lower(email) = lower($2)
        limit 1;
        `,
        [loginId, email]
      );

      if (existingUser.rowCount > 0) {
        throw new ApiError(409, "Login ID or email already exists.");
      }

      await client.query(
        `
        delete from pending_signup
        where lower(login_id) = lower($1)
           or lower(email) = lower($2);
        `,
        [loginId, email]
      );

      await client.query(
        `
        insert into pending_signup
        (
          login_id,
          display_name,
          email,
          password_hash,
          otp_hash,
          otp_expires_at,
          attempts,
          created_at,
          updated_at
        )
        values ($1, $2, $3, $4, $5, $6, 0, now(), now());
        `,
        [loginId, displayName, email, passwordHash, otpHash, otpExpiresAt]
      );

      await client.query("commit");
    } catch (error) {
      await client.query("rollback");
      throw error;
    }
  });

  try {
    await sendOtpEmail({ email, displayName, otp });
  } catch (error) {
    await withClient((client) =>
      client.query("delete from pending_signup where lower(email) = lower($1);", [email]));
    throw new ApiError(502, "OTP email could not be delivered.");
  }

  return sendJson(res, 200, {
    ok: true,
    message: "OTP sent successfully.",
    requiresOtp: true,
    redirectTo: "/account/verify/",
    email
  });
});
