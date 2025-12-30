# Yurt Coffee - Development Roadmap & Todo List

## 🔴 CRITICAL (Must Fix - Build Blockers)

- [x] **Fix Mongoose Connection Pooling** ✅ COMPLETED

  - Issue #9 on GitHub
  - Resolve duplicate connection errors
  - Add proper connection caching
  - Status: Fixed and tested
  - Priority: **CRITICAL** - Blocks deployment

- [x] **Enforce Location Selection on Checkout** ✅ COMPLETED
  - Created LocationSelector reusable component
  - Locations fetched from database (not hardcoded)
  - Removed "All Locations" option
  - Made location mandatory in cart/checkout flow
  - Added validation with clear error messages
  - Extract components: CartHeaderIcons, MobileBottomNav
  - Priority: **CRITICAL** - Data integrity issue

## 🟠 HIGH (Core Features - Should Complete Before MVP)

### Payment & Checkout

- [ ] **Implement Stripe Payment Integration**

  - Issue #2 on GitHub
  - Add Stripe payment form
  - Implement payment processing
  - Handle payment errors gracefully
  - Add webhook support for payment events
  - Estimated effort: 2-3 days
  - Priority: **HIGH** - Required for revenue

- [x] **Fix Cart Persistence & Session Management** ✅ COMPLETED
  - ✅ Cart survives page refresh (localStorage + Zustand persist)
  - ✅ Cart survives login/logout (persisted independently)
  - ✅ Clear cart on order completion (checkout page integration)
  - ✅ Add cart recovery for abandoned carts (backup mechanism with timestamps)
  - ✅ Track last saved time for cart analytics
  - ✅ Build passes with no TypeScript errors
  - Priority: **HIGH** - User experience critical

### Order Management

- [x] **Implement Real-time Order Notifications** ✅ COMPLETED

  - Issue #4 on GitHub (WebSockets)
  - ✅ Setup Socket.io server with namespace configuration
  - ✅ Create client-side SocketProvider and useSocket hook
  - ✅ Emit events on order creation (orderCreated event)
  - ✅ Emit events on order updates (orderUpdated event)
  - ✅ Emit events on status changes (orderStatusChanged event)
  - ✅ Admin dashboard listens for real-time order updates (replaces polling)
  - ✅ Customer orders page listens for status changes (replaces polling)
  - ✅ Fallback to polling if WebSocket connection unavailable
  - ✅ Build passes with no TypeScript errors
  - ✅ Tested end-to-end with multiple clients
  - Estimated effort: 2-3 days
  - Priority: **HIGH** - Core admin feature

- [ ] **Add Email Notifications**
  - Issue #3 on GitHub
  - Order confirmation emails
  - Status update emails (accepted, rejected, completed)
  - Review reminder emails
  - Use Nodemailer (already in deps)
  - Estimated effort: 1-2 days
  - Priority: **HIGH** - User engagement

### Location Management

- [ ] **Implement Location Hours & Availability**
  - Issue #8 on GitHub
  - Add opening/closing hours per location
  - Set daily hours (different by day)
  - Add holiday/special hours
  - Show availability on checkout
  - Block orders outside working hours
  - Estimated effort: 1-2 days
  - Priority: **HIGH** - Business logic

### Admin Features

- [x] **Complete Topping Management Dashboard** ✅ COMPLETED

  - Issue #7 on GitHub
  - Create admin UI for topping CRUD
  - Display topping categories
  - Search & filter functionality
  - Category statistics cards
  - Success/error messaging with auto-dismiss
  - Form validation improvements
  - Delete with name confirmation dialogs
  - Estimated effort: 1 day
  - Priority: **HIGH** - Admin tool

- [ ] **Implement Advanced Order Filtering**
  - Issue #6 on GitHub
  - Filter by status (pending, accepted, etc.)
  - Filter by date range
  - Filter by location
  - Filter by payment status
  - Search by order number or customer name
  - Export orders to CSV/PDF
  - Estimated effort: 1-2 days
  - Priority: **HIGH** - Admin productivity

## 🟡 MEDIUM (Important Features - Complete in Next Sprint)

### User Experience

- [x] **Implement Loading States & Skeletons** ✅ COMPLETED

  - Issue #10 on GitHub
  - ✅ Add skeleton loaders for menu items (MenuGridSkeleton)
  - ✅ Add loading spinners for checkout/orders (OrderGridSkeleton, CheckoutSkeleton)
  - ✅ Implement Suspense boundaries throughout (menu page)
  - ✅ Add smooth transitions (fadeInUp, slideInRight animations)
  - ✅ Created reusable skeleton components (SkeletonLoaders.tsx)
  - ✅ Added CSS animations for page transitions (globals.css)
  - Estimated effort: 1 day
  - Priority: **MEDIUM** - UX polish

- [ ] **Strengthen Input Validation & Error Handling**

  - Issue #11 on GitHub
  - Validate all inputs with Zod
  - Add clear error messages to forms
  - Implement error boundaries
  - Add error logging/monitoring
  - Estimated effort: 1 day
  - Priority: **MEDIUM** - Stability

- [ ] **Add Product Reviews & Ratings**

  - Issue #5 on GitHub
  - Create review form after order completion
  - Allow 1-5 star ratings
  - Display average ratings on menu
  - Show reviews on order history
  - Admin moderation features
  - Estimated effort: 1-2 days
  - Priority: **MEDIUM** - Social proof

- [ ] **Improve Search & Filter on Menu**
  - Add autocomplete to search
  - Filter by dietary preferences (if applicable)
  - Sort by price, rating, preparation time
  - Save favorite items
  - Recently ordered items
  - Estimated effort: 1 day
  - Priority: **MEDIUM** - Discovery

### Admin Features

- [ ] **Add Location Management Dashboard**

  - Create admin UI for location CRUD
  - Display location details and availability
  - Edit location info (address, phone, hours)
  - Delete locations with confirmation
  - Map integration for addresses
  - Estimated effort: 1 day
  - Priority: **MEDIUM** - Admin feature

- [ ] **Implement Order Analytics Dashboard**

  - Total orders and revenue
  - Popular menu items
  - Peak order times
  - Customer metrics
  - Revenue trends (daily/weekly/monthly)
  - Estimated effort: 1-2 days
  - Priority: **MEDIUM** - Business insights

- [ ] **Add Inventory Management**
  - Track available quantities per item
  - Low stock warnings
  - Mark items as unavailable
  - Bulk inventory updates
  - Estimated effort: 1 day
  - Priority: **MEDIUM** - Operations

### Database & Performance

- [ ] **Database Schema Optimization**

  - Issue #13 on GitHub
  - Remove duplicate indexes
  - Add missing performance indexes
  - Optimize embedded schemas
  - Add database migration scripts
  - Estimated effort: 1 day
  - Priority: **MEDIUM** - Performance

- [ ] **Implement Caching Layer**
  - Cache menu items (5 min TTL)
  - Cache toppings (5 min TTL)
  - Cache locations (10 min TTL)
  - Use Redis or in-memory cache
  - Estimated effort: 1 day
  - Priority: **MEDIUM** - Performance

## 🟢 LOW (Nice-to-Have Features)

### Mobile & UX

- [ ] **Mobile App Features**

  - Add PWA support
  - Install to home screen
  - Offline support for menu
  - Push notifications for order updates
  - Estimated effort: 2 days
  - Priority: **LOW** - Enhancement

- [ ] **Accessibility Improvements**
  - Add keyboard navigation
  - Improve screen reader support
  - Add ARIA labels to all interactive elements
  - Test with accessibility tools
  - Estimated effort: 1 day
  - Priority: **LOW** - Compliance

### Features

- [ ] **Loyalty Program / Points System**

  - Award points per order
  - Redeem points for discounts
  - Tier-based rewards
  - Birthday discounts
  - Estimated effort: 2 days
  - Priority: **LOW** - Engagement

- [ ] **Scheduled Orders / Pre-orders**

  - Schedule orders for future time
  - Recurring orders (daily/weekly)
  - Order reminders
  - Calendar UI for scheduling
  - Estimated effort: 2 days
  - Priority: **LOW** - Feature

- [ ] **Social Features**

  - Share order on social media
  - Gift cards / digital vouchers
  - Referral program
  - Customer testimonials
  - Estimated effort: 2 days
  - Priority: **LOW** - Marketing

- [ ] **Multi-language Support**
  - Add i18n/internationalization
  - Support multiple languages
  - Right-to-left support (RTL)
  - Estimated effort: 1-2 days
  - Priority: **LOW** - Expansion

### Admin Features

- [ ] **Staff Management**

  - Create staff accounts
  - Role-based permissions
  - Shift scheduling
  - Performance tracking
  - Estimated effort: 2 days
  - Priority: **LOW** - Operations

- [ ] **Marketing Tools**

  - Email campaigns
  - SMS notifications
  - Push notification campaigns
  - Promotion management
  - Estimated effort: 2 days
  - Priority: **LOW** - Marketing

- [ ] **Integration Features**
  - Third-party POS integration
  - Delivery service integration (Uber Eats, etc.)
  - Accounting software integration
  - Estimated effort: 3+ days
  - Priority: **LOW** - Integration

## 📋 Testing & QA

- [ ] **Unit Tests**

  - Add Jest tests for components
  - Test utility functions
  - Test API routes
  - Target 70%+ coverage
  - Estimated effort: 2 days
  - Priority: **MEDIUM**

- [ ] **Integration Tests**

  - Test checkout flow
  - Test order creation
  - Test admin features
  - Test authentication
  - Estimated effort: 2 days
  - Priority: **MEDIUM**

- [ ] **End-to-End Tests**
  - Cypress/Playwright tests
  - User flow testing
  - Cross-browser testing
  - Mobile testing
  - Estimated effort: 2 days
  - Priority: **MEDIUM**

## 🚀 Deployment & DevOps

- [ ] **Setup CI/CD Pipeline**

  - GitHub Actions workflow
  - Automated tests on PR
  - Automated deployment on merge
  - Environment management
  - Estimated effort: 1 day
  - Priority: **HIGH**

- [ ] **Production Deployment**

  - Deploy to Vercel or self-hosted
  - Setup custom domain
  - SSL certificate
  - CDN for static assets
  - Estimated effort: 1 day
  - Priority: **HIGH**

- [ ] **Monitoring & Logging**
  - Error tracking (Sentry)
  - Performance monitoring
  - Database monitoring
  - Log aggregation
  - Estimated effort: 1 day
  - Priority: **MEDIUM**

## 📊 Current Project Status

**Build Status:** ✅ Passing (Real-time notifications + Cart persistence + Loading states)
**Last Update:** December 30, 2025
**Total Features:** 19 completed + 8 remaining in GitHub issues + 30+ todo items
**MVP Readiness:** ~75% complete

### Completed Features

✅ User authentication (login/register)
✅ Coffee menu browsing with search/filter
✅ Order customization (size, toppings)
✅ Shopping cart
✅ Checkout flow (payment method selection)
✅ Order tracking/history
✅ Admin dashboard (live orders)
✅ Order management (accept/reject)
✅ Menu management (CRUD)
✅ Topping management (complete with search/filter)
✅ Location management (partial)
✅ Review system (model exists)
✅ Notification system (model exists)
✅ Mobile-responsive design
✅ TypeScript type safety
✅ Real-time Order Notifications (Socket.io + polling fallback)
✅ Cart Persistence & Session Management (localStorage + abandoned cart recovery)
✅ Order Details Page (dynamic routing with real-time updates)
✅ Loading States & Skeleton Loaders (smooth UX transitions)

### In Progress

🔄 Stripe payment integration (HIGH priority)
🔄 Advanced order filtering (HIGH priority)

### Blocked (Needs Dependencies)

⏸️ Stripe payment processing
⏸️ Email notifications
⏸️ Location hours management

---

## 🎯 Recommended Sprint Planning

**Sprint 1 (Week 1-2):** CRITICAL + HIGH Priority

- Fix location selection validation
- Test/verify Mongoose connection fix
- Implement Stripe payment integration
- Start email notifications

**Sprint 2 (Week 3-4):** HIGH + MEDIUM Priority

- Complete email notifications
- Implement WebSocket real-time updates
- Location hours & availability
- Complete topping dashboard

**Sprint 3 (Week 5-6):** MEDIUM Priority

- Loading states & skeletons
- Input validation improvements
- Product reviews full implementation
- Advanced order filtering

**Sprint 4 (Week 7-8):** Additional Features

- Analytics dashboard
- Inventory management
- Performance optimization
- Testing & QA

---

## 🔗 Related Issues

- GitHub Project: https://github.com/users/atukenov/projects/2
- Issue Tracker: https://github.com/atukenov/yurt/issues
