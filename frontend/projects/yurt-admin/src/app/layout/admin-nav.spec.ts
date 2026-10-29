import { buildAdminNav, homeLink, NavGroup } from './admin-nav';

const links = (groups: NavGroup[]) => groups.flatMap((g) => g.items.map((i) => i.link));
const all = () => true;
const none = () => false;

describe('admin sidebar', () => {
  it('gives the platform operator only the platform screens', () => {
    const nav = buildAdminNav({ isAdmin: true, isSuperAdmin: true, moduleEnabled: all });
    expect(nav.map((g) => g.id)).toEqual(['platform']);
    expect(links(nav)).toEqual(['/platform', '/platform/new-cafe', '/platform/app-releases']);
    expect(links(nav)).not.toContain('/subscription');
    expect(links(nav)).not.toContain('/orders');
  });

  it('groups a cafe admin\'s screens, with every module on', () => {
    const nav = buildAdminNav({ isAdmin: true, isSuperAdmin: false, moduleEnabled: all });
    expect(nav.map((g) => g.id)).toEqual(['pos', 'menu', 'guests', 'reports', 'settings']);
    const settings = nav.find((g) => g.id === 'settings')!;
    expect(settings.items.map((i) => i.link)).toContain('/subscription');
    expect(links(nav)).not.toContain('/platform');
  });

  it('hides the screens of modules that are off', () => {
    const nav = buildAdminNav({ isAdmin: true, isSuperAdmin: false, moduleEnabled: none });
    expect(links(nav)).toEqual(['/orders', '/menu', '/customers', '/reports', '/locations', '/subscription']);
  });

  it('shows only the screens of the modules that are on', () => {
    const nav = buildAdminNav({ isAdmin: true, isSuperAdmin: false, moduleEnabled: (k) => k === 'own_pos' });
    expect(links(nav)).toContain('/kitchen');
    expect(links(nav)).toContain('/stock');
    expect(links(nav)).toContain('/printers');
    expect(links(nav)).not.toContain('/promotions');
    expect(links(nav)).not.toContain('/tables');
  });

  it('gives plain staff the live orders only, and drops empty groups', () => {
    const nav = buildAdminNav({ isAdmin: false, isSuperAdmin: false, moduleEnabled: all });
    expect(nav.map((g) => g.id)).toEqual(['pos']);
    expect(links(nav)).toEqual(['/orders']);
  });

  it('lists every page once', () => {
    const l = links(buildAdminNav({ isAdmin: true, isSuperAdmin: false, moduleEnabled: all }));
    expect(new Set(l).size).toBe(l.length);
  });

  it('sends each person to their own start page', () => {
    expect(homeLink(true)).toBe('/platform');
    expect(homeLink(false)).toBe('/orders');
  });
});
