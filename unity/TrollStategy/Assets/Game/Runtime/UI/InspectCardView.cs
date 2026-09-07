using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.UI
{
    public class InspectCardView : MonoBehaviour
    {
        [Header("Containers")]
        [SerializeField] private GameObject _panelRoot;

        [Header("Card Header")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _subtitleText;

        [Header("Card Details")]
        [SerializeField] private TextMeshProUGUI _detailText;

        [Header("Actions")]
        [SerializeField] private Button _actionButton1;
        [SerializeField] private TextMeshProUGUI _actionButton1Text;
        [SerializeField] private Button _actionButton2;
        [SerializeField] private TextMeshProUGUI _actionButton2Text;
        [SerializeField] private Button _closeButton;

        private InteractionController _interaction;
        private GameSession _session;

        public void Setup(
            InteractionController interaction,
            GameSession session,
            GameObject root,
            TextMeshProUGUI title,
            TextMeshProUGUI subtitle,
            TextMeshProUGUI details,
            Button btn1,
            TextMeshProUGUI btn1Text,
            Button btn2,
            TextMeshProUGUI btn2Text,
            Button closeBtn)
        {
            _panelRoot = root;
            _titleText = title;
            _subtitleText = subtitle;
            _detailText = details;
            _actionButton1 = btn1;
            _actionButton1Text = btn1Text;
            _actionButton2 = btn2;
            _actionButton2Text = btn2Text;
            _closeButton = closeBtn;

            Bind(session, interaction);
        }

        public void Bind(GameSession session, InteractionController interaction)
        {
            _session = session;
            _interaction = interaction;

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.onClick.AddListener(() => _interaction?.CloseInspect());
            }
        }

        public void UpdateView(GameSnapshot snapshot, string inspectedBuildingId, string inspectedUnitId)
        {
            if (_panelRoot == null) return;

            if (!string.IsNullOrEmpty(inspectedBuildingId))
            {
                BuildingSnapshot building = null;
                for (int i = 0; i < snapshot.Buildings.Count; i++)
                {
                    if (snapshot.Buildings[i].Id == inspectedBuildingId)
                    {
                        building = snapshot.Buildings[i];
                        break;
                    }
                }

                if (building != null)
                {
                    _panelRoot.SetActive(true);
                    RenderBuilding(building, snapshot);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(inspectedUnitId))
            {
                UnitSnapshot unit = null;
                for (int i = 0; i < snapshot.Units.Count; i++)
                {
                    if (snapshot.Units[i].Id == inspectedUnitId)
                    {
                        unit = snapshot.Units[i];
                        break;
                    }
                }

                if (unit != null)
                {
                    _panelRoot.SetActive(true);
                    RenderUnit(unit, snapshot);
                    return;
                }
            }

            _panelRoot.SetActive(false);
        }

        private void RenderBuilding(BuildingSnapshot b, GameSnapshot snapshot)
        {
            if (_titleText != null) _titleText.text = b.Name;
            if (_subtitleText != null) _subtitleText.text = "Клетка: (" + b.Cell.X + ", " + b.Cell.Y + ") | Размер: " + b.Width + "x" + b.Height;

            string details = "";
            if (b.Kind == BuildingKind.Mine)
            {
                details = "Запас руды: " + b.Ore + " / " + b.MaxOre + "\n" +
                          "Рабочие: " + b.WorkerCount + " / " + b.MaxWorkers + "\n" +
                          "Добыча: +" + b.ProductionPerSecond.ToString("F1") + " руды/сек";

                SetupAction(1, "Освободить рабочих", () =>
                {
                    var workerIds = new List<string>();
                    for (int i = 0; i < snapshot.Units.Count; i++)
                    {
                        var u = snapshot.Units[i];
                        if (u.Assignment.Kind == AssignmentKind.Work && u.Assignment.BuildingId == b.Id)
                            workerIds.Add(u.Id);
                    }
                    if (workerIds.Count > 0)
                        _session.Dispatch(new ReleaseUnitsCommand(workerIds));
                });

                SetupAction(2, null, null);
            }
            else if (b.Kind == BuildingKind.Warehouse)
            {
                details = "Запас руды: " + b.Ore + " / " + b.MaxOre + "\nОсновное хранилище ресурсов поселения.";
                SetupAction(1, null, null);
                SetupAction(2, null, null);
            }
            else if (b.Kind == BuildingKind.Market)
            {
                details = "Всего продано руды: " + snapshot.SoldOre + "\nЦена продажи: +" + _session.Catalog.Economy.OreSellPrice + " золота за ед.";
                SetupAction(1, null, null);
                SetupAction(2, null, null);
            }
            else if (b.Kind == BuildingKind.Barracks)
            {
                details = "Бараки поселения.\nЗдесь собираются и отдыхают свободные миньоны.";
                SetupAction(1, "Нанять Гоблина (25G)", () => _interaction.RecruitUnit(UnitKind.Goblin));
                SetupAction(2, "Нанять Тролля (50G)", () => _interaction.RecruitUnit(UnitKind.Troll));
            }

            if (_detailText != null) _detailText.text = details;
        }

        private void RenderUnit(UnitSnapshot u, GameSnapshot snapshot)
        {
            var def = _session.Catalog.GetUnit(u.UnitKind);
            string speciesName = def != null ? def.DisplayName : u.UnitKind.ToString();
            if (_titleText != null) _titleText.text = speciesName + " [#" + ColonySimulation.GetUnitNumber(u.Id) + "]";

            string status = "Отдыхает";
            if (u.Assignment != null)
            {
                switch (u.Assignment.Kind)
                {
                    case AssignmentKind.Idle:
                        status = "Отдыхает (Idle)";
                        break;
                    case AssignmentKind.ToWork:
                        status = "Идёт на работу в шахту";
                        break;
                    case AssignmentKind.Work:
                        status = "Добывает руду в шахте";
                        break;
                    case AssignmentKind.Haul:
                        status = u.Assignment.Carried > 0
                            ? "Несёт " + u.Assignment.Carried + " руды на склад/рынок"
                            : "Идёт за рудой к шахте";
                        break;
                }
            }

            if (_subtitleText != null) _subtitleText.text = status;

            int str = def != null ? def.Strength : 1;
            int cap = def != null ? def.CargoCapacity : 3;
            float spd = def != null ? def.Speed : 10f;
            int price = def != null ? def.Price : 25;

            if (_detailText != null)
            {
                _detailText.text = "Сила: " + str + " | Груз: " + cap + " | Скорость: " + spd + "\nПозиция: (" + u.Position.X.ToString("F1") + ", " + u.Position.Y.ToString("F1") + ")";
            }

            SetupAction(1, "В бараки", () => _interaction.SendSelectedToBarracks());
            SetupAction(2, "Продать (" + (price / 2) + "G)", () => _interaction.SellSelected());
        }

        private void SetupAction(int index, string label, UnityEngine.Events.UnityAction action)
        {
            Button btn = index == 1 ? _actionButton1 : _actionButton2;
            TextMeshProUGUI txt = index == 1 ? _actionButton1Text : _actionButton2Text;

            if (btn == null) return;

            btn.onClick.RemoveAllListeners();
            if (string.IsNullOrEmpty(label) || action == null)
            {
                btn.gameObject.SetActive(false);
            }
            else
            {
                btn.gameObject.SetActive(true);
                btn.onClick.AddListener(action);
                if (txt != null) txt.text = label;
            }
        }
    }
}
