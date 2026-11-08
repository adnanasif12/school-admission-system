import { useEffect, useRef, useState } from 'react';
import { serverNow } from './api';

/** Current server-corrected time in ms, re-rendering every `intervalMs` (drives the live countdowns). */
export function useNow(intervalMs = 1000): number {
  const [now, setNow] = useState(serverNow());
  useEffect(() => {
    const id = setInterval(() => setNow(serverNow()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return now;
}

/**
 * Calls `load` immediately and then every `intervalMs` (skipped while the tab is hidden; runs again
 * when it becomes visible). Changing `key` restarts the cycle with an immediate load.
 */
export function usePolling(load: () => Promise<void> | void, intervalMs: number, key: unknown): void {
  const loadRef = useRef(load);
  loadRef.current = load;

  useEffect(() => {
    const run = () => {
      if (!document.hidden) void loadRef.current();
    };
    run();
    const id = setInterval(run, intervalMs);
    document.addEventListener('visibilitychange', run);
    return () => {
      clearInterval(id);
      document.removeEventListener('visibilitychange', run);
    };
  }, [intervalMs, key]);
}

export function formatRemaining(ms: number): string {
  const total = Math.max(0, Math.ceil(ms / 1000));
  const m = Math.floor(total / 60);
  const s = total % 60;
  return `${m}:${s.toString().padStart(2, '0')}`;
}
