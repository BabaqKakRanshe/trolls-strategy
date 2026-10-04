"""Checks for players.py: python -m unittest discover -s tools/stats"""
import csv
import datetime as dt
import io
import json
import tempfile
import unittest
from pathlib import Path

import players


def event(name, user, session, level=None, uuid=None, time="2026-10-05 10:00:00.000", **fields):
    body = {"eventName": name, "userID": user, "sessionID": session, "eventTimestamp": time,
            "eventUUID": uuid or f"{session}-{name}-{level}-{time}", "clientVersion": "1.0", "platform": "PC_CLIENT",
            "userCountry": "KZ"}
    if level is not None:
        body["questLevel"] = level
        body["questId"] = f"quest-{level}"
    body.update(fields)
    return body


def write_csv(path, bodies, delimiter=","):
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        writer = csv.writer(f, delimiter=delimiter)
        writer.writerow(["EVENT_TIMESTAMP", "EVENT_NAME", "USER_ID", "EVENT_JSON"])
        for b in bodies:
            writer.writerow([b["eventTimestamp"], b["eventName"], b["userID"], json.dumps(b)])


# Three games: one stuck on quest 2, one that finished quest 2 and left before claiming it, one that won a
# battle on quest 3 and is stuck there.
GAMES = [
    event("questStarted", "u1", "s1", 1, time="2026-10-05 10:00:00.000", activeSeconds=0),
    event("questCompleted", "u1", "s1", 1, time="2026-10-05 10:01:00.000", questSeconds=60, activeSeconds=60),
    event("questStarted", "u1", "s1", 2, time="2026-10-05 10:01:00.001", activeSeconds=60),
    event("progressHeartbeat", "u1", "s1", 2, time="2026-10-05 10:02:00.000", activeSeconds=120, gold=900,
          buildingCount=3, unitCount=2),
    event("questStarted", "u2", "s2", 1, time="2026-10-05 11:00:00.000", activeSeconds=0),
    event("questCompleted", "u2", "s2", 1, time="2026-10-05 11:02:00.000", questSeconds=120, activeSeconds=120),
    event("questStarted", "u2", "s2", 2, time="2026-10-05 11:02:00.001", activeSeconds=120),
    event("questCompleted", "u2", "s2", 2, time="2026-10-05 11:05:00.000", questSeconds=180, activeSeconds=300),
    event("questStarted", "u1", "s3", 1, time="2026-10-06 09:00:00.000", activeSeconds=0),
    event("questCompleted", "u1", "s3", 1, time="2026-10-06 09:00:30.000", questSeconds=30, activeSeconds=30),
    event("questStarted", "u1", "s3", 2, time="2026-10-06 09:00:30.001", activeSeconds=30),
    event("questCompleted", "u1", "s3", 2, time="2026-10-06 09:02:00.000", questSeconds=90, activeSeconds=120),
    event("questStarted", "u1", "s3", 3, time="2026-10-06 09:02:00.001", activeSeconds=120),
    event("battleFinished", "u1", "s3", 3, time="2026-10-06 09:03:00.000", missionId="mission-1", won=True,
          fallen=1, activeSeconds=180),
    event("progressHeartbeat", "u1", "s3", 3, time="2026-10-06 09:04:00.000", activeSeconds=120, gold=1100,
          buildingCount=4, unitCount=6),
]


class PlayersTests(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())

    def build(self, bodies, test_users=(), **kw):
        write_csv(self.dir / "a.csv", bodies)
        events, files = players.read_exports(self.dir)
        return players.build(events, files, set(test_users), now=dt.datetime(2026, 10, 7, 12, 0), **kw)

    def test_funnel_endings_and_times(self):
        data = self.build(GAMES)
        totals = data["totals"]
        self.assertEqual((totals["sessions"], totals["players"], totals["returningPlayers"]), (3, 2, 1))
        q1, q2, q3 = data["quests"]
        self.assertEqual((q1["reached"], q2["reached"], q3["reached"]), (3, 3, 1))
        self.assertEqual((q1["completed"], q2["completed"], q3["completed"]), (3, 2, 0))
        self.assertEqual((q2["endedStuck"], q2["endedCompleted"], q3["endedStuck"]), (1, 1, 1))
        self.assertEqual(q1["times"]["median"], 60)
        self.assertEqual(q2["times"]["n"], 2)
        self.assertEqual(q2["id"], "quest-2")
        self.assertEqual(data["battles"], [{"missionId": "mission-1", "fought": 1, "won": 1, "fallenAvg": 1,
                                            "levels": "3"}])
        endings = {s["start"][:13]: s["ending"] for s in data["sessions"]}
        self.assertEqual(endings, {"2026-10-05 10": "stuck", "2026-10-05 11": "completed", "2026-10-06 09": "stuck"})

    def test_colony_takes_the_median_per_minute(self):
        minute2 = next(c for c in self.build(GAMES)["colony"] if c["minute"] == 2)
        self.assertEqual((minute2["sessions"], minute2["gold"], minute2["units"]), (2, 1000, 4))

    def test_an_event_in_two_exports_counts_once(self):
        write_csv(self.dir / "a.csv", GAMES[:8])
        write_csv(self.dir / "b.csv", GAMES[4:], delimiter=";")
        events, files = players.read_exports(self.dir)
        self.assertEqual(files, ["a.csv", "b.csv"])
        self.assertEqual(len(events), len(GAMES))

    def test_test_users_and_old_events_are_left_out(self):
        data = self.build(GAMES, test_users={"u2"}, since="2026-10-06")
        self.assertEqual(data["totals"]["sessions"], 1)
        self.assertEqual(data["info"]["excludedSessions"], 0, "u2 played before --since, so it was never counted")
        data = self.build(GAMES, test_users={"u2"})
        self.assertEqual((data["totals"]["sessions"], data["info"]["excludedSessions"]), (2, 1))

    def test_campaign_end_is_its_own_ending(self):
        done = GAMES[4:8] + [event("campaignCompleted", "u2", "s2", None, time="2026-10-05 11:05:00.001",
                                   activeSeconds=300)]
        data = self.build(done)
        self.assertEqual(data["totals"]["campaignCompleted"], 1)
        self.assertEqual(data["sessions"][0]["ending"], "campaign")

    def test_page_and_history(self):
        data = self.build(GAMES)
        out = self.dir / "Stats" / "players"
        page = players.write_page(data, out)
        self.assertTrue(page.exists())
        self.assertTrue((out.parent / "index.html").exists(), "the hub sits above the players page")
        script = (out / "players-data.js").read_text(encoding="utf-8")
        self.assertTrue(script.startswith("window.PLAYER_DATA = {"))
        parsed = json.loads(script[len("window.PLAYER_DATA = "):].rstrip().rstrip(";"))
        self.assertEqual(parsed["totals"]["sessions"], 3)
        players.write_page(self.build(GAMES), out)
        self.assertEqual(len((out / "history.jsonl").read_text(encoding="utf-8").splitlines()), 1,
                         "the same export built twice is one history line")

    def test_an_export_without_event_json_is_refused(self):
        (self.dir / "x.csv").write_text("EVENT_NAME,USER_ID\nquestStarted,u1\n", encoding="utf-8")
        with self.assertRaises(ValueError):
            players.read_exports(self.dir)


if __name__ == "__main__":
    unittest.main()
