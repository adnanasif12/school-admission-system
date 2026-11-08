import { useCallback, useEffect, useRef, useState } from 'react';
import { api } from './api';
import { ClassList } from './components/ClassList';
import { CreateClassForm } from './components/CreateClassForm';
import { MyApplications } from './components/MyApplications';
import { Notice, type NoticeData } from './components/Notice';
import { StudentIdBar } from './components/StudentIdBar';
import { friendlyError } from './errors';
import { usePolling } from './hooks';
import { isValidStudentId, loadStudentId, saveStudentId } from './studentId';
import type { Application } from './types';

type Tab = 'classes' | 'mine' | 'create';

const TABS: { id: Tab; label: string }[] = [
  { id: 'classes', label: 'Classes' },
  { id: 'mine', label: 'My applications' },
  { id: 'create', label: 'Create class (admin)' },
];

export default function App() {
  const [studentInput, setStudentInput] = useState(loadStudentId);
  const studentId = isValidStudentId(studentInput) ? studentInput.trim() : null;

  const [tab, setTab] = useState<Tab>('classes');
  const [notice, setNotice] = useState<NoticeData | null>(null);
  const [applications, setApplications] = useState<Application[] | null>(null);
  const [applicationsError, setApplicationsError] = useState<string | null>(null);
  const [classRefresh, setClassRefresh] = useState(0);
  const [applyingClassId, setApplyingClassId] = useState<string | null>(null);
  const [highlightId, setHighlightId] = useState<string | null>(null);

  // Persist whatever is typed so a refresh or a closed browser keeps it.
  useEffect(() => saveStudentId(studentInput), [studentInput]);

  // Never show one student's applications under another student's ID.
  const latestId = useRef(studentId);
  latestId.current = studentId;
  useEffect(() => {
    setApplications(null);
    setApplicationsError(null);
  }, [studentId]);

  const loadApplications = useCallback(async () => {
    const id = latestId.current;
    if (!id) return;
    try {
      const list = await api.myHolds(id);
      if (latestId.current !== id) return; // the user changed the ID while this was in flight
      setApplications(list);
      setApplicationsError(null);
    } catch (e) {
      if (latestId.current !== id) return;
      setApplicationsError(friendlyError(e, 'load'));
    }
  }, []);

  usePolling(loadApplications, 4000, studentId);

  const refreshAll = useCallback(async () => {
    setClassRefresh((k) => k + 1);
    await loadApplications();
  }, [loadApplications]);

  const apply = useCallback(
    async (classId: string) => {
      if (!studentId) return;
      setApplyingClassId(classId);
      try {
        const hold = await api.createHold(classId, studentId);
        setHighlightId(hold.holdId);
        setNotice({
          kind: 'success',
          text: 'A seat is being held for you. Pay the admission fee before the timer runs out, or the seat will be released.',
        });
        await refreshAll();
        setTab('mine');
      } catch (e) {
        setNotice({ kind: 'error', text: friendlyError(e, 'apply') });
        setClassRefresh((k) => k + 1); // numbers on screen were probably stale
      } finally {
        setApplyingClassId(null);
      }
    },
    [studentId, refreshAll],
  );

  // Anything that slips through still produces a visible message instead of a silent console error.
  useEffect(() => {
    const onRejection = (ev: PromiseRejectionEvent) => {
      ev.preventDefault();
      setNotice({ kind: 'error', text: friendlyError(ev.reason) });
    };
    window.addEventListener('unhandledrejection', onRejection);
    return () => window.removeEventListener('unhandledrejection', onRejection);
  }, []);

  const closeNotice = useCallback(() => setNotice(null), []);

  return (
    <div className="page">
      <header className="top">
        <div className="brand">
          <span className="logo" aria-hidden="true">🎓</span>
          <div>
            <h1>School Admission</h1>
            <p className="tagline">First come, first served. Apply, then pay the fee before the timer ends.</p>
          </div>
        </div>
      </header>

      <StudentIdBar value={studentInput} onChange={setStudentInput} />
      <Notice notice={notice} onClose={closeNotice} />

      <nav className="tabs" role="tablist">
        {TABS.map((t) => (
          <button
            key={t.id}
            role="tab"
            aria-selected={tab === t.id}
            className={tab === t.id ? 'tab active' : 'tab'}
            onClick={() => setTab(t.id)}
          >
            {t.label}
          </button>
        ))}
      </nav>

      <main>
        {tab === 'classes' && (
          <ClassList
            studentId={studentId}
            applications={applications}
            refreshKey={classRefresh}
            applyingClassId={applyingClassId}
            onApply={(id) => void apply(id)}
          />
        )}
        {tab === 'mine' && (
          <MyApplications
            studentId={studentId}
            applications={applications}
            error={applicationsError}
            highlightId={highlightId}
            onChanged={refreshAll}
            onNotice={setNotice}
            onRetry={() => void loadApplications()}
          />
        )}
        {tab === 'create' && (
          <CreateClassForm
            onNotice={setNotice}
            onCreated={() => {
              setClassRefresh((k) => k + 1);
              setTab('classes');
            }}
          />
        )}
      </main>
    </div>
  );
}
