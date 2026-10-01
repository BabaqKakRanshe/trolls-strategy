using TrollStrategy.Content;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Buildings;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>Turning 3D model of a building from the catalog, beside the catalog.</summary>
    public sealed class ShowcasePanel
    {
        private readonly BuildingShowcase _renderer;
        private readonly VisualElement _panel;
        private readonly VisualElement _image;
        private readonly Label _title;
        private readonly Label _caption;

        public ShowcasePanel(VisualElement root, BuildingShowcase renderer)
        {
            _renderer = renderer;
            _panel = Ui.Require<VisualElement>(root, "showcase");
            _image = Ui.Require<VisualElement>(root, "showcase-image");
            _title = Ui.Require<Label>(root, "showcase-title");
            _caption = Ui.Require<Label>(root, "showcase-caption");
            UiFeel.Bind(Ui.Require<Button>(root, "showcase-close"), Hide, Sfx.UiBack);
            Ui.Show(_panel, false);
        }

        public bool CanShow => _renderer != null;
        public bool IsShown => Ui.IsShown(_panel);

        public void Show(BuildingDefinition building, int price, string recipes)
        {
            var texture = _renderer?.Show(building) as RenderTexture;
            if (texture == null)
            {
                Hide();
                return;
            }
            _image.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(texture));
            Ui.SetText(_title, building.DisplayName);
            string caption = $"{building.Width}×{building.Height}, {Ui.Gold(price)}";
            if (!string.IsNullOrEmpty(recipes)) caption += "\n" + recipes;
            Ui.SetText(_caption, caption);
            bool fresh = !IsShown;
            Ui.Show(_panel, true);
            if (fresh) UiMotion.PopIn(_panel, .25f);
        }

        public void Hide()
        {
            _renderer?.Hide();
            Ui.Show(_panel, false);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (IsShown) _renderer?.Tick(unscaledDeltaTime);
        }
    }
}
