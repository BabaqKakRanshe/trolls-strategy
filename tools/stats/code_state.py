"""The rules as they stand, for the statistics hub: the latest commits that changed what the bots measure and the
rule files changed but not committed. The hub warns when the bots' run is older than those commits.

python tools/stats/code_state.py [--out unity/TrollStategy/Builds/Stats]
players.py and tools/bots/run-bots.ps1 call it, so the hub knows the code whenever either page is refreshed.
"""
import argparse
import datetime as dt
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
STATS = REPO / "unity" / "TrollStategy" / "Builds" / "Stats"
# BotMenu.RulePaths keeps the same list: the domain, the application, content code and data, the bots' play
# (not the files that only report on the bots or check them).
RULE_PATHS = [
    "unity/TrollStategy/Assets/Game/Runtime/Domain",
    "unity/TrollStategy/Assets/Game/Runtime/Application",
    "unity/TrollStategy/Assets/Game/Runtime/Content",
    "unity/TrollStategy/Assets/Game/Content/Definitions",
    "unity/TrollStategy/Assets/Game/Bots/*.cs",
    ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotMenu.cs",
    ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotReport.cs",
    ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotReportData.cs",
    ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotPopulationStats.cs",
    ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotInGame.cs",
    ":(exclude)unity/TrollStategy/Assets/Game/Bots/BotParity.cs",
    ":(exclude)unity/TrollStategy/Assets/Game/Bots/SnapshotDigest.cs",
]
COMMITS = 30


def git(repo, *args):
    """git's output, or None when git cannot answer."""
    try:
        result = subprocess.run(["git", "-C", str(repo), *args], capture_output=True, text=True, encoding="utf-8",
                                errors="replace", timeout=30)
    except (OSError, subprocess.TimeoutExpired):
        return None
    return result.stdout if result.returncode == 0 else None


def state(repo=REPO, now=None):
    head = git(repo, "rev-parse", "--short", "HEAD")
    log = git(repo, "log", f"-n{COMMITS}", "--format=%h%x09%cI%x09%s", "--", *RULE_PATHS)
    status = git(repo, "status", "--porcelain", "--", *RULE_PATHS)
    commits = []
    # only "\n" ends a line: splitlines() would also cut a subject at U+2028
    for line in (log or "").split("\n"):
        parts = line.split("\t", 2)
        if len(parts) == 3:
            commits.append({"hash": parts[0], "date": parts[1], "subject": parts[2]})
    return {
        "checkedAt": (now or dt.datetime.now().astimezone()).isoformat(timespec="seconds"),
        "head": head.strip() if head else None,
        "commits": commits,
        "uncommitted": None if status is None else [line[3:].strip() for line in status.split("\n") if len(line) > 3],
    }


def write(stats=STATS, repo=REPO, now=None):
    stats = Path(stats)
    stats.mkdir(parents=True, exist_ok=True)
    path = stats / "code-state.js"
    text = json.dumps(state(repo, now), ensure_ascii=False, separators=(",", ":"))
    text = text.replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")
    path.write_text("window.CODE_STATE = " + text + ";\n", encoding="utf-8")
    return path


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", type=Path, default=STATS, help="the statistics folder (the hub's)")
    args = parser.parse_args(argv)
    print(f"code state: {write(args.out)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
