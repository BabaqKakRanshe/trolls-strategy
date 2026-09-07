using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrollStrategy.Application;
using TrollStrategy.Domain;

namespace TrollStrategy.UI
{
    public class CommandDockView : MonoBehaviour
    {
        [SerializeField] private Button _workButton;
        [SerializeField] private Button _haulButton;
        [SerializeField] private Button _releaseButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Transform _targetsContainer;
        [SerializeField] private TextMeshProUGUI _targetPromptText;

        private InteractionController _interaction;
        private readonly List<GameObject> _spawnedTargetButtons = new();

        public void Setup(InteractionController interaction, Button workBtn, Button haulBtn, Button releaseBtn, Button cancelBtn, Transform targetsContainer, TextMeshProUGUI promptText)
        {
            _interaction = interaction;
            _workButton = workBtn;
            _haulButton = haulBtn;
            _releaseButton = releaseBtn;
            _cancelButton = cancelBtn;
            _targetsContainer = targetsContainer;
            _targetPromptText = promptText;

            if (_workButton != null) _workButton.onClick.AddListener(() => _interaction.BeginWorkTarget());
            if (_haulButton != null) _haulButton.onClick.AddListener(() => _interaction.BeginHaulTarget());
            if (_releaseButton != null) _releaseButton.onClick.AddListener(() => _interaction.ReleaseSelected());
            if (_cancelButton != null) _cancelButton.onClick.AddListener(() => _interaction.CancelOrClear());
        }

        public void BindInteraction(InteractionController interaction)
        {
            _interaction = interaction;
        }

        public void UpdateState(int selectedCount, InteractionMode mode, GameSnapshot snapshot)
        {
            bool hasSelection = selectedCount > 0;
            if (_workButton != null) _workButton.interactable = hasSelection;
            if (_haulButton != null) _haulButton.interactable = hasSelection;
            if (_releaseButton != null) _releaseButton.interactable = hasSelection;
            if (_cancelButton != null) _cancelButton.interactable = hasSelection || mode.Type != InteractionModeType.Neutral;

            for (int i = 0; i < _spawnedTargetButtons.Count; i++)
            {
                if (_spawnedTargetButtons[i] != null)
                    Destroy(_spawnedTargetButtons[i]);
            }
            _spawnedTargetButtons.Clear();

            if (_targetsContainer == null || _interaction == null) return;

            var targetIds = _interaction.GetTargetBuildingIds();
            bool isChoosing = targetIds.Count > 0;
            _targetsContainer.gameObject.SetActive(isChoosing);

            if (!isChoosing) return;

            if (_targetPromptText != null)
            {
                _targetPromptText.text = mode.Type == InteractionModeType.ChoosingHaulSource ? "Источник:" : "Цель:";
            }

            for (int i = 0; i < targetIds.Count; i++)
            {
                string bId = targetIds[i];
                var buildingSnap = snapshot?.Buildings != null ? ((List<BuildingSnapshot>)snapshot.Buildings).Find(b => b.Id == bId) : null;
                string bName = buildingSnap?.Name ?? bId;

                var btnGo = new GameObject($"TargetBtn_{bId}");
                btnGo.transform.SetParent(_targetsContainer, false);
                var rt = btnGo.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(110f, 34f);

                var img = btnGo.AddComponent<Image>();
                img.color = new Color(0.2f, 0.35f, 0.45f, 0.9f);

                var btn = btnGo.AddComponent<Button>();
                string capturedId = bId;
                btn.onClick.AddListener(() => _interaction.ChooseBuilding(capturedId));

                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(btnGo.transform, false);
                var txtRt = txtGo.AddComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.sizeDelta = Vector2.zero;

                var tmp = txtGo.AddComponent<TextMeshProUGUI>();
                if (_targetPromptText != null) tmp.font = _targetPromptText.font;
                tmp.fontSize = 12f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.text = bName;

                _spawnedTargetButtons.Add(btnGo);
            }
        }
    }
}
