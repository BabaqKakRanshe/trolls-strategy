"""Checks for code_state.py: python -m unittest discover -s tools/stats"""
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

import code_state


def git(repo, *args):
    subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True)


class CodeStateTests(unittest.TestCase):
    def setUp(self):
        self.repo = Path(tempfile.mkdtemp())
        git(self.repo, "init", "-q")
        git(self.repo, "config", "user.email", "test@example.com")
        git(self.repo, "config", "user.name", "test")

    def commit(self, path, text, message):
        file = self.repo / path
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_text(text, encoding="utf-8")
        git(self.repo, "add", path)
        git(self.repo, "commit", "-q", "-m", message)

    def test_only_rule_commits_and_rule_edits_count(self):
        self.commit("unity/TrollStategy/Assets/Game/Runtime/Domain/Rules.cs", "a", "rules: cheaper mines")
        self.commit("docs/readme.md", "b", "docs only")
        self.commit("unity/TrollStategy/Assets/Game/Bots/Dashboard/page.html", "c", "bots page only")
        self.commit("unity/TrollStategy/Assets/Game/Content/Definitions/Mine.asset", "d", "content: mine output")
        (self.repo / "unity/TrollStategy/Assets/Game/Runtime/Domain/Rules.cs").write_text("changed", encoding="utf-8")

        state = code_state.state(self.repo)

        self.assertEqual([c["subject"] for c in state["commits"]], ["content: mine output", "rules: cheaper mines"])
        self.assertEqual(state["uncommitted"], ["unity/TrollStategy/Assets/Game/Runtime/Domain/Rules.cs"])
        self.assertTrue(state["head"])

    def test_the_script_the_hub_loads(self):
        self.commit("unity/TrollStategy/Assets/Game/Bots/CampaignBot.cs", "a", "bots:   odd subject")
        out = self.repo / "Stats"

        path = code_state.write(out, self.repo)

        text = path.read_text(encoding="utf-8")
        self.assertTrue(text.startswith("window.CODE_STATE = {"))
        self.assertNotIn(" ", text)
        parsed = json.loads(text[len("window.CODE_STATE = "):].rstrip().rstrip(";"))
        self.assertEqual(parsed["commits"][0]["subject"], "bots:   odd subject")

    def test_outside_a_repository_it_says_unknown(self):
        state = code_state.state(Path(tempfile.mkdtemp()))
        self.assertEqual((state["head"], state["commits"], state["uncommitted"]), (None, [], None))


if __name__ == "__main__":
    unittest.main()
