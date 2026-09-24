using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrollStrategy.Application;
using TrollStrategy.Domain;
using TrollStrategy.Presentation;

namespace TrollStrategy.UI
{
    public class CommandDockView : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TextMeshProUGUI _selectedCountText;

        [Header("Command Actions")]
        [SerializeField] private Button _workBtn;
        [SerializeField] private Button _haulBtn;
        [SerializeField] private Button _releaseBtn;
        [SerializeField] private Button _cancelBtn;

        [Header("Target Choices")]
        [SerializeField] private GameObject _targetChoicesRow;
        [SerializeField] private TextMeshProUGUI _targetPromptText;
        [SerializeField] private Transform _targetButtonsContainer;

        private InteractionController _interaction;
        private GameSession _session;
        private readonly List<Button> _spawnedTargetButtons = new();
        private string _targetSignature;

        public void Setup(
            InteractionController interaction,
            GameSession session,
            GameObject root,
            TextMeshProUGUI countText,
            Button workBtn, Button haulBtn, Button releaseBtn, Button cancelBtn,
            GameObject targetRow, TextMeshProUGUI targetPrompt, Transform targetContainer)
        {
            _panelRoot = root;
            _selectedCountText = countText;
            _workBtn = workBtn;
            _haulBtn = haulBtn;
            _releaseBtn = releaseBtn;
            _cancelBtn = cancelBtn;

            _targetChoicesRow = targetRow;
            _targetPromptText = targetPrompt;
            _targetButtonsContainer = targetContainer;

            Bind(session, interaction);
        }

        public void Bind(GameSession session, InteractionController interaction)
        {
            _session = session;
            _interaction = interaction;

            if (_workBtn != null)
            {
                _workBtn.onClick.RemoveAllListeners();
                _workBtn.onClick.AddListener(() => _interaction?.BeginWorkTarget());
            }

            if (_haulBtn != null)
            {
                _haulBtn.onClick.RemoveAllListeners();
                _haulBtn.onClick.AddListener(() => _interaction?.BeginHaulTarget());
            }

            if (_releaseBtn != null)
            {
                _releaseBtn.onClick.RemoveAllListeners();
                _releaseBtn.onClick.AddListener(() => _interaction?.ReleaseSelected());
            }

            if (_cancelBtn != null)
            {
                _cancelBtn.onClick.RemoveAllListeners();
                _cancelBtn.onClick.AddListener(() => _interaction?.CancelOrClear());
            }
        }

        public void UpdateView(int selectedCount, bool commandsOpen, int stackQuantity)
        {
            if (_session == null || _interaction == null || _panelRoot == null) return;

            var mode = _interaction.Mode;
            bool isTargeting = mode.Type == InteractionModeType.ChoosingWorkTarget ||
                              mode.Type == InteractionModeType.ChoosingHaulSource ||
                              mode.Type == InteractionModeType.ChoosingHaulDestination;

            bool visible = true;
            _panelRoot.SetActive(visible);

            if (!visible)
            {
                ClearTargetButtons();
                _targetSignature = null;
                return;
            }

            if (_selectedCountText != null)
                _selectedCountText.text = selectedCount > 0 ? $"{selectedCount} ВЫБРАНО" : "НИКТО НЕ ВЫБРАН";

            if (_workBtn != null) _workBtn.interactable = selectedCount > 0;
            if (_haulBtn != null) _haulBtn.interactable = selectedCount > 0;
            if (_releaseBtn != null) _releaseBtn.interactable = selectedCount > 0;

            // Target choices
            if (_targetChoicesRow != null)
            {
                _targetChoicesRow.SetActive(isTargeting);
                if (isTargeting)
                {
                    if (_targetPromptText != null)
                    {
                        if (mode.Type == InteractionModeType.ChoosingHaulSource)
                            _targetPromptText.text = "Источник:";
                        else if (mode.Type == InteractionModeType.ChoosingHaulDestination)
                            _targetPromptText.text = "Цель:";
                        else
                            _targetPromptText.text = "Шахта:";
                    }

                    var targetIds = _interaction.GetTargetBuildingIds();
                    string signature = $"{mode.Type}:{string.Join("|", targetIds)}";

                    if (_targetButtonsContainer != null && signature != _targetSignature)
                    {
                        ClearTargetButtons();
                        _targetSignature = signature;
                        var snapshot = _session.CurrentSnapshot;

                        for (int i = 0; i < targetIds.Count; i++)
                        {
                            string bId = targetIds[i];
                            BuildingSnapshot bSnap = null;
                            for (int b = 0; b < snapshot.Buildings.Count; b++)
                            {
                                if (snapshot.Buildings[b].Id == bId) { bSnap = snapshot.Buildings[b]; break; }
                            }
                            string bName = bSnap != null ? bSnap.Name : bId;

                            var btnGo = new GameObject($"TargetBtn_{bId}", typeof(RectTransform));
                            btnGo.transform.SetParent(_targetButtonsContainer, false);
                            var btnRt = btnGo.GetComponent<RectTransform>();
                            btnRt.sizeDelta = new Vector2(100f, 32f);

                            var img = btnGo.AddComponent<Image>();
                            img.color = ColonyPalette.Raised;

                            var btn = btnGo.AddComponent<Button>();
                            btn.onClick.AddListener(() => _interaction?.ChooseBuilding(bId));

                            var txtGo = new GameObject("Text", typeof(RectTransform));
                            txtGo.transform.SetParent(btnGo.transform, false);
                            var txtRt = txtGo.GetComponent<RectTransform>();
                            txtRt.anchorMin = Vector2.zero;
                            txtRt.anchorMax = Vector2.one;
                            txtRt.sizeDelta = Vector2.zero;

                            var txt = txtGo.AddComponent<TextMeshProUGUI>();
                            txt.text = bName;
                            txt.fontSize = 11;
                            txt.alignment = TextAlignmentOptions.Center;
                            txt.color = ColonyPalette.Text;

                            _spawnedTargetButtons.Add(btn);
                        }
                    }
                }
                else
                {
                    ClearTargetButtons();
                    _targetSignature = null;
                }
            }
        }

        private void ClearTargetButtons()
        {
            for (int i = 0; i < _spawnedTargetButtons.Count; i++)
            {
                if (_spawnedTargetButtons[i] != null)
                    Destroy(_spawnedTargetButtons[i].gameObject);
            }
            _spawnedTargetButtons.Clear();
        }
    }
}
