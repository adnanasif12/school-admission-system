import type { HoldStatus } from './types';

/** m:ss for a countdown. Rounds up so "0:01" is shown until the last second has really passed. */
export function formatRemaining(ms: number): string {
  const total = Math.max(0, Math.ceil(ms / 1000));
  const m = Math.floor(total / 60);
  const s = total % 60;
  return `${m}:${s.toString().padStart(2, '0')}`;
}

/**
 * What to show for an application. A HELD application whose expiry time has passed is shown as
 * EXPIRED immediately, without waiting for the next poll or a page reload.
 */
export function effectiveStatus(stored: HoldStatus, expiresAtMs: number, nowMs: number): HoldStatus {
  return stored === 'HELD' && expiresAtMs <= nowMs ? 'EXPIRED' : stored;
}
