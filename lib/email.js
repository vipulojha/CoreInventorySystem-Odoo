const nodemailer = require("nodemailer");

let transporter;

function getTransporter() {
  if (transporter) {
    return transporter;
  }

  const requiredVars = ["SMTP_HOST", "SMTP_PORT", "SMTP_USER", "SMTP_PASS", "SMTP_FROM"];
  for (const key of requiredVars) {
    if (!process.env[key]) {
      throw new Error(`${key} is required for OTP email delivery.`);
    }
  }

  transporter = nodemailer.createTransport({
    host: process.env.SMTP_HOST,
    port: Number(process.env.SMTP_PORT),
    secure: String(process.env.SMTP_SECURE || "false").toLowerCase() === "true",
    auth: {
      user: process.env.SMTP_USER,
      pass: process.env.SMTP_PASS
    }
  });

  return transporter;
}

async function sendOtpEmail({ email, displayName, otp }) {
  const from = process.env.SMTP_FROM;
  const transport = getTransporter();

  await transport.sendMail({
    from,
    to: email,
    subject: "Your CoreInventory OTP code",
    text: `Hello ${displayName}, your CoreInventory OTP is ${otp}. It expires in ${process.env.OTP_TTL_MINUTES || 10} minutes.`,
    html: `
      <div style="font-family:Segoe UI,Arial,sans-serif;line-height:1.6;color:#1f2a30">
        <h2>CoreInventory verification</h2>
        <p>Hello ${displayName},</p>
        <p>Your one-time password is:</p>
        <p style="font-size:28px;font-weight:700;letter-spacing:4px">${otp}</p>
        <p>This code expires in ${process.env.OTP_TTL_MINUTES || 10} minutes.</p>
      </div>
    `
  });
}

module.exports = {
  sendOtpEmail
};
