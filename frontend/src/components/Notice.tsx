import { useEffect } from 'react';

export interface NoticeData {
  kind: 'success' | 'error' | 'info';
  text: string;
}

export function Notice({ notice, onClose }: { notice: NoticeData | null; onClose: () => void }) {
  useEffect(() => {
    if (!notice || notice.kind === 'error') return; // errors stay until dismissed
    const id = setTimeout(onClose, 7000);
    return () => clearTimeout(id);
  }, [notice, onClose]);

  if (!notice) return null;
  return (
    <div className={`notice ${notice.kind}`} role={notice.kind === 'error' ? 'alert' : 'status'}>
      <span>{notice.text}</span>
      <button className="notice-close" onClick={onClose} aria-label="Dismiss message">
        ×
      </button>
    </div>
  );
}
