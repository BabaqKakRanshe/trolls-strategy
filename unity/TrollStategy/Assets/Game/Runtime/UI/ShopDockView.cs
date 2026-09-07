using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.UI
{
    public enum ShopCategory
    {
        None,
        Minions,
        Buildings,
        Decorations
    }

    public class ShopDockView : MonoBehaviour
    {
        [Header("Category Tabs")]
        [SerializeField] private Button _minionsTab;
        [SerializeField] private Button _buildingsTab;
        [SerializeField] private Button _decorationsTab;

        [Header("Drawer")]
        [SerializeField] private GameObject _drawerRoot;
        [SerializeField] private TextMeshProUGUI _drawerTitle;

        [Header("Minions Category")]
        [SerializeField] private GameObject _minionsContent;
        [SerializeField] private Button _buyGoblinBtn;
        [SerializeField] private Button _buyTrollBtn;
        [SerializeField] private TextMeshProUGUI _goblinCostText;
        [SerializeField] private TextMeshProUGUI _trollCostText;

        [Header("Buildings Category")]
        [SerializeField] private GameObject _buildingsContent;
        [SerializeField] private Button _buildMineBtn;
        [SerializeField] private Button _autoPlaceMineBtn;
        [SerializeField] private TextMeshProUGUI _mineCostText;

        [Header("Decorations Category")]
        [SerializeField] private GameObject _decorationsContent;

        private InteractionController _interaction;
        private GameSession _session;
        private ShopCategory _activeCategory = ShopCategory.None;

        public void Setup(
            InteractionController interaction,
            GameSession session,
            Button minionsTab, Button buildingsTab, Button decorationsTab,
            GameObject drawerRoot, TextMeshProUGUI drawerTitle,
            GameObject minionsContent, Button buyGoblinBtn, Button buyTrollBtn, TextMeshProUGUI goblinCost, TextMeshProUGUI trollCost,
            GameObject buildingsContent, Button buildMineBtn, Button autoPlaceMineBtn, TextMeshProUGUI mineCost,
            GameObject decorationsContent)
        {
            _minionsTab = minionsTab;
            _buildingsTab = buildingsTab;
            _decorationsTab = decorationsTab;

            _drawerRoot = drawerRoot;
            _drawerTitle = drawerTitle;

            _minionsContent = minionsContent;
            _buyGoblinBtn = buyGoblinBtn;
            _buyTrollBtn = buyTrollBtn;
            _goblinCostText = goblinCost;
            _trollCostText = trollCost;

            _buildingsContent = buildingsContent;
            _buildMineBtn = buildMineBtn;
            _autoPlaceMineBtn = autoPlaceMineBtn;
            _mineCostText = mineCost;

            _decorationsContent = decorationsContent;

            Bind(session, interaction);
        }

        public void Bind(GameSession session, InteractionController interaction)
        {
            _session = session;
            _interaction = interaction;

            if (_minionsTab != null)
            {
                _minionsTab.onClick.RemoveAllListeners();
                _minionsTab.onClick.AddListener(() => ToggleCategory(ShopCategory.Minions));
            }
            if (_buildingsTab != null)
            {
                _buildingsTab.onClick.RemoveAllListeners();
                _buildingsTab.onClick.AddListener(() => ToggleCategory(ShopCategory.Buildings));
            }
            if (_decorationsTab != null)
            {
                _decorationsTab.onClick.RemoveAllListeners();
                _decorationsTab.onClick.AddListener(() => ToggleCategory(ShopCategory.Decorations));
            }

            if (_buyGoblinBtn != null)
            {
                _buyGoblinBtn.onClick.RemoveAllListeners();
                _buyGoblinBtn.onClick.AddListener(() => _interaction?.RecruitUnit(UnitKind.Goblin));
            }
            if (_buyTrollBtn != null)
            {
                _buyTrollBtn.onClick.RemoveAllListeners();
                _buyTrollBtn.onClick.AddListener(() => _interaction?.RecruitUnit(UnitKind.Troll));
            }

            if (_buildMineBtn != null)
            {
                _buildMineBtn.onClick.RemoveAllListeners();
                _buildMineBtn.onClick.AddListener(() =>
                {
                    _interaction?.BeginMinePlacement();
                    _activeCategory = ShopCategory.None;
                    RefreshDrawer();
                });
            }

            if (_autoPlaceMineBtn != null)
            {
                _autoPlaceMineBtn.onClick.RemoveAllListeners();
                _autoPlaceMineBtn.onClick.AddListener(() =>
                {
                    _interaction?.PlaceMineAutomatically();
                });
            }

            RefreshDrawer();
        }

        public void ToggleCategory(ShopCategory cat)
        {
            _activeCategory = _activeCategory == cat ? ShopCategory.None : cat;
            RefreshDrawer();
        }

        private void RefreshDrawer()
        {
            bool open = _activeCategory != ShopCategory.None;
            if (_drawerRoot != null) _drawerRoot.SetActive(open);

            if (!open) return;

            if (_minionsContent != null) _minionsContent.SetActive(_activeCategory == ShopCategory.Minions);
            if (_buildingsContent != null) _buildingsContent.SetActive(_activeCategory == ShopCategory.Buildings);
            if (_decorationsContent != null) _decorationsContent.SetActive(_activeCategory == ShopCategory.Decorations);

            if (_drawerTitle != null)
            {
                switch (_activeCategory)
                {
                    case ShopCategory.Minions: _drawerTitle.text = "МИНЬОНЫ"; break;
                    case ShopCategory.Buildings: _drawerTitle.text = "ПОСТРОЙКИ"; break;
                    case ShopCategory.Decorations: _drawerTitle.text = "ДЕКОРАЦИИ"; break;
                }
            }
        }

        public void UpdateView(GameSnapshot snapshot)
        {
            if (_session == null || snapshot == null) return;

            int gold = snapshot.Gold;
            var catalog = _session.Catalog;

            var goblinDef = catalog.GetUnit(UnitKind.Goblin);
            var trollDef = catalog.GetUnit(UnitKind.Troll);
            var mineDef = catalog.GetBuilding(BuildingKind.Mine);

            if (_goblinCostText != null && goblinDef != null)
                _goblinCostText.text = goblinDef.Price + " золота";

            if (_trollCostText != null && trollDef != null)
                _trollCostText.text = trollDef.Price + " золота";

            if (_mineCostText != null && mineDef != null)
                _mineCostText.text = mineDef.Price + " золота";

            if (_buyGoblinBtn != null && goblinDef != null)
                _buyGoblinBtn.interactable = gold >= goblinDef.Price;

            if (_buyTrollBtn != null && trollDef != null)
                _buyTrollBtn.interactable = gold >= trollDef.Price;

            if (_buildMineBtn != null && mineDef != null)
                _buildMineBtn.interactable = gold >= mineDef.Price;

            if (_autoPlaceMineBtn != null && mineDef != null)
                _autoPlaceMineBtn.interactable = gold >= mineDef.Price;
        }
    }
}
