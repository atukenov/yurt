/**
 * The admin sidebar, as data: groups of links, each with the role and module it needs.
 *
 * Cafe staff and cafe admins see the screens of their own cafe. The platform operator (SuperAdmin) runs the
 * platform and controls cafes, so they see only the platform screens; the default "Yurt" cafe is just another
 * cafe with its own admin account. Route guards in app.routes.ts enforce the same split.
 */

export interface NavItem {
  /** Route to open, e.g. '/orders'. */
  link: string;
  /** Translation key of the label. */
  label: string;
  /** SVG path data of the 24x24 outline icon. */
  icon: string[];
  /** Highlight only on this exact address (for a page that has sub-pages listed separately). */
  exact?: boolean;
  /** Shows the stock alert count. */
  badge?: 'stock';
}

export interface NavGroup {
  /** Stable id, also used to remember whether the group is folded. */
  id: string;
  /** Translation key of the group heading. */
  label: string;
  items: NavItem[];
}

export interface NavContext {
  isAdmin: boolean;
  /** The cafe's CEO: money screens and payroll. */
  isCeo: boolean;
  isSuperAdmin: boolean;
  moduleEnabled: (key: string) => boolean;
}

interface NavItemDef extends NavItem {
  /** Cafe admins only (staff see just what has no flag). */
  adminOnly?: boolean;
  /** The CEO only: money screens (sales, billing, audit log) and payroll. Admins do not see them. */
  ceoOnly?: boolean;
  /** Hidden unless this module is on for the cafe. */
  module?: string;
}

interface NavGroupDef {
  id: string;
  label: string;
  items: NavItemDef[];
}

const ICON = {
  orders: ['M9 12l2 2 4-4M7 4h10a2 2 0 012 2v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6a2 2 0 012-2z'],
  kitchen: ['M4 6h16M4 12h16M4 18h7'],
  cashier: ['M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z'],
  tables: ['M4 4h6v6H4V4zm10 0h6v6h-6V4zM4 14h6v6H4v-6zm10 0h2v2h-2v-2zm4 0h2v2h-2v-2zm-4 4h2v2h-2v-2zm4 0h2v2h-2v-2z'],
  printers: ['M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z'],
  combos: ['M20 12H4m16 0a2 2 0 01-2 2H6a2 2 0 01-2-2m16 0a2 2 0 00-2-2H6a2 2 0 00-2 2m8-8v4m-4-3v3m8-3v3M6 18h12'],
  stock: ['M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4'],
  purchasing: ['M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z'],
  forecast: ['M3 17l6-6 4 4 8-8M14 7h7v7'],
  stockMoves: ['M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4'],
  menu: ['M9 5H7a2 2 0 00-2 2v10a2 2 0 002 2h8a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 012-2h2a2 2 0 012 2'],
  timeMenus: ['M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z'],
  locations: [
    'M17.657 16.657L13.414 20.9a2 2 0 01-2.828 0l-4.243-4.243a8 8 0 1111.314 0z',
    'M15 11a3 3 0 11-6 0 3 3 0 016 0z',
  ],
  analytics: ['M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z'],
  promotions: ['M11 5.882V19.24a1.76 1.76 0 01-3.417.592l-2.147-6.15M18 13a3 3 0 100-6M5.436 13.683A4.001 4.001 0 017 6h1.832c4.1 0 7.625-1.234 9.168-3v14c-1.543-1.766-5.067-3-9.168-3H7a3.988 3.988 0 01-1.564-.317z'],
  discountCodes: ['M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z'],
  guestMessages: ['M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z'],
  customers: ['M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z'],
  workers: ['M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z'],
  lock: ['M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z'],
  auditLog: ['M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z'],
  hours: ['M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0zM4 4l3 3m13-3l-3 3'],
  tips: ['M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8V7m0 1v8m0 0v1M5 12a7 7 0 1114 0 7 7 0 01-14 0z'],
  profit: ['M13 7h8m0 0v8m0-8l-8 8-4-4-6 6'],
  accounting: ['M12 10v6m0 0l-3-3m3 3l3-3M3 17V7a2 2 0 012-2h6l2 2h6a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2z'],
  payroll: ['M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z'],
  staffWatch: ['M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z'],
  reports: ['M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'],
  subscription: ['M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z'],
  platform: ['M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4'],
  newCafe: ['M12 4v16m8-8H4'],
  goLive: ['M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4'],
  appReleases: ['M12 18h.01M8 21h8a2 2 0 002-2V5a2 2 0 00-2-2H8a2 2 0 00-2 2v14a2 2 0 002 2z'],
};

/** What a cafe's own staff and admins work with. */
const CAFE_NAV: NavGroupDef[] = [
  {
    id: 'pos',
    label: 'nav.group.pos',
    items: [
      { link: '/orders', label: 'nav.orders', icon: ICON.orders },
      { link: '/stock-moves', label: 'nav.stockMoves', icon: ICON.stockMoves, module: 'own_pos' },
      { link: '/kitchen', label: 'nav.kitchen', icon: ICON.kitchen, adminOnly: true, module: 'own_pos' },
      { link: '/tv', label: 'nav.tv', icon: ICON.kitchen, adminOnly: true, module: 'own_pos' },
      { link: '/cashier', label: 'nav.cashier', icon: ICON.cashier, adminOnly: true, module: 'own_pos' },
      { link: '/tables', label: 'nav.tables', icon: ICON.tables, adminOnly: true, module: 'table_ordering' },
    ],
  },
  {
    id: 'menu',
    label: 'nav.group.menu',
    items: [
      { link: '/menu', label: 'nav.menu', icon: ICON.menu, adminOnly: true },
      { link: '/stock', label: 'nav.stock', icon: ICON.stock, adminOnly: true, module: 'own_pos', badge: 'stock' },
      // Staff type in deliveries too; supplier details and payments stay with admins on the page itself.
      { link: '/purchasing', label: 'nav.purchasing', icon: ICON.purchasing, module: 'own_pos' },
      // Item counts and ingredient amounts only, no money: every staff role can open it.
      { link: '/forecast', label: 'nav.forecast', icon: ICON.forecast, module: 'own_pos' },
    ],
  },
  {
    id: 'guests',
    label: 'nav.group.guests',
    items: [
      { link: '/customers', label: 'nav.customers', icon: ICON.customers, adminOnly: true },
      { link: '/promotions', label: 'nav.promotions', icon: ICON.promotions, adminOnly: true, module: 'marketing' },
      { link: '/discount-codes', label: 'nav.discountCodes', icon: ICON.discountCodes, adminOnly: true, module: 'marketing' },
      { link: '/guest-messages', label: 'nav.guestMessages', icon: ICON.guestMessages, adminOnly: true, module: 'marketing' },
    ],
  },
  {
    id: 'reports',
    label: 'nav.group.reports',
    items: [
      { link: '/reports', label: 'nav.reports', icon: ICON.reports, adminOnly: true },
      { link: '/analytics', label: 'nav.analytics', icon: ICON.analytics, ceoOnly: true, module: 'analytics' },
      { link: '/hours', label: 'nav.hours', icon: ICON.hours, adminOnly: true, module: 'own_pos' },
      { link: '/tips', label: 'nav.tips', icon: ICON.tips, ceoOnly: true, module: 'own_pos' },
      { link: '/profit', label: 'nav.profit', icon: ICON.profit, ceoOnly: true, module: 'analytics' },
      { link: '/accounting', label: 'nav.accounting', icon: ICON.accounting, ceoOnly: true, module: 'analytics' },
      { link: '/staff-watch', label: 'nav.staffWatch', icon: ICON.staffWatch, adminOnly: true, module: 'staff_watch' },
      { link: '/audit-log', label: 'nav.auditLog', icon: ICON.auditLog, ceoOnly: true, module: 'staff_audit' },
    ],
  },
  {
    id: 'settings',
    label: 'nav.group.settings',
    items: [
      { link: '/locations', label: 'nav.locations', icon: ICON.locations, adminOnly: true },
      { link: '/workers', label: 'nav.workers', icon: ICON.workers, adminOnly: true, module: 'staff_audit' },
      { link: '/printers', label: 'nav.printers', icon: ICON.printers, adminOnly: true, module: 'own_pos' },
      { link: '/staff-pin', label: 'nav.staffPin', icon: ICON.lock, module: 'own_pos' },
      { link: '/manager-pin', label: 'nav.managerPin', icon: ICON.lock, adminOnly: true, module: 'own_pos' },
      { link: '/payroll', label: 'nav.payroll', icon: ICON.payroll, ceoOnly: true, module: 'payroll' },
      { link: '/subscription', label: 'nav.subscription', icon: ICON.subscription, ceoOnly: true },
    ],
  },
];

/** What the platform operator works with: the cafes, not the inside of one cafe. */
const PLATFORM_NAV: NavGroupDef[] = [
  {
    id: 'platform',
    label: 'nav.group.platform',
    items: [
      { link: '/platform', label: 'nav.platform', icon: ICON.platform, exact: true },
      { link: '/platform/new-cafe', label: 'wiz.title', icon: ICON.newCafe },
      { link: '/platform/go-live', label: 'nav.goLive', icon: ICON.goLive },
      { link: '/platform/app-releases', label: 'nav.appReleases', icon: ICON.appReleases },
    ],
  },
];

/** The sidebar for this person: groups with nothing to show are left out. */
export function buildAdminNav(ctx: NavContext): NavGroup[] {
  const defs = ctx.isSuperAdmin ? PLATFORM_NAV : CAFE_NAV;
  return defs
    .map((g) => ({
      id: g.id,
      label: g.label,
      items: g.items
        .filter(
          (i) =>
            (!i.adminOnly || ctx.isAdmin) &&
            (!i.ceoOnly || ctx.isCeo) &&
            (!i.module || ctx.moduleEnabled(i.module)),
        )
        .map(({ adminOnly: _a, ceoOnly: _c, module: _m, ...item }) => item),
    }))
    .filter((g) => g.items.length > 0);
}

/** Where a person lands after signing in or opening the bare address. */
export function homeLink(isSuperAdmin: boolean): string {
  return isSuperAdmin ? '/platform' : '/orders';
}
