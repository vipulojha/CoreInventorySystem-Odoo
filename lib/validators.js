function normalizeLoginId(loginId) {
  return String(loginId || "").trim();
}

function normalizeEmail(email) {
  return String(email || "").trim().toLowerCase();
}

function normalizeDisplayName(displayName) {
  return String(displayName || "").trim();
}

function validateSignupInput({ loginId, displayName, email }) {
  const normalizedLoginId = normalizeLoginId(loginId);
  const normalizedDisplayName = normalizeDisplayName(displayName);
  const normalizedEmail = normalizeEmail(email);

  if (normalizedLoginId.length < 6 || normalizedLoginId.length > 12) {
    throw new Error("Login ID must be between 6 and 12 characters.");
  }

  if (!normalizedDisplayName) {
    throw new Error("Display name is required.");
  }

  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalizedEmail)) {
    throw new Error("A valid email address is required.");
  }

  return {
    loginId: normalizedLoginId,
    displayName: normalizedDisplayName,
    email: normalizedEmail
  };
}

module.exports = {
  normalizeDisplayName,
  normalizeEmail,
  normalizeLoginId,
  validateSignupInput
};
