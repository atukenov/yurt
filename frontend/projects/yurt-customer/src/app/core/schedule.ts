export interface WorkingSlot {
  day: number; // 1=Mon … 7=Sun
  enabled: boolean;
  from: string; // 'HH:MM'
  to: string;
}

export interface ScheduleInfo {
  isOpen: boolean;
  todayLabel: string;     // "08:00 – 22:00" or "Closed today"
  nextOpenAt: string | null; // "08:00" when closed and opens later/tomorrow
  weekSummary: string[];  // ["Mon–Fri  08:00–22:00", "Sat–Sun  Closed"]
}

const DAY_ABBR = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

const DAY_INDEX: Record<string, number> = {
  mon: 1, tue: 2, wed: 3, thu: 4, fri: 5, sat: 6, sun: 7,
  пн: 1, вт: 2, ср: 3, чт: 4, пт: 5, сб: 6, вс: 7,
};
const TIME_RANGE = /(\d{1,2})[:.](\d{2})\s*(?:[–—-]|to|до)\s*(\d{1,2})[:.](\d{2})/i;

function dayOf(word: string | undefined): number | undefined {
  if (!word) return undefined;
  const w = word.toLowerCase();
  return DAY_INDEX[w.slice(0, 3)] ?? DAY_INDEX[w.slice(0, 2)];
}

/** Legacy plain-text hours, e.g. "Daily 08:00–21:00", "Mon–Fri 07:00–22:00 | Sat–Sun 08:00–23:00"
 *  or just "09:00-22:00" (no day names = every day). */
function parseLegacy(wh: string): WorkingSlot[] | null {
  const slots: WorkingSlot[] = Array.from({ length: 7 }, (_, i) => ({
    day: i + 1, enabled: false, from: '00:00', to: '00:00',
  }));
  const pad = (h: string, m: string) => `${h.padStart(2, '0')}:${m}`;
  let matched = false;
  for (const part of wh.split(/[|;,\n]/)) {
    const t = part.match(TIME_RANGE);
    if (!t) continue;
    const label = part.slice(0, t.index).trim();
    const words = label.match(/[A-Za-zА-Яа-яЁёӘәҒғҚқҢңӨөҰұҮүҺһІі]+/g) ?? [];
    let first = 1, last = 7;
    const d1 = dayOf(words[0]);
    if (d1) { first = d1; last = dayOf(words[1]) ?? d1; }
    for (let d = first; ; d = (d % 7) + 1) {
      slots[d - 1] = { day: d, enabled: true, from: pad(t[1], t[2]), to: pad(t[3], t[4]) };
      if (d === last) break;
    }
    matched = true;
  }
  return matched ? slots : null;
}

function parseSlots(wh: string): WorkingSlot[] | null {
  try {
    const p = JSON.parse(wh);
    if (Array.isArray(p) && p.length === 7) return p as WorkingSlot[];
  } catch { /* legacy plain-text */ }
  return parseLegacy(wh ?? '');
}

function toMin(hhmm: string): number {
  const [h, m] = hhmm.split(':').map(Number);
  return (h || 0) * 60 + (m || 0);
}

export function buildScheduleInfo(wh: string, now: Date): ScheduleInfo {
  const slots = parseSlots(wh);

  if (!slots) {
    // Legacy plain-text — can't compute status, just show raw value
    return {
      isOpen: true,
      todayLabel: wh || '—',
      nextOpenAt: null,
      weekSummary: [wh || '—'],
    };
  }

  // Current day  (1=Mon … 7=Sun)
  const jsDay = now.getDay();
  const today = jsDay === 0 ? 7 : jsDay;
  const nowMin = now.getHours() * 60 + now.getMinutes();

  // Today's slot
  const todaySlot = slots.find((s) => s.day === today)!;
  const todayOpen = todaySlot.enabled && nowMin >= toMin(todaySlot.from) && nowMin < toMin(todaySlot.to);
  const todayLabel = todaySlot.enabled
    ? `${todaySlot.from} – ${todaySlot.to}`
    : 'Closed today';

  // Next open time (when closed)
  let nextOpenAt: string | null = null;
  if (!todayOpen) {
    // Check today after current time
    if (todaySlot.enabled && nowMin < toMin(todaySlot.from)) {
      nextOpenAt = todaySlot.from;
    } else {
      // Search upcoming days
      for (let i = 1; i <= 7; i++) {
        const nextDay = ((today - 1 + i) % 7) + 1;
        const s = slots.find((sl) => sl.day === nextDay && sl.enabled);
        if (s) { nextOpenAt = s.from; break; }
      }
    }
  }

  // Week summary — group consecutive slots with identical enabled + hours
  interface Group { days: number[]; enabled: boolean; from: string; to: string; }
  const groups: Group[] = [];
  for (const slot of slots) {
    const last = groups[groups.length - 1];
    if (last && last.enabled === slot.enabled && last.from === slot.from && last.to === slot.to) {
      last.days.push(slot.day);
    } else {
      groups.push({ days: [slot.day], enabled: slot.enabled, from: slot.from, to: slot.to });
    }
  }

  const weekSummary = groups.map((g) => {
    const first = DAY_ABBR[g.days[0] - 1];
    const last  = DAY_ABBR[g.days[g.days.length - 1] - 1];
    const dayStr = g.days.length === 1 ? first : `${first}–${last}`;
    return g.enabled ? `${dayStr}  ${g.from}–${g.to}` : `${dayStr}  Closed`;
  });

  return { isOpen: todayOpen, todayLabel, nextOpenAt, weekSummary };
}

