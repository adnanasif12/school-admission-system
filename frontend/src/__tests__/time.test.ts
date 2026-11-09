import { describe, expect, it } from 'vitest';
import { effectiveStatus, formatRemaining } from '../time';

describe('formatRemaining (countdown text)', () => {
  it('formats minutes and zero-padded seconds', () => {
    expect(formatRemaining(600_000)).toBe('10:00');
    expect(formatRemaining(65_000)).toBe('1:05');
    expect(formatRemaining(9_000)).toBe('0:09');
  });

  it('rounds up, so 0:01 stays visible until the last second has really passed', () => {
    expect(formatRemaining(1)).toBe('0:01');
    expect(formatRemaining(59_200)).toBe('1:00');
  });

  it('never goes negative', () => {
    expect(formatRemaining(0)).toBe('0:00');
    expect(formatRemaining(-5_000)).toBe('0:00');
  });
});

describe('effectiveStatus (flips to EXPIRED without a reload)', () => {
  const expiresAt = 1_000_000;

  it('shows HELD while time is left', () => {
    expect(effectiveStatus('HELD', expiresAt, expiresAt - 1)).toBe('HELD');
  });

  it('shows EXPIRED exactly at and after the expiry time', () => {
    expect(effectiveStatus('HELD', expiresAt, expiresAt)).toBe('EXPIRED');
    expect(effectiveStatus('HELD', expiresAt, expiresAt + 60_000)).toBe('EXPIRED');
  });

  it('never changes a paid or cancelled application, whatever the clock says', () => {
    expect(effectiveStatus('CONFIRMED', expiresAt, expiresAt + 1)).toBe('CONFIRMED');
    expect(effectiveStatus('CANCELLED', expiresAt, expiresAt + 1)).toBe('CANCELLED');
    expect(effectiveStatus('EXPIRED', expiresAt, expiresAt - 1)).toBe('EXPIRED');
  });
});
