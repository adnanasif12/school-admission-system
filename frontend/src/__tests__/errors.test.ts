import { describe, expect, it } from 'vitest';
import { ApiError } from '../api';
import { friendlyError } from '../errors';

describe('friendlyError (plain-language messages)', () => {
  it('server unreachable (network error)', () => {
    expect(friendlyError(new ApiError(0, 'NETWORK'))).toMatch(/cannot reach the admission server/i);
  });

  it.each([
    ['NO_SEATS', 409, /no seats left/i],
    ['ALREADY_APPLIED', 409, /already applied/i],
    ['HOLD_EXPIRED', 409, /time is up|expired/i],
    ['HOLD_CANCELLED', 409, /cancelled/i],
    ['ALREADY_CONFIRMED', 409, /already paid/i],
    ['NOT_YOUR_HOLD', 403, /different student/i],
    ['HOLD_NOT_FOUND', 404, /could not find/i],
    ['MISSING_STUDENT_ID', 400, /17-digit/i],
    ['INVALID_STUDENT_ID', 400, /exactly 17 digits/i],
  ])('maps %s (%i)', (code, status, expected) => {
    expect(friendlyError(new ApiError(status, code, 'raw server text'), 'apply')).toMatch(expected);
  });

  it('shows the server validation message for 400 (it is already written for humans)', () => {
    const msg = 'Total seats must be a whole number between 1 and 1,000,000.';
    expect(friendlyError(new ApiError(400, 'INVALID_SEATS', msg), 'create')).toBe(msg);
  });

  it('falls back by HTTP status when the code is unknown', () => {
    expect(friendlyError(new ApiError(403))).toMatch(/different student/i);
    expect(friendlyError(new ApiError(404))).toMatch(/could not find/i);
    expect(friendlyError(new ApiError(409), 'apply')).toMatch(/no seats left, or you may already have applied/i);
    expect(friendlyError(new ApiError(500))).toMatch(/problem/i);
  });

  it('never leaks raw HTTP text or returns an empty message', () => {
    for (const err of [new ApiError(502), new ApiError(418), new Error('boom'), 'oops', null, undefined]) {
      const text = friendlyError(err);
      expect(text.length).toBeGreaterThan(10);
      expect(text).not.toMatch(/^HTTP \d+/);
    }
  });
});
