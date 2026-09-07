using TMPro;
using UnityEngine;

namespace TrollStrategy.UI
{
    public class StatusMessageView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _messageText;

        public void Setup(TextMeshProUGUI messageText)
        {
            _messageText = messageText;
        }

        public void SetMessage(string message)
        {
            if (_messageText != null)
                _messageText.text = message;
        }
    }
}
