using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Presentation.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The current quest at the top left: tutorial step or level, what to do and how, each goal with its
    /// count, and what it gives. A met goal ticks with a sound; a finished quest offers its reward. Hidden in a
    /// sandbox game. The body folds away so the card can stay small while the player works.
    /// </summary>
    public sealed class QuestTracker
    {
        private sealed class GoalRow
        {
            public VisualElement Root;
            public VisualElement Check;
            public Label Text;
            public Label Count;
            public bool Done;
        }

        private sealed class RewardChip
        {
            public VisualElement Root;
            public Image Art;
            public Label Text;
        }

        private readonly ColonyHudContext _context;
        private readonly VisualElement _panel;
        private readonly VisualElement _body;
        private readonly Label _chapter;
        private readonly Label _title;
        private readonly Label _description;
        private readonly VisualElement _goals;
        private readonly VisualElement _rewards;
        private readonly Button _toggle;
        private readonly Button _claim;
        private readonly List<GoalRow> _goalRows = new();
        private readonly List<RewardChip> _rewardChips = new();
        private string _questId;
        private bool _collapsed;
        private bool _complete;

        /// <summary>The player asked for the finished quest's reward.</summary>
        public event Action ClaimRequested;

        public QuestTracker(VisualElement root, ColonyHudContext context)
        {
            _context = context;
            _panel = Ui.Require<VisualElement>(root, "quest");
            _body = Ui.Require<VisualElement>(root, "quest-body");
            _chapter = Ui.Require<Label>(root, "quest-chapter");
            _title = Ui.Require<Label>(root, "quest-title");
            _description = Ui.Require<Label>(root, "quest-description");
            _goals = Ui.Require<VisualElement>(root, "quest-goals");
            _rewards = Ui.Require<VisualElement>(root, "quest-reward-items");
            _toggle = UiFeel.Bind(Ui.Require<Button>(root, "quest-toggle"), () => SetCollapsed(!_collapsed), Sfx.UiBack);
            _claim = UiFeel.Bind(Ui.Require<Button>(root, "quest-claim"), () => ClaimRequested?.Invoke());
            Ui.Show(_claim, false);
            Ui.Show(_panel, false);
        }

        public bool IsShown => Ui.IsShown(_panel);
        public bool IsCollapsed => _collapsed;
        public string Chapter => _chapter.text;
        public string Title => _title.text;
        public Button ClaimButton => _claim;

        public IReadOnlyList<string> GoalLines
        {
            get
            {
                var lines = new List<string>(_goalRows.Count);
                foreach (var row in _goalRows)
                    if (Ui.IsShown(row.Root)) lines.Add($"{row.Text.text} {row.Count.text}");
                return lines;
            }
        }

        public void Refresh(GameSnapshot snapshot)
        {
            var progress = snapshot.Progress;
            var quest = progress.Enabled ? progress.Quest : null;
            if (quest == null)
            {
                Ui.Show(_panel, false);
                _questId = null;
                return;
            }

            bool fresh = quest.Id != _questId;
            bool wasShown = IsShown;
            _questId = quest.Id;
            Ui.Show(_panel, true);

            // one numbering everywhere: the catalog and the battle button name the same levels
            Ui.SetText(_chapter, quest.IsTutorial
                ? $"ОБУЧЕНИЕ · УРОВЕНЬ {quest.TutorialStep} ИЗ {quest.TutorialSteps}"
                : $"ЗАДАНИЕ · УРОВЕНЬ {quest.Level}");
            Ui.SetText(_title, quest.Title);
            Ui.SetText(_description, quest.Description);
            Ui.Show(_description, !string.IsNullOrEmpty(quest.Description));
            RenderGoals(quest, fresh);
            RenderRewards(quest);

            bool complete = quest.IsComplete;
            _panel.EnableInClassList("is-complete", complete);
            Ui.Show(_claim, complete);
            if (complete && !_complete && !fresh)
            {
                UiMotion.Punch(_panel, .06f, .35f);
                UiMotion.PopIn(_claim, .3f);
            }
            _complete = complete;

            if (fresh && wasShown)
            {
                // a new quest: unfold it so the player reads what comes next
                SetCollapsed(false);
                UiMotion.PopIn(_panel, .3f);
            }
        }

        public void SetCollapsed(bool collapsed)
        {
            _collapsed = collapsed;
            Ui.Show(_body, !collapsed);
            Ui.SetText(_toggle, collapsed ? "+" : "−");
        }

        private void RenderGoals(QuestSnapshot quest, bool fresh)
        {
            var goals = quest.Goals;
            while (_goalRows.Count < goals.Count) _goalRows.Add(CreateGoalRow());
            for (int i = 0; i < _goalRows.Count; i++)
            {
                var row = _goalRows[i];
                Ui.Show(row.Root, i < goals.Count);
                if (i >= goals.Count) continue;
                var goal = goals[i];
                Ui.SetText(row.Text, goal.Text);
                Ui.SetText(row.Count, goal.ProgressText);
                row.Root.EnableInClassList("is-done", goal.Done);
                if (goal.Done && !row.Done && !fresh)
                {
                    // a goal was just met: tick it off where the player is looking
                    UiMotion.Punch(row.Check, .45f, .35f);
                    GameAudio.Play(Sfx.Equip, .8f, 1.15f);
                }
                row.Done = goal.Done;
            }
        }

        private void RenderRewards(QuestSnapshot quest)
        {
            var rewards = quest.Rewards;
            while (_rewardChips.Count < rewards.Count) _rewardChips.Add(CreateRewardChip());
            for (int i = 0; i < _rewardChips.Count; i++)
            {
                var chip = _rewardChips[i];
                Ui.Show(chip.Root, i < rewards.Count);
                if (i >= rewards.Count) continue;
                var reward = rewards[i];
                Ui.SetText(chip.Text, reward.Title);
                var sprite = RewardArt.For(reward, _context.Catalog);
                if (chip.Art.sprite != sprite) chip.Art.sprite = sprite;
                Ui.Show(chip.Art, sprite != null);
            }
        }

        private GoalRow CreateGoalRow()
        {
            var row = new GoalRow { Root = Ui.Box("quest-goal") };
            row.Check = Ui.Box("quest-goal__check");
            row.Text = Ui.Text(string.Empty, "quest-goal__text");
            row.Count = Ui.Text(string.Empty, "quest-goal__count t-medium");
            row.Root.Add(row.Check);
            row.Root.Add(row.Text);
            row.Root.Add(row.Count);
            _goals.Add(row.Root);
            return row;
        }

        private RewardChip CreateRewardChip()
        {
            var chip = new RewardChip { Root = Ui.Box("quest-reward-chip") };
            chip.Art = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            chip.Art.AddToClassList("quest-reward-chip__art");
            chip.Text = Ui.Text(string.Empty, "quest-reward-chip__text t-medium");
            chip.Root.Add(chip.Art);
            chip.Root.Add(chip.Text);
            _rewards.Add(chip.Root);
            return chip;
        }
    }
}
