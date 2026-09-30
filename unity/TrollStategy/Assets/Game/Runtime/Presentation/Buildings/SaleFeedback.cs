using TrollStrategy.Presentation.Units;
using TrollStrategy.Presentation.WorldUi;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Presentation.Buildings
{
    /// <summary>Short visual response to an already completed sale; never changes economy state.</summary>
    public sealed class SaleFeedback : MonoBehaviour
    {
        private const float Duration = 1.6f;
        private const float ProductPhase = 0.35f;
        private const float GainDelay = 0.15f;
        private const float RiseHeight = 1.1f;
        private const float CoinHeight = 0.6f;

        private SpriteRenderer _product;
        private Transform _gain;
        private SpriteRenderer _coin;
        private float _coinScale;
        private WorldPanel _amount;
        private Label _gold;
        private Vector3 _start;
        private float _age;

        public static void Spawn(Vector3 position, Sprite productSprite, Sprite coinSprite, int gold)
        {
            var root = new GameObject("SaleFeedback");
            root.transform.position = position;
            var feedback = root.AddComponent<SaleFeedback>();
            feedback._start = position;

            var product = new GameObject("SoldProduct");
            product.transform.SetParent(root.transform, false);
            feedback._product = product.AddComponent<SpriteRenderer>();
            feedback._product.sprite = productSprite != null ? productSprite : UnitView.GetFallbackOreSprite();
            feedback._product.sortingOrder = 40;

            feedback._gain = new GameObject("GoldGain").transform;
            feedback._gain.SetParent(root.transform, false);
            feedback._gain.localScale = Vector3.zero;

            float textX = -0.3f;
            if (coinSprite != null)
            {
                var coin = new GameObject("Coin");
                coin.transform.SetParent(feedback._gain, false);
                coin.transform.localPosition = new Vector3(-0.65f, 0f, 0f);
                feedback._coin = coin.AddComponent<SpriteRenderer>();
                feedback._coin.sprite = coinSprite;
                feedback._coin.sortingOrder = 42;
                feedback._coinScale = CoinHeight / Mathf.Max(0.01f, coinSprite.bounds.size.y);
            }
            else textX = -0.45f;

            // the amount starts just right of the coin and grows to the right; far away the coin and the amount
            // grow together by the amount's hold, so the panel keeps its own scale
            var amount = WorldPanel.Create("Amount", feedback._gain, 43, Pivot.LeftCenter);
            amount.KeepReadable = false;
            amount.transform.localPosition = new Vector3(textX, 0.02f, 0f);
            feedback._amount = amount;
            feedback._gold = amount.AddLabel("world-label world-label--gain");
            feedback._gold.text = $"+{gold}";
            feedback.FaceCamera();
            feedback.Update();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float progress = Mathf.Clamp01(_age / Duration);

            // The sold product drops into the market and vanishes.
            float productT = Mathf.Clamp01(progress / ProductPhase);
            _product.transform.localPosition = Vector3.down * (0.45f * productT * productT);
            _product.transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 0.2f, productT);
            _product.color = WithAlpha(_product.color, 1f - productT);

            // The gold gain pops out with an overshoot, floats up, then fades.
            float gainT = Mathf.Clamp01((progress - GainDelay) / (1f - GainDelay));
            float rise = 1f - (1f - gainT) * (1f - gainT);
            _gain.localPosition = Vector3.up * (RiseHeight * rise);
            _gain.localScale = Vector3.one * (PopScale(Mathf.Clamp01(gainT / 0.3f)) * _amount.Hold);
            float alpha = 1f - Mathf.Clamp01((gainT - 0.7f) / 0.3f);
            _gold.style.opacity = alpha;
            if (_coin != null)
            {
                _coin.color = WithAlpha(_coin.color, alpha);
                // A little wobble keeps the coins lively while they float.
                _coin.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_age * 9f) * 8f);
                _coin.transform.localScale = Vector3.one * _coinScale;
            }

            FaceCamera();
            if (_age >= Duration) Destroy(gameObject);
        }

        private static float PopScale(float t)
        {
            // Ease-out-back: 0 -> ~1.15 -> 1.
            const float c1 = 1.9f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void FaceCamera()
        {
            if (Camera.main == null) return;
            var rotation = Camera.main.transform.rotation;
            _product.transform.rotation = rotation;
            _gain.rotation = rotation;
        }
    }
}
