using System;
using System.IO;
using Dreamy.Economy.UI;
using Dreamy.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.Editor
{
    public static class ResourceRecoverySampleBuilder
    {
        public const string Output = "Assets/DreamyResourceRecoveryGenerated";

        [MenuItem("Dreamy/Economy/Create Resource Recovery Prefabs")]
        public static void Create()
        {
            if (Directory.Exists(Output)) throw new InvalidOperationException("Output already exists; choose a fresh path before generating.");
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            GameObject row = null;
            try
            {
                row = Node("DeficitRow", null);
                row.AddComponent<LayoutElement>().preferredHeight = 115;
                var layout = row.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(12, 12, 8, 8);
                layout.spacing = 12;
                layout.childControlHeight = layout.childControlWidth = true;
                var view = row.AddComponent<ResourceDeficitRow>();
                var icon = Node("Icon", row.transform).AddComponent<Image>();
                icon.gameObject.AddComponent<LayoutElement>().preferredWidth = 64;
                var amount = Label("Amount", row.transform, "Resource", 24);
                amount.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                var acquire = Button("Acquire", row.transform, "Get more");
                acquire.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;
                Set(view, "icon", icon); Set(view, "amountText", amount); Set(view, "acquireButton", acquire);
                var rowPrefab = PrefabUtility.SaveAsPrefabAsset(row, Output + "/DeficitRow.prefab").GetComponent<ResourceDeficitRow>();
                CreatePanelVariant(Output + "/NotEnoughResourcesPanel.prefab", rowPrefab);
                Debug.Log("Created resource recovery prefabs at " + Output);
            }
            finally { if (row != null) UnityEngine.Object.DestroyImmediate(row); }
        }

        // Instantiate the base as a connected prefab instance so SaveAsPrefabAsset produces a real variant.
        public static void CreatePanelVariant(string path, ResourceDeficitRow rowPrefab)
        {
            if (rowPrefab == null) throw new ArgumentNullException(nameof(rowPrefab));
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.dreamy.feature/Runtime/Prefabs/BaseFeaturePanel.prefab");
            if (basePrefab == null) throw new InvalidOperationException("Install Dreamy Feature before generating the recovery panel.");
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var panel = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, preview);
                panel.name = "NotEnoughResourcesPanel";
                var panelView = panel.AddComponent<NotEnoughResourcesPanel>();
                Set(panelView, "canvasGroup", panel.GetComponent<CanvasGroup>());
                Set(panelView, "tweenPlayer", panel.GetComponent<UITweenPlayer>());
                var content = panel.transform.Find("SafeArea/Content");
                if (content == null) throw new InvalidOperationException("BaseFeaturePanel is missing SafeArea/Content.");
                var dialog = Node("Dialog", content);
                var rect = dialog.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.05f, .2f); rect.anchorMax = new Vector2(.95f, .8f); rect.offsetMin = rect.offsetMax = Vector2.zero;
                dialog.AddComponent<Image>().color = new Color(.09f, .12f, .2f, 1);
                var vertical = dialog.AddComponent<VerticalLayoutGroup>(); vertical.padding = new RectOffset(24, 24, 24, 24); vertical.spacing = 16;
                vertical.childControlHeight = vertical.childControlWidth = true; vertical.childForceExpandHeight = false;
                Label("Title", dialog.transform, "Not enough resources", 34).gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
                var viewport = Node("ResourceRows", dialog.transform);
                viewport.AddComponent<LayoutElement>().flexibleHeight = 1;
                viewport.AddComponent<Image>().color = Color.clear; viewport.AddComponent<RectMask2D>();
                var scroll = viewport.AddComponent<ScrollRect>(); scroll.horizontal = false;
                var rows = Node("Content", viewport.transform);
                var rowsRect = rows.GetComponent<RectTransform>(); rowsRect.anchorMin = new Vector2(0, 1); rowsRect.anchorMax = Vector2.one; rowsRect.pivot = new Vector2(.5f, 1); rowsRect.sizeDelta = Vector2.zero;
                var rowLayout = rows.AddComponent<VerticalLayoutGroup>(); rowLayout.childControlHeight = rowLayout.childControlWidth = true; rowLayout.childForceExpandHeight = false;
                rows.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scroll.content = rowsRect; scroll.viewport = viewport.GetComponent<RectTransform>();
                var status = Label("Status", dialog.transform, "Choose a resource", 24); status.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;
                var cancel = Button("Cancel", dialog.transform, "Return"); cancel.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
                Set(panelView, "rowContainer", rows.transform); Set(panelView, "rowPrefab", rowPrefab); Set(panelView, "statusText", status); Set(panelView, "cancelButton", cancel);
                PrefabUtility.SaveAsPrefabAsset(panel, path);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static GameObject Node(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform)); node.transform.SetParent(parent, false); return node;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static TMP_Text Label(string name, Transform parent, string text, int size)
        {
            var label = Node(name, parent).AddComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size; label.color = Color.white; label.raycastTarget = false;
            label.font = TMP_Settings.defaultFontAsset;
            return label;
        }
        private static Button Button(string name, Transform parent, string caption)
        {
            var node = Node(name, parent); node.AddComponent<Image>().color = new Color(.18f, .36f, .65f);
            var button = node.AddComponent<Button>(); var text = Label("Label", node.transform, caption, 26);
            Stretch(text.rectTransform); text.alignment = TextAlignmentOptions.Center; return button;
        }
        private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(name).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
