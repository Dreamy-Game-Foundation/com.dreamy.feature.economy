using System;
using Dreamy.Economy.UI;
using Dreamy.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.Editor
{
    public static class ResourceSuggestionPrefabBuilder
    {
        [MenuItem("Dreamy/Economy/Create Single Resource Suggestion Prefab")]
        public static void Create()
        {
            const string path = "Assets/ResourceSuggestionPanel.prefab";
            CreateBase(path, TMP_Settings.defaultFontAsset);
        }

        public static void CreateBase(string path, TMP_FontAsset font, bool overwrite = false)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) throw new InvalidOperationException("Prefab already exists: " + path);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.dreamy.feature/Runtime/Prefabs/BaseFeaturePanel.prefab");
            if (source == null || font == null) throw new InvalidOperationException("Feature foundation and TMP font are required.");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                root.name = "ResourceSuggestionPanel";
                var panel = root.AddComponent<ResourceSuggestionPanel>();
                Set(panel, "canvasGroup", root.GetComponent<CanvasGroup>());
                Set(panel, "tweenPlayer", root.GetComponent<UITweenPlayer>());
                SetString(panel, "resourceId", "currency.coin");
                var content = root.transform.Find("SafeArea/Content");
                if (content == null) throw new InvalidOperationException("Expected Feature SafeArea/Content.");
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.padding = new RectOffset(24, 24, 24, 24);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                var dialog = Node("Dialog", content);
                var dimensions = dialog.AddComponent<LayoutElement>();
                dimensions.preferredWidth = 620; dimensions.preferredHeight = 540;
                dimensions.minWidth = 280; dimensions.minHeight = 320;
                Background(dialog, new Color32(47, 99, 164, 255));
                var rim = Node("InnerRim", dialog.transform); Inset(rim, 7); Background(rim, new Color32(243, 236, 220, 255));
                var surface = Node("Surface", rim.transform); Inset(surface, 8); Background(surface, new Color32(229, 212, 193, 255));
                var title = Label("Message", dialog.transform, "You do not have enough\ncoins to continue", font, 34, new Color32(22, 22, 24, 255));
                Place(title.rectTransform, new Vector2(.07f, .73f), new Vector2(.93f, .93f));
                title.fontStyle = FontStyles.Bold;
                var resource = Label("ResourceName", dialog.transform, "COIN", font, 64, new Color32(206, 136, 0, 255));
                resource.fontStyle = FontStyles.Bold;
                Place(resource.rectTransform, new Vector2(.12f, .25f), new Vector2(.88f, .7f));
                var state = Label("Balance", dialog.transform, "", font, 18, new Color32(93, 74, 57, 255));
                Place(state.rectTransform, new Vector2(.08f, .14f), new Vector2(.92f, .20f));
                var actions = Node("Actions", dialog.transform);
                Place((RectTransform)actions.transform, new Vector2(.07f, -.025f), new Vector2(.93f, .13f));
                var horizontal = actions.AddComponent<HorizontalLayoutGroup>(); horizontal.spacing = 44;
                horizontal.childControlWidth = horizontal.childControlHeight = true;
                horizontal.childForceExpandWidth = true; horizontal.childForceExpandHeight = true;
                var acquire = Button("Shop", actions.transform, "SHOP", font, new Color32(255, 171, 28, 255), new Color32(237, 130, 0, 255));
                var later = Button("Later", actions.transform, "LATER", font, new Color32(53, 188, 244, 255), new Color32(0, 107, 218, 255));
                Set(panel, "acquireButton", acquire); Set(panel, "laterButton", later); Set(panel, "balanceText", state);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void CreateVariant(string basePath, string path, ResourceId resource, string resourceName, Color resourceColor, bool overwrite = false)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) throw new InvalidOperationException("Prefab already exists: " + path);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath), scene);
                root.name = System.IO.Path.GetFileNameWithoutExtension(path);
                SetString(root.GetComponent<ResourceSuggestionPanel>(), "resourceId", resource.Value);
                root.transform.Find("SafeArea/Content/Dialog/Message").GetComponent<TMP_Text>().text = "You do not have enough\n" + resourceName + " to continue";
                var label = root.transform.Find("SafeArea/Content/Dialog/ResourceName").GetComponent<TMP_Text>();
                label.text = resourceName.ToUpperInvariant(); label.color = resourceColor;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static GameObject Node(string name, Transform parent)
        { var node = new GameObject(name, typeof(RectTransform)); node.transform.SetParent(parent, false); return node; }
        private static void Background(GameObject node, Color color)
        { var graphic = node.AddComponent<Image>(); graphic.color = color; graphic.raycastTarget = false; }
        private static void Inset(GameObject node, float amount)
        { var r = (RectTransform)node.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.one * amount; r.offsetMax = Vector2.one * -amount; }
        private static void Place(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static TMP_Text Label(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color)
        {
            var label = Node(name, parent).AddComponent<TextMeshProUGUI>(); label.font = font; label.text = text;
            label.fontSize = size; label.enableAutoSizing = true; label.fontSizeMin = size * .65f; label.fontSizeMax = size;
            label.color = color; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            return label;
        }
        private static Button Button(string name, Transform parent, string caption, TMP_FontAsset font, Color rim, Color face)
        {
            var root = Node(name, parent); root.AddComponent<LayoutElement>().minWidth = 80;
            var graphic = root.AddComponent<Image>(); graphic.color = rim;
            var button = root.AddComponent<Button>(); button.targetGraphic = graphic;
            var surface = Node("Face", root.transform); Inset(surface, 4); Background(surface, face);
            var label = Label("Label", root.transform, caption, font, 34, Color.white); label.fontStyle = FontStyles.Bold;
            Place(label.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetString(UnityEngine.Object target, string field, string value)
        { var data = new SerializedObject(target); data.FindProperty(field).stringValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
