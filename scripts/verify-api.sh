#!/bin/bash
# Manual smoke test of the running API (docker compose up). Prints status + JSON for each step of the main flow.
B=http://localhost:8080
S1=20152691234567801; S2=20152691234567802; S3=20152691234567803
call() { # method url [student] [body] -> prints "STATUS BODY"
  local m=$1 u=$2 s=$3 d=$4
  local args=(-s -w '\n%{http_code}' -X "$m" "$B$u")
  [ -n "$s" ] && args+=(-H "X-Student-Id: $s")
  [ -n "$d" ] && args+=(-H 'Content-Type: application/json' -d "$d")
  local out; out=$(curl "${args[@]}")
  echo "$(echo "$out" | tail -1) $(echo "$out" | sed '$d')"
}
id() { sed 's/.*"'$1'":"\([^"]*\)".*/\1/'; }

echo "1  POST /classes";                 R=$(call POST /classes "" '{"name":"Verify Class","totalSeats":2}'); echo "   $R"; C=$(echo "$R" | id id)
echo "2  GET /classes/{id}";             call GET /classes/$C
echo "3  GET /classes (contains it)";    call GET /classes | grep -o "^[0-9]*\|\"id\":\"$C\"[^}]*}" | tr '\n' ' '; echo
echo "4  hold S1";                       R=$(call POST /classes/$C/holds $S1); echo "   $R"; H1=$(echo "$R" | id holdId)
echo "5  hold S2";                       R=$(call POST /classes/$C/holds $S2); echo "   $R"; H2=$(echo "$R" | id holdId)
echo "6  hold S3 (expect 409)";          call POST /classes/$C/holds $S3
echo "7  hold S1 again (expect 409)";    call POST /classes/$C/holds $S1
echo "8  GET /holds/{H1}";               call GET /holds/$H1
echo "9  class before confirm";          call GET /classes/$C
echo "10 confirm H1 (1st)";              call POST /holds/$H1/confirm $S1
echo "11 class after 1st confirm";       call GET /classes/$C
echo "12 confirm H1 (2nd)";              call POST /holds/$H1/confirm $S1
echo "13 class after 2nd confirm";       call GET /classes/$C
echo "14 DELETE H2 (expect 204)";        call DELETE /holds/$H2 $S2
echo "15 class after cancel";            call GET /classes/$C
echo "16 GET /me/holds S1";              call GET /me/holds $S1
echo "17 GET /me/holds S2";              call GET /me/holds $S2
echo "18 hold S3 now (seat freed)";      call POST /classes/$C/holds $S3

