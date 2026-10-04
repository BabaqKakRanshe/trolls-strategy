"""Player statistics page from Unity Analytics exports, next to the bots' page.

    python tools/stats/players.py [--fetch [--days 30]] [--exports DIR] [--out DIR] [--since YYYY-MM-DD]
                                  [--include-test-users] [--open]

With --fetch it first downloads the events itself (fetch_players.py, through your Unity login in a browser of its
own; once: fetch_players.py --login). Then it
reads every CSV in the exports folder (SQL Data Explorer -> run tools/stats/players-export.sql -> Export), merges
them (an event exported twice counts once), drops the developers' own installs listed in tools/stats/test-users.txt
and writes the page:

    unity/TrollStategy/Builds/Stats/index.html                 the hub: bots and players side by side
    unity/TrollStategy/Builds/Stats/players/index.html         the players page (from templates/players.html)
    unity/TrollStategy/Builds/Stats/players/players-data.js    what the page shows
    unity/TrollStategy/Builds/Stats/players/history.jsonl      one line per export, for the history table

The bots write Builds/Stats/bots (TrollStrategy > Bots > Run Campaign Bots); the players page reads their data to
compare quest times. An open page picks a new run of this script up on its own. Events and fields are the ones
CampaignTelemetry sends (docs/analytics.md).
"""
import argparse
import csv
import datetime as dt
import json
import os
import shutil
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
STATS = REPO / "unity" / "TrollStategy" / "Builds" / "Stats"
TEMPLATES = HERE / "templates"
TEST_USERS = HERE / "test-users.txt"

EVENTS = ("questStarted", "questCompleted", "campaignCompleted", "battleFinished", "progressHeartbeat")
HISTORY_LENGTH = 50
SESSION_ROWS = 300
COLONY_MINUTES = 240


# ---------- reading exports

def read_exports(folder):
    """Events from every CSV in the folder, oldest first; an event in two exports is kept once."""
    events, seen, files = [], set(), []
    for path in sorted(Path(folder).glob("*.csv")):
        files.append(path.name)
        for event in read_csv(path):
            key = event["uuid"] or (event["user"], event["session"], event["name"], event["time"], event["level"])
            if key in seen:
                continue
            seen.add(key)
            events.append(event)
    events.sort(key=lambda e: e["time"] or "")
    return events, files


def read_csv(path):
    csv.field_size_limit(min(sys.maxsize, 2 ** 31 - 1))
    with open(path, encoding="utf-8-sig", newline="") as f:
        # the header names the delimiter; quoting stays Excel's (doubled quotes inside EVENT_JSON), which the csv
        # sniffer would guess wrong from a header that has none
        first = f.readline()
        f.seek(0)
        delimiter = max(",;\t", key=first.count)
        rows = csv.reader(f, csv.excel, delimiter=delimiter)
        header = next(rows, None)
        if not header:
            return
        columns = {name.strip().strip('"').upper(): i for i, name in enumerate(header)}
        if "EVENT_JSON" not in columns:
            raise ValueError(f"{path.name}: no EVENT_JSON column; export tools/stats/players-export.sql")
        for row in rows:
            if not row or len(row) <= columns["EVENT_JSON"]:
                continue
            event = parse_event(row, columns)
            if event and event["name"] in EVENTS:
                yield event


def parse_event(row, columns):
    def column(name):
        i = columns.get(name)
        return row[i].strip() if i is not None and i < len(row) else ""

    try:
        body = json.loads(column("EVENT_JSON"))
    except json.JSONDecodeError:
        return None
    if not isinstance(body, dict):
        return None
    return {
        "name": body.get("eventName") or column("EVENT_NAME"),
        "user": body.get("userID") or column("USER_ID"),
        "session": body.get("sessionID") or "",
        "uuid": body.get("eventUUID") or "",
        "time": normalize_time(body.get("eventTimestamp") or column("EVENT_TIMESTAMP")),
        "version": body.get("clientVersion") or "",
        "platform": body.get("platform") or "",
        "country": body.get("userCountry") or "",
        "quest": body.get("questId") or "",
        "level": number(body.get("questLevel")),
        "questSeconds": number(body.get("questSeconds")),
        "activeSeconds": number(body.get("activeSeconds")),
        "mission": body.get("missionId") or "",
        "won": truth(body.get("won")),
        "fallen": number(body.get("fallen")),
        "gold": number(body.get("gold")),
        "buildings": number(body.get("buildingCount")),
        "units": number(body.get("unitCount")),
    }


def normalize_time(value):
    """'2026-10-04 09:16:22.799' (UTC, as the dashboard writes it) with any 'T' or 'Z' spelling folded in."""
    text = str(value or "").strip().replace("T", " ").rstrip("Z")
    return text[:23]


def number(value):
    if value is None or value == "" or (isinstance(value, float) and value != value):
        return None
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return None


def truth(value):
    if isinstance(value, bool):
        return value
    if value is None:
        return None
    return str(value).strip().lower() in ("1", "true", "yes")


def read_test_users(path=TEST_USERS):
    if not Path(path).exists():
        return set()
    users = set()
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        line = line.split("#", 1)[0].strip()
        if line:
            users.add(line)
    return users


# ---------- one game: a session

def sessions_of(events):
    """Events grouped by session in time order; a session without an id falls back to its player."""
    sessions = defaultdict(list)
    for e in events:
        sessions[e["session"] or e["user"]].append(e)
    return [summarize(key, evs) for key, evs in sessions.items()]


def summarize(key, events):
    levels = [e["level"] for e in events if e["level"] is not None]
    completed = {e["level"] for e in events if e["name"] == "questCompleted" and e["level"] is not None}
    last_level = max(levels) if levels else 0
    campaign = any(e["name"] == "campaignCompleted" for e in events)
    battles = [e for e in events if e["name"] == "battleFinished"]
    active = [e["activeSeconds"] for e in events if e["activeSeconds"] is not None]
    quests = Counter((e["level"], e["quest"]) for e in events if e["quest"])
    last_quest = next((q for (lvl, q), _ in quests.most_common() if lvl == last_level), "")
    if campaign:
        ending = "campaign"
    elif last_level in completed:
        ending = "completed"
    else:
        ending = "stuck"
    first = events[0]
    return {
        "key": key,
        "user": first["user"],
        "start": first["time"],
        "end": events[-1]["time"],
        "version": first["version"],
        "platform": first["platform"],
        "country": first["country"],
        "lastLevel": last_level,
        "lastQuest": last_quest,
        "completed": completed,
        "ending": ending,
        "seconds": max(active) if active else 0,
        "battles": battles,
        "events": events,
    }


def median(values):
    return statistics.median(values) if values else None


def quantile(values, q):
    if not values:
        return None
    ordered = sorted(values)
    pos = (len(ordered) - 1) * q
    low = int(pos)
    high = min(low + 1, len(ordered) - 1)
    return ordered[low] + (ordered[high] - ordered[low]) * (pos - low)


# ---------- the page's numbers

def build(events, files, test_users, since=None, include_test=False, now=None):
    excluded_sessions, excluded_users = set(), set()
    kept = []
    for e in events:
        if since and e["time"] and e["time"][:10] < since:
            continue
        if not include_test and e["user"] in test_users:
            excluded_sessions.add(e["session"] or e["user"])
            excluded_users.add(e["user"])
            continue
        kept.append(e)
    sessions = sessions_of(kept)
    sessions.sort(key=lambda s: s["start"] or "")

    quest_ids = {}
    for e in kept:
        if e["level"] is not None and e["quest"]:
            quest_ids.setdefault(e["level"], Counter())[e["quest"]] += 1
    top = max([s["lastLevel"] for s in sessions] + [0])
    quests = []
    for level in range(1, top + 1):
        times = [e["questSeconds"] for e in kept
                 if e["name"] == "questCompleted" and e["level"] == level and e["questSeconds"] is not None]
        quests.append({
            "level": level,
            "id": quest_ids[level].most_common(1)[0][0] if level in quest_ids else None,
            "reached": sum(1 for s in sessions if s["lastLevel"] >= level),
            "completed": sum(1 for s in sessions if level in s["completed"] or s["lastLevel"] > level),
            "endedStuck": sum(1 for s in sessions if s["lastLevel"] == level and s["ending"] == "stuck"),
            "endedCompleted": sum(1 for s in sessions if s["lastLevel"] == level and s["ending"] == "completed"),
            "times": {"n": len(times), "median": median(times), "p25": quantile(times, 0.25),
                      "p75": quantile(times, 0.75)},
        })

    battles = defaultdict(list)
    for s in sessions:
        for b in s["battles"]:
            battles[b["mission"] or "?"].append(b)
    battle_rows = []
    for mission, rows in sorted(battles.items(), key=lambda kv: (min(r["level"] or 0 for r in kv[1]), kv[0])):
        levels = sorted({r["level"] for r in rows if r["level"] is not None})
        battle_rows.append({
            "missionId": mission,
            "fought": len(rows),
            "won": sum(1 for r in rows if r["won"]),
            "fallenAvg": statistics.mean([r["fallen"] or 0 for r in rows]),
            "levels": f"{levels[0]}–{levels[-1]}" if len(levels) > 1 else (str(levels[0]) if levels else ""),
        })

    by_minute = defaultdict(list)
    for s in sessions:
        for e in s["events"]:
            if e["name"] == "progressHeartbeat" and e["activeSeconds"] is not None:
                minute = round(e["activeSeconds"] / 60)
                if minute <= COLONY_MINUTES:
                    by_minute[minute].append(e)
    colony = [{
        "minute": minute,
        "sessions": len(rows),
        "gold": median([r["gold"] for r in rows if r["gold"] is not None]),
        "buildings": median([r["buildings"] for r in rows if r["buildings"] is not None]),
        "units": median([r["units"] for r in rows if r["units"] is not None]),
    } for minute, rows in sorted(by_minute.items())]

    versions = defaultdict(list)
    for s in sessions:
        versions[s["version"] or "?"].append(s)
    version_rows = [{
        "version": version,
        "sessions": len(rows),
        "players": len({s["user"] for s in rows}),
        "medianLastLevel": median([s["lastLevel"] for s in rows]),
        "campaignCompleted": sum(1 for s in rows if s["ending"] == "campaign"),
        "firstSeen": rows[0]["start"],
    } for version, rows in sorted(versions.items(), key=lambda kv: kv[1][0]["start"] or "")]

    per_user = Counter(s["user"] for s in sessions)
    session_rows = [{
        "start": s["start"],
        "user": s["user"][:8],
        "version": s["version"],
        "platform": s["platform"],
        "country": s["country"],
        "lastLevel": s["lastLevel"],
        "lastQuest": s["lastQuest"],
        "ending": s["ending"],
        "minutes": round(s["seconds"] / 60, 1),
        "battles": len(s["battles"]),
        "won": sum(1 for b in s["battles"] if b["won"]),
    } for s in reversed(sessions[-SESSION_ROWS:])]

    generated = (now or dt.datetime.now()).strftime("%Y-%m-%dT%H:%M:%S")
    return {
        "info": {
            "generatedAt": generated,
            "exports": files,
            "events": len(kept),
            "firstEvent": kept[0]["time"] if kept else None,
            "lastEvent": kept[-1]["time"] if kept else None,
            "since": since,
            "excludedSessions": len(excluded_sessions),
            "excludedUsers": len(excluded_users),
            "includeTestUsers": include_test,
        },
        "totals": {
            "sessions": len(sessions),
            "players": len(per_user),
            "returningPlayers": sum(1 for n in per_user.values() if n > 1),
            "campaignCompleted": sum(1 for s in sessions if s["ending"] == "campaign"),
            "medianLastLevel": median([s["lastLevel"] for s in sessions]),
            "medianMinutes": median([s["seconds"] / 60 for s in sessions]),
        },
        "quests": quests,
        "battles": battle_rows,
        "colony": colony,
        "versions": version_rows,
        "sessions": session_rows,
    }


def history_line(data):
    info, totals = data["info"], data["totals"]
    return {
        "generatedAt": info["generatedAt"],
        "firstEvent": info["firstEvent"],
        "lastEvent": info["lastEvent"],
        "sessions": totals["sessions"],
        "players": totals["players"],
        "campaignCompleted": totals["campaignCompleted"],
        "medianLastLevel": totals["medianLastLevel"],
        "medianMinutes": totals["medianMinutes"],
    }


def update_history(path, line):
    """Appends the export's line; the same data built again replaces the last line instead of repeating it."""
    lines = []
    if path.exists():
        lines = [json.loads(l) for l in path.read_text(encoding="utf-8").splitlines() if l.strip()]
    same = lambda a, b: all(a.get(k) == b.get(k) for k in ("lastEvent", "sessions", "players", "campaignCompleted"))
    if lines and same(lines[-1], line):
        lines[-1] = line
    else:
        lines.append(line)
    lines = lines[-HISTORY_LENGTH:]
    path.write_text("".join(json.dumps(l, ensure_ascii=False) + "\n" for l in lines), encoding="utf-8")
    return lines


def write_page(data, out):
    out = Path(out)
    (out / "exports").mkdir(parents=True, exist_ok=True)
    data["history"] = update_history(out / "history.jsonl", history_line(data))
    text = json.dumps(data, ensure_ascii=False, indent=None, separators=(",", ":"))
    # line and paragraph separators end a JavaScript string literal in older engines
    text = text.replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")
    (out / "players-data.js").write_text("window.PLAYER_DATA = " + text + ";\n", encoding="utf-8")
    shutil.copyfile(TEMPLATES / "players.html", out / "index.html")
    shutil.copyfile(TEMPLATES / "index.html", out.parent / "index.html")
    return out / "index.html"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--exports", type=Path, default=STATS / "players" / "exports",
                        help="folder with the SQL Data Explorer CSV exports")
    parser.add_argument("--out", type=Path, default=STATS / "players", help="where the players page goes")
    parser.add_argument("--since", help="ignore events before this date (YYYY-MM-DD, UTC)")
    parser.add_argument("--include-test-users", action="store_true",
                        help="keep the installs listed in tools/stats/test-users.txt")
    parser.add_argument("--open", action="store_true", help="open the stats page when done")
    parser.add_argument("--fetch", action="store_true",
                        help="download the events from Unity Analytics first (fetch_players.py)")
    parser.add_argument("--days", type=int, default=30, help="with --fetch: how many days back")
    args = parser.parse_args(argv)

    args.exports.mkdir(parents=True, exist_ok=True)
    if args.fetch:
        import fetch_players
        try:
            fetch_players.fetch(args.exports, args.days)
        except fetch_players.FetchError as error:
            print(f"fetch failed: {error}", file=sys.stderr)
            return 1
    events, files = read_exports(args.exports)
    data = build(events, files, read_test_users(), args.since, args.include_test_users)
    page = write_page(data, args.out)
    totals, info = data["totals"], data["info"]
    print(f"players: {len(files)} export(s), {info['events']} events, {totals['sessions']} sessions, "
          f"{totals['players']} players; test sessions left out: {info['excludedSessions']}")
    if not files:
        print(f"no exports yet: put SQL Data Explorer CSVs into {args.exports}")
    print(f"page: {page}")
    if args.open:
        os.startfile(page.parent.parent / "index.html")
    return 0


if __name__ == "__main__":
    sys.exit(main())
