import { parseUtc } from './dates';

describe('parseUtc', () => {
  it('treats a date with no time-zone marker as UTC (the API often leaves the Z off)', () => {
    expect(parseUtc('2026-10-17T09:30:00').toISOString()).toBe('2026-10-17T09:30:00.000Z');
  });

  it('leaves a date that already ends in Z alone', () => {
    expect(parseUtc('2026-10-17T09:30:00Z').toISOString()).toBe('2026-10-17T09:30:00.000Z');
  });

  it('respects an explicit offset instead of adding a Z', () => {
    expect(parseUtc('2026-10-17T11:30:00+02:00').toISOString()).toBe('2026-10-17T09:30:00.000Z');
  });

  it('handles fractional seconds without a zone', () => {
    expect(parseUtc('2026-10-17T09:30:00.1234567').toISOString()).toBe('2026-10-17T09:30:00.123Z');
  });
});