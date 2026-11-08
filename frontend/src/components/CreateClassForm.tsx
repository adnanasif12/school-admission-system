import { useState, type FormEvent } from 'react';
import { api } from '../api';
import { friendlyError } from '../errors';
import type { NoticeData } from './Notice';

const MAX_SEATS = 1_000_000;

interface Props {
  onCreated: () => void;
  onNotice: (n: NoticeData) => void;
}

export function CreateClassForm({ onCreated, onNotice }: Props) {
  const [name, setName] = useState('');
  const [seats, setSeats] = useState('');
  const [errors, setErrors] = useState<{ name?: string; seats?: string }>({});
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    const next: typeof errors = {};
    const cleanName = name.trim();
    if (!cleanName) next.name = 'Please enter a class name, for example "Class 6 - Morning Shift".';
    else if (cleanName.length > 200) next.name = 'The class name is too long (200 characters at most).';

    const seatText = seats.trim();
    const seatCount = Number(seatText);
    if (!/^[0-9]+$/.test(seatText) || !Number.isSafeInteger(seatCount) || seatCount < 1) {
      next.seats = 'Total seats must be a whole number of at least 1.';
    } else if (seatCount > MAX_SEATS) {
      next.seats = `Total seats cannot be more than ${MAX_SEATS.toLocaleString()}.`;
    }
    setErrors(next);
    if (next.name || next.seats) return;

    setBusy(true);
    try {
      const created = await api.createClass(cleanName, seatCount);
      onNotice({ kind: 'success', text: `Class "${created.name}" created with ${created.totalSeats} seats.` });
      setName('');
      setSeats('');
      onCreated();
    } catch (err) {
      onNotice({ kind: 'error', text: friendlyError(err, 'create') });
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="card form" onSubmit={submit} noValidate>
      <h2>Create a class (school admin)</h2>
      <p className="muted">Each shift is its own class, for example “Class 6 - Morning Shift” and “Class 6 - Day Shift”.</p>

      <label htmlFor="class-name">Class name</label>
      <input
        id="class-name"
        value={name}
        maxLength={200}
        onChange={(e) => setName(e.target.value)}
        aria-invalid={errors.name ? true : undefined}
        placeholder="Class 6 - Morning Shift"
      />
      {errors.name && <p className="help error">{errors.name}</p>}

      <label htmlFor="class-seats">Total seats</label>
      <input
        id="class-seats"
        value={seats}
        inputMode="numeric"
        onChange={(e) => setSeats(e.target.value)}
        aria-invalid={errors.seats ? true : undefined}
        placeholder="120"
      />
      {errors.seats && <p className="help error">{errors.seats}</p>}

      <button className="btn primary" type="submit" disabled={busy}>
        {busy ? 'Creating…' : 'Create class'}
      </button>
    </form>
  );
}
