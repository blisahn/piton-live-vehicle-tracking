// Measures what one WebSocket client receives for a given subscription, to compare
// "everything" with an area or a line subscription.
//
// Node 22 or newer (global WebSocket):
//   node measure.mjs <seconds> <label> [query]
//   node measure.mjs 60 all
//   node measure.mjs 60 oslo "bbox=10.55,59.85,10.95,60.0"
//   node measure.mjs 60 line "lines=ATB:Line:2_3"
// Without a local Node 22:
//   docker run --rm --network host -v "$PWD":/m node:22-bookworm-slim node /m/measure.mjs 60 all
const [seconds = "60", label = "all", query = ""] = process.argv.slice(2);
const base = process.env.WS_URL ?? "ws://localhost:8080/ws/vehicles";
const url = query ? `${base}?${query}` : base;

const stats = { snapshots: [], deltas: [], vehicles: 0 };
const socket = new WebSocket(url);
socket.onmessage = (event) => {
  const text = String(event.data);
  const bytes = Buffer.byteLength(text);
  const message = JSON.parse(text);
  if (message.type === "snapshot") {
    stats.snapshots.push(bytes);
    stats.vehicles = message.vehicles.length;
  } else if (message.type === "delta") {
    stats.deltas.push(bytes);
  }
};
socket.onerror = (event) => console.error(label, "error", event.message ?? event);

setTimeout(() => {
  socket.close();
  const kb = (n) => (n / 1024).toFixed(1);
  const sum = (xs) => xs.reduce((a, b) => a + b, 0);
  const average = stats.deltas.length ? sum(stats.deltas) / stats.deltas.length : 0;
  console.log(
    `${label.padEnd(6)} | snapshot ${kb(stats.snapshots[0] ?? 0).padStart(7)} KB, ${String(stats.vehicles).padStart(4)} vehicles` +
      ` | ${String(stats.deltas.length).padStart(3)} deltas, avg ${kb(average).padStart(6)} KB` +
      ` | total ${kb(sum(stats.snapshots) + sum(stats.deltas)).padStart(7)} KB in ${seconds} s`,
  );
}, Number(seconds) * 1000);
