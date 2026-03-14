const crypto = require("crypto");

const ITERATIONS = 100000;
const SALT_SIZE = 16;
const HASH_SIZE = 32;

function meetsPasswordPolicy(password) {
  return typeof password === "string"
    && password.length >= 8
    && /[a-z]/.test(password)
    && /[A-Z]/.test(password)
    && /[^a-zA-Z0-9]/.test(password);
}

function hashPassword(password) {
  const salt = crypto.randomBytes(SALT_SIZE);
  const hash = crypto.pbkdf2Sync(password, salt, ITERATIONS, HASH_SIZE, "sha1");
  return `PBKDF2-SHA1$${ITERATIONS}$${salt.toString("base64")}$${hash.toString("base64")}`;
}

function verifyPassword(password, storedHash) {
  const parts = String(storedHash || "").split("$");
  if (parts.length !== 4 || parts[0] !== "PBKDF2-SHA1") {
    return false;
  }

  const iterations = Number(parts[1]);
  const salt = Buffer.from(parts[2], "base64");
  const expected = Buffer.from(parts[3], "base64");
  const actual = crypto.pbkdf2Sync(password, salt, iterations, expected.length, "sha1");

  return crypto.timingSafeEqual(expected, actual);
}

module.exports = {
  hashPassword,
  meetsPasswordPolicy,
  verifyPassword
};
