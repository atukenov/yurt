import { groupModules, MODULE_GROUPS, planTotal } from './module-groups';

describe('module groups', () => {
  it('puts every module in exactly one group', () => {
    const all = MODULE_GROUPS.flatMap((g) => g.modules);
    expect(new Set(all).size).toBe(all.length);
    expect(all).toContain('core');
  });

  it('keeps unknown modules visible under Other and drops empty groups', () => {
    const groups = groupModules(['core', 'iiko_sync', 'teleport']);
    expect(groups.map((g) => g.key)).toEqual(['basics', 'connections', 'other']);
    expect(groups[2].modules).toEqual(['teleport']);
  });
});

describe('planTotal', () => {
  it('counts core and switched-on modules only', () => {
    expect(planTotal([
      { key: 'core', on: true, price: 20 },
      { key: 'marketing', on: true, price: 10.1 },
      { key: 'loyalty', on: false, price: 15 },
      { key: 'push', on: true, price: null },
    ])).toBe(30.1);
  });

  it('adds money without float drift', () => {
    expect(planTotal([{ key: 'core', on: true, price: 0.1 }, { key: 'push', on: true, price: 0.2 }])).toBe(0.3);
  });
});
