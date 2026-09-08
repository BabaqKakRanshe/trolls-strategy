using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.UI
{
    public class ShopDockView : MonoBehaviour
    {
        [Header("Resource Grid (2x2)")]
        [SerializeField] private TextMeshProUGUI _goldValueText;
        [SerializeField] private TextMeshProUGUI _oreValueText;
        [SerializeField] private TextMeshProUGUI _soldValueText;
        [SerializeField] private TextMeshProUGUI _populationValueText;

        [Header("Buildings Section")]
        [SerializeField] private Image _mineArt;
        [SerializeField] private Button _buildMineBtn;
        [SerializeField] private TextMeshProUGUI _mineCostText;
        [SerializeField] private Button _autoPlaceMineBtn;

        [Header("Hire Section")]
        [SerializeField] private Button _decreaseHireBtn;
        [SerializeField] private Button _increaseHireBtn;
        [SerializeField] private TextMeshProUGUI _hireAmountText;

        [SerializeField] private Image _goblinArt;
        [SerializeField] private Button _buyGoblinBtn;
        [SerializeField] private TextMeshProUGUI _goblinCostText;

        [SerializeField] private Image _trollArt;
        [SerializeField] private Button _buyTrollBtn;
        [SerializeField] private TextMeshProUGUI _trollCostText;

        [Header("Unit Placement Banner")]
        [SerializeField] private GameObject _unitPlacementBanner;
        [SerializeField] private TextMeshProUGUI _unitPlacementTitle;
        [SerializeField] private Button _cancelPlacementBtn;

        [Header("Game Status")]
        [SerializeField] private TextMeshProUGUI _gameStatusText;

        private InteractionController _interaction;
        private GameSession _session;
        private int _hireAmount = 1;

        public int HireAmount => _hireAmount;

        public void Setup(
            InteractionController interaction,
            GameSession session,
            TextMeshProUGUI goldVal, TextMeshProUGUI oreVal, TextMeshProUGUI soldVal, TextMeshProUGUI popVal,
            Image mineArt, Button buildMineBtn, TextMeshProUGUI mineCost, Button autoPlaceBtn,
            Button decHireBtn, Button incHireBtn, TextMeshProUGUI hireAmountTxt,
            Image goblinArt, Button buyGoblinBtn, TextMeshProUGUI goblinCost,
            Image trollArt, Button buyTrollBtn, TextMeshProUGUI trollCost,
            GameObject placementBanner, TextMeshProUGUI placementTitle, Button cancelPlacementBtn,
            TextMeshProUGUI gameStatusText)
        {
            _goldValueText = goldVal;
            _oreValueText = oreVal;
            _soldValueText = soldVal;
            _populationValueText = popVal;

            _mineArt = mineArt;
            _buildMineBtn = buildMineBtn;
            _mineCostText = mineCost;
            _autoPlaceMineBtn = autoPlaceBtn;

            _decreaseHireBtn = decHireBtn;
            _increaseHireBtn = incHireBtn;
            _hireAmountText = hireAmountTxt;

            _goblinArt = goblinArt;
            _buyGoblinBtn = buyGoblinBtn;
            _goblinCostText = goblinCost;

            _trollArt = trollArt;
            _buyTrollBtn = buyTrollBtn;
            _trollCostText = trollCost;

            _unitPlacementBanner = placementBanner;
            _unitPlacementTitle = placementTitle;
            _cancelPlacementBtn = cancelPlacementBtn;

            _gameStatusText = gameStatusText;

            Bind(session, interaction);
        }

        public void Bind(GameSession session, InteractionController interaction)
        {
            _session = session;
            _interaction = interaction;

            if (_decreaseHireBtn != null)
            {
                _decreaseHireBtn.onClick.RemoveAllListeners();
                _decreaseHireBtn.onClick.AddListener(() => SetHireAmount(_hireAmount - 1));
            }
            if (_increaseHireBtn != null)
            {
                _increaseHireBtn.onClick.RemoveAllListeners();
                _increaseHireBtn.onClick.AddListener(() => SetHireAmount(_hireAmount + 1));
            }

            if (_buildMineBtn != null)
            {
                _buildMineBtn.onClick.RemoveAllListeners();
                _buildMineBtn.onClick.AddListener(() => _interaction?.BeginMinePlacement());
            }

            if (_autoPlaceMineBtn != null)
            {
                _autoPlaceMineBtn.onClick.RemoveAllListeners();
                _autoPlaceMineBtn.onClick.AddListener(() => _interaction?.PlaceMineAutomatically());
            }

            if (_buyGoblinBtn != null)
            {
                _buyGoblinBtn.onClick.RemoveAllListeners();
                _buyGoblinBtn.onClick.AddListener(() => _interaction?.BeginUnitPlacement(UnitKind.Goblin, _hireAmount));
            }

            if (_buyTrollBtn != null)
            {
                _buyTrollBtn.onClick.RemoveAllListeners();
                _buyTrollBtn.onClick.AddListener(() => _interaction?.BeginUnitPlacement(UnitKind.Troll, _hireAmount));
            }

            if (_cancelPlacementBtn != null)
            {
                _cancelPlacementBtn.onClick.RemoveAllListeners();
                _cancelPlacementBtn.onClick.AddListener(() => _interaction?.CancelOrClear());
            }
        }

        public void SetHireAmount(int amount)
        {
            _hireAmount = Mathf.Clamp(amount, 1, 20);
            if (_hireAmountText != null) _hireAmountText.text = _hireAmount.ToString();
            if (_session != null) UpdateView(_session.CurrentSnapshot);
        }

        public void UpdateView(GameSnapshot snapshot)
        {
            if (_session == null || snapshot == null) return;

            int gold = snapshot.Gold;
            var catalog = _session.Catalog;

            if (_goldValueText != null) _goldValueText.text = gold.ToString();
            if (_oreValueText != null) _oreValueText.text = snapshot.TotalOre.ToString();
            if (_soldValueText != null) _soldValueText.text = snapshot.SoldOre.ToString();
            if (_populationValueText != null) _populationValueText.text = snapshot.Units.Count.ToString();

            var goblinDef = catalog?.GetUnit(UnitKind.Goblin);
            var trollDef = catalog?.GetUnit(UnitKind.Troll);
            var mineDef = catalog?.GetBuilding(BuildingKind.Mine);

            int goblinUnitPrice = goblinDef != null ? goblinDef.Price : 40;
            int trollUnitPrice = trollDef != null ? trollDef.Price : 170;
            int minePrice = mineDef != null ? mineDef.Price : 200;

            int goblinTotalCost = goblinUnitPrice * _hireAmount;
            int trollTotalCost = trollUnitPrice * _hireAmount;

            if (_goblinCostText != null) _goblinCostText.text = goblinTotalCost.ToString();
            if (_trollCostText != null) _trollCostText.text = trollTotalCost.ToString();
            if (_mineCostText != null) _mineCostText.text = minePrice.ToString();

            if (_buyGoblinBtn != null) _buyGoblinBtn.interactable = gold >= goblinTotalCost;
            if (_buyTrollBtn != null) _buyTrollBtn.interactable = gold >= trollTotalCost;
            if (_buildMineBtn != null) _buildMineBtn.interactable = gold >= minePrice;

            if (_hireAmountText != null) _hireAmountText.text = _hireAmount.ToString();

            if (_interaction != null)
            {
                var mode = _interaction.Mode;
                bool isPlacingMine = mode.Type == InteractionModeType.PlacingMine;
                bool isPlacingUnits = mode.Type == InteractionModeType.PlacingUnits;

                if (_autoPlaceMineBtn != null)
                    _autoPlaceMineBtn.gameObject.SetActive(isPlacingMine);

                if (_unitPlacementBanner != null)
                {
                    _unitPlacementBanner.SetActive(isPlacingUnits);
                    if (isPlacingUnits && _unitPlacementTitle != null)
                    {
                        string name = mode.UnitKind == UnitKind.Goblin ? "Гоблин" : "Тролль";
                        _unitPlacementTitle.text = $"{name} ×{mode.Amount}";
                    }
                }

                if (_gameStatusText != null)
                    _gameStatusText.text = _interaction.Message;
            }
        }
    }
}
