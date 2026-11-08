#!/bin/sh
# Runs on every container start (the nginx image executes /docker-entrypoint.d/*.sh).
# Vite env vars are build-time only, so the backend URL is written into config.js here from API_URL.
set -eu

API_URL="${API_URL:-http://localhost:8080}"
# Tolerate stray carriage returns (CRLF env files) and escape characters that would break the JS string.
API_URL="$(printf '%s' "$API_URL" | tr -d '\r' | sed 's/\\/\\\\/g; s/"/\\"/g')"

printf 'window.__CONFIG__ = { API_URL: "%s" };\n' "$API_URL" > /usr/share/nginx/html/config.js
echo "frontend: config.js written with API_URL=$API_URL"
