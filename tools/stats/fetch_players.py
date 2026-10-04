"""Downloads player events from Unity Analytics into the players page's exports folder, through your Unity login.

    python tools/stats/fetch_players.py --login          once: sign in to Unity in the window that opens
    python tools/stats/players.py --fetch [--days 30]    download, then build the page
    python tools/stats/fetch_players.py [--days 30]      download only

It drives a browser of its own (Playwright's Chromium, `pip install playwright` and `python -m playwright install
chromium`) with a profile in ~/.trollstrategy/unity-browser, apart from your everyday browser. --login opens it in a
window for you to sign in; later runs are headless: the script opens SQL Data Explorer, takes the session the page
itself uses and runs tools/stats/players-export.sql one day at a time through the service the page calls
(live-ops/composer/v2/.../charts/sql_de, then .../jobs/<id>). The login stays in that profile: nothing about it is
printed or written into the repository. Each day lands in exports/fetched-YYYY-MM-DD.csv in the four columns of a
dashboard export; today and yesterday are fetched again on every run (events keep arriving), older days once.

SQL Data Explorer has no documented API, so Unity may change the page or the service without notice; the export by
hand (docs/analytics.md) keeps working when it does. Service accounts cannot do this at all: as of 2026-10-04 the
service answers their key with 401 and their exchanged token with "Untrusted issuer".
"""
import argparse
import csv
import datetime as dt
import json
import re
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
PROJECT_SETTINGS = REPO / "unity" / "TrollStategy" / "ProjectSettings" / "ProjectSettings.asset"
EXPORTS = REPO / "unity" / "TrollStategy" / "Builds" / "Stats" / "players" / "exports"
PROFILE = Path.home() / ".trollstrategy" / "unity-browser"

# The dashboard's ids for the project's organization and its production environment (not secrets: they are in every
# dashboard address).
ORGANIZATION = "18968377466176"
ENVIRONMENT = "7e43d0a8-0052-48a7-b6b5-b46134e73b29"
SQL_PAGE = ("https://cloud.unity.com/organizations/{organization}/projects/{project}/environments/{environment}"
            "/analytics/v2/sql-data-explorer")
COMPOSER = "https://services.unity.com/api/live-ops/composer/v2/projects/{project}/environments/{environment}"
COLUMNS = ("EVENT_TIMESTAMP", "EVENT_NAME", "USER_ID", "EVENT_JSON")
EVENTS = ("questStarted", "questCompleted", "campaignCompleted", "battleFinished", "progressHeartbeat")
POLL_SECONDS = 2
QUERY_TIMEOUT = 300
LOGIN_SECONDS = 600


class FetchError(Exception):
    pass


def project_id(path=PROJECT_SETTINGS):
    match = re.search(r"^\s*cloudProjectId:\s*(\S+)", Path(path).read_text(encoding="utf-8"), re.M)
    if not match:
        raise FetchError(f"no cloudProjectId in {path}: the project is not linked to Unity Cloud")
    return match.group(1)


def detail(payload):
    if not isinstance(payload, dict):
        return str(payload)[:300]
    return payload.get("detail") or payload.get("title") or json.dumps(payload)[:300]


def run_query(call, base, sql, sleep=time.sleep):
    """Rows of a SQL Data Explorer query, as dicts by column name. <call>(method, url, body) -> (status, payload)."""
    status, payload = call("POST", f"{base}/charts/sql_de", {"sql": sql})
    if status in (401, 403):
        raise FetchError(f"Unity refused the session ({status}): {detail(payload)}. "
                         "Sign in again: python tools/stats/fetch_players.py --login")
    if status != 200:
        raise FetchError(f"query: {status} {detail(payload)}")
    job = (payload or {}).get("job") or {}
    deadline = time.monotonic() + QUERY_TIMEOUT
    while job.get("status") not in ("COMPLETE", "FAILED", "ERROR", "CANCELLED"):
        if time.monotonic() > deadline:
            raise FetchError(f"the query did not finish in {QUERY_TIMEOUT} s")
        sleep(POLL_SECONDS)
        status, job = call("GET", f"{base}/jobs/{job['jobId']}", None)
        if status not in (200, 202):
            raise FetchError(f"job: {status} {detail(job)}")
    if job.get("status") != "COMPLETE":
        raise FetchError(f"the query failed: {json.dumps(job)[:300]}")
    return rows_of(job.get("results") or {})


class BrowserSession:
    """The fetch browser signed in to Unity: it opens SQL Data Explorer, keeps the session the page sends with its own
    calls and makes the same calls with it."""

    def __init__(self, project, headless=True, wait_seconds=60, log=print):
        try:
            from playwright.sync_api import sync_playwright
        except ImportError:
            raise FetchError("Playwright is missing: pip install playwright, then python -m playwright install chromium")
        self._playwright = sync_playwright().start()
        self._context = None
        try:
            PROFILE.mkdir(parents=True, exist_ok=True)
            self._context = self._playwright.chromium.launch_persistent_context(
                str(PROFILE), headless=headless, viewport={"width": 1400, "height": 900})
            page = self._context.pages[0] if self._context.pages else self._context.new_page()
            seen = {}

            def watch(request):
                if request.url.startswith("https://services.unity.com/api/"):
                    auth = request.headers.get("authorization", "")
                    if auth.lower().startswith("bearer "):
                        seen["auth"] = auth

            page.on("request", watch)
            page.goto(SQL_PAGE.format(organization=ORGANIZATION, project=project, environment=ENVIRONMENT),
                      wait_until="domcontentloaded")
            if not headless:
                log("fetch: sign in to Unity in the window that opened; it closes by itself once the dashboard loads")
            deadline = time.monotonic() + wait_seconds
            while "auth" not in seen and time.monotonic() < deadline:
                page.wait_for_timeout(500)
            if "auth" not in seen:
                raise FetchError("the fetch browser is not signed in to Unity: run python tools/stats/fetch_players.py --login")
            self._auth = seen["auth"]
            self.base = COMPOSER.format(project=project, environment=ENVIRONMENT)
        except BaseException:
            self.close()
            raise

    def call(self, method, url, body=None):
        response = self._context.request.fetch(url, method=method, data=json.dumps(body) if body is not None else None,
                                               headers={"Authorization": self._auth, "Accept": "application/json",
                                                        "Content-Type": "application/json",
                                                        "X-Client-ID": "unity-dashboard"})
        text = response.text()
        try:
            payload = json.loads(text) if text else None
        except json.JSONDecodeError:
            payload = {"detail": text[:300]}
        return response.status, payload

    def query(self, sql):
        return run_query(self.call, self.base, sql)

    def close(self):
        if self._context is not None:
            self._context.close()
            self._context = None
        if self._playwright is not None:
            self._playwright.stop()
            self._playwright = None

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()


def rows_of(results):
    """The service answers by column (name, then one one-element list per row); this turns it into rows."""
    columns = [c for c in results.get("mainChart") or [] if c.get("type") == "table"]
    if not columns:
        return []
    count = max(len(c.get("data") or []) for c in columns)
    rows = []
    for i in range(count):
        row = {}
        for c in columns:
            cell = (c.get("data") or [])[i] if i < len(c.get("data") or []) else None
            row[c["name"].upper()] = cell[0] if isinstance(cell, list) and cell else cell
        rows.append(row)
    return rows


def day_sql(day):
    names = ", ".join(f"'{e}'" for e in EVENTS)
    return (f"SELECT {', '.join(COLUMNS)} FROM EVENTS WHERE EVENT_DATE = '{day.isoformat()}' "
            f"AND EVENT_NAME IN ({names}) ORDER BY EVENT_TIMESTAMP")


def write_day(path, rows):
    tmp = path.with_suffix(".tmp")
    with open(tmp, "w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(COLUMNS)
        for row in rows:
            body = row.get("EVENT_JSON")
            if not isinstance(body, str):
                body = json.dumps(body)
            writer.writerow([row.get("EVENT_TIMESTAMP") or "", row.get("EVENT_NAME") or "", row.get("USER_ID") or "", body])
    tmp.replace(path)


def fetch(exports=EXPORTS, days=30, today=None, session=None, log=print):
    """Downloads the last <days> days into <exports>; returns how many events came down."""
    exports = Path(exports)
    exports.mkdir(parents=True, exist_ok=True)
    today = today or dt.datetime.now(dt.timezone.utc).date()
    wanted = []
    for back in range(days - 1, -1, -1):
        day = today - dt.timedelta(days=back)
        path = exports / f"fetched-{day.isoformat()}.csv"
        if not (path.exists() and back > 1):
            wanted.append((back, day, path))
    if not wanted:
        return 0
    own = session is None
    session = session or BrowserSession(project_id(), log=log)
    try:
        total = 0
        for back, day, path in wanted:
            rows = session.query(day_sql(day))
            write_day(path, rows)
            total += len(rows)
            if rows or back <= 1:
                log(f"fetch: {day} {len(rows)} events")
        return total
    finally:
        if own:
            session.close()


def login(log=print):
    """Opens the fetch browser in a window until the person has signed in to Unity."""
    with BrowserSession(project_id(), headless=False, wait_seconds=LOGIN_SECONDS, log=log):
        pass
    log(f"fetch: signed in; the session is kept in {PROFILE}")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--days", type=int, default=30, help="how many days back to download (UTC)")
    parser.add_argument("--exports", type=Path, default=EXPORTS, help="where the daily CSVs go")
    parser.add_argument("--login", action="store_true", help="sign in to Unity in the fetch browser (once)")
    args = parser.parse_args(argv)
    try:
        if args.login:
            login()
        else:
            fetch(args.exports, args.days)
    except FetchError as error:
        print(f"fetch failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
