/**
 * The API stamps every ledger line in UTC but may serialize it without a trailing "Z" (SQL Server hands
 * dates back with no "kind"). JavaScript reads "2026-10-17T09:00:00" as LOCAL time, which would show the
 * wrong hour. This treats a date with no time-zone marker as UTC, and leaves one that has a marker alone.
 */
export function parseUtc(value: string): Date {
  const hasZone = /(Z|[+-]\d{2}:?\d{2})$/i.test(value);

  return new Date(hasZone ? value : `${value}Z`);
}