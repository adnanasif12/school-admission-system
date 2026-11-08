import { ApiError } from './api';

export type Action = 'load' | 'apply' | 'pay' | 'cancel' | 'create';

const BY_CODE: Record<string, string> = {
  NO_SEATS: 'Sorry, there are no seats left in this class.',
  ALREADY_APPLIED: 'You have already applied to this class. See "My applications" to pay the fee or cancel.',
  HOLD_EXPIRED: 'Time is up: this application has expired and the seat was released. Please apply again.',
  HOLD_CANCELLED: 'This application was cancelled, so it cannot be paid. Please apply again.',
  ALREADY_CONFIRMED: 'The admission fee is already paid, so this application can no longer be cancelled here.',
  NOT_YOUR_HOLD: 'This application belongs to a different student ID.',
  HOLD_NOT_FOUND: 'We could not find that application. It may belong to a different student ID.',
  CLASS_NOT_FOUND: 'We could not find that class. It may have been removed.',
  MISSING_STUDENT_ID: 'Please enter your 17-digit birth registration number at the top of the page.',
  INVALID_STUDENT_ID: 'The student ID must be exactly 17 digits (your birth registration number).',
};

/** Turns anything thrown by the API layer into a message a parent or school admin can act on. */
export function friendlyError(err: unknown, action: Action = 'load'): string {
  if (!(err instanceof ApiError)) {
    return 'Something unexpected went wrong. Please try again.';
  }
  if (err.status === 0) {
    return 'We cannot reach the admission server right now. Please check your internet connection and try again in a moment.';
  }
  if (err.code && BY_CODE[err.code]) return BY_CODE[err.code];

  switch (err.status) {
    case 400:
      // Validation messages from the server are already written for humans.
      return err.message && err.message !== `HTTP 400`
        ? err.message
        : 'Some of the information you entered is not valid. Please check it and try again.';
    case 403:
      return 'This application belongs to a different student ID.';
    case 404:
      return 'We could not find what you asked for. It may have been removed or belong to a different student ID.';
    case 409:
      return action === 'apply'
        ? 'We could not hold a seat for you. There may be no seats left, or you may already have applied.'
        : 'This could not be completed because the application has changed. Please refresh and try again.';
    default:
      if (err.status >= 500) return 'The admission server had a problem. Please try again in a moment.';
      return 'Something went wrong. Please try again.';
  }
}
