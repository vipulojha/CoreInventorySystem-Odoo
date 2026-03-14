const crypto = require("crypto");
const bcrypt = require("bcryptjs");

const OTP_ROUNDS = 10;

function generateOtp() {
  return crypto.randomInt(100000, 1000000).toString();
}

async function hashOtp(otp) {
  return bcrypt.hash(otp, OTP_ROUNDS);
}

async function verifyOtp(otp, otpHash) {
  return bcrypt.compare(otp, otpHash);
}

function getOtpExpiryDate() {
  const minutes = Number(process.env.OTP_TTL_MINUTES || 10);
  return new Date(Date.now() + minutes * 60 * 1000);
}

module.exports = {
  generateOtp,
  getOtpExpiryDate,
  hashOtp,
  verifyOtp
};
