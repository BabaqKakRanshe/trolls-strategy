using System;
using TrollStrategy.Content;
using UnityEngine;

namespace TrollStrategy.Presentation.Buildings
{
    // Visual part of a building prefab variant: the model of each building level, footprint rim and the product it ships.
    public sealed class BuildingModel : MonoBehaviour
    {
        [SerializeField] private Transform _selectionRim;
        [Tooltip("Sprite of the product picked up from this building.")]
        [SerializeField] private Sprite _outgoingProductSprite;
        [Tooltip("Icon shown beside the gold earned when this building sells goods.")]
        [SerializeField] private Sprite _saleIncomeSprite;
        [Tooltip("Point where the sale feedback appears.")]
        [SerializeField] private Transform _saleFeedbackAnchor;
        [Tooltip("Distance in cells between units waiting in the group at the entrance; smaller packs them closer. " +
                 "Copied into the BuildingDefinition when the prefab is saved.")]
        [SerializeField, Range(BuildingDefinition.MinCrowdSpacingCells, BuildingDefinition.MaxCrowdSpacingCells)]
        private float _crowdSpacingCells = BuildingDefinition.DefaultCrowdSpacingCells;
        [Tooltip("The kit model of each building level, from level 1; only the building's own is shown. " +
                 "Written by TrollStrategy/Dev/Setup Building Level Models.")]
        [SerializeField] private GameObject[] _levelModels = Array.Empty<GameObject>();
        [Tooltip("Top of each level model above the ground, m; the production bar floats over the shown one.")]
        [SerializeField] private float[] _levelTops = Array.Empty<float>();

        public Sprite OutgoingProductSprite => _outgoingProductSprite;
        public Sprite SaleIncomeSprite => _saleIncomeSprite;
        public Transform SaleFeedbackAnchor => _saleFeedbackAnchor;
        public float CrowdSpacingCells => _crowdSpacingCells;
        public int LevelCount => _levelModels?.Length ?? 0;

        /// <summary>The model level a building of this level shows: the last model carries every higher level.</summary>
        public int ModelLevel(int level) => LevelCount == 0 ? 1 : Mathf.Clamp(level, 1, LevelCount);

        public GameObject LevelModel(int level) => LevelCount == 0 ? null : _levelModels[ModelLevel(level) - 1];

        /// <summary>Top of the model this building level shows, m above the ground; 0 when it is unknown.</summary>
        public float LevelTop(int level)
        {
            int index = ModelLevel(level) - 1;
            return _levelTops != null && index < _levelTops.Length ? _levelTops[index] : 0f;
        }

        /// <summary>Shows the model of this building level and hides the others.</summary>
        public void ShowLevel(int level)
        {
            int shown = ModelLevel(level) - 1;
            for (int i = 0; i < LevelCount; i++)
                if (_levelModels[i] != null && _levelModels[i].activeSelf != (i == shown))
                    _levelModels[i].SetActive(i == shown);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_selectionRim != null) _selectionRim.gameObject.SetActive(highlighted);
        }
    }
}
