import { Injectable, effect, inject, signal } from '@angular/core';
import { TableLookup } from 'shared-models';
import { LocationService } from './location.service';

const KEY = 'yurt_table';
// A guest who comes back the next day is not at that table any more.
const MAX_AGE_MS = 4 * 60 * 60 * 1000;

interface SavedTable extends TableLookup {
  at: number;
}

function read(): TableLookup | null {
  try {
    const saved = JSON.parse(localStorage.getItem(KEY) ?? 'null') as SavedTable | null;
    if (!saved?.code || Date.now() - saved.at > MAX_AGE_MS) return null;
    return saved;
  } catch {
    return null;
  }
}

/**
 * The table code in what the guest typed: the code from the sticker (spaces and dashes allowed) or the whole QR link
 * (…/t/CODE). Empty when there is none.
 */
export function tableCodeFrom(input: string): string {
  const text = (input ?? '').trim();
  const fromLink = /\/t\/([A-Za-z0-9]+)/.exec(text);
  return (fromLink ? fromLink[1] : text.replace(/[\s-]/g, '')).toUpperCase();
}

/**
 * The table the guest is sitting at, after scanning its QR code (table_ordering). While it is set, orders go to this
 * table and staff bring them over. It is forgotten after a few hours, when the guest picks another branch, or when
 * they choose to pick up at the counter instead.
 */
@Injectable({ providedIn: 'root' })
export class TableService {
  private locationSvc = inject(LocationService);

  readonly table = signal<TableLookup | null>(read());

  constructor() {
    effect(() => {
      const t = this.table();
      if (t && this.locationSvc.locationId() !== t.locationId) this.clear();
    });
  }

  set(table: TableLookup): void {
    try {
      localStorage.setItem(KEY, JSON.stringify({ ...table, at: Date.now() } satisfies SavedTable));
    } catch {
      /* private mode: the table still holds for this visit */
    }
    this.locationSvc.setLocation(table.locationId, table.locationName);
    this.table.set(table);
  }

  /** The table, unless the few hours have passed since the scan. */
  current(): TableLookup | null {
    const t = this.table();
    if (t && !read()) this.clear();
    return this.table();
  }

  clear(): void {
    try {
      localStorage.removeItem(KEY);
    } catch {
      /* nothing saved */
    }
    this.table.set(null);
  }
}
