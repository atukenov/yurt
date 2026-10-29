/** How the Platform screen groups the modules, in display order. Every module key belongs to exactly one group. */
export const MODULE_GROUPS: { key: string; modules: string[] }[] = [
  { key: 'basics', modules: ['core', 'branding', 'verification', 'multi_location'] },
  { key: 'ordering', modules: ['online_payments', 'table_ordering', 'group_orders'] },
  { key: 'customers', modules: ['loyalty', 'marketing', 'engagement', 'push'] },
  { key: 'running', modules: ['own_pos', 'staff_audit', 'analytics'] },
  { key: 'connections', modules: ['iiko_sync'] },
];

/** Groups with the modules the server knows; a module missing from the list above goes to "Other". */
export function groupModules(keys: string[]): { key: string; modules: string[] }[] {
  const known = new Set(MODULE_GROUPS.flatMap((g) => g.modules));
  const groups = MODULE_GROUPS
    .map((g) => ({ key: g.key, modules: g.modules.filter((m) => keys.includes(m)) }))
    .filter((g) => g.modules.length > 0);
  const other = keys.filter((k) => !known.has(k));
  return other.length > 0 ? [...groups, { key: 'other', modules: other }] : groups;
}

/** Monthly total: core is the base fee and always counts; other modules count while switched on. Same rule as the server. */
export function planTotal(items: { key: string; on: boolean; price: number | null }[]): number {
  const cents = items
    .filter((m) => m.key === 'core' || m.on)
    .reduce((sum, m) => sum + Math.round((Number(m.price) || 0) * 100), 0);
  return cents / 100;
}
