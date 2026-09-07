using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrollStrategy.Application;

namespace TrollStrategy.UI
{
    public class StatusMessageView : MonoBehaviour
    {
        [Header("Mode Prompt (Targeting / Placement)")]
        [SerializeField] private GameObject _promptRoot;
        [SerializeField] private TextMeshProUGUI _promptText;
        [SerializeField] private Button _cancelModeBtn;

        [Header("Normal Status")]
        [SerializeField] private TextMeshProUGUI _statusText;

        private InteractionController _interaction;

        public void Setup(InteractionController interaction, GameObject promptRoot, TextMeshProUGUI promptText, Button cancelBtn, TextMeshProUGUI statusText)
        {
            _promptRoot = promptRoot;
            _promptText = promptText;
            _cancelModeBtn = cancelBtn;
            _statusText = statusText;

            Bind(interaction);
        }

        public void Bind(InteractionController interaction)
        {
            _interaction = interaction;

            if (_cancelModeBtn != null)
            {
                _cancelModeBtn.onClick.RemoveAllListeners();
                _cancelModeBtn.onClick.AddListener(() => _interaction?.CancelOrClear());
            }
        }

        public void UpdateView(InteractionMode mode, string message)
        {
            bool isPrompt = mode.Type != InteractionModeType.Neutral;

            if (_promptRoot != null)
            {
                _promptRoot.SetActive(isPrompt);
                if (isPrompt && _promptText != null)
                    _promptText.text = message;
            }

            if (_statusText != null)
            {
                _statusText.gameObject.SetActive(!isPrompt);
                if (!isPrompt)
                    _statusText.text = message;
            }
        }
    }
}
