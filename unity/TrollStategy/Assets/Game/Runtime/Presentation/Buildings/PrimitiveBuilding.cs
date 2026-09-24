using TrollStrategy.Application;
using TrollStrategy.Content;
using UnityEngine;

namespace TrollStrategy.Presentation.Buildings
{
    public sealed class PrimitiveBuilding : MonoBehaviour
    {
        [SerializeField] private BuildingKind _kind;
        [SerializeField] private Transform _selectionRim;

        public BuildingKind Kind => _kind;

        public void Sync(BuildingSnapshot snapshot, bool highlighted)
        {
            if (snapshot.Kind != _kind) return;
            if (_selectionRim == null) _selectionRim = transform.Find("SelectionRim");
            if (_selectionRim != null) _selectionRim.gameObject.SetActive(highlighted);
        }
    }
}
