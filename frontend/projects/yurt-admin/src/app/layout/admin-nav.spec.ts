import { buildAdminNav, homeLink, NavGroup } from './admin-nav';

const links = (groups: NavGroup[]) => groups.flatMap((g) => g.items.map((i) => i.link));
const all = () => true;
const none = () => false;

describe('admin sidebar', () => {
  it('gives the platform operator only the platform screens', () => {
    const nav = buildAdminNav({ isAdmin: true, isCeo: false, isSuperAdmin: true, moduleEnabled: all });
    expect(nav.map((g) => g.id)).toEqual(['platform']);
    expect(links(nav)).toEqual(['/platform', '/platform/new-cafe', '/platform/go-live', '/platform/app-releases']);
    expect(links(nav)).not.toContain('/subscription');
    expect(links(nav)).not.toContain('/orders');
  });

  it('groups a cafe admin\'s screens, with every module on', () => {
    const nav = buildAdminNav({ isAdmin: true, isCeo: false, isSuperAdmin: false, moduleEnabled: all });
    expect(nav.map((g) => g.id)).toEqual(['pos', 'menu', 'guests', 'reports', 'settings']);
    expect(links(nav)).toContain('/stock');
    expect(links(nav)).toContain('/purchasing');
    expect(links(nav)).toContain('/workers');
    expect(links(nav)).not.toContain('/platform');
  });

  it('keeps the money screens and payroll away from a cafe admin', () => {
    const l = links(buildAdminNav({ isAdmin: true, isCeo: false, isSuperAdmin: false, moduleEnabled: all }));
    for (const money of ['/analytics', '/audit-log', '/subscription', '/payroll']) expect(l).not.toContain(money);
  });

  it('gives the CEO the money screens and payroll on top of the admin screens', () => {
    const nav = buildAdminNav({ isAdmin: true, isCeo: true, isSuperAdmin: false, moduleEnabled: all });
    const l = links(nav);
    for (const money of ['/analytics', '/audit-log', '/subscription', '/payroll', '/stock', '/workers']) expect(l).toContain(money);
    expect(links(buildAdminNav({ isAdmin: true, isCeo: true, isSuperAdmin: false, moduleEnabled: (k) => k !== 'payroll' }))).not.toContain('/payroll');
  });

  it('hides the screens of modules that are off', () => {
    const nav = buildAdminNav({ isAdmin: true, isCeo: false, isSuperAdmin: false, moduleEnabled: none });
    expect(links(nav)).toEqual(['/orders', '/menu', '/time-menus', '/customers', '/reports', '/locations']);
  });

  it('shows only the screens of the modules that are on', () => {
    const nav = buildAdminNav({ isAdmin: true, isCeo: false, isSuperAdmin: false, moduleEnabled: (k) => k === 'own_pos' });
    expect(links(nav)).toContain('/kitchen');
    expect(links(nav)).toContain('/stock');
    expect(links(nav)).toContain('/stock-moves');
    expect(links(nav)).toContain('/purchasing');
    expect(links(nav)).toContain('/printers');
    expect(links(nav)).not.toContain('/promotions');
    expect(links(nav)).not.toContain('/tables');
  });

  it('gives plain staff the live orders, stock write-offs/moves and deliveries only, and drops empty groups', () => {
    const nav = buildAdminNav({ isAdmin: false, isCeo: false, isSuperAdmin: false, moduleEnabled: all });
    expect(nav.map((g) => g.id)).toEqual(['pos', 'menu']);
    expect(links(nav)).toEqual(['/orders', '/stock-moves', '/purchasing']);
    expect(links(buildAdminNav({ isAdmin: false, isCeo: false, isSuperAdmin: false, moduleEnabled: none }))).toEqual(['/orders']);
  });

  it('lists every page once', () => {
    const l = links(buildAdminNav({ isAdmin: true, isCeo: false, isSuperAdmin: false, moduleEnabled: all }));
    expect(new Set(l).size).toBe(l.length);
  });

  it('sends each person to their own start page', () => {
    expect(homeLink(true)).toBe('/platform');
    expect(homeLink(false)).toBe('/orders');
  });
});
