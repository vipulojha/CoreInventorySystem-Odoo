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

  if (normalizedLoginId.length < 3 || normalizedLoginId.length > 30) {
    throw new Error("Login ID must be between 3 and 30 characters.");
  }

  if (!/^[a-zA-Z0-9@._-]+$/.test(normalizedLoginId)) {
    throw new Error("Login ID may only contain letters, numbers, @, dot, underscore, or hyphen.");
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
