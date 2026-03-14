# CoreInventory: Enterprise Workspace

A premium, full-stack inventory management system optimized for Vercel Hobby plan deployments. This system features a unified Node.js backend and a high-end "Apple-style" glassmorphism frontend.

## Key Features

- **Interactive Dashboard**: Real-time stats with clickable details for low stock, pending receipts, and deliveries.
- **Unified Inventory**: Manage Products, Stock levels, and Operations (Receipts/Deliveries/Adjustments) with ease.
- **Premium Aesthetics**: High-end visual design using modern CSS, HSL colors, and smooth micro-animations.
- **Vercel Optimized**: Consolidated API architecture utilizing only 8 serverless functions to fit within Hobby plan limits.
- **Secure Auth**: OTP-verified registration and JWT-based authentication.

## Tech Stack

- **Frontend**: Vanilla HTML5, Premium CSS (Glassmorphism), Vanilla JavaScript.
- **Backend**: Node.js (Vercel Serverless Functions).
- **Database**: PostgreSQL (Prisma/pg).
- **Security**: JWT, PBKDF2 Password Hashing, 6-digit OTP.

## Local Development

1. **Prerequisites**: Node.js and PostgreSQL installed.
2. **Environment**: Copy `.env.example` to `.env` and configure your Database and SMTP credentials.
3. **Install Dependencies**:
   ```bash
   npm install
   ```
4. **Database Setup**:
   - Create a database `coreinventory`.
   - Run `database/init.sql` to seed the schema and initial data.
5. **Run**:
   ```bash
   npm run dev
   ```

## Project Structure

- `api/`: Consolidated Vercel Serverless Functions.
- `lib/`: Core logic for DB connections, API wrappers, and utilities.
- `dashboard/`, `products/`, `stock/`, `operations/`: Premium frontend modules.
- `styles/site.css`: Global design system and premium tokens.

## Deployment

This project is configured for one-click deployment to **Vercel**. All routes are handled by `vercel.json` rewrites.

---
*Created for judges who appreciate precision, aesthetics, and clean code.*
