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
        [Header("Selection Panel Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TextMeshProUGUI _selectedCountText;

        [Header("Main Buttons (Collapsed)")]
        [SerializeField] private GameObject _mainActionsRow;
        [SerializeField] private Button _openCommandsBtn;
        [SerializeField] private Button _sellSelectedBtn;
        [SerializeField] private Button _clearSelectionBtn;

        [Header("Commands Row (Expanded)")]
        [SerializeField] private GameObject _commandsRow;
        [SerializeField] private Button _workBtn;
        [SerializeField] private Button _haulBtn;
        [SerializeField] private Button _barracksBtn;
        [SerializeField] private Button _releaseBtn;
        [SerializeField] private Button _cancelCommandsBtn;

        [Header("Stack Picker")]
        [SerializeField] private GameObject _stackPickerRow;
        [SerializeField] private Slider _stackSlider;
        [SerializeField] private TextMeshProUGUI _stackLabel;
        [SerializeField] private Button _confirmStackBtn;

        private InteractionController _interaction;
        private GameSession _session;

        public void Setup(
            InteractionController interaction,
            GameSession session,
            GameObject root,
            TextMeshProUGUI countText,
            GameObject mainRow, Button openCmdsBtn, Button sellBtn, Button clearBtn,
            GameObject cmdRow, Button workBtn, Button haulBtn, Button barracksBtn, Button releaseBtn, Button cancelCmdsBtn,
            GameObject stackRow, Slider stackSlider, TextMeshProUGUI stackLabel, Button confirmStackBtn)
        {
            _panelRoot = root;
            _selectedCountText = countText;

            _mainActionsRow = mainRow;
            _openCommandsBtn = openCmdsBtn;
            _sellSelectedBtn = sellBtn;
            _clearSelectionBtn = clearBtn;

            _commandsRow = cmdRow;
            _workBtn = workBtn;
            _haulBtn = haulBtn;
            _barracksBtn = barracksBtn;
            _releaseBtn = releaseBtn;
            _cancelCommandsBtn = cancelCmdsBtn;

            _stackPickerRow = stackRow;
            _stackSlider = stackSlider;
            _stackLabel = stackLabel;
            _confirmStackBtn = confirmStackBtn;

            Bind(session, interaction);
        }

        public void Bind(GameSession session, InteractionController interaction)
        {
            _session = session;
            _interaction = interaction;

            if (_openCommandsBtn != null)
            {
                _openCommandsBtn.onClick.RemoveAllListeners();
                _openCommandsBtn.onClick.AddListener(() => _interaction?.ToggleCommands(true));
            }
            if (_sellSelectedBtn != null)
            {
                _sellSelectedBtn.onClick.RemoveAllListeners();
                _sellSelectedBtn.onClick.AddListener(() => _interaction?.SellSelected());
            }
            if (_clearSelectionBtn != null)
            {
                _clearSelectionBtn.onClick.RemoveAllListeners();
                _clearSelectionBtn.onClick.AddListener(() => _interaction?.CancelOrClear());
            }

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
            if (_barracksBtn != null)
            {
                _barracksBtn.onClick.RemoveAllListeners();
                _barracksBtn.onClick.AddListener(() => _interaction?.SendSelectedToBarracks());
            }
            if (_releaseBtn != null)
            {
                _releaseBtn.onClick.RemoveAllListeners();
                _releaseBtn.onClick.AddListener(() => _interaction?.ReleaseSelected());
            }
            if (_cancelCommandsBtn != null)
            {
                _cancelCommandsBtn.onClick.RemoveAllListeners();
                _cancelCommandsBtn.onClick.AddListener(() => _interaction?.ToggleCommands(false));
            }

            if (_stackSlider != null)
            {
                _stackSlider.onValueChanged.RemoveAllListeners();
                _stackSlider.onValueChanged.AddListener(val =>
                {
                    _interaction?.SetStackQuantity((int)val);
                });
            }

            if (_confirmStackBtn != null)
            {
                _confirmStackBtn.onClick.RemoveAllListeners();
                _confirmStackBtn.onClick.AddListener(() =>
                {
                    if (_interaction != null)
                    {
                        var list = new List<string>(_interaction.SelectedIds);
                        _interaction.ConfirmStackSelection(list);
                    }
                });
            }
        }

        public void UpdateView(int selectedCount, bool commandsOpen, int stackQuantity)
        {
            if (_panelRoot == null) return;

            bool hasSelection = selectedCount > 0;
            _panelRoot.SetActive(hasSelection);

            if (!hasSelection) return;

            if (_selectedCountText != null)
                _selectedCountText.text = "Выбрано: " + selectedCount;

            if (_mainActionsRow != null) _mainActionsRow.SetActive(!commandsOpen);
            if (_commandsRow != null) _commandsRow.SetActive(commandsOpen);

            if (_stackPickerRow != null)
            {
                bool showStack = selectedCount > 1 && !commandsOpen;
                _stackPickerRow.SetActive(showStack);
                if (showStack)
                {
                    if (_stackSlider != null)
                    {
                        _stackSlider.minValue = 1;
                        _stackSlider.maxValue = selectedCount;
                        _stackSlider.value = Mathf.Clamp(stackQuantity, 1, selectedCount);
                    }
                    if (_stackLabel != null)
                    {
                        _stackLabel.text = "Выбрать: " + Mathf.Clamp(stackQuantity, 1, selectedCount) + " из " + selectedCount;
                    }
                }
            }
        }
    }
}
