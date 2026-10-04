"""Checks for fetch_players.py against a stand-in for the Unity services: python -m unittest discover -s tools/stats"""
import datetime as dt
import json
import tempfile
import unittest
from pathlib import Path

import fetch_players
import players

PROJECT, ENV = "proj", "env-prod"


def body(name, level, day):
    return json.dumps({"eventName": name, "userID": "u1", "sessionID": f"s-{day}", "questLevel": level,
                       "questId": f"quest-{level}", "eventUUID": f"{day}-{name}-{level}",
                       "eventTimestamp": f"{day} 10:00:0{level}.000", "activeSeconds": level * 10})


class FakeUnity:
    """Answers like the services: the key reads environments, the SQL endpoint wants an exchanged token, a job runs
    once before it completes, and results come back by column."""

    def __init__(self, basic_works_for_sql=False):
        self.basic_works_for_sql = basic_works_for_sql
        self.calls = []
        self.jobs = {}

    def __call__(self, method, url, auth, body_=None):
        self.calls.append((method, url.split("?")[0].rsplit("/", 2)[-2:], auth.split(" ")[0]))
        if url.endswith(f"/projects/{PROJECT}/environments"):
            return 200, {"results": [{"name": "development", "id": "env-dev"}, {"name": "production", "id": ENV}]}
        if "/auth/v1/token-exchange" in url:
            return 200, {"accessToken": "token"}
        if url.endswith("/charts/sql_de"):
            if auth.startswith("Basic") and not self.basic_works_for_sql:
                return 401, {"detail": "unauthorized"}
            day = body_["sql"].split("EVENT_DATE = '")[1][:10]
            job = f"job-{len(self.jobs)}"
            self.jobs[job] = day
            return 200, {"job": {"jobId": job, "status": "EXECUTING", "results": None}}
        if "/jobs/" in url:
            job = url.rsplit("/", 1)[1]
            day = self.jobs.pop(job, None)
            if day is None:
                return 404, {"detail": "no job"}
            if day.endswith("-03"):
                return 200, {"jobId": job, "status": "COMPLETE", "results": {"mainChart": []}}
            events = [("questStarted", 1), ("questCompleted", 1)]
            return 200, {"jobId": job, "status": "COMPLETE", "results": {"mainChart": [
                {"type": "table", "name": "EVENT_TIMESTAMP", "data": [[f"{day} 10:00:0{l}.000"] for _, l in events]},
                {"type": "table", "name": "EVENT_NAME", "data": [[n] for n, _ in events]},
                {"type": "table", "name": "USER_ID", "data": [["u1"] for _ in events]},
                {"type": "table", "name": "EVENT_JSON", "data": [[body(n, l, day)] for n, l in events]}]}}
        return 404, {"detail": url}


class FetchTests(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        fetch_players.POLL_SECONDS = 0

    def session(self, fake):
        return fetch_players.Session("Basic key", PROJECT, call=fake)

    def test_days_come_down_as_exports_the_page_reads(self):
        fake = FakeUnity()
        total = fetch_players.fetch(self.dir, days=3, today=dt.date(2026, 10, 5), session=self.session(fake), log=lambda *_: None)
        self.assertEqual(total, 4, "two events on each of the 4th and 5th, none on the 3rd")
        self.assertEqual(sorted(p.name for p in self.dir.iterdir()),
                         ["fetched-2026-10-03.csv", "fetched-2026-10-04.csv", "fetched-2026-10-05.csv"])
        events, files = players.read_exports(self.dir)
        self.assertEqual(len(events), 4)
        self.assertEqual({e["session"] for e in events}, {"s-2026-10-04", "s-2026-10-05"})

    def test_the_sql_endpoint_gets_an_exchanged_token_when_the_key_is_refused(self):
        fake = FakeUnity()
        session = self.session(fake)
        self.assertEqual(session.environment, ENV)
        session.query(fetch_players.day_sql(dt.date(2026, 10, 4)))
        self.assertEqual(session.auth, "Bearer token")
        self.assertIn(("POST", ["v1", "token-exchange"], "Basic"), fake.calls)
        fast = FakeUnity(basic_works_for_sql=True)
        session = self.session(fast)
        session.query(fetch_players.day_sql(dt.date(2026, 10, 4)))
        self.assertEqual(session.auth, "Basic key", "a key the endpoint takes is used as it is")

    def test_old_days_are_kept_and_the_last_two_fetched_again(self):
        fake = FakeUnity()
        fetch_players.fetch(self.dir, days=4, today=dt.date(2026, 10, 6), session=self.session(fake), log=lambda *_: None)
        fake.calls.clear()
        fetch_players.fetch(self.dir, days=4, today=dt.date(2026, 10, 6), session=self.session(fake), log=lambda *_: None)
        queried = [c for c in fake.calls if c[1][-1] == "sql_de" and c[2] == "Bearer"]
        self.assertEqual(len(queried), 2, "yesterday and today only")

    def test_a_refused_service_account_says_what_it_needs(self):
        for status, words in ((401, "keyId"), (403, "access to the project")):
            with self.assertRaises(fetch_players.FetchError) as caught:
                fetch_players.Session("Basic key", PROJECT, call=lambda *a, s=status: (s, {"detail": "no"}))
            self.assertIn(words, str(caught.exception))

    def test_missing_key_is_explained(self):
        with self.assertRaises(fetch_players.FetchError) as caught:
            fetch_players.credentials(self.dir / "nothing.json")
        self.assertIn("secretKey", str(caught.exception))

    def test_columns_become_rows(self):
        rows = fetch_players.rows_of({"mainChart": [
            {"type": "table", "name": "a", "data": [["1"], ["2"]]},
            {"type": "table", "name": "B", "data": [["x"], ["y"]]}]})
        self.assertEqual(rows, [{"A": "1", "B": "x"}, {"A": "2", "B": "y"}])


if __name__ == "__main__":
    unittest.main()
