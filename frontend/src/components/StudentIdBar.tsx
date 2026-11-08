import { isValidStudentId, studentIdProblem } from '../studentId';

interface Props {
  value: string;
  onChange: (value: string) => void;
}

export function StudentIdBar({ value, onChange }: Props) {
  const problem = studentIdProblem(value);
  const valid = isValidStudentId(value);

  return (
    <section className="student-bar" aria-label="Student identity">
      <label htmlFor="student-id">Student birth registration number (17 digits)</label>
      <input
        id="student-id"
        type="text"
        inputMode="numeric"
        autoComplete="off"
        maxLength={17}
        placeholder="e.g. 20152691234567890"
        value={value}
        aria-invalid={problem ? true : undefined}
        aria-describedby="student-id-help"
        onChange={(e) => onChange(e.target.value.replace(/\s/g, ''))}
      />
      <p id="student-id-help" className={problem ? 'help error' : valid ? 'help ok' : 'help'}>
        {problem ??
          (valid
            ? 'Saved on this device. You are using this ID for all applications.'
            : 'Enter the student’s 17-digit birth registration number to apply. No login needed.')}
      </p>
    </section>
  );
}
