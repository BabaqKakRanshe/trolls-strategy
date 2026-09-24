using System.Collections.Generic;
using UnityEngine;

namespace TrollStrategy.Presentation.Visuals
{
    public static class PrimitiveArt
    {
        private static readonly Dictionary<Color, Material> Materials = new();
        private static Mesh _coneMesh;
        private static Mesh _rockMesh;

        public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            return Shape(PrimitiveType.Cube, parent, name, position, scale, color);
        }

        public static GameObject Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            return Shape(PrimitiveType.Cylinder, parent, name, position, scale, color);
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            return Shape(PrimitiveType.Sphere, parent, name, position, scale, color);
        }

        public static GameObject Cone(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
            return go;
        }

        // Flat, irregular ground accents. Negative local Z faces the colony camera.
        public static GameObject GroundPolygon(Transform parent, string name, Vector3[] boundary, Color color)
        {
            var mesh = new Mesh { name = $"DioramaGround_{name}" };
            var center = Vector3.zero;
            foreach (var point in boundary) center += point;
            center /= boundary.Length;
            float signedArea = 0;
            for (int i = 0; i < boundary.Length; i++)
            {
                var next = boundary[(i + 1) % boundary.Length];
                signedArea += boundary[i].x * next.y - next.x * boundary[i].y;
            }
            var vertices = new Vector3[boundary.Length + 1];
            var triangles = new int[boundary.Length * 3];
            vertices[0] = center;
            for (int i = 0; i < boundary.Length; i++)
            {
                vertices[i + 1] = boundary[i];
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = signedArea > 0 ? (i + 1) % boundary.Length + 1 : i + 1;
                triangles[i * 3 + 2] = signedArea > 0 ? i + 1 : (i + 1) % boundary.Length + 1;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) uv[i] = new Vector2((vertices[i].x + 4f) / 22f, (vertices[i].y + 4f) / 22f);
            mesh.uv = uv;
            return SavedMeshObject(parent, name, mesh, color, false);
        }

        public static GameObject GroundRibbon(Transform parent, string name, Vector3[] centers, float width, Color color)
        {
            var vertices = new Vector3[centers.Length * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(centers.Length - 1) * 6];
            for (int i = 0; i < centers.Length; i++)
            {
                var tangent = centers[Mathf.Min(i + 1, centers.Length - 1)] - centers[Mathf.Max(i - 1, 0)];
                var across = new Vector3(-tangent.y, tangent.x, 0).normalized;
                float half = width * (.48f + .07f * Mathf.Sin(i * 1.7f));
                half *= Mathf.Lerp(.06f, 1f, Mathf.SmoothStep(0, 1, Mathf.Min(i, centers.Length - 1 - i) / 2f));
                vertices[i * 2] = centers[i] + across * half;
                vertices[i * 2 + 1] = centers[i] - across * half;
                uv[i * 2] = new Vector2(0, i * .35f);
                uv[i * 2 + 1] = new Vector2(1, i * .35f);
                if (i == centers.Length - 1) continue;
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = $"DioramaGround_{name}", vertices = vertices, triangles = triangles, uv = uv };
            mesh.RecalculateNormals();
            return SavedMeshObject(parent, name, mesh, color, false);
        }

        public static GameObject GroundRim(Transform parent, string name, Vector3[] boundary, float depth, Color color)
        {
            var vertices = new Vector3[boundary.Length * 4];
            var triangles = new int[boundary.Length * 6];
            for (int i = 0; i < boundary.Length; i++)
            {
                int v = i * 4, t = i * 6;
                vertices[v] = boundary[i]; vertices[v + 1] = boundary[(i + 1) % boundary.Length];
                vertices[v + 2] = vertices[v] + Vector3.forward * depth;
                vertices[v + 3] = vertices[v + 1] + Vector3.forward * depth;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            var mesh = new Mesh { name = $"DioramaGround_{name}", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            return SavedMeshObject(parent, name, mesh, color, true);
        }

        private static GameObject SavedMeshObject(Transform parent, string name, Mesh mesh, Color color, bool shadows)
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                const string folder = "Assets/Game/Art/Meshes";
                if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                    UnityEditor.AssetDatabase.CreateFolder("Assets/Game/Art", "Meshes");
                string path = $"{folder}/{mesh.name}.asset";
                var saved = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) UnityEditor.AssetDatabase.CreateAsset(mesh, path);
                else
                {
                    UnityEditor.EditorUtility.CopySerialized(mesh, saved);
                    Object.DestroyImmediate(mesh);
                    mesh = saved;
                }
            }
#endif
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(color);
            renderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Rock(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            if (_rockMesh == null) _rockMesh = SculptedMesh("DioramaRock", false);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = _rockMesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
            return go;
        }

        private static Mesh ConeMesh()
        {
            if (_coneMesh == null) _coneMesh = SculptedMesh("DioramaPineBough", true);
            return _coneMesh;
        }

        private static Mesh SculptedMesh(string name, bool pine)
        {
#if UNITY_EDITOR
            string path = $"Assets/Game/Art/Meshes/{name}.asset";
            if (!UnityEngine.Application.isPlaying)
            {
                var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null) return existing;
            }
#endif
            int sides = pine ? 12 : 7;
            var rings = new Vector3[4, sides];
            for (int ring = 0; ring < 4; ring++)
            {
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    float radius = pine ? new[] { .5f, .32f, .19f, .012f }[ring] : new[] { .28f, .52f, .43f, .23f }[ring];
                    float z = pine ? new[] { 0f, -.29f, -.57f, -.98f }[ring] : new[] { .47f, .22f, -.27f, -.48f }[ring];
                    radius *= 1 + (pine ? (i % 2 == 0 ? .12f : -.16f) : .12f * Mathf.Sin(i * 2.4f + ring));
                    if (pine && ring == 0) z += i % 2 == 0 ? .065f : -.075f;
                    rings[ring, i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
                }
            }
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int n = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                indices.Add(n); indices.Add(n + 1); indices.Add(n + 2);
            }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                Triangle(new Vector3(0, 0, pine ? .02f : .47f), rings[0, i], rings[0, next]);
                Triangle(new Vector3(0, 0, pine ? -.98f : -.48f), rings[3, next], rings[3, i]);
                for (int ring = 0; ring < 3; ring++)
                {
                    Triangle(rings[ring, i], rings[ring + 1, next], rings[ring, next]);
                    Triangle(rings[ring, i], rings[ring + 1, i], rings[ring + 1, next]);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Game/Art/Meshes"))
                    UnityEditor.AssetDatabase.CreateFolder("Assets/Game/Art", "Meshes");
                UnityEditor.AssetDatabase.CreateAsset(mesh, path);
            }
#endif
            return mesh;
        }

        private static GameObject Shape(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (UnityEngine.Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
            return go;
        }

        private static Material MaterialFor(Color color)
        {
            if (Materials.TryGetValue(color, out var cached))
            {
#if UNITY_EDITOR
                if (!UnityEngine.Application.isPlaying && !UnityEditor.AssetDatabase.Contains(cached))
                    Materials.Remove(color);
                else
#endif
                    return cached;
            }
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                const string folder = "Assets/Game/Art/Materials";
                if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                    UnityEditor.AssetDatabase.CreateFolder("Assets/Game/Art", "Materials");
                Color32 c = color;
                string path = $"{folder}/Primitive_{c.r:X2}{c.g:X2}{c.b:X2}.mat";
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                if (asset == null)
                {
                    var assetShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    asset = new Material(assetShader) { color = color };
                    asset.SetFloat("_Smoothness", 0.08f);
                    UnityEditor.AssetDatabase.CreateAsset(asset, path);
                }
                asset.shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                asset.color = color;
                asset.SetFloat("_Smoothness", 0.08f);
                asset.SetFloat("_Metallic", 0f);
                UnityEditor.EditorUtility.SetDirty(asset);
                Materials.Add(color, asset);
                return asset;
            }
#endif
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            material.SetFloat("_Smoothness", 0.08f);
            material.SetFloat("_Metallic", 0f);
            Materials.Add(color, material);
            return material;
        }
    }
}
