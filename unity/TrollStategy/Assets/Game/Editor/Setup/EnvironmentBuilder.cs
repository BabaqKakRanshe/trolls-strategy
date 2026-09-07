using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TrollStrategy.Editor.Setup
{
    public static class EnvironmentBuilder
    {
        public static void BuildEnvironment(Transform gridTransform)
        {
            // Clear existing environment children if any
            var existing = gridTransform.Find("EnvironmentWorld");
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var envRoot = new GameObject("EnvironmentWorld");
            envRoot.transform.SetParent(gridTransform, false);

            // 1. Load tiles
            string tilesPath = "Assets/Game/Art/Sprites/Environment/forgotten-memories-tiles.png";
            var allTileObjects = AssetDatabase.LoadAllAssetsAtPath(tilesPath);
            var tileSprites = new Dictionary<string, Sprite>();
            foreach (var obj in allTileObjects)
            {
                if (obj is Sprite s)
                    tileSprites[s.name] = s;
            }

            // 2. Load props
            string propsPath = "Assets/Game/Art/Sprites/Environment/forgotten-memories-props.png";
            var allProps = AssetDatabase.LoadAllAssetsAtPath(propsPath);
            var propSprites = new Dictionary<string, Sprite>();
            foreach (var obj in allProps)
            {
                if (obj is Sprite s)
                    propSprites[s.name] = s;
            }

            // Palette mapping from forgottenMemoriesPreview.json
            var palette = new Dictionary<char, int>
            {
                { 'l', 1 }, { 't', 2 }, { 'r', 3 },
                { 'm', 128 }, { 'c', 130 }, { 'n', 132 },
                { 'b', 257 }, { 'u', 258 }, { 'd', 259 },
                { 'f', 46 }, { 'g', 47 }, { 'h', 110 }, { 'i', 175 }
            };

            string[] layout = {
                "lttttttttttttr",
                "mccfccccccgccn",
                "mccccchccccccn",
                "mcgccccccicccn",
                "mccccfcccccccn",
                "mccchcccccgccn",
                "mccccccicccccn",
                "mcfccccccccgcn",
                "mccccgcccccccn",
                "mcchccccccfccn",
                "mcccccccgccccn",
                "mcgccccccccicn",
                "mccccfcccccccn",
                "buuuuuuuuuuuud"
            };

            // Container for ground tiles
            var tilesContainer = new GameObject("GroundTiles");
            tilesContainer.transform.SetParent(envRoot.transform, false);

            // 14x14 Playable Terrain
            for (int row = 0; row < 14; row++)
            {
                // In Unity, y = 13 - row (so row 0 is at top y=13, row 13 is at bottom y=0)
                int unityY = 13 - row;
                string rowStr = layout[row];

                for (int col = 0; col < 14; col++)
                {
                    char code = rowStr[col];
                    if (palette.TryGetValue(code, out int frameId))
                    {
                        string spriteName = $"tile_{frameId}";
                        if (tileSprites.TryGetValue(spriteName, out var sprite))
                        {
                            var tGo = new GameObject($"Tile_{col}_{unityY}");
                            tGo.transform.SetParent(tilesContainer.transform, false);
                            tGo.transform.position = new Vector3(col + 0.5f, unityY + 0.5f, 0f);
                            var sr = tGo.AddComponent<SpriteRenderer>();
                            sr.sprite = sprite;
                            sr.sortingOrder = -10;
                        }
                    }
                }
            }

            // Locked Terrain Border (surrounding columns -3..-1 and 14..16, and top/bottom rows -1 and 14)
            var lockedContainer = new GameObject("LockedBorderTerrain");
            lockedContainer.transform.SetParent(envRoot.transform, false);

            if (tileSprites.TryGetValue("tile_130", out var baseGrass))
            {
                for (int cy = -2; cy <= 15; cy++)
                {
                    for (int cx = -4; cx <= 17; cx++)
                    {
                        if (cx >= 0 && cx < 14 && cy >= 0 && cy < 14) continue; // Skip playable area

                        var tGo = new GameObject($"BorderTile_{cx}_{cy}");
                        tGo.transform.SetParent(lockedContainer.transform, false);
                        tGo.transform.position = new Vector3(cx + 0.5f, cy + 0.5f, 0f);
                        var sr = tGo.AddComponent<SpriteRenderer>();
                        sr.sprite = baseGrass;
                        sr.color = new Color(0.65f, 0.72f, 0.55f); // Soft darker tint for border
                        sr.sortingOrder = -12;
                    }
                }
            }

            // Decorative Trees on Left & Right sides
            var treesContainer = new GameObject("ForestTrees");
            treesContainer.transform.SetParent(envRoot.transform, false);

            string[] treeTypes = { "tree.autumn", "tree.blue", "tree.gold", "tree.pine", "tree.willow" };

            // Left forest grove (x ~ -3.5 to -0.8)
            float[] leftCols = { -3.2f, -2.2f, -1.2f };
            for (int r = 0; r < 12; r++)
            {
                float y = -0.5f + r * 1.25f;
                for (int c = 0; c < leftCols.Length; c++)
                {
                    string tName = treeTypes[(r + c * 2) % treeTypes.Length];
                    if (propSprites.TryGetValue(tName, out var tSprite))
                    {
                        var treeGo = new GameObject($"Tree_L_{c}_{r}");
                        treeGo.transform.SetParent(treesContainer.transform, false);
                        float x = leftCols[c] + (((r * 7 + c * 11) % 5) * 0.15f - 0.3f);
                        treeGo.transform.position = new Vector3(x, y, 0f);
                        var sr = treeGo.AddComponent<SpriteRenderer>();
                        sr.sprite = tSprite;
                        sr.flipX = (r + c) % 2 == 0;
                        sr.sortingOrder = (int)(10 - y); // Correct Y-sorting!
                    }
                }
            }

            // Right forest grove (x ~ 14.8 to 17.2)
            float[] rightCols = { 14.8f, 15.8f, 16.8f };
            for (int r = 0; r < 12; r++)
            {
                float y = -0.5f + r * 1.25f;
                for (int c = 0; c < rightCols.Length; c++)
                {
                    string tName = treeTypes[(r + c * 3 + 1) % treeTypes.Length];
                    if (propSprites.TryGetValue(tName, out var tSprite))
                    {
                        var treeGo = new GameObject($"Tree_R_{c}_{r}");
                        treeGo.transform.SetParent(treesContainer.transform, false);
                        float x = rightCols[c] + (((r * 5 + c * 7) % 5) * 0.15f - 0.3f);
                        treeGo.transform.position = new Vector3(x, y, 0f);
                        var sr = treeGo.AddComponent<SpriteRenderer>();
                        sr.sprite = tSprite;
                        sr.flipX = (r + c) % 2 != 0;
                        sr.sortingOrder = (int)(10 - y);
                    }
                }
            }

            // Decorative Props (Rocks, Stumps)
            if (propSprites.TryGetValue("prop.rock.gold", out var rockGold))
            {
                CreateProp(envRoot.transform, "Rock_1", rockGold, new Vector3(-0.5f, 3.5f, 0f));
                CreateProp(envRoot.transform, "Rock_2", rockGold, new Vector3(14.5f, 9.5f, 0f));
            }
            if (propSprites.TryGetValue("prop.rock.moss", out var rockMoss))
            {
                CreateProp(envRoot.transform, "Rock_Moss", rockMoss, new Vector3(-0.8f, 10.2f, 0f));
            }
            if (propSprites.TryGetValue("prop.stump", out var stump))
            {
                CreateProp(envRoot.transform, "Stump", stump, new Vector3(14.6f, 3.2f, 0f));
            }

            Debug.Log("[EnvironmentBuilder] Environment built with genuine Minifantasy tiles and forest props!");
        }

        private static void CreateProp(Transform parent, string name, Sprite sprite, Vector3 pos)
        {
            var pGo = new GameObject(name);
            pGo.transform.SetParent(parent, false);
            pGo.transform.position = pos;
            var sr = pGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = (int)(10 - pos.y);
        }
    }
}
