import { TrainingSessionStatus, WeightUnit } from '$lib/api/ApiClient';

// ---------- Date Formatting ----------

export function toIsoDateOnly(raw: any): string {
  if (!raw) return '';
  if (typeof raw === 'string') return raw.slice(0, 10);
  if (raw instanceof Date) return raw.toISOString().slice(0, 10);

  // DateOnly commonly arrives shaped like { year, month, day }
  if (typeof raw === 'object' && raw.year && raw.month && raw.day) {
    const y = String(raw.year).padStart(4, '0');
    const m = String(raw.month).padStart(2, '0');
    const d = String(raw.day).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  return '';
}

export function dateToUS(raw: any): string {
  const iso = toIsoDateOnly(raw);
  if (!iso) return '';
  const d = new Date(`${iso}T00:00:00Z`);
  if (Number.isNaN(d.getTime())) return String(raw);

  const weekday = d.toLocaleDateString('en-US', { weekday: 'long', timeZone: 'UTC' });
  const md = d.toLocaleDateString('en-US', {
    month: '2-digit',
    day: '2-digit',
    timeZone: 'UTC',
  });
  return `${weekday} ${md}`;
}

export function formatDateShort(raw: any): string {
  const iso = toIsoDateOnly(raw);
  if (!iso) return '';
  const d = new Date(`${iso}T00:00:00Z`);
  if (Number.isNaN(d.getTime())) return String(raw);

  return d.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  });
}

// ---------- Status Formatting ----------

export function statusLabel(s: TrainingSessionStatus): string {
  switch (s) {
    case TrainingSessionStatus._0:
      return 'Planned';
    case TrainingSessionStatus._1:
      return 'Active';
    case TrainingSessionStatus._2:
      return 'Completed';
    case TrainingSessionStatus._3:
      return 'Skipped';
    case TrainingSessionStatus._4:
      return 'AI Modified';
    default:
      return 'Unknown';
  }
}

// ---------- Weight Formatting ----------

export function weightUnitLabel(u: WeightUnit): string {
  return u === WeightUnit._1 ? 'lb' : 'kg';
}

export function lbToKg(lb: number): number {
  return lb / 2.2046226218;
}

export function kgToLb(kg: number): number {
  return kg * 2.2046226218;
}

export function displayWeight(
  value: number | null | undefined,
  fromUnit: WeightUnit,
  displayUnit: WeightUnit
): string {
  if (value === null || value === undefined) return '—';
  const v = Number(value);
  if (!Number.isFinite(v)) return '—';

  if (fromUnit === displayUnit) return String(roundTo(v, 2));
  const converted = fromUnit === WeightUnit._1 ? lbToKg(v) : kgToLb(v);
  return String(roundTo(converted, 2));
}

// ---------- Number Formatting ----------

export function roundTo(v: number, dp: number): number {
  const p = Math.pow(10, dp);
  return Math.round(v * p) / p;
}

export function parseNumberOrZero(v: any): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}

// ---------- Time Formatting ----------

export function pad2(n: number): string {
  return String(n).padStart(2, '0');
}

export function formatTimeSpan(ts: any): string {
  if (ts === null || ts === undefined) return '—';
  if (typeof ts === 'string') return ts;
  return String(ts);
}
