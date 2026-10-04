"""Downloads player events from Unity Analytics into the players page's exports folder.

    python tools/stats/players.py --fetch [--days 30]      download, then build the page
    python tools/stats/fetch_players.py [--days 30]         download only

Runs tools/stats/players-export.sql one day at a time through the SQL Data Explorer service and writes one CSV
per day (exports/fetched-YYYY-MM-DD.csv) in the same four columns as a dashboard export. Today and yesterday are
fetched again on every run (events keep arriving); older days are kept once fetched.

It signs in with a Unity service account, never with a person's login:
  Unity Dashboard -> Administration -> Service accounts -> New
  (https://cloud.unity.com/organizations/<org>/settings/service-accounts), give it the project roles Unity Project
  Viewer and Unity Environments Viewer (service accounts have no role made for reading Analytics), then Add key.
  Save the key with
      python tools/stats/fetch_players.py --save-key
  which asks for the key id and secret and writes tools/stats/unity-service-account.json (git-ignored), or put them
  into the environment variables UNITY_SERVICE_ACCOUNT_KEY_ID and UNITY_SERVICE_ACCOUNT_SECRET.

SQL Data Explorer has no documented API: this calls the endpoint the dashboard page itself uses
(live-ops/composer/v2/.../charts/sql_de, then .../jobs/<id>), so Unity may change it without notice. The dashboard
export (docs/analytics.md) keeps working when it does.
"""
import argparse
import base64
import csv
import datetime as dt
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
PROJECT_SETTINGS = REPO / "unity" / "TrollStategy" / "ProjectSettings" / "ProjectSettings.asset"
CREDENTIALS = HERE / "unity-service-account.json"
EXPORTS = REPO / "unity" / "TrollStategy" / "Builds" / "Stats" / "players" / "exports"
ENVIRONMENT = "production"

PUBLIC_API = "https://services.api.unity.com"
COMPOSER = "https://services.unity.com/api/live-ops/composer/v2/projects/{project}/environments/{environment}"
COLUMNS = ("EVENT_TIMESTAMP", "EVENT_NAME", "USER_ID", "EVENT_JSON")
EVENTS = ("questStarted", "questCompleted", "campaignCompleted", "battleFinished", "progressHeartbeat")
POLL_SECONDS = 2
QUERY_TIMEOUT = 300


class FetchError(Exception):
    pass


def credentials(path=CREDENTIALS):
    key_id = os.environ.get("UNITY_SERVICE_ACCOUNT_KEY_ID")
    secret = os.environ.get("UNITY_SERVICE_ACCOUNT_SECRET")
    if not (key_id and secret) and Path(path).exists():
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        key_id, secret = data.get("keyId"), data.get("secretKey")
    if not (key_id and secret):
        raise FetchError(
            f"no service account key: put {{\"keyId\": ..., \"secretKey\": ...}} into {path} or set "
            "UNITY_SERVICE_ACCOUNT_KEY_ID and UNITY_SERVICE_ACCOUNT_SECRET (see the top of this file)")
    return "Basic " + base64.b64encode(f"{key_id}:{secret}".encode()).decode()


def project_id(path=PROJECT_SETTINGS):
    match = re.search(r"^\s*cloudProjectId:\s*(\S+)", Path(path).read_text(encoding="utf-8"), re.M)
    if not match:
        raise FetchError(f"no cloudProjectId in {path}: the project is not linked to Unity Cloud")
    return match.group(1)


def request(method, url, auth, body=None):
    """(status, parsed JSON) of one call; HTTP errors come back as their status instead of raising."""
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method, headers={
        "Authorization": auth, "Accept": "application/json", "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=60) as res:
            text = res.read().decode("utf-8")
            return res.status, json.loads(text) if text else None
    except urllib.error.HTTPError as error:
        text = error.read().decode("utf-8", "replace")
        try:
            return error.code, json.loads(text)
        except json.JSONDecodeError:
            return error.code, {"detail": text[:300]}


def detail(payload):
    return (payload or {}).get("detail") or (payload or {}).get("title") or json.dumps(payload)[:300]


class Session:
    """A service account's way into the project: the environment id and an authorization that the SQL endpoint
    takes, the key itself or the access token exchanged for it."""

    def __init__(self, basic, project, environment_name=ENVIRONMENT, call=request):
        self.call = call
        self.basic = basic
        self.project = project
        self.environment = self._environment_id(environment_name)
        self.auth = basic
        self.base = COMPOSER.format(project=project, environment=self.environment)

    def _environment_id(self, name):
        status, payload = self.call("GET", f"{PUBLIC_API}/unity/v1/projects/{self.project}/environments", self.basic)
        if status == 401:
            raise FetchError(f"Unity did not take the key (check keyId and secretKey): {detail(payload)}")
        if status == 403:
            raise FetchError(f"the service account may not read the project's environments: {detail(payload)}. "
                             "Give it access to the project.")
        if status != 200:
            raise FetchError(f"environments: {status} {detail(payload)}")
        for env in payload.get("results", []):
            if env.get("name") == name:
                return env["id"]
        raise FetchError(f"no environment named {name!r} in the project")

    def _exchange(self):
        url = f"{PUBLIC_API}/auth/v1/token-exchange?projectId={self.project}&environmentId={self.environment}"
        status, payload = self.call("POST", url, self.basic, {})
        if status != 200 or not (payload or {}).get("accessToken"):
            raise FetchError(f"token exchange: {status} {detail(payload)}")
        return "Bearer " + payload["accessToken"]

    def query(self, sql):
        """Rows of a SQL Data Explorer query, as dicts by column name."""
        status, payload = self.call("POST", f"{self.base}/charts/sql_de", self.auth, {"sql": sql})
        if status in (401, 403) and self.auth == self.basic:
            self.auth = self._exchange()
            status, payload = self.call("POST", f"{self.base}/charts/sql_de", self.auth, {"sql": sql})
        if status in (401, 403):
            raise FetchError(f"SQL Data Explorer refused the service account ({status}): {detail(payload)}. "
                             "Service accounts have no role made for reading Analytics; when Unity Project Viewer is "
                             "not enough, export by hand or use Data Access (docs/analytics.md).")
        if status != 200:
            raise FetchError(f"query: {status} {detail(payload)}")
        job = payload.get("job") or {}
        deadline = time.monotonic() + QUERY_TIMEOUT
        while job.get("status") not in ("COMPLETE", "FAILED", "ERROR", "CANCELLED"):
            if time.monotonic() > deadline:
                raise FetchError(f"the query did not finish in {QUERY_TIMEOUT} s")
            time.sleep(POLL_SECONDS)
            status, job = self.call("GET", f"{self.base}/jobs/{job['jobId']}", self.auth)
            if status not in (200, 202):
                raise FetchError(f"job: {status} {detail(job)}")
        if job.get("status") != "COMPLETE":
            raise FetchError(f"the query failed: {json.dumps(job)[:300]}")
        return rows_of(job.get("results") or {})


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
    session = session or Session(credentials(), project_id())
    today = today or dt.datetime.now(dt.timezone.utc).date()
    total = 0
    for back in range(days - 1, -1, -1):
        day = today - dt.timedelta(days=back)
        path = exports / f"fetched-{day.isoformat()}.csv"
        if path.exists() and back > 1:
            continue
        rows = session.query(day_sql(day))
        write_day(path, rows)
        total += len(rows)
        if rows or back <= 1:
            log(f"fetch: {day} {len(rows)} events")
    return total


def save_key(path=CREDENTIALS, ask=input, ask_secret=None):
    """Asks for the service account's key id and secret and writes them where credentials() looks."""
    import getpass
    ask_secret = ask_secret or getpass.getpass
    key_id = ask("Key ID: ").strip()
    secret = ask_secret("Secret key (not shown): ").strip()
    if not (key_id and secret):
        raise FetchError("both the key id and the secret are needed")
    Path(path).write_text(json.dumps({"keyId": key_id, "secretKey": secret}) + "\n", encoding="utf-8")
    return Path(path)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--days", type=int, default=30, help="how many days back to download (UTC)")
    parser.add_argument("--exports", type=Path, default=EXPORTS, help="where the daily CSVs go")
    parser.add_argument("--save-key", action="store_true",
                        help=f"ask for the service account key and save it to {CREDENTIALS.name}, then check it")
    args = parser.parse_args(argv)
    if args.save_key:
        try:
            path = save_key()
            session = Session(credentials(path), project_id())
        except FetchError as error:
            print(f"key not saved or not taken: {error}", file=sys.stderr)
            return 1
        print(f"saved to {path}; Unity took it (environment {ENVIRONMENT} = {session.environment})")
        return 0
    try:
        fetch(args.exports, args.days)
    except FetchError as error:
        print(f"fetch failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
