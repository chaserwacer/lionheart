import { describe, expect, it } from 'vitest';
import { WeightUnit } from '$lib/api/ApiClient';
import {
  dateToUS,
  displayWeight,
  formatDateShort,
  kgToLb,
  lbToKg,
  pad2,
  parseNumberOrZero,
  roundTo,
  toIsoDateOnly,
  weightUnitLabel,
} from './formatters';

describe('toIsoDateOnly', () => {
  it('returns an empty string for absent input', () => {
    expect(toIsoDateOnly(null)).toBe('');
    expect(toIsoDateOnly(undefined)).toBe('');
    expect(toIsoDateOnly('')).toBe('');
  });

  it('truncates an ISO timestamp to its date part', () => {
    expect(toIsoDateOnly('2026-03-09T14:30:00Z')).toBe('2026-03-09');
  });

  it('passes an already date-only string through', () => {
    expect(toIsoDateOnly('2026-03-09')).toBe('2026-03-09');
  });

  it('reads a Date in UTC', () => {
    expect(toIsoDateOnly(new Date(Date.UTC(2026, 2, 9)))).toBe('2026-03-09');
  });

  it('zero-pads the parts of a DateOnly-shaped object', () => {
    expect(toIsoDateOnly({ year: 2026, month: 3, day: 9 })).toBe('2026-03-09');
  });

  it('returns an empty string for a shape it does not recognise', () => {
    expect(toIsoDateOnly({ foo: 'bar' })).toBe('');
    expect(toIsoDateOnly(42)).toBe('');
  });
});

describe('dateToUS', () => {
  // Guards the off-by-one that a local-midnight parse would produce west of UTC.
  it('names the weekday of the date itself, not the local-time shift', () => {
    expect(dateToUS('2026-03-09')).toBe('Monday 03/09');
  });

  it('returns an empty string when there is no date', () => {
    expect(dateToUS(null)).toBe('');
  });

  it('falls back to the raw value when the date cannot be parsed', () => {
    expect(dateToUS('not-a-date')).toBe('not-a-date');
  });
});

describe('formatDateShort', () => {
  it('formats a date in UTC', () => {
    expect(formatDateShort('2026-03-09')).toBe('Mar 9, 2026');
  });

  it('returns an empty string when there is no date', () => {
    expect(formatDateShort(undefined)).toBe('');
  });
});

describe('weight conversion', () => {
  it('labels the units', () => {
    expect(weightUnitLabel(WeightUnit._1)).toBe('lb');
    expect(weightUnitLabel(WeightUnit._0)).toBe('kg');
  });

  it('round-trips through both conversions', () => {
    expect(kgToLb(lbToKg(225))).toBeCloseTo(225, 10);
  });

  it('converts 100 kg to pounds', () => {
    expect(kgToLb(100)).toBeCloseTo(220.462, 3);
  });
});

describe('displayWeight', () => {
  it('renders an em dash for a missing value', () => {
    expect(displayWeight(null, WeightUnit._1, WeightUnit._1)).toBe('—');
    expect(displayWeight(undefined, WeightUnit._1, WeightUnit._1)).toBe('—');
  });

  it('renders an em dash for a non-finite value', () => {
    expect(displayWeight(Number.NaN, WeightUnit._1, WeightUnit._1)).toBe('—');
    expect(displayWeight(Number.POSITIVE_INFINITY, WeightUnit._1, WeightUnit._1)).toBe('—');
  });

  it('rounds without converting when the units already match', () => {
    expect(displayWeight(102.567, WeightUnit._1, WeightUnit._1)).toBe('102.57');
  });

  it('converts pounds to kilograms when the display unit differs', () => {
    expect(displayWeight(220.462, WeightUnit._1, WeightUnit._0)).toBe('100');
  });

  it('converts kilograms to pounds when the display unit differs', () => {
    expect(displayWeight(100, WeightUnit._0, WeightUnit._1)).toBe('220.46');
  });
});

describe('roundTo', () => {
  it('rounds to the requested number of decimal places', () => {
    expect(roundTo(1.005, 2)).toBe(1.0);
    expect(roundTo(2.345, 2)).toBe(2.35);
    expect(roundTo(2.5, 0)).toBe(3);
  });

  it('preserves the sign', () => {
    expect(roundTo(-1.234, 1)).toBe(-1.2);
  });
});

describe('parseNumberOrZero', () => {
  it('parses numeric strings', () => {
    expect(parseNumberOrZero('42.5')).toBe(42.5);
  });

  it('falls back to zero for anything non-finite', () => {
    expect(parseNumberOrZero('abc')).toBe(0);
    expect(parseNumberOrZero(undefined)).toBe(0);
    expect(parseNumberOrZero(Number.NaN)).toBe(0);
  });

  // An empty input box must read as zero rather than as NaN.
  it('treats an empty string as zero', () => {
    expect(parseNumberOrZero('')).toBe(0);
  });
});

describe('pad2', () => {
  it('pads a single digit', () => {
    expect(pad2(7)).toBe('07');
  });

  it('leaves two or more digits alone', () => {
    expect(pad2(12)).toBe('12');
    expect(pad2(123)).toBe('123');
  });
});
