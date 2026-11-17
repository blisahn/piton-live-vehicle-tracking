#!/usr/bin/env python3
"""Measure how the Entur GTFS-Realtime vehicle-positions feed behaves in practice.

Polls the feed at a fixed interval and reports what a collector would see:
snapshot sizes, how identities repeat, how often timestamps regress, and how
many rows the "write only measurements that moved forward" rule would produce.

Standard library only (a minimal protobuf wire-format reader is included), so it
runs anywhere:  python3 probe.py --minutes 30 --out result.json
"""
from __future__ import annotations

import argparse
import collections
import json
import struct
import sys
import time
import urllib.request

FEED_URL = "https://api.entur.io/realtime/v1/gtfs-rt/vehicle-positions"
CLIENT_NAME = "gtfs-rt-case-study-probe"


# --- protobuf wire format -------------------------------------------------

def read_varint(buf: bytes, i: int) -> tuple[int, int]:
    value = shift = 0
    while True:
        byte = buf[i]
        i += 1
        value |= (byte & 0x7F) << shift
        if byte < 0x80:
            return value, i
        shift += 7


def fields(buf: bytes) -> dict[int, list]:
    """Decode one message level into {field_number: [raw values]}."""
    out: dict[int, list] = collections.defaultdict(list)
    i, end = 0, len(buf)
    while i < end:
        key, i = read_varint(buf, i)
        number, wire = key >> 3, key & 7
        if wire == 0:
            value, i = read_varint(buf, i)
        elif wire == 2:
            length, i = read_varint(buf, i)
            value = buf[i:i + length]
            i += length
        elif wire == 5:
            value = buf[i:i + 4]
            i += 4
        elif wire == 1:
            value = buf[i:i + 8]
            i += 8
        else:
            raise ValueError(f"unsupported wire type {wire}")
        out[number].append(value)
    return out


def text(msg: dict, number: int) -> str:
    return msg[number][0].decode() if number in msg else ""


def f32(msg: dict, number: int) -> float | None:
    return struct.unpack("<f", msg[number][0])[0] if number in msg else None


# --- feed model -----------------------------------------------------------

def parse_snapshot(raw: bytes) -> tuple[int | None, list[dict]]:
    feed = fields(raw)
    header = fields(feed[1][0])
    header_ts = header[3][0] if 3 in header else None
    vehicles = []
    for entity_raw in feed.get(2, []):
        entity = fields(entity_raw)
        if 4 not in entity:
            continue
        vp = fields(entity[4][0])
        trip = fields(vp[1][0]) if 1 in vp else {}
        position = fields(vp[2][0]) if 2 in vp else {}
        descriptor = fields(vp[8][0]) if 8 in vp else {}
        entity_id = text(entity, 1)
        vehicles.append({
            "codespace": entity_id.split(":", 1)[0] if ":" in entity_id else "",
            "vehicle_id": text(descriptor, 1),
            "trip_id": text(trip, 1),
            "route_id": text(trip, 5),
            "lat": f32(position, 1),
            "lon": f32(position, 2),
            "has_bearing": 3 in position,
            "has_speed": 5 in position,
            "ts": vp[5][0] if 5 in vp else None,
            "has_status": 4 in vp,
            "has_stop": 7 in vp,
            "has_occupancy": 9 in vp,
        })
    return header_ts, vehicles


def valid_position(lat: float | None, lon: float | None) -> bool:
    if lat is None or lon is None:
        return False
    if abs(lat) > 90 or abs(lon) > 180:
        return False
    # (0,0) and (-1,-1) show up as "no fix" sentinels.
    return not (lat == 0 and lon == 0) and not (lat == -1 and lon == -1)


def percentile(sorted_values: list[float], p: float) -> float | None:
    if not sorted_values:
        return None
    return sorted_values[min(len(sorted_values) - 1, int(len(sorted_values) * p))]


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--minutes", type=float, default=30)
    ap.add_argument("--interval", type=float, default=3.0)
    ap.add_argument("--retention-hours", type=float, default=3.0)
    ap.add_argument("--out", help="write the JSON summary here (default: stdout)")
    args = ap.parse_args()

    deadline = time.time() + args.minutes * 60
    retention = args.retention_hours * 3600

    counters = collections.Counter()
    fetch_ms: list[float] = []
    sizes: list[int] = []
    entity_counts: list[int] = []
    last_header_ts: int | None = None
    last_ts: dict[tuple[str, str], int] = {}
    update_gaps: list[int] = []
    rows_per_poll: list[int] = []
    codespaces_by_vehicle_id: dict[str, set[str]] = collections.defaultdict(set)
    first_snapshot_profile: dict | None = None
    started = time.time()
    next_report = started + 60

    while time.time() < deadline:
        tick = time.time()
        counters["polls"] += 1
        try:
            req = urllib.request.Request(FEED_URL, headers={"ET-Client-Name": CLIENT_NAME})
            raw = urllib.request.urlopen(req, timeout=10).read()
            fetch_ms.append((time.time() - tick) * 1000)
            header_ts, vehicles = parse_snapshot(raw)
        except Exception as exc:  # network error, HTTP error or undecodable body
            counters["failed_polls"] += 1
            print(f"poll failed: {type(exc).__name__}: {exc}", file=sys.stderr, flush=True)
            time.sleep(max(0.0, args.interval - (time.time() - tick)))
            continue

        sizes.append(len(raw))
        entity_counts.append(len(vehicles))
        if header_ts is not None and last_header_ts is not None:
            if header_ts == last_header_ts:
                counters["repeated_snapshots"] += 1
            elif header_ts < last_header_ts:
                counters["header_ts_regressions"] += 1
        last_header_ts = header_ts

        now = time.time()
        rows = 0
        keys_in_snapshot = collections.Counter()
        # One vehicle appears in several entities (one per trip it served today).
        # Only its newest usable measurement in this snapshot is a candidate row.
        newest: dict[tuple[str, str], int] = {}
        for v in vehicles:
            counters["entities_seen"] += 1
            if not v["vehicle_id"]:
                counters["entities_without_vehicle_id"] += 1
                continue
            key = (v["codespace"], v["vehicle_id"])
            keys_in_snapshot[key] += 1
            codespaces_by_vehicle_id[v["vehicle_id"]].add(v["codespace"])
            ts = v["ts"]
            if ts is None:
                counters["entities_without_timestamp"] += 1
                continue
            if not valid_position(v["lat"], v["lon"]):
                counters["entities_with_invalid_position"] += 1
                continue
            if ts > now + 60:
                counters["entities_from_the_future"] += 1
                continue
            if ts > newest.get(key, -1):
                newest[key] = ts

        for key, ts in newest.items():
            previous = last_ts.get(key)
            if previous is not None and ts <= previous:
                if ts < previous:
                    counters["entity_ts_regressions"] += 1
                continue
            if now - ts > retention:
                counters["measurements_older_than_retention"] += 1
                last_ts[key] = ts
                continue
            if previous is not None:
                update_gaps.append(ts - previous)
            last_ts[key] = ts
            rows += 1
        rows_per_poll.append(rows)
        counters["rows"] += rows

        if first_snapshot_profile is None:
            n = len(vehicles) or 1
            ages = sorted(now - v["ts"] for v in vehicles if v["ts"] is not None)
            first_snapshot_profile = {
                "entities": len(vehicles),
                "distinct_vehicle_keys": len(keys_in_snapshot),
                "vehicle_keys_with_multiple_entities": sum(1 for c in keys_in_snapshot.values() if c > 1),
                "missing_pct": {
                    name: round(100 * sum(1 for v in vehicles if not v[field]) / n, 1)
                    for name, field in [
                        ("route_id", "route_id"), ("trip_id", "trip_id"), ("bearing", "has_bearing"),
                        ("speed", "has_speed"), ("current_status", "has_status"),
                        ("stop_id", "has_stop"), ("occupancy", "has_occupancy"),
                    ]
                },
                "position_age_seconds": {
                    "p10": percentile(ages, 0.10), "p50": percentile(ages, 0.50),
                    "p90": percentile(ages, 0.90), "max": ages[-1] if ages else None,
                },
                "fresh_within_60s": sum(1 for a in ages if a <= 60),
                "within_retention": sum(1 for a in ages if a <= retention),
            }

        if time.time() >= next_report:
            elapsed = time.time() - started
            print(f"{elapsed / 60:5.1f} min  polls={counters['polls']} failed={counters['failed_polls']} "
                  f"rows={counters['rows']} ({counters['rows'] / elapsed * 3600:,.0f}/h)", flush=True)
            next_report += 60
        time.sleep(max(0.0, args.interval - (time.time() - tick)))

    elapsed = time.time() - started
    # The first poll writes every vehicle once; exclude it from the steady-state rate.
    steady_rows = sum(rows_per_poll[1:])
    steady_seconds = elapsed - args.interval if len(rows_per_poll) > 1 else elapsed
    fetch_ms.sort()
    update_gaps.sort()
    summary = {
        "feed_url": FEED_URL,
        "started_at_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(started)),
        "duration_seconds": round(elapsed, 1),
        "poll_interval_seconds": args.interval,
        "retention_hours": args.retention_hours,
        "counters": dict(counters),
        "snapshot_bytes": {"min": min(sizes, default=None), "max": max(sizes, default=None)},
        "entities_per_snapshot": {"min": min(entity_counts, default=None), "max": max(entity_counts, default=None)},
        "fetch_ms": {"p50": percentile(fetch_ms, 0.5), "p95": percentile(fetch_ms, 0.95),
                     "max": fetch_ms[-1] if fetch_ms else None},
        "distinct_vehicle_keys_seen": len(last_ts),
        "vehicle_ids_shared_across_codespaces": sum(1 for s in codespaces_by_vehicle_id.values() if len(s) > 1),
        "seconds_between_measurements_per_vehicle": {
            "p10": percentile(update_gaps, 0.10), "p50": percentile(update_gaps, 0.50),
            "p90": percentile(update_gaps, 0.90),
        },
        "rows": {
            "first_poll": rows_per_poll[0] if rows_per_poll else 0,
            "steady_state_total": steady_rows,
            "per_hour": round(steady_rows / steady_seconds * 3600) if steady_seconds > 0 else None,
            "per_retention_window": round(steady_rows / steady_seconds * retention) if steady_seconds > 0 else None,
        },
        "first_snapshot": first_snapshot_profile,
    }
    payload = json.dumps(summary, indent=2)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as fh:
            fh.write(payload + "\n")
    print(payload)
    return 0


if __name__ == "__main__":
    sys.exit(main())
