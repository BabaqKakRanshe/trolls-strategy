using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TrollStrategy.Application;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Units;

namespace TrollStrategy.Presentation.Visuals
{
    public class SelectionBoxRenderer : MonoBehaviour
    {
        [SerializeField] private LineRenderer _lineRenderer;
        private InteractionController _interaction;
        private UnitVisualsManager _unitVisuals;
        private Camera _camera;
        private Vector3 _startWorldPos;
        private bool _isDragging;

        public void Init(InteractionController interaction, UnitVisualsManager unitVisuals, Camera cam)
        {
            _interaction = interaction;
            _unitVisuals = unitVisuals;
            _camera = cam != null ? cam : Camera.main;

            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
                _lineRenderer.positionCount = 5;
                _lineRenderer.startWidth = 0.04f;
                _lineRenderer.endWidth = 0.04f;
                _lineRenderer.useWorldSpace = true;
                _lineRenderer.sortingOrder = 95;
                _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                _lineRenderer.startColor = new Color(1f, 0.9f, 0.2f, 0.9f);
                _lineRenderer.endColor = new Color(1f, 0.9f, 0.2f, 0.9f);
            }

            _lineRenderer.enabled = false;
        }

        private void Update()
        {
            if (_interaction == null || _camera == null) return;
            if (_interaction.Mode.Type != InteractionModeType.Neutral)
            {
                _isDragging = false;
                _lineRenderer.enabled = false;
                return;
            }

            if (Mouse.current == null) return;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld = _camera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -_camera.transform.position.z));
            mouseWorld.z = 0f;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (UIInputUtils.IsPointerOverInteractiveUI())
                    return;

                _startWorldPos = mouseWorld;
                _isDragging = true;
                _lineRenderer.enabled = true;
            }

            if (_isDragging && Mouse.current.leftButton.isPressed)
            {
                float xMin = Mathf.Min(_startWorldPos.x, mouseWorld.x);
                float xMax = Mathf.Max(_startWorldPos.x, mouseWorld.x);
                float yMin = Mathf.Min(_startWorldPos.y, mouseWorld.y);
                float yMax = Mathf.Max(_startWorldPos.y, mouseWorld.y);

                _lineRenderer.SetPosition(0, new Vector3(xMin, yMin, 0));
                _lineRenderer.SetPosition(1, new Vector3(xMax, yMin, 0));
                _lineRenderer.SetPosition(2, new Vector3(xMax, yMax, 0));
                _lineRenderer.SetPosition(3, new Vector3(xMin, yMax, 0));
                _lineRenderer.SetPosition(4, new Vector3(xMin, yMin, 0));
            }

            if (_isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _isDragging = false;
                _lineRenderer.enabled = false;

                float xMin = Mathf.Min(_startWorldPos.x, mouseWorld.x);
                float xMax = Mathf.Max(_startWorldPos.x, mouseWorld.x);
                float yMin = Mathf.Min(_startWorldPos.y, mouseWorld.y);
                float yMax = Mathf.Max(_startWorldPos.y, mouseWorld.y);

                if (Mathf.Abs(xMax - xMin) > 0.25f || Mathf.Abs(yMax - yMin) > 0.25f)
                {
                    var enclosed = new List<string>();
                    if (_unitVisuals != null)
                    {
                        foreach (var kvp in _unitVisuals.Views)
                        {
                            var pos = kvp.Value.transform.position;
                            if (pos.x >= xMin && pos.x <= xMax && pos.y >= yMin && pos.y <= yMax)
                                enclosed.Add(kvp.Key);
                        }
                    }
                    _interaction.SelectUnits(enclosed);
                }
                else
                {
                    bool shift = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
                    var hit = Physics2D.OverlapPoint(new Vector2(mouseWorld.x, mouseWorld.y));
                    if (hit == null)
                        hit = Physics2D.OverlapCircle(new Vector2(mouseWorld.x, mouseWorld.y), 0.45f);

                    if (hit != null)
                    {
                        var uv = hit.GetComponentInParent<UnitView>();
                        if (uv != null)
                        {
                            _interaction.ClickUnit(uv.UnitId, shift);
                        }
                        else
                        {
                            var bv = hit.GetComponentInParent<BuildingView>();
                            if (bv != null)
                            {
                                _interaction.ChooseBuilding(bv.BuildingId);
                            }
                        }
                    }
                    else if (!shift)
                    {
                        _interaction.CancelOrClear();
                    }
                }
            }
        }
    }
}
