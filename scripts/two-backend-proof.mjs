// Proof that held + confirmed never exceeds totalSeats with TWO backend containers on ONE database.
//
//   docker compose --profile scale run --rm two-backend-proof
//
// For several classes with very few seats, it fires many parallel "apply" requests, alternating between
// backend 1 and backend 2 (separate processes, separate connection pools, same PostgreSQL), then checks
// the seat arithmetic through BOTH backends. Exits non-zero if any invariant is violated.
//
// Node 20+ only (global fetch). Env: BACKEND_URLS, ROUNDS, REQUESTS.

const URLS = (process.env.BACKEND_URLS || 'http://localhost:8080,http://localhost:8081').split(',').map((u) => u.trim());
const ROUNDS = Number(process.env.ROUNDS || 6);
const REQUESTS = Number(process.env.REQUESTS || 150);
const SEATS = [1, 2, 3, 5, 7, 10];

const RUN = String(Date.now()).slice(-8);
let counter = 0;
const student = () => `7${RUN}${String(++counter).padStart(8, '0')}`; // 1 + 8 + 8 = 17 digits

const post = (base, path, studentId, body) =>
  fetch(base + path, {
    method: 'POST',
    headers: { ...(studentId ? { 'X-Student-Id': studentId } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
const getClass = async (base, id) => (await fetch(`${base}/classes/${id}`)).json();

async function waitHealthy(base) {
  for (let i = 0; i < 60; i++) {
    try {
      if ((await fetch(`${base}/health`)).ok) return;
    } catch { /* not up yet */ }
    await new Promise((r) => setTimeout(r, 2000));
  }
  throw new Error(`Backend ${base} did not become healthy`);
}

const failures = [];
const check = (ok, message) => { if (!ok) failures.push(message); };

console.log(`Backends: ${URLS.join('  |  ')}`);
await Promise.all(URLS.map(waitHealthy));
console.log(`Both healthy. ${ROUNDS} rounds x ${REQUESTS} parallel requests, alternating between the backends.\n`);
console.log('round  seats  requests  201 (b1/b2)   409  other  held+confirmed  available   verdict');

for (let round = 0; round < ROUNDS; round++) {
  const seats = SEATS[round % SEATS.length];
  const created = await (await post(URLS[0], '/classes', null, { name: `Proof ${RUN}-${round}`, totalSeats: seats })).json();
  const classId = created.id;

  // Burst: every request has its own student, so only the seat limit can stop them.
  const results = await Promise.all(
    Array.from({ length: REQUESTS }, async (_, i) => {
      const backend = i % URLS.length;
      const res = await post(URLS[backend], `/classes/${classId}/holds`, student());
      return { backend, status: res.status, body: res.status === 201 ? await res.json() : null };
    }),
  );
  const ok = results.filter((r) => r.status === 201);
  const full = results.filter((r) => r.status === 409).length;
  const other = results.length - ok.length - full;
  const perBackend = URLS.map((_, b) => ok.filter((r) => r.backend === b).length);

  // Seat arithmetic as seen through both backends.
  const views = await Promise.all(URLS.map((u) => getClass(u, classId)));
  views.forEach((v, b) => {
    check(v.heldSeats + v.confirmedSeats <= v.totalSeats, `round ${round}: backend ${b + 1} shows held+confirmed ${v.heldSeats + v.confirmedSeats} > total ${v.totalSeats}`);
    check(v.heldSeats + v.confirmedSeats === ok.length, `round ${round}: backend ${b + 1} disagrees with the number of created holds`);
    check(v.availableSeats === 0, `round ${round}: backend ${b + 1} still shows available seats`);
  });
  check(ok.length === seats, `round ${round}: ${ok.length} holds created but the class has ${seats} seats`);
  check(other === 0, `round ${round}: ${other} unexpected responses`);

  // Pay for half of the holds (alternating backends), then try to over-book again.
  const toConfirm = ok.filter((_, i) => i % 2 === 0);
  const paid = await Promise.all(toConfirm.map((r, i) => post(URLS[i % URLS.length], `/holds/${r.body.holdId}/confirm`, r.body.studentId)));
  check(paid.every((p) => p.status === 200), `round ${round}: a confirm did not return 200`);
  const late = await Promise.all(Array.from({ length: 20 }, (_, i) => post(URLS[i % URLS.length], `/classes/${classId}/holds`, student())));
  check(late.every((l) => l.status === 409), `round ${round}: a hold was accepted on a full class`);
  const after = await Promise.all(URLS.map((u) => getClass(u, classId)));
  after.forEach((v, b) => {
    check(v.heldSeats + v.confirmedSeats === seats, `round ${round}: after confirms, backend ${b + 1} shows held+confirmed ${v.heldSeats + v.confirmedSeats} (expected ${seats})`);
    check(v.confirmedSeats === toConfirm.length, `round ${round}: backend ${b + 1} shows ${v.confirmedSeats} confirmed (expected ${toConfirm.length})`);
  });

  const v = views[0];
  const verdict = failures.length === 0 ? 'OK' : 'VIOLATION';
  console.log(
    [
      String(round + 1).padStart(5),
      String(seats).padStart(5),
      String(REQUESTS).padStart(8),
      `${ok.length} (${perBackend.join('/')})`.padEnd(11),
      String(full).padStart(4),
      String(other).padStart(5),
      String(v.heldSeats + v.confirmedSeats).padStart(14),
      String(v.availableSeats).padStart(9),
      ' ' + verdict,
    ].join('  '),
  );
}

if (failures.length) {
  console.error('\nFAILED:');
  failures.forEach((f) => console.error(' - ' + f));
  process.exit(1);
}
console.log(`\nPASS: held + confirmed never exceeded totalSeats across ${ROUNDS * REQUESTS} concurrent requests on 2 backends sharing 1 database.`);
