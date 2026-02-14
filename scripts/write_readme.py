#!/usr/bin/env python3
import pathlib

readme = """# Yurt Coffee - Modern Coffee Ordering System

A production-ready, mobile-first coffee ordering system with admin dashboard.

## Architecture

pnpm monorepo managed with Turborepo:

- **apps/web** - Next.js 16 frontend (React 19, TailwindCSS 4)
- **apps/backend** - NestJS 11 API server (Mongoose, Socket.IO)
- **packages/shared-types** - TypeScript interfaces and constants
- **packages/shared-validation** - Zod schemas
- **packages/shared-config** - Shared config (socket events, env keys)

## Quick Start

Prerequisites: Node.js 18+, pnpm 9+, MongoDB

```bash
pnpm install
cp apps/web/.env.example apps/web/.env.local
cp apps/backend/.env.example apps/backend/.env
pnpm dev
```

Frontend runs on :3000, backend on :4000.

## Scripts

```bash
pnpm dev            # Start all apps
pnpm dev:web        # Frontend only
pnpm dev:backend    # Backend only
pnpm build          # Build all
pnpm build:web      # Build frontend
pnpm build:backend  # Build backend
pnpm lint           # Lint all
pnpm type-check     # TypeScript check all
```

## Features

**Client:** Menu browsing with search, customizable orders (size/toppings),
multiple pickup locations with hours, persistent cart, checkout (Kaspi/Apple Pay),
real-time order tracking (WebSocket), NextAuth login, order history and reviews,
loyalty program (points/tiers/birthday), multilingual (EN/RU/KK).

**Admin:** Live orders dashboard, accept/reject/complete actions, menu and
toppings CRUD, location and hours management, review moderation, analytics.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Next.js 16, React 19, TailwindCSS 4, Zustand |
| Backend | NestJS 11, Mongoose 9, Passport, Socket.IO |
| Auth | NextAuth v4 (JWE), jose (cross-domain decryption) |
| Validation | Zod 4 |
| Database | MongoDB |
| Build | pnpm workspaces, Turborepo |

## Environment Variables

**Frontend** (apps/web/.env.local):
- MONGODB_URI - MongoDB connection string
- NEXTAUTH_SECRET - JWT secret (must match backend)
- NEXT_PUBLIC_BACKEND_URL - Backend URL (default: http://localhost:4000)

**Backend** (apps/backend/.env):
- MONGODB_URI - MongoDB connection string (same database)
- NEXTAUTH_SECRET - Must match frontend for JWE decryption
- PORT - Server port (default: 4000)
- FRONTEND_URL - For CORS (default: http://localhost:3000)
"""

pathlib.Path("/Users/amakenzi/Desktop/Dev/yurt/README.md").write_text(readme)
print("README written successfully")
