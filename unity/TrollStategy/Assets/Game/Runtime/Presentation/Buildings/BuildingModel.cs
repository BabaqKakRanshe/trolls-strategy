using TrollStrategy.Content;
using UnityEngine;

namespace TrollStrategy.Presentation.Buildings
{
    // Visual part of a building prefab variant: the model children, footprint rim and the product it ships.
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

        public Sprite OutgoingProductSprite => _outgoingProductSprite;
        public Sprite SaleIncomeSprite => _saleIncomeSprite;
        public Transform SaleFeedbackAnchor => _saleFeedbackAnchor;
        public float CrowdSpacingCells => _crowdSpacingCells;

        public void SetHighlighted(bool highlighted)
        {
            if (_selectionRim != null) _selectionRim.gameObject.SetActive(highlighted);
        }
    }
}
