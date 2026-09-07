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
    public class UnitRosterView : MonoBehaviour
    {
        [SerializeField] private Transform _itemsContainer;
        [SerializeField] private TextMeshProUGUI _selectedCountText;
        [SerializeField] private Button _selectThreeButton;
        [SerializeField] private Button _selectNextButton;

        private InteractionController _interaction;
        private GameContentCatalog _catalog;
        private readonly List<GameObject> _spawnedItems = new();

        public void Setup(InteractionController interaction, GameContentCatalog catalog, Button sel3Btn, Button selNextBtn, TextMeshProUGUI selCountText, Transform container)
        {
            _interaction = interaction;
            _catalog = catalog;
            _selectThreeButton = sel3Btn;
            _selectNextButton = selNextBtn;
            _selectedCountText = selCountText;
            _itemsContainer = container;

            if (_selectThreeButton != null) _selectThreeButton.onClick.AddListener(() => _interaction?.SelectFirstIdle(3));
            if (_selectNextButton != null) _selectNextButton.onClick.AddListener(() => _interaction?.SelectNextIdle());
        }

        public void BindInteraction(InteractionController interaction)
        {
            _interaction = interaction;
        }

        public void UpdateRoster(IReadOnlyList<UnitSnapshot> units, IReadOnlyCollection<string> selectedIds)
        {
            if (_selectedCountText != null)
                _selectedCountText.text = $"{selectedIds.Count} выбрано";

            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                if (_spawnedItems[i] != null)
                    Destroy(_spawnedItems[i]);
            }
            _spawnedItems.Clear();

            if (_itemsContainer == null || units == null) return;

            var selectedSet = new HashSet<string>(selectedIds);

            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var itemGo = new GameObject($"RosterItem_{u.Id}", typeof(RectTransform));
                itemGo.transform.SetParent(_itemsContainer, false);

                var rt = itemGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(186f, 34f);

                var img = itemGo.AddComponent<Image>();
                bool isSel = selectedSet.Contains(u.Id);
                img.color = isSel ? new Color(0.22f, 0.45f, 0.62f, 0.95f) : new Color(0.12f, 0.16f, 0.22f, 0.88f);

                var btn = itemGo.AddComponent<Button>();
                string capturedId = u.Id;
                btn.onClick.AddListener(() => _interaction?.ClickUnit(capturedId, false));

                var txtGo = new GameObject("Text", typeof(RectTransform));
                txtGo.transform.SetParent(itemGo.transform, false);
                var txtRt = txtGo.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.sizeDelta = Vector2.zero;

                var tmp = txtGo.AddComponent<TextMeshProUGUI>();
                if (_selectedCountText != null) tmp.font = _selectedCountText.font;
                tmp.fontSize = 11f;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.margin = new Vector4(8f, 0f, 8f, 0f);

                tmp.text = $"<b>{u.Name} {u.Number}</b>  <size=9><color=#88ccbb>{u.Status}</color></size>";

                _spawnedItems.Add(itemGo);
            }
        }
    }
}
