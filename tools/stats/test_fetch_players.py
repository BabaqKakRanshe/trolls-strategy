"""Checks for fetch_players.py against a stand-in for SQL Data Explorer: python -m unittest discover -s tools/stats"""
import datetime as dt
import json
import tempfile
import unittest
from pathlib import Path

import fetch_players
import players


def body(name, level, day):
    return json.dumps({"eventName": name, "userID": "u1", "sessionID": f"s-{day}", "questLevel": level,
                       "questId": f"quest-{level}", "eventUUID": f"{day}-{name}-{level}",
                       "eventTimestamp": f"{day} 10:00:0{level}.000", "activeSeconds": level * 10})


class FakeExplorer:
    """Answers like the service behind SQL Data Explorer: a job runs once before it completes, results come back by
    column, and no events on the 3rd."""

    def __init__(self):
        self.calls = []
        self.jobs = {}

    def __call__(self, method, url, body_=None):
        self.calls.append((method, url.rsplit("/", 1)[-1]))
        if url.endswith("/charts/sql_de"):
            day = body_["sql"].split("EVENT_DATE = '")[1][:10]
            job = f"job-{len(self.jobs)}"
            self.jobs[job] = [day, False]
            return 200, {"job": {"jobId": job, "status": "EXECUTING", "results": None}}
        job = url.rsplit("/", 1)[1]
        day, ran = self.jobs[job]
        if not ran:
            self.jobs[job][1] = True
            return 202, {"jobId": job, "status": "EXECUTING", "results": None}
        if day.endswith("-03"):
            return 200, {"jobId": job, "status": "COMPLETE", "results": {"mainChart": []}}
        events = [("questStarted", 1), ("questCompleted", 1)]
        return 200, {"jobId": job, "status": "COMPLETE", "results": {"mainChart": [
            {"type": "table", "name": "EVENT_TIMESTAMP", "data": [[f"{day} 10:00:0{l}.000"] for _, l in events]},
            {"type": "table", "name": "EVENT_NAME", "data": [[n] for n, _ in events]},
            {"type": "table", "name": "USER_ID", "data": [["u1"] for _ in events]},
            {"type": "table", "name": "EVENT_JSON", "data": [[body(n, l, day)] for n, l in events]}]}}


class FakeSession:
    def __init__(self, explorer):
        self.explorer = explorer

    def query(self, sql):
        return fetch_players.run_query(self.explorer, "https://base", sql, sleep=lambda _: None)


class FetchTests(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())

    def fetch(self, explorer, days, today):
        return fetch_players.fetch(self.dir, days=days, today=today, session=FakeSession(explorer), log=lambda *_: None)

    def test_days_come_down_as_exports_the_page_reads(self):
        total = self.fetch(FakeExplorer(), 3, dt.date(2026, 10, 5))
        self.assertEqual(total, 4, "two events on each of the 4th and 5th, none on the 3rd")
        self.assertEqual(sorted(p.name for p in self.dir.iterdir()),
                         ["fetched-2026-10-03.csv", "fetched-2026-10-04.csv", "fetched-2026-10-05.csv"])
        events, files = players.read_exports(self.dir)
        self.assertEqual(len(events), 4)
        self.assertEqual({e["session"] for e in events}, {"s-2026-10-04", "s-2026-10-05"})

    def test_a_query_waits_for_its_job(self):
        explorer = FakeExplorer()
        rows = fetch_players.run_query(explorer, "https://base", fetch_players.day_sql(dt.date(2026, 10, 4)),
                                       sleep=lambda _: None)
        self.assertEqual(len(rows), 2)
        self.assertEqual([c[0] for c in explorer.calls], ["POST", "GET", "GET"], "started, still running, done")

    def test_old_days_are_kept_and_the_last_two_fetched_again(self):
        explorer = FakeExplorer()
        self.fetch(explorer, 4, dt.date(2026, 10, 6))
        explorer.calls.clear()
        self.fetch(explorer, 4, dt.date(2026, 10, 6))
        self.assertEqual(sum(1 for c in explorer.calls if c == ("POST", "sql_de")), 2, "yesterday and today only")

    def test_a_refused_session_asks_to_sign_in_again(self):
        with self.assertRaises(fetch_players.FetchError) as caught:
            fetch_players.run_query(lambda *a: (401, {"detail": "expired"}), "https://base", "select 1")
        self.assertIn("--login", str(caught.exception))

    def test_a_failed_job_is_reported(self):
        def failing(method, url, body_=None):
            if method == "POST":
                return 200, {"job": {"jobId": "j", "status": "EXECUTING"}}
            return 200, {"jobId": "j", "status": "FAILED", "results": None}
        with self.assertRaises(fetch_players.FetchError):
            fetch_players.run_query(failing, "https://base", "select 1", sleep=lambda _: None)

    def test_columns_become_rows(self):
        rows = fetch_players.rows_of({"mainChart": [
            {"type": "table", "name": "a", "data": [["1"], ["2"]]},
            {"type": "table", "name": "B", "data": [["x"], ["y"]]}]})
        self.assertEqual(rows, [{"A": "1", "B": "x"}, {"A": "2", "B": "y"}])


if __name__ == "__main__":
    unittest.main()
