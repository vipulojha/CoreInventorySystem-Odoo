class ApiError extends Error {
  constructor(statusCode, message) {
    super(message);
    this.statusCode = statusCode;
  }
}

function sendJson(res, statusCode, payload) {
  res.statusCode = statusCode;
  res.setHeader("Content-Type", "application/json");
  res.end(JSON.stringify(payload));
}

function parseJsonBody(req) {
  if (!req.body) {
    return {};
  }

  if (typeof req.body === "string") {
    try {
      return JSON.parse(req.body);
    } catch {
      throw new ApiError(400, "Invalid JSON body.");
    }
  }

  return req.body;
}

function allowMethod(req, res, method) {
  if (req.method !== method) {
    sendJson(res, 405, { ok: false, message: `Method ${req.method} not allowed.` });
    return false;
  }

  return true;
}

function asyncHandler(fn) {
  return async function wrapped(req, res) {
    try {
      await fn(req, res);
    } catch (error) {
      console.error(error);
      sendJson(res, error.statusCode || 500, {
        ok: false,
        message: error.message || "Internal server error."
      });
    }
  };
}

module.exports = {
  ApiError,
  allowMethod,
  asyncHandler,
  parseJsonBody,
  sendJson
};
