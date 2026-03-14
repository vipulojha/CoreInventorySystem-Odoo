const byId = (id) => document.getElementById(id);

function initNavigation() {
  document.querySelectorAll("[data-nav]").forEach((element) => {
    element.addEventListener("click", (event) => {
      event.preventDefault();
      const target = element.getAttribute("data-nav");
      if (!target) {
        return;
      }

      window.location.assign(target);
    });
  });
}

function showStatus(element, type, message) {
  if (!element) {
    return;
  }

  element.className = `status visible ${type}`;
  element.textContent = message;
}

async function postJson(url, payload) {
  const response = await fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(payload)
  });

  let body = null;
  try {
    body = await response.json();
  } catch {
    body = { message: "Unexpected response from server." };
  }

  if (!response.ok) {
    throw new Error(body.message || "Request failed.");
  }

  return body;
}

function initRegisterPage() {
  const form = byId("register-form");
  if (!form) {
    return;
  }

  const status = byId("register-status");
  form.addEventListener("submit", async (event) => {
    event.preventDefault();

    const payload = {
      loginId: byId("loginId").value.trim(),
      displayName: byId("displayName").value.trim(),
      email: byId("email").value.trim(),
      password: byId("password").value
    };

    const confirmPassword = byId("confirmPassword").value;
    if (payload.password !== confirmPassword) {
      showStatus(status, "error", "Passwords do not match.");
      return;
    }

    showStatus(status, "info", "Sending OTP...");

    try {
      const result = await postJson("/api/auth/register/request-otp", payload);
      sessionStorage.setItem("coreinventory.pendingEmail", result.email || payload.email);
      showStatus(status, "success", result.message);
      window.setTimeout(() => {
        window.location.href = result.redirectTo || "/account/verify/";
      }, 700);
    } catch (error) {
      showStatus(status, "error", error.message);
    }
  });
}

function initVerifyPage() {
  const form = byId("verify-form");
  if (!form) {
    return;
  }

  const status = byId("verify-status");
  const emailInput = byId("verifyEmail");
  const savedEmail = sessionStorage.getItem("coreinventory.pendingEmail");
  if (savedEmail && emailInput && !emailInput.value) {
    emailInput.value = savedEmail;
  }

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    const payload = {
      email: emailInput.value.trim(),
      otp: byId("otp").value.trim()
    };

    showStatus(status, "info", "Verifying OTP...");

    try {
      const result = await postJson("/api/auth/register/verify-otp", payload);
      sessionStorage.removeItem("coreinventory.pendingEmail");
      showStatus(status, "success", result.message);
      window.setTimeout(() => {
        window.location.href = result.redirectTo || "/account/login/";
      }, 900);
    } catch (error) {
      showStatus(status, "error", error.message);
    }
  });
}

function initLoginPage() {
  const form = byId("login-form");
  if (!form) {
    return;
  }

  const status = byId("login-status");
  form.addEventListener("submit", async (event) => {
    event.preventDefault();

    const payload = {
      loginId: byId("loginLoginId").value.trim(),
      password: byId("loginPassword").value
    };

    showStatus(status, "info", "Signing in...");

    try {
      const result = await postJson("/api/auth/login", payload);
      localStorage.setItem("coreinventory.token", result.token);
      localStorage.setItem("coreinventory.user", JSON.stringify(result.user));
      showStatus(status, "success", result.message);
      window.setTimeout(() => {
        window.location.href = result.redirectTo || "/dashboard/";
      }, 700);
    } catch (error) {
      showStatus(status, "error", error.message);
    }
  });
}

function initDashboardPage() {
  const nameTarget = byId("dashboard-name");
  if (!nameTarget) {
    return;
  }
  
  const user = localStorage.getItem("coreinventory.user");
  const token = localStorage.getItem("coreinventory.token");
  const emailTarget = byId("dashboard-email");
  const tokenTarget = byId("dashboard-token");

  if (!user || !token) {
    window.location.href = "/account/login/";
    return;
  }

  try {
    const parsed = JSON.parse(user);
    if (nameTarget) {
      nameTarget.textContent = parsed.name || parsed.loginId || "Operator";
    }
    if (emailTarget) {
      emailTarget.textContent = parsed.email || "-";
    }
  } catch {
    window.location.href = "/account/login/";
    return;
  }

  if (tokenTarget) {
    tokenTarget.textContent = token;
  }

  const logoutButton = byId("logout-button");
  if (logoutButton) {
    logoutButton.addEventListener("click", () => {
      localStorage.removeItem("coreinventory.token");
      localStorage.removeItem("coreinventory.user");
      window.location.href = "/account/login/";
    });
  }
}

document.addEventListener("DOMContentLoaded", () => {
  initNavigation();
  initRegisterPage();
  initVerifyPage();
  initLoginPage();
  initDashboardPage();
});
