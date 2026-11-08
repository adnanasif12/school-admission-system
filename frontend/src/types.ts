export type HoldStatus = 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';

export interface ClassInfo {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

/** One entry of GET /me/holds. */
export interface Application {
  holdId: string;
  classId: string;
  className: string;
  status: HoldStatus;
  expiresAt: string;
}

export interface CreatedHold {
  holdId: string;
  classId: string;
  studentId: string;
  status: HoldStatus;
  expiresAt: string;
}

declare global {
  interface Window {
    /** Written by /config.js (generated when the container starts). */
    __CONFIG__?: { API_URL?: string };
  }
}
