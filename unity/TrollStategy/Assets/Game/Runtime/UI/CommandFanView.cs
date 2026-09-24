using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrollStrategy.Application;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Visuals;

namespace TrollStrategy.UI
{
    [DisallowMultipleComponent]
    public class CommandFanView : MonoBehaviour
    {
        private const float HorizontalMargin = 150f;
        private const float VerticalMargin = 105f;

        private InteractionController _interaction;
        private MapInputHandler _inputHandler;
        private Canvas _canvas;
        private RectTransform _rect;
        private bool _built;
        private static Sprite _hexSprite;

        public void Init(InteractionController interaction, MapInputHandler inputHandler, Canvas canvas)
        {
            if (_interaction != null) _interaction.OnInteractionChanged -= RefreshVisibility;
            if (_inputHandler != null) _inputHandler.CommandFanRequested -= OpenAt;

            _interaction = interaction;
            _inputHandler = inputHandler;
            _canvas = canvas;
            _rect = GetComponent<RectTransform>();

            BuildIfNeeded();
            _interaction.OnInteractionChanged += RefreshVisibility;
            _inputHandler.CommandFanRequested += OpenAt;
            RefreshVisibility();
        }

        private void OnDestroy()
        {
            if (_interaction != null) _interaction.OnInteractionChanged -= RefreshVisibility;
            if (_inputHandler != null) _inputHandler.CommandFanRequested -= OpenAt;
        }

        private void BuildIfNeeded()
        {
            if (_built || _rect == null) return;
            _built = true;
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(300f, 200f);

            CreateButton("Work", "Работа", new Vector2(-92f, -18f), () => _interaction.BeginWorkTarget());
            CreateButton("Haul", "Перенос", new Vector2(0f, 58f), () => _interaction.BeginHaulTarget());
            CreateButton("Release", "Свободны", new Vector2(92f, -18f), () => _interaction.ReleaseSelected());
        }

        private void CreateButton(string name, string label, Vector2 position, Action action)
        {
            var buttonObject = new GameObject(
                $"CommandFan{name}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(transform, false);

            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(108f, 92f);
            buttonRect.anchoredPosition = position;

            var image = buttonObject.GetComponent<Image>();
            image.sprite = GetHexSprite();
            image.color = Color.white;

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = ColonyPalette.Gold;
            colors.pressedColor = ColonyPalette.GrassLight;
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                _interaction.ToggleCommands(false);
                action();
            });

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 8f);
            textRect.offsetMax = new Vector2(-8f, -8f);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = ColonyPalette.Text;
            text.raycastTarget = false;
        }

        private void OpenAt(Vector2 screenPosition)
        {
            if (_canvas == null || _rect == null) return;
            var canvasRect = _canvas.transform as RectTransform;
            if (canvasRect == null) return;

            Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out var localPoint))
                return;

            Rect bounds = canvasRect.rect;
            localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin + HorizontalMargin, bounds.xMax - HorizontalMargin);
            localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin + VerticalMargin, bounds.yMax - VerticalMargin);
            _rect.anchoredPosition = localPoint;
            _rect.SetAsLastSibling();
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            bool visible = _interaction != null &&
                           _interaction.CommandsOpen &&
                           _interaction.SelectedIds.Count > 0 &&
                           _interaction.Mode.Type == InteractionModeType.Neutral;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        private static Sprite GetHexSprite()
        {
            if (_hexSprite != null) return _hexSprite;

            const int width = 108;
            const int height = 92;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "CommandFanHexTexture";
            texture.filterMode = FilterMode.Bilinear;
            Color clear = Color.clear;
            Color fill = ColonyPalette.WithAlpha(ColonyPalette.Panel, 0.97f);
            Color border = ColonyPalette.Gold;

            for (int y = 0; y < height; y++)
            {
                float normalizedY = Mathf.Abs((y + 0.5f) / height * 2f - 1f);
                float inset = normalizedY * width * 0.25f;
                for (int x = 0; x < width; x++)
                {
                    bool inside = x >= inset && x < width - inset;
                    bool edge = inside && (x < inset + 3f || x >= width - inset - 3f || y < 3 || y >= height - 3);
                    texture.SetPixel(x, y, !inside ? clear : edge ? border : fill);
                }
            }

            texture.Apply();
            _hexSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            return _hexSprite;
        }
    }
}
