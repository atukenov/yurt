# Yurt Coffee - Modern Coffee Ordering System

pnpm monorepo: apps/web (Next.js 16) + apps/backend (NestJS 11)

## Quick Start

    pnpm install
    cp apps/web/.env.example apps/web/.env.local
    cp apps/backend/.env.example apps/backend/.env
    pnpm dev

Frontend :3000, backend :4000.

## Tech Stack

Frontend: Next.js 16, React 19, TailwindCSS 4, Zustand
Backend: NestJS 11, Mongoose 9, Passport, Socket.IO
Auth: NextAuth v4 (JWE) + jose cross-domain decryption
Validation: Zod 4, Database: MongoDB, Build: pnpm + Turborepo
