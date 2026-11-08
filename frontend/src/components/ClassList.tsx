import { useCallback, useState } from 'react';
import { api, serverNow } from '../api';
import { friendlyError } from '../errors';
import { usePolling } from '../hooks';
import type { Application, ClassInfo } from '../types';

export const CLASS_POLL_MS = 4000;

interface Props {
  studentId: string | null;
  applications: Application[] | null;
  /** Bump to force an immediate reload (after create / cancel / expiry). */
  refreshKey: number;
  applyingClassId: string | null;
  onApply: (classId: string) => void;
}

function hasActiveApplication(apps: Application[] | null, classId: string): boolean {
  return (apps ?? []).some(
    (a) =>
      a.classId === classId &&
      (a.status === 'CONFIRMED' || (a.status === 'HELD' && Date.parse(a.expiresAt) > serverNow())),
  );
}

export function ClassList({ studentId, applications, refreshKey, applyingClassId, onApply }: Props) {
  const [classes, setClasses] = useState<ClassInfo[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setClasses(await api.listClasses());
      setError(null);
    } catch (e) {
      setError(friendlyError(e, 'load')); // keep showing the last known list underneath
    }
  }, []);

  usePolling(load, CLASS_POLL_MS, refreshKey);

  return (
    <section>
      <div className="section-head">
        <h2>Classes</h2>
        <span className="muted">Seat counts update automatically every few seconds.</span>
      </div>

      {error && (
        <div className="notice error" role="alert">
          <span>{error}{classes ? ' The numbers below may be out of date.' : ''}</span>
          <button className="btn small" onClick={() => void load()}>Try again</button>
        </div>
      )}

      {classes === null && !error && <p className="muted">Loading classes…</p>}
      {classes?.length === 0 && (
        <p className="card muted">No classes yet. A school admin can add one in the “Create class” tab.</p>
      )}

      <div className="class-grid">
        {classes?.map((c) => {
          const applied = hasActiveApplication(applications, c.id);
          const full = c.availableSeats <= 0;
          const busy = applyingClassId === c.id;
          const disabled = !studentId || full || applied || applyingClassId !== null;
          const hint = !studentId
            ? 'Enter a valid 17-digit student ID above to apply.'
            : applied
              ? 'You already have an application for this class.'
              : full
                ? 'No seats left right now. Seats return if someone’s hold expires.'
                : '';
          return (
            <article className="card class-card" key={c.id}>
              <h3>{c.name}</h3>
              <dl className="seats">
                <div><dt>Total</dt><dd>{c.totalSeats}</dd></div>
                <div><dt>On hold</dt><dd>{c.heldSeats}</dd></div>
                <div><dt>Confirmed</dt><dd>{c.confirmedSeats}</dd></div>
                <div className={full ? 'none' : 'avail'}><dt>Available</dt><dd>{c.availableSeats}</dd></div>
              </dl>
              <button className="btn primary" disabled={disabled} onClick={() => onApply(c.id)}>
                {busy ? 'Applying…' : 'Apply'}
              </button>
              {hint && <p className="help">{hint}</p>}
            </article>
          );
        })}
      </div>
    </section>
  );
}
