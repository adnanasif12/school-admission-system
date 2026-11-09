const FALLBACK = 'http://localhost:8080';

function resolveApiUrl(): string {
  // 1. Runtime config written at container start (config.js), 2. build-time Vite env, 3. local default.
  const runtime = typeof window !== 'undefined' ? window.__CONFIG__?.API_URL?.trim() : undefined;
  const buildTime = (import.meta.env.VITE_API_URL as string | undefined)?.trim();
  return (runtime || buildTime || FALLBACK).replace(/\/+$/, '');
}

export const API_URL = resolveApiUrl();
