using System;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Map;
using UnityEngine;

namespace TrollStrategy.Presentation.Feel
{
    /// <summary>
    /// Answers every colony command within a frame: a sound for what happened, a mark on the building it
    /// concerned, and for a refusal the reason where the player clicked plus the HUD's own "no".
    /// Listens only; the session and interaction controller still decide every outcome.
    /// </summary>
    public sealed class ColonyFeedback : MonoBehaviour
    {
        private static readonly Color RefusalColor = new(1f, .5f, .42f, 1f);
        private static readonly Color TargetColor = new(1f, .84f, .35f, 1f);

        private GameSession _session;
        private InteractionController _interaction;
        private TilemapWorldView _worldView;
        private BuildingVisualsManager _buildings;
        private Action _refusalCue;

        public void Init(GameSession session, InteractionController interaction, TilemapWorldView worldView,
            BuildingVisualsManager buildings, Action refusalCue)
        {
            _session = session;
            _interaction = interaction;
            _worldView = worldView;
            _buildings = buildings;
            _refusalCue = refusalCue;
            if (_session != null) _session.OnCommandResolved += OnCommandResolved;
            if (_interaction != null) _interaction.OnRefused += OnRefused;
        }

        private void OnDestroy()
        {
            if (_session != null) _session.OnCommandResolved -= OnCommandResolved;
            if (_interaction != null) _interaction.OnRefused -= OnRefused;
        }

        private void OnCommandResolved(IGameCommand command, CommandResult result)
        {
            // the battle scene gives its own feedback
            if (command is StartBattleCommand || command is AcknowledgeBattleCommand) return;
            if (!result.Ok)
            {
                Refuse(command, result.Error);
                return;
            }
            switch (command)
            {
                case BuildBuildingCommand _:
                case BuildMineCommand _:
                    GameAudio.Play(Sfx.Build);
                    break;
                case BuyUnitsCommand _:
                    GameAudio.Play(Sfx.Spawn);
                    break;
                case MoveBuildingCommand _:
                    GameAudio.Play(Sfx.Land);
                    break;
                case UpgradeBuildingCommand _:
                    GameAudio.Play(Sfx.Upgrade);
                    break;
                case DemolishBuildingCommand _:
                    GameAudio.Play(Sfx.Demolish);
                    break;
                case AssignWorkCommand work:
                    GameAudio.Play(Sfx.Select);
                    MarkBuilding(work.BuildingId);
                    break;
                case AssignHaulCommand haul:
                    GameAudio.Play(Sfx.Select);
                    MarkBuilding(haul.SourceId);
                    MarkBuilding(haul.DestinationId);
                    break;
                case SellUnitsCommand _:
                    GameAudio.Play(Sfx.Coins);
                    break;
                case SendToBarracksCommand _:
                case ReleaseUnitsCommand _:
                    GameAudio.Play(Sfx.UiBack);
                    break;
            }
        }

        private void OnRefused(string reason) => Refuse(null, reason);

        private void Refuse(IGameCommand command, string reason)
        {
            GameAudio.Play(Sfx.UiDenied);
            _refusalCue?.Invoke();
            if (_worldView == null || !TryWhere(command, out var position)) return;
            var up = _worldView.GroundOffset(1f);
            WorldPing.Show(position + up * .03f, _worldView.GroundRotation, RefusalColor, .8f * _worldView.CellSize);
            WorldToast.Show(position + up * 1.4f, up, reason, RefusalColor);
        }

        private bool TryWhere(IGameCommand command, out Vector3 position)
        {
            position = default;
            switch (command)
            {
                case BuildBuildingCommand build:
                    position = FootprintCenter(build.Cell, build.Kind);
                    return true;
                case BuildMineCommand mine:
                    position = FootprintCenter(mine.Cell, BuildingKind.Mine);
                    return true;
                case MoveBuildingCommand move:
                    position = FootprintCenter(move.Cell, KindOf(move.BuildingId));
                    return true;
                case BuyUnitsCommand buy:
                    position = _worldView.BuildingCenterWorld(buy.Cell, 1, 1);
                    return true;
                case UpgradeBuildingCommand upgrade:
                    return TryBuilding(upgrade.BuildingId, out position);
                case DemolishBuildingCommand demolish:
                    return TryBuilding(demolish.BuildingId, out position);
                case AssignWorkCommand work:
                    return TryBuilding(work.BuildingId, out position);
                case AssignHaulCommand haul:
                    return TryBuilding(haul.DestinationId, out position);
            }
            return false;
        }

        /// <summary>Centre of the footprint the command asked for, so the answer sits on the ghost, not its corner.</summary>
        private Vector3 FootprintCenter(Cell cell, BuildingKind? kind)
        {
            int width = 1, height = 1;
            if (kind != null && _session?.Catalog != null)
            {
                try
                {
                    var definition = _session.Catalog.GetBuilding(kind.Value);
                    width = Mathf.Max(1, definition.Width);
                    height = Mathf.Max(1, definition.Height);
                }
                catch (System.ArgumentOutOfRangeException) { }
            }
            return _worldView.BuildingCenterWorld(cell, width, height);
        }

        private BuildingKind? KindOf(string buildingId)
        {
            if (_session == null || buildingId == null) return null;
            foreach (var building in _session.CurrentSnapshot.Buildings)
                if (building.Id == buildingId) return building.Kind;
            return null;
        }

        private bool TryBuilding(string buildingId, out Vector3 position)
        {
            position = default;
            if (_buildings == null || buildingId == null || !_buildings.Views.TryGetValue(buildingId, out var view) ||
                view == null) return false;
            position = view.transform.position;
            return true;
        }

        private void MarkBuilding(string buildingId)
        {
            if (!TryBuilding(buildingId, out var position)) return;
            var view = _buildings.Views[buildingId];
            if (view.Model != null) Juice.Punch(view.Model.transform, .1f, .35f);
            WorldPing.Show(position + _worldView.GroundOffset(.06f), _worldView.GroundRotation, TargetColor,
                1.1f * _worldView.CellSize, .5f);
        }
    }
}
