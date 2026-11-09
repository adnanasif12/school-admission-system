import { describe, expect, it } from 'vitest';
import { isValidStudentId, studentIdProblem } from '../studentId';

describe('student ID (17-digit birth registration number)', () => {
  it('accepts exactly 17 digits (surrounding spaces are ignored)', () => {
    expect(isValidStudentId('20152691234567890')).toBe(true);
    expect(isValidStudentId(' 20152691234567890 ')).toBe(true);
  });

  it.each(['', '123', '2015269123456789', '201526912345678901', '2015269123456789a', '2015-2691-2345-6789'])(
    'rejects %j',
    (value) => expect(isValidStudentId(value)).toBe(false),
  );

  it('explains the problem in plain language', () => {
    expect(studentIdProblem('')).toBeNull(); // empty is not an error yet
    expect(studentIdProblem('123')).toMatch(/exactly 17 digits \(you typed 3\)/);
    expect(studentIdProblem('12a')).toMatch(/digits only/i);
    expect(studentIdProblem('20152691234567890')).toBeNull();
  });
});
