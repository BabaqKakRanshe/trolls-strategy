using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The current quest at the top left: tutorial step or level, what to do and how, each goal with its
    /// count, and what it gives. A met goal ticks with a sound; a finished quest offers its reward. Hidden in a
    /// sandbox game. The card folds (its button or Q) to the heading, the title and the goals' count, so it stays small
    /// while the player works; the reward button stays on a folded card, and a new quest opens it again.
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
            // a haul goal's route as pictures: where from, an arrow, where to
            public VisualElement Route;
            public Image From;
            public Image To;
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
        private readonly Label _progress;
        private readonly Label _title;
        private readonly Label _description;
        private readonly VisualElement _goals;
        private readonly VisualElement _rewards;
        private readonly Button _toggle;
        private readonly Label _toggleCaption;
        private readonly Button _claim;
        private readonly List<GoalRow> _goalRows = new();
        private readonly List<RewardChip> _rewardChips = new();
        private string _questId;
        private bool _collapsed;
        private bool _complete;

        /// <summary>The player asked for the finished quest's reward.</summary>
        public event Action ClaimRequested;

        public QuestTracker(VisualElement root, ColonyHudContext context, HudTooltip tooltip = null)
        {
            _context = context;
            _panel = Ui.Require<VisualElement>(root, "quest");
            _body = Ui.Require<VisualElement>(root, "quest-body");
            _chapter = Ui.Require<Label>(root, "quest-chapter");
            _progress = Ui.Require<Label>(root, "quest-progress");
            _title = Ui.Require<Label>(root, "quest-title");
            _description = Ui.Require<Label>(root, "quest-description");
            _goals = Ui.Require<VisualElement>(root, "quest-goals");
            _rewards = Ui.Require<VisualElement>(root, "quest-reward-items");
            _toggle = UiFeel.Bind(Ui.Require<Button>(root, "quest-toggle"), ToggleCollapsed, Sfx.UiBack);
            _toggleCaption = Ui.Require<Label>(root, "quest-toggle-caption");
            tooltip?.Attach(_toggle, () => _collapsed ? "Развернуть задание" : "Свернуть задание",
                () => _collapsed ? "Показать, что делать и что дадут." : "Оставить только название и счёт целей.", "Q");
            _claim = UiFeel.Bind(Ui.Require<Button>(root, "quest-claim"), () => ClaimRequested?.Invoke());
            Ui.Show(_claim, false);
            Ui.Show(_panel, false);
        }

        public bool IsShown => Ui.IsShown(_panel);
        public bool IsCollapsed => _collapsed;
        /// <summary>What the fold button says it will do: "Свернуть", or "Развернуть" on a folded card.</summary>
        public string ToggleCaption => _toggleCaption.text;
        public string Chapter => _chapter.text;
        public string Title => _title.text;
        /// <summary>How far the goals are, as the folded card shows it: "0/1" for one goal, "1 из 3" for several.</summary>
        public string Progress => _progress.text;
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
                ? $"Обучение, уровень {quest.TutorialStep} из {quest.TutorialSteps}"
                : $"Задание, уровень {quest.Level}");
            Ui.SetText(_title, quest.Title);
            Ui.SetText(_description, quest.Description);
            Ui.Show(_description, !string.IsNullOrEmpty(quest.Description));
            RenderGoals(quest, fresh);
            RenderRewards(quest);
            Ui.SetText(_progress, ProgressText(quest));

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

        /// <summary>Folds the card to its heading, title and the goals' count, or opens it again (Q).</summary>
        public void ToggleCollapsed()
        {
            _madeRoom = false;
            SetCollapsed(!_collapsed);
        }

        private bool _madeRoom;
        private bool _roomWanted;

        /// <summary>
        /// Folds the card to its header when another card comes to need the column, and opens it again afterwards
        /// unless the player folded it themselves. Only the change folds: a card the player opens meanwhile stays open.
        /// </summary>
        public void SetMakingRoom(bool making)
        {
            if (making == _roomWanted) return;
            _roomWanted = making;
            if (making && !_collapsed)
            {
                _madeRoom = true;
                SetCollapsed(true);
            }
            else if (!making && _madeRoom)
            {
                _madeRoom = false;
                SetCollapsed(false);
            }
        }

        public void SetCollapsed(bool collapsed)
        {
            _collapsed = collapsed;
            Ui.Show(_body, !collapsed);
            _panel.EnableInClassList("is-collapsed", collapsed);
            Ui.SetText(_toggleCaption, collapsed ? "Развернуть" : "Свернуть");
        }

        private static string ProgressText(QuestSnapshot quest)
        {
            var goals = quest.Goals;
            if (goals.Count == 1) return goals[0].ProgressText;
            int done = 0;
            foreach (var goal in goals)
                if (goal.Done) done++;
            return $"{done} из {goals.Count}";
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
                ShowRoute(row, goal.Goal);
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

        // a haul goal shows where from and where to as the buildings' pictures (specs/006-tutorial-guidance, FR-011)
        private void ShowRoute(GoalRow row, QuestGoal goal)
        {
            bool haul = goal.Kind == QuestGoalKind.HaulRoute;
            Ui.Show(row.Route, haul);
            if (!haul) return;
            SetArt(row.From, RewardArt.BuildingIcon(_context.Catalog, goal.Building));
            SetArt(row.To, RewardArt.BuildingIcon(_context.Catalog, goal.Destination));
        }

        private static void SetArt(Image image, Sprite sprite)
        {
            if (image.sprite != sprite) image.sprite = sprite;
            Ui.Show(image, sprite != null);
        }

        private static Image RouteArt()
        {
            var art = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            art.AddToClassList("quest-goal__route-art");
            return art;
        }

        /// <summary>The pictures of a goal's route in the card, or nulls when it shows none.</summary>
        public (Sprite From, Sprite To) RouteOf(int goalIndex) =>
            goalIndex >= 0 && goalIndex < _goalRows.Count && Ui.IsShown(_goalRows[goalIndex].Route)
                ? (_goalRows[goalIndex].From.sprite, _goalRows[goalIndex].To.sprite)
                : (null, null);

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
            row.Route = Ui.Box("quest-goal__route");
            row.Route.pickingMode = PickingMode.Ignore;
            row.From = RouteArt();
            row.To = RouteArt();
            var arrow = Ui.Text("→", "quest-goal__route-arrow t-bold");
            arrow.pickingMode = PickingMode.Ignore;
            row.Route.Add(row.From);
            row.Route.Add(arrow);
            row.Route.Add(row.To);
            Ui.Show(row.Route, false);
            row.Root.Add(row.Text);
            row.Root.Add(row.Route);
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
