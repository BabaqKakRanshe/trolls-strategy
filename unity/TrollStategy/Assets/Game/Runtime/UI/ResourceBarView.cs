using TMPro;
using UnityEngine;

namespace TrollStrategy.UI
{
    public class ResourceBarView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _totalOreText;
        [SerializeField] private TextMeshProUGUI _soldOreText;
        [SerializeField] private TextMeshProUGUI _populationText;
        [SerializeField] private TextMeshProUGUI _selectedText;

        public void Setup(TextMeshProUGUI gold, TextMeshProUGUI totalOre, TextMeshProUGUI soldOre, TextMeshProUGUI population, TextMeshProUGUI selected)
        {
            _goldText = gold;
            _totalOreText = totalOre;
            _soldOreText = soldOre;
            _populationText = population;
            _selectedText = selected;
        }

        public void UpdateValues(int gold, int totalOre, int soldOre, int population, int selected)
        {
            if (_goldText != null) _goldText.text = $"Золото: {gold:N0}";
            if (_totalOreText != null) _totalOreText.text = $"Руда: {totalOre:N0}";
            if (_soldOreText != null) _soldOreText.text = $"Продано: {soldOre:N0}";
            if (_populationText != null) _populationText.text = $"Существ: {population}";
            if (_selectedText != null) _selectedText.text = $"Выбрано: {selected}";
        }
    }
}
