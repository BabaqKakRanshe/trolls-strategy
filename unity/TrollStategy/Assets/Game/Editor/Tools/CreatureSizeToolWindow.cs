using TrollStrategy.Content;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Units;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Tools
{
    public sealed class CreatureSizeToolWindow : EditorWindow
    {
        private const float MinScale = 0.05f;
        private const float SliderMaxScale = 4f;
        private const float PreviewCellPixels = 72f;

        private GameContentCatalog _catalog;
        private Vector2 _scroll;

        [MenuItem("TrollStrategy/Dev/Tools/Creature Size Tool")]
        private static void Open()
        {
            var window = GetWindow<CreatureSizeToolWindow>();
            window.titleContent = new GUIContent("Creature Sizes");
            window.minSize = new Vector2(390f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_catalog == null)
                _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                    "Assets/Game/Content/Definitions/GameContentCatalog.asset");

            Undo.undoRedoPerformed += HandleUndoRedo;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Размеры спрайтов существ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Размер применяется только к изображению существа. Коллайдер, круг выделения и игровая логика не меняются.",
                MessageType.Info);

            _catalog = (GameContentCatalog)EditorGUILayout.ObjectField(
                "Каталог", _catalog, typeof(GameContentCatalog), false);

            if (_catalog == null)
            {
                EditorGUILayout.HelpBox("Выберите GameContentCatalog с существами.", MessageType.Warning);
                return;
            }

            if (_catalog.Units == null || _catalog.Units.Count == 0)
            {
                EditorGUILayout.HelpBox("В выбранном каталоге нет существ.", MessageType.Warning);
                return;
            }

            float cellSize = _catalog.Economy != null ? Mathf.Max(0.01f, _catalog.Economy.CellSize) : 1f;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _catalog.Units.Count; i++)
            {
                var definition = _catalog.Units[i];
                if (definition != null) DrawCreature(definition, cellSize);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawCreature(UnitDefinition definition, float cellSize)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(definition.DisplayName, definition.Kind.ToString(), EditorStyles.boldLabel);

                var view = ContentPrefabs.Unit(definition);
                if (view == null)
                {
                    EditorGUILayout.HelpBox("У существа не назначен префаб с UnitView.", MessageType.Warning);
                    return;
                }

                var serializedView = new SerializedObject(view);
                serializedView.Update();
                var scaleProperty = serializedView.FindProperty("_spriteScale");
                float currentScale = scaleProperty != null && scaleProperty.floatValue > 0f
                    ? Mathf.Max(MinScale, scaleProperty.floatValue)
                    : 1f;

                var previewRect = GUILayoutUtility.GetRect(120f, 128f, GUILayout.ExpandWidth(true));
                DrawCellPreview(previewRect, view.IdleSprite, currentScale, cellSize);

                EditorGUI.BeginChangeCheck();
                float nextScale = EditorGUILayout.Slider("Масштаб", currentScale, MinScale, SliderMaxScale);
                nextScale = EditorGUILayout.FloatField("Точное значение", nextScale);
                if (EditorGUI.EndChangeCheck())
                    SaveScale(serializedView, scaleProperty, view, definition, nextScale);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Сбросить до 1", GUILayout.Width(120f)))
                        SaveScale(serializedView, scaleProperty, view, definition, 1f);
                }
            }
        }

        private static void DrawCellPreview(Rect rect, Sprite sprite, float scale, float cellSize)
        {
            EditorGUI.DrawRect(rect, new Color(0.10f, 0.13f, 0.12f));

            var cellRect = new Rect(
                rect.center.x - PreviewCellPixels * 0.5f,
                rect.center.y - PreviewCellPixels * 0.5f,
                PreviewCellPixels,
                PreviewCellPixels);
            EditorGUI.DrawRect(cellRect, new Color(0.22f, 0.28f, 0.23f));
            Handles.DrawSolidRectangleWithOutline(cellRect, Color.clear, new Color(0.55f, 0.68f, 0.44f));

            if (sprite == null)
            {
                GUI.Label(rect, "Нет спрайта", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Texture texture = AssetPreview.GetAssetPreview(sprite);
            if (texture == null) texture = AssetPreview.GetMiniThumbnail(sprite);
            if (texture == null) return;

            Vector2 worldSize = sprite.bounds.size * scale;
            float width = Mathf.Max(1f, worldSize.x / cellSize * PreviewCellPixels);
            float height = Mathf.Max(1f, worldSize.y / cellSize * PreviewCellPixels);
            var spriteRect = new Rect(rect.center.x - width * 0.5f, rect.center.y - height * 0.5f, width, height);
            GUI.DrawTexture(spriteRect, texture, ScaleMode.StretchToFill, true);
        }

        private void SaveScale(
            SerializedObject serializedView,
            SerializedProperty scaleProperty,
            UnitView prefabView,
            UnitDefinition definition,
            float requestedScale)
        {
            if (scaleProperty == null) return;

            float scale = float.IsNaN(requestedScale) || float.IsInfinity(requestedScale)
                ? 1f
                : Mathf.Max(MinScale, requestedScale);
            scaleProperty.floatValue = scale;
            serializedView.ApplyModifiedProperties();
            EditorUtility.SetDirty(prefabView);
            AssetDatabase.SaveAssetIfDirty(prefabView.gameObject);
            RefreshLiveViews(definition, scale);
            SceneView.RepaintAll();
            Repaint();
        }

        // Spawned creatures keep their own copy of the prefab value, so push the new scale to them.
        private static void RefreshLiveViews(UnitDefinition changedDefinition, float scale)
        {
            if (!UnityEngine.Application.isPlaying) return;

            var views = Object.FindObjectsByType<UnitView>(FindObjectsInactive.Exclude);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i].Definition == changedDefinition)
                    views[i].SetSpriteScale(scale);
            }
        }

        private void HandleUndoRedo()
        {
            Repaint();
        }

        private void HandlePlayModeChanged(PlayModeStateChange state)
        {
            Repaint();
        }
    }
}
