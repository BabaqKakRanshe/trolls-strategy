using UnityEngine;
using TrollStrategy.Presentation.Map;

namespace TrollStrategy.Presentation.Visuals
{
    [ExecuteAlways]
    public sealed class PrimitiveEnvironment : MonoBehaviour
    {
        // Imported FBX assets are assigned by the scene rebuild command and serialized
        // on the Grid, so a player build can reconstruct this visual-only environment.
        [SerializeField] private GameObject[] _trees;
        [SerializeField] private GameObject[] _bushes;
        [SerializeField] private GameObject[] _rocks;
        [SerializeField] private GameObject[] _groundCover;
        [SerializeField] private GameObject[] _props;

        public static readonly Color Field = new Color32(137, 153, 100, 255);
        public static readonly Color Road = new Color32(188, 150, 105, 255);
        private static readonly Color Soil = new Color32(82, 65, 47, 255);

        private void OnEnable()
        {
            if (transform.Find("ProceduralWorld") == null) Build();
        }

        public void Rebuild()
        {
            var old = transform.Find("ProceduralWorld");
            if (old != null)
            {
#if UNITY_EDITOR
                if (!UnityEngine.Application.isPlaying) DestroyImmediate(old.gameObject);
                else Destroy(old.gameObject);
#else
                Destroy(old.gameObject);
#endif
            }
            Build();
        }

        private void Build()
        {
            var world = GetComponent<TilemapWorldView>();
            if (world == null || world.GridWidth <= 0 || world.GridHeight <= 0) return;
            float width = world.GridWidth * world.CellSize, height = world.GridHeight * world.CellSize;
            var root = new GameObject("ProceduralWorld").transform;
            root.SetParent(transform, false);

            // The entire playable rectangle stays flat and clear. Only its decorative skirt varies.
            var boundary = new Vector3[64];
            for (int i = 0; i < boundary.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / boundary.Length;
                float x = Mathf.Cos(angle), y = Mathf.Sin(angle);
                float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                float margin = 2.25f + .22f * Mathf.Sin(angle * 3 + .7f) + .18f * Mathf.Sin(angle * 7) + .11f * Mathf.Sin(angle * 11);
                boundary[i] = new Vector3(width * .5f + x / edge * (width * .5f + margin),
                    height * .5f + y / edge * (height * .5f + margin), -.08f);
            }
            PrimitiveArt.GroundPolygon(root, "Meadow", boundary, Field);
            PrimitiveArt.GroundRim(root, "EarthEdge", boundary, .65f, Soil);

            Trail(root, "MarketTrail", new Vector2(.08f * width, .24f * height), new Vector2(.88f * width, .24f * height), .9f, .35f, 0);
            Trail(root, "WarehouseTrail", new Vector2(.09f * width, .67f * height), new Vector2(.88f * width, .66f * height), .78f, -.3f, 1);
            Trail(root, "ConnectingTrail", new Vector2(.49f * width, .23f * height), new Vector2(.5f * width, .68f * height), .67f, .5f, 2);

            var nature = new GameObject("KitNature").transform;
            nature.SetParent(root, false);
            int variant = 0;
            for (int side = 0; side < 2; side++)
            {
                for (int cluster = 0; cluster < 4; cluster++)
                {
                    float cx = side == 0 ? -2.15f : width + 2.15f;
                    float cy = height * (.1f + cluster * .255f);
                    for (int member = 0; member < 3; member++)
                    {
                        float x = cx + Mathf.Sin(cluster * 2.1f + member * 2.8f) * .55f;
                        float y = cy + (member - 1) * .75f;
                        var tree = _trees != null && _trees.Length > 0 ? _trees[variant % _trees.Length] : null;
                        Place(nature, tree, $"Tree_{variant}", x, y, .82f + variant % 4 * .09f, variant * 47f);
                        variant++;
                    }
                    Place(nature, Pick(_bushes, variant), $"Bush_{variant}",
                        side == 0 ? -.9f : width + .9f, cy + 1.5f, .68f, variant * 31f);
                    variant++;
                }
            }
            for (int cluster = 0; cluster < 4; cluster++)
            {
                float cx = width * (.05f + cluster * .3f);
                for (int member = 0; member < 3; member++)
                {
                    Place(nature, Pick(_trees, variant), $"Tree_{variant}", cx + (member - 1) * .75f,
                        height + 2.05f + Mathf.Sin(cluster * 3 + member) * .55f,
                        .82f + variant % 4 * .09f, variant * 47f);
                    variant++;
                }
            }

            // Ground cover is visual only. Larger rocks and props remain outside the cells.
            for (int i = 0; i < 28; i++)
            {
                float y = .35f + (i / 2) * height / 14;
                float x = i % 2 == 0 ? -.6f - .22f * Mathf.Sin(i) : width + .6f + .22f * Mathf.Sin(i);
                if (i % 3 == 0)
                    Place(nature, Pick(_rocks, i), $"EdgeRock_{i}", x, y, .58f, i * 17f);
                Place(nature, Pick(_groundCover, i), $"EdgeGrass_{i}", x, y + .3f, .75f, i * 23f);
            }
            for (int i = 0; i < 10; i++)
                Place(nature, Pick(_groundCover, i + 30), $"FrontGrass_{i}",
                    width * (.08f + .085f * i), -.6f - .2f * Mathf.Sin(i * 1.7f), .8f, i * 29f);
            for (int i = 0; i < 36; i++)
            {
                var x = .6f + (i * 37 % 127) / 10f;
                var y = .5f + (i * 53 % 129) / 10f;
                Place(nature, Pick(_groundCover, i), $"MeadowDetail_{i}", x, y, .55f, i * 71f);
            }

            var props = new GameObject("KitProps").transform;
            props.SetParent(root, false);
            Place(props, Pick(_props, 0), "WestLogPile", -1.1f, height * .4f, .75f, 25f);
            Place(props, Pick(_props, 0), "EastLogPile", width + 1.2f, height * .73f, .75f, -30f);
            Place(props, Pick(_props, 1), "FrontFence", width * .25f, -1.15f, .85f, 0f);
            Place(props, Pick(_props, 2), "WestLantern", -.75f, height * .25f, .75f, 0f);
            Place(props, Pick(_props, 2), "EastLantern", width + .75f, height * .66f, .75f, 180f);
            Place(props, Pick(_props, 3), "FrontBarrel", width * .8f, -1.05f, .72f, 15f);
        }

        private static void Trail(Transform root, string name, Vector2 a, Vector2 b, float width, float bend, int layer)
        {
            const int samples = 32;
            var centers = new Vector3[samples];
            Vector2 across = new Vector2(-(b - a).y, (b - a).x).normalized;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                var point = Vector2.Lerp(a, b, t) + across * (Mathf.Sin(t * Mathf.PI * 2) * bend + Mathf.Sin(t * Mathf.PI * 5) * .045f);
                centers[i] = new Vector3(point.x, point.y, -.093f - layer * .001f);
            }
            var shoulder = new Color32(155, 147, 99, 255);
            PrimitiveArt.GroundRibbon(root, name + "Verge", centers, width * 1.3f, shoulder);
            for (int i = 0; i < samples; i++) centers[i].z -= .006f;
            PrimitiveArt.GroundRibbon(root, name, centers, width, Road);
            // A lighter worn strip breaks up the even fill without gravel clutter.
            for (int i = 0; i < samples; i++) centers[i].z -= .004f;
            PrimitiveArt.GroundRibbon(root, name + "Wear", centers, width * .48f, new Color32(196, 162, 116, 255));
        }

        private static GameObject Pick(GameObject[] assets, int index)
        {
            return assets != null && assets.Length > 0 ? assets[index % assets.Length] : null;
        }

        private static void Place(Transform parent, GameObject source, string name, float x, float y, float scale, float yaw)
        {
            if (source == null) return;
            GameObject instance;
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
                instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(source, parent);
            else
#endif
                instance = Instantiate(source, parent);
            instance.name = name;
            instance.transform.localPosition = new Vector3(x, y, 0f);
            instance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
#if UNITY_EDITOR
                if (!UnityEngine.Application.isPlaying) DestroyImmediate(collider);
                else Destroy(collider);
#else
                Destroy(collider);
#endif
            }
        }
    }
}
