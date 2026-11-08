import type { Application } from '../types';
import { ApplicationCard } from './ApplicationCard';
import type { NoticeData } from './Notice';

interface Props {
  studentId: string | null;
  applications: Application[] | null;
  error: string | null;
  highlightId: string | null;
  onChanged: () => Promise<void> | void;
  onNotice: (n: NoticeData) => void;
  onRetry: () => void;
}

export function MyApplications({ studentId, applications, error, highlightId, onChanged, onNotice, onRetry }: Props) {
  if (!studentId) {
    return (
      <section>
        <h2>My applications</h2>
        <p className="card muted">Enter your 17-digit student ID at the top of the page to see your applications.</p>
      </section>
    );
  }

  return (
    <section>
      <div className="section-head">
        <h2>My applications</h2>
        <span className="muted">For student ID {studentId}</span>
      </div>

      {error && (
        <div className="notice error" role="alert">
          <span>{error}{applications ? ' What you see below may be out of date.' : ''}</span>
          <button className="btn small" onClick={onRetry}>Try again</button>
        </div>
      )}

      {applications === null && !error && <p className="muted">Loading your applications…</p>}
      {applications?.length === 0 && (
        <p className="card muted">You have no applications yet. Open the Classes tab and press “Apply”.</p>
      )}

      <div className="app-list">
        {applications?.map((a) => (
          <ApplicationCard
            key={a.holdId}
            app={a}
            studentId={studentId}
            highlight={a.holdId === highlightId}
            onChanged={onChanged}
            onNotice={onNotice}
          />
        ))}
      </div>
    </section>
  );
}
