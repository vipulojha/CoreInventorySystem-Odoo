const jwt = require("jsonwebtoken");
const { allowMethod, ApiError, asyncHandler, parseJsonBody, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");
const { verifyPassword } = require("../../lib/password");
const { normalizeLoginId } = require("../../lib/validators");

module.exports = asyncHandler(async (req, res) => {
  if (!allowMethod(req, res, "POST")) {
    return;
  }

  if (!process.env.AUTH_JWT_SECRET) {
    throw new ApiError(500, "AUTH_JWT_SECRET is required.");
  }

  const body = parseJsonBody(req);
  const loginId = normalizeLoginId(body.loginId);
  const password = String(body.password || "");

  if (!loginId || !password) {
    return sendJson(res, 400, {
      ok: false,
      message: "Login ID and password are required."
    });
  }

  const user = await withClient(async (client) => {
    const result = await client.query(
      `
      select id, login_id, display_name, email, password_hash, is_active, email_verified_at
      from app_user
      where lower(login_id) = lower($1)
      limit 1;
      `,
      [loginId]
    );

    return result.rows[0] || null;
  });

  if (!user || !user.is_active || !verifyPassword(password, user.password_hash)) {
    return sendJson(res, 401, {
      ok: false,
      message: "Invalid login ID or password."
    });
  }

  if (!user.email_verified_at) {
    return sendJson(res, 403, {
      ok: false,
      message: "Email verification is pending."
    });
  }

  const token = jwt.sign(
    {
      sub: String(user.id),
      loginId: user.login_id,
      name: user.display_name,
      email: user.email
    },
    process.env.AUTH_JWT_SECRET,
    { expiresIn: "7d" }
  );

  return sendJson(res, 200, {
    ok: true,
    message: "Login successful.",
    token,
    user: {
      id: user.id,
      loginId: user.login_id,
      name: user.display_name,
      email: user.email
    }
  });
});
