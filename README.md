# CoreInventory

CoreInventory is a hackathon-style full-stack inventory app built from the supplied workflow sketch. It covers:

- Login, sign up, and password reset
- Dashboard stats for receipts, deliveries, late operations, and low stock
- Receipt, delivery, and adjustment operations with list and kanban views
- Product catalog plus stock balances by warehouse/location
- Move history journal
- Warehouse and location settings

## Stack

- ASP.NET Core MVC, server-rendered
- PostgreSQL via `Npgsql`
- Plain CSS and vanilla JavaScript

There is also a separate Vercel-ready Node.js auth API layer in `api/` for OTP-based email signup.

This keeps the dependency surface small, avoids Node-based build tooling, and gives a consistent UI across major browsers.

## Project Layout

- `CoreInventory.csproj`: ASP.NET Core project file
- `api/`: Vercel-compatible Node.js auth APIs
- `lib/`: shared Node.js helpers for OTP, mail, JWT, and PostgreSQL
- `Controllers/`: MVC controllers
- `Services/`: auth and inventory service logic
- `ViewModels/`: typed models for pages/forms
- `Views/`: Razor views
- `wwwroot/`: CSS and small JS helpers
- `database/init.sql`: full schema plus demo seed data
- `database/vercel_auth_migration.sql`: add-on migration for OTP signup support

## Local Setup

1. Wait for PostgreSQL installation to finish.
2. Install the .NET 8 SDK if it is not already installed.
3. Create a PostgreSQL database named `coreinventory`.
4. Run `database/init.sql` against that database.
5. Run `dotnet restore`.
6. Run `dotnet run`.

Current default connection string in [appsettings.json](C:/Users/vipul/OneDrive/Documents/CoreInventoryApp/appsettings.json):

```json
"Host=localhost;Port=5432;Database=coreinventory;Username=postgres;Password=vips1860"
```

If your PostgreSQL username is not `postgres`, change only the `Username` part.

## Windows Run Steps

1. Finish installing PostgreSQL.
2. Make sure PostgreSQL service is running.
3. Open a terminal in [C:/Users/vipul/OneDrive/Documents/CoreInventoryApp](C:/Users/vipul/OneDrive/Documents/CoreInventoryApp).
4. Create the database:

```powershell
psql -U postgres -h localhost -p 5432 -c "CREATE DATABASE coreinventory;"
```

5. Load the schema and seed data:

```powershell
psql -U postgres -h localhost -p 5432 -d coreinventory -f database/init.sql
```

6. Restore packages:

```powershell
dotnet restore
```

7. Run the website:

```powershell
dotnet run
```

8. Open the URL shown in the terminal. Usually it will be similar to:

```text
https://localhost:5001
http://localhost:5000
```

Default seeded login:

- Login ID: `admin`
- Password: `vipul1860`

## Notes

- The current implementation uses PostgreSQL because it is the cleanest fit for a lean ASP.NET setup. If you need MySQL instead, the repository/service layer is isolated enough to swap the provider.
- The machine used to generate this code has the .NET runtime installed but not the SDK, so the app could not be compiled or executed here. The codebase and SQL bootstrap are complete, but you will need the SDK locally to run and verify it.
- If PostgreSQL is still installing, do not run the SQL import or the app yet. Wait until installation finishes first. You can install the .NET 8 SDK in parallel if you want.

## Vercel Node Auth API

The ASP.NET app itself is not something you deploy directly to Vercel. To support a Vercel deployment for signup/login, this repo now includes a Node.js auth layer under `api/` with these endpoints:

- `POST /api/auth/register/request-otp`
- `POST /api/auth/register/verify-otp`
- `POST /api/auth/login`

These Node APIs:

- generate secure 6-digit OTPs using Node `crypto`
- hash OTPs before storage
- send OTP email using `nodemailer`
- store passwords in the same `PBKDF2-SHA1$...` format the ASP.NET app already understands
- issue JWTs on login

Environment variables for the Node/Vercel auth layer are documented in [.env.example](C:/Users/vipul/OneDrive/Documents/CoreInventoryApp/.env.example) and the full flow is documented in [docs/vercel-node-auth.md](C:/Users/vipul/OneDrive/Documents/CoreInventoryApp/docs/vercel-node-auth.md).
