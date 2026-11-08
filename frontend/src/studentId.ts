const KEY = 'schoolAdmission.studentId';

export const STUDENT_ID_PATTERN = /^[0-9]{17}$/;

export const isValidStudentId = (value: string): boolean => STUDENT_ID_PATTERN.test(value.trim());

// localStorage can throw (private mode, blocked storage): the app must still work without it.
export function loadStudentId(): string {
  try {
    return localStorage.getItem(KEY) ?? '';
  } catch {
    return '';
  }
}

export function saveStudentId(value: string): void {
  try {
    localStorage.setItem(KEY, value);
  } catch {
    /* ignore */
  }
}

/** Message under the input, or null when the value is fine (or still empty). */
export function studentIdProblem(value: string): string | null {
  const v = value.trim();
  if (v === '') return null;
  if (!/^[0-9]*$/.test(v)) return 'Use digits only (0-9).';
  if (v.length !== 17) return `The ID must be exactly 17 digits (you typed ${v.length}).`;
  return null;
}
