using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrollStrategy.Application;
using TrollStrategy.Content;

namespace TrollStrategy.UI
{
    public class ShopDockView : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _oreText;
        [SerializeField] private TextMeshProUGUI _soldText;
        [SerializeField] private TextMeshProUGUI _popText;

        [Header("Buildings")]
        [SerializeField] private Button _buildMineButton;
        [SerializeField] private Button _autoPlaceMineButton;

        [Header("Hire Controls")]
        [SerializeField] private Button _buyGoblinButton;
        [SerializeField] private Button _buyTrollButton;
        [SerializeField] private Button _plusAmountButton;
        [SerializeField] private Button _minusAmountButton;
        [SerializeField] private TextMeshProUGUI _amountText;
        [SerializeField] private TextMeshProUGUI _goblinCostText;
        [SerializeField] private TextMeshProUGUI _trollCostText;

        [Header("Placement Notification")]
        [SerializeField] private GameObject _placementBox;
        [SerializeField] private TextMeshProUGUI _placementTitle;
        [SerializeField] private Button _cancelPlacementButton;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI _statusText;

        private InteractionController _interaction;
        private GameSession _session;
        private int _hireAmount = 1;

        public void Setup(
            InteractionController interaction,
            GameSession session,
            TextMeshProUGUI gold, TextMeshProUGUI ore, TextMeshProUGUI sold, TextMeshProUGUI pop,
            Button buildMineBtn, Button autoPlaceBtn,
            Button buyGoblinBtn, Button buyTrollBtn,
            Button plusBtn, Button minusBtn, TextMeshProUGUI amtTxt,
            TextMeshProUGUI goblinCost, TextMeshProUGUI trollCost,
            GameObject placementBox, TextMeshProUGUI placementTitle, Button cancelPlacementBtn,
            TextMeshProUGUI statusTxt)
        {
            _interaction = interaction;
            _session = session;

            _goldText = gold;
            _oreText = ore;
            _soldText = sold;
            _popText = pop;

            _buildMineButton = buildMineBtn;
            _autoPlaceMineButton = autoPlaceBtn;
            _buyGoblinButton = buyGoblinBtn;
            _buyTrollButton = buyTrollBtn;
            _plusAmountButton = plusBtn;
            _minusAmountButton = minusBtn;
            _amountText = amtTxt;
            _goblinCostText = goblinCost;
            _trollCostText = trollCost;

            _placementBox = placementBox;
            _placementTitle = placementTitle;
            _cancelPlacementButton = cancelPlacementBtn;
            _statusText = statusTxt;

            if (_buildMineButton != null) _buildMineButton.onClick.AddListener(() => _interaction.BeginMinePlacement());
            if (_autoPlaceMineButton != null) _autoPlaceMineButton.onClick.AddListener(() => _interaction.PlaceMineAutomatically());
            if (_buyGoblinButton != null) _buyGoblinButton.onClick.AddListener(() => _interaction.BeginUnitPlacement(UnitKind.Goblin, _hireAmount));
            if (_buyTrollButton != null) _buyTrollButton.onClick.AddListener(() => _interaction.BeginUnitPlacement(UnitKind.Troll, _hireAmount));

            if (_plusAmountButton != null) _plusAmountButton.onClick.AddListener(() => ChangeAmount(1));
            if (_minusAmountButton != null) _minusAmountButton.onClick.AddListener(() => ChangeAmount(-1));
            if (_cancelPlacementButton != null) _cancelPlacementButton.onClick.AddListener(() => _interaction.CancelOrClear());

            UpdateAmountDisplay();
        }

        public void BindInteraction(InteractionController interaction)
        {
            _interaction = interaction;
        }

        public void UpdateView(GameSnapshot snapshot, InteractionMode mode, string statusMessage)
        {
            if (_goldText != null) _goldText.text = $"{snapshot.Gold:N0}";
            if (_oreText != null) _oreText.text = $"{snapshot.TotalOre:N0}";
            if (_soldText != null) _soldText.text = $"{snapshot.SoldOre:N0}";
            if (_popText != null) _popText.text = $"{snapshot.Units.Count}";

            int gCost = 40 * _hireAmount;
            int tCost = 170 * _hireAmount;

            if (_goblinCostText != null) _goblinCostText.text = $"{gCost}з";
            if (_trollCostText != null) _trollCostText.text = $"{tCost}з";

            if (_buildMineButton != null) _buildMineButton.interactable = snapshot.Gold >= 200;
            if (_buyGoblinButton != null) _buyGoblinButton.interactable = snapshot.Gold >= gCost;
            if (_buyTrollButton != null) _buyTrollButton.interactable = snapshot.Gold >= tCost;

            if (_autoPlaceMineButton != null)
                _autoPlaceMineButton.gameObject.SetActive(mode.Type == InteractionModeType.PlacingMine);

            if (_placementBox != null)
            {
                bool isPlacing = mode.Type == InteractionModeType.PlacingUnits;
                _placementBox.SetActive(isPlacing);
                if (isPlacing && _placementTitle != null)
                {
                    string unitName = mode.UnitKind == UnitKind.Goblin ? "Гоблин" : "Тролль";
                    _placementTitle.text = $"{unitName} x{mode.Amount}\n<size=10><color=#aaccbb>Кликните по свободной клетке</color></size>";
                }
            }

            if (_statusText != null)
                _statusText.text = statusMessage;
        }

        private void ChangeAmount(int delta)
        {
            _hireAmount = Mathf.Clamp(_hireAmount + delta, 1, 20);
            UpdateAmountDisplay();
        }

        private void UpdateAmountDisplay()
        {
            if (_amountText != null)
                _amountText.text = $"{_hireAmount}";

            int gCost = 40 * _hireAmount;
            int tCost = 170 * _hireAmount;
            if (_goblinCostText != null) _goblinCostText.text = $"{gCost}з";
            if (_trollCostText != null) _trollCostText.text = $"{tCost}з";
        }
    }
}
