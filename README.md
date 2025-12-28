# ☕ Yurt Coffee - Modern Coffee Ordering System

A production-ready, mobile-first coffee ordering system with a comprehensive admin dashboard built with **Next.js 14+**, **MongoDB**, **TypeScript**, and **TailwindCSS**.

## 🚀 Features

### Client Features
- ✅ **Modern Coffee Menu** - Browse, search, and filter coffee by category
- ✅ **Customizable Orders** - Select size, toppings, and add special instructions
- ✅ **Multiple Locations** - Choose pickup location with working hours
- ✅ **Smart Cart** - Persistent cart with real-time price calculation
- ✅ **Checkout Flow** - Multiple payment methods (Cash, Card, Stripe)
- ✅ **Order Tracking** - Real-time order status updates
- ✅ **User Authentication** - Secure login/registration with NextAuth
- ✅ **Order History** - View past orders and leave reviews

### Admin Features
- ✅ **Live Orders Dashboard** - Real-time order management
- ✅ **Order Actions** - Accept with prep time or reject with reason
- ✅ **Menu Management** - Full CRUD for coffee items
- ✅ **Location Management** - Manage coffee shop locations

### Technical Highlights
- 📱 **Mobile-First Responsive Design** - Works seamlessly on all devices
- 🔐 **Secure Authentication** - NextAuth.js with password hashing
- �� **MongoDB Integration** - Mongoose ODM with proper indexing
- 🎯 **Type-Safe** - Full TypeScript implementation
- 🎨 **TailwindCSS** - Utility-first styling
- ♿ **Accessible** - ARIA labels and keyboard navigation

## 📋 Tech Stack

- **Framework**: Next.js 14+ (App Router)
- **Language**: TypeScript
- **Styling**: TailwindCSS
- **Database**: MongoDB with Mongoose
- **Authentication**: NextAuth.js
- **State Management**: Zustand
- **Validation**: Zod
- **Payment**: Stripe-ready

## 🚀 Getting Started

### Prerequisites
- Node.js 18+
- npm or yarn
- MongoDB instance (local or Atlas)

### 1. Installation

```bash
cd /Users/amakenzi/Desktop/Dev/yurt-v2
npm install
```

### 2. Environment Setup

Copy `.env.local.example` to `.env.local`:

```bash
# MongoDB Connection
MONGODB_URI=mongodb://localhost:27017/yurt

# NextAuth Configuration
NEXTAUTH_SECRET=$(openssl rand -hex 32)
NEXTAUTH_URL=http://localhost:3000
```

### 3. MongoDB Setup

**Local MongoDB:**
```bash
brew services start mongodb-community
```

**Or use MongoDB Atlas** at https://www.mongodb.com/cloud/atlas

### 4. Run Development Server

```bash
npm run dev
```

Visit **http://localhost:3000**

## 📱 Usage Guide

### Customer Flow
1. Register/Login at `/login`
2. Browse menu and customize orders
3. Add items to cart
4. Checkout with payment method
5. Track order in real-time

### Admin Flow
1. Login as admin
2. Access `/admin` dashboard
3. Manage orders, menu, and locations

## 📊 Database Schema

Models included:
- **User** - Authentication and profiles
- **MenuItem** - Coffee items with categories
- **Topping** - Customization options
- **Location** - Coffee shop locations
- **Order** - Customer orders
- **Review** - Customer reviews
- **Notification** - Order notifications

## 🔒 Security

- ✅ Password hashing with bcryptjs
- ✅ NextAuth session management
- ✅ Role-based access control
- ✅ Input validation with Zod
- ✅ Protected API routes

## 📦 Scripts

```bash
npm run dev      # Development server
npm run build    # Production build
npm start        # Start production
npm run lint     # Linting
```

## 📁 Project Structure

```
src/
├── app/
│   ├── (auth)/          # Login/Register
│   ├── (client)/        # Customer pages
│   ├── (admin)/         # Admin dashboard
│   └── api/             # API routes
├── components/          # Reusable components
├── lib/                 # Utilities
├── models/              # MongoDB schemas
├── store/               # Zustand state
└── types/               # TypeScript types
```

## 🎨 Customization

Modify TailwindCSS classes and colors throughout the project to match your brand.

## 🚀 Deployment

Deploy to Vercel:
```bash
npm install -g vercel
vercel
```

Or use Docker for other platforms.

---

**Built with ❤️ for coffee lovers** ☕
