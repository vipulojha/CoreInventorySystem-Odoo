# Vercel Node Auth

This module adds a Vercel-compatible Node.js auth API on top of the same PostgreSQL database used by the ASP.NET Core inventory app.

## Why it exists

The current inventory UI is an ASP.NET Core MVC app. That is fine for local/server hosting, but not a direct fit for Vercel. For Vercel deployment, the auth part is split into Node.js serverless APIs under `api/`.

## Endpoints

### `POST /api/auth/register/request-otp`

Request body:

```json
{
  "loginId": "vipul01",
  "displayName": "Vipul",
  "email": "vipul@example.com",
  "password": "Vipul@1860"
}
```

Behavior:

- validates login ID, email, and password policy
- rejects if login ID or email already exists
- stores a pending signup record
- sends a 6-digit OTP to the email address

### `POST /api/auth/register/verify-otp`

Request body:

```json
{
  "email": "vipul@example.com",
  "otp": "123456"
}
```

Behavior:

- validates OTP
- creates the actual `app_user` row only after OTP success
- returns `Account created successfully.` and a redirect target to `/account/login`

### `POST /api/auth/login`

Request body:

```json
{
  "loginId": "vipul01",
  "password": "Vipul@1860"
}
```

Behavior:

- validates stored password against the shared PostgreSQL user table
- requires `email_verified_at`
- returns a JWT token

## Required environment variables

Use `.env.example` as the template:

- `DATABASE_URL`
- `AUTH_JWT_SECRET`
- `OTP_TTL_MINUTES`
- `SMTP_HOST`
- `SMTP_PORT`
- `SMTP_SECURE`
- `SMTP_USER`
- `SMTP_PASS`
- `SMTP_FROM`

## Database step

If your current database was already created before this module was added, run:

```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -p 5432 -d coreinventory -f "C:\Users\vipul\OneDrive\Documents\CoreInventoryApp\database\vercel_auth_migration.sql"
```

## Frontend flow

Typical frontend behavior:

1. User fills register form.
2. Frontend calls `request-otp`.
3. Frontend shows OTP form.
4. User enters OTP.
5. Frontend calls `verify-otp`.
6. On success, show `Account created successfully`.
7. Redirect to login page.

## Important note

This adds the auth APIs only. If you want the full inventory website itself to run on Vercel, the frontend/server side would need to be moved away from ASP.NET MVC to a Vercel-friendly stack such as Next.js or another static + API approach.
