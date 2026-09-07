using TMPro;
using UnityEngine;
using TrollStrategy.Application;

namespace TrollStrategy.UI
{
    public class ResourceBarView : MonoBehaviour
    {
        [Header("Stats Displays")]
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _minionsText;
        [SerializeField] private TextMeshProUGUI _oreText;

        public void Setup(TextMeshProUGUI gold, TextMeshProUGUI minions, TextMeshProUGUI ore)
        {
            _goldText = gold;
            _minionsText = minions;
            _oreText = ore;
        }

        public void UpdateView(GameSnapshot snapshot)
        {
            if (snapshot == null) return;

            if (_goldText != null) _goldText.text = snapshot.Gold.ToString("N0");
            if (_minionsText != null) _minionsText.text = snapshot.Units.Count.ToString();
            if (_oreText != null) _oreText.text = snapshot.TotalOre.ToString("N0");
        }
    }
}
