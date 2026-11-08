import { useEffect, useRef, useState } from 'react';
import { api } from '../api';
import { friendlyError } from '../errors';
import { formatRemaining, useNow } from '../hooks';
import type { Application, HoldStatus } from '../types';
import type { NoticeData } from './Notice';

interface Props {
  app: Application;
  studentId: string;
  highlight?: boolean;
  /** Called after any change (or detected expiry) so lists and seat counts reload. */
  onChanged: () => Promise<void> | void;
  onNotice: (n: NoticeData) => void;
}

const LABEL: Record<HoldStatus, string> = {
  HELD: 'Seat on hold',
  CONFIRMED: 'Confirmed',
  EXPIRED: 'EXPIRED',
  CANCELLED: 'Cancelled',
};

export function ApplicationCard({ app, studentId, highlight, onChanged, onNotice }: Props) {
  const now = useNow(1000);
  const [busy, setBusy] = useState<'pay' | 'cancel' | null>(null);
  const checkedExpiry = useRef<string | null>(null);

  const remainingMs = Date.parse(app.expiresAt) - now;
  const lapsed = app.status === 'HELD' && remainingMs <= 0;
  const status: HoldStatus = lapsed ? 'EXPIRED' : app.status; // flips locally, no reload needed
  const active = status === 'HELD';

  // The moment the countdown hits zero, ask the server what really happened (once per application).
  useEffect(() => {
    if (!lapsed || checkedExpiry.current === app.holdId) return;
    checkedExpiry.current = app.holdId;
    void (async () => {
      try {
        await api.getHold(app.holdId, studentId);
      } catch {
        /* the list reload below shows any problem */
      }
      await onChanged();
    })();
  }, [lapsed, app.holdId, studentId, onChanged]);

  async function pay() {
    setBusy('pay');
    try {
      await api.confirmHold(app.holdId, studentId);
      onNotice({ kind: 'success', text: `Fee paid. Admission to “${app.className}” is confirmed.` });
    } catch (e) {
      onNotice({ kind: 'error', text: friendlyError(e, 'pay') });
    } finally {
      setBusy(null);
      await onChanged();
    }
  }

  async function cancel() {
    setBusy('cancel');
    try {
      await api.cancelHold(app.holdId, studentId);
      onNotice({ kind: 'info', text: `Your application for “${app.className}” was cancelled and the seat was released.` });
    } catch (e) {
      onNotice({ kind: 'error', text: friendlyError(e, 'cancel') });
    } finally {
      setBusy(null);
      await onChanged();
    }
  }

  return (
    <article className={`card app-card st-${status.toLowerCase()}${highlight ? ' highlight' : ''}`}>
      <div className="app-head">
        <h3>{app.className}</h3>
        <span className={`badge ${status.toLowerCase()}`}>{LABEL[status]}</span>
      </div>

      {active && (
        <p className={remainingMs < 60_000 ? 'countdown urgent' : 'countdown'} aria-live="off">
          Time left to pay: <strong>{formatRemaining(remainingMs)}</strong>
        </p>
      )}
      {status === 'EXPIRED' && <p className="help error">The time ran out and the seat was released. You can apply again from the Classes tab.</p>}
      {status === 'CONFIRMED' && <p className="help ok">The admission fee is paid. The seat is yours.</p>}
      {status === 'CANCELLED' && <p className="help">You cancelled this application.</p>}

      <div className="actions">
        <button className="btn primary" disabled={!active || busy !== null} onClick={() => void pay()}>
          {busy === 'pay' ? 'Paying…' : 'Pay fee'}
        </button>
        <button className="btn" disabled={!active || busy !== null} onClick={() => void cancel()}>
          {busy === 'cancel' ? 'Cancelling…' : 'Cancel'}
        </button>
      </div>
    </article>
  );
}
