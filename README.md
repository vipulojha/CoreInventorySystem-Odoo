# 🌌 CoreInventory: Worksspace

CoreInventory is a high-fidelity, enterprise-grade warehouse management system. It is designed with a "Judge-Ready" aesthetic, featuring the **Midnight & Aurora** design system, advanced glassmorphism, and fluid motion backgrounds.

## ✨ Key Features

- **Command Dashboard**: Real-time KPI monitoring with staggered entrance animations and "Aurora" motion heroes.
- **Granular Logistics**: Mission-control style operation detail views with visual pipeline tracking.
- **Security First**: JWT-based authentication with OTP verification and secure RBAC (Role-Based Access Control).
- **Audit-Ready**: Immutable move history and real-time stock adjustment logging.
- **Vercel Optimized**: High-performance architecture utilizing only 8 consolidated serverless functions.

## 🛠 Tech Stack

- **Frontend**: Vanilla HTML5, Ultra-Premium CSS (Glassmorphism), Vanilla JavaScript.
- **Backend**: Node.js (Vercel Serverless Functions).
- **Database**: PostgreSQL (Cloud-managed).
- **Animations**: Custom CSS Keyframes with cubic-bezier easing.

---

## 🔒 Important Note on Security & Database

You may notice that the **PostgreSQL Database** itself is not "stored" in this repository. This is intentional for the following reasons:

1. **Cloud-Native Architecture**: The live production database is hosted on a managed cloud service (like Vercel Postgres or Neon). This ensures 99.9% uptime and enterprise-grade performance.
2. **Security Best Practices**: Database credentials and connection strings are stored securely in **Environment Variables** (`POSTGRES_URL`). We never commit these to the repo to prevent security breaches and unauthorized access.
3. **Database Blueprint**: If you need to replicate the database locally or on a new server, the full schema blueprint is provided in the `/database/` directory (see `init.sql`).

---

## 🚀 Deployment

This project is optimized for deployment on **Vercel**. Every navigation route and API endpoint is precisely mapped in `vercel.json` to ensure a frictionless user experience across all web browsers.
deployment link-(https://vercel.com/vipulojha1860-9512s-projects/coreinventorysystem-odoo/HYRztnUwYUqV4QBMs3TjsRJjwXai)

---


