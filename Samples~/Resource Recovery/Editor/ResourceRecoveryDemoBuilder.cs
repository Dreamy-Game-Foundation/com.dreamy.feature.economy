using System;
using System.IO;
using Dreamy.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dreamy.Economy.Samples.Editor
{
    public static class ResourceRecoveryDemoBuilder
    {
        [MenuItem("Dreamy/Economy/Create Recovery Demo Scene")]
        public static void Create()
        {
            string[] scripts = AssetDatabase.FindAssets("ResourceRecoveryDemoBuilder t:MonoScript");
            string root = Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(scripts[0]))).Replace('\\', '/');
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(root + "/ResourceRecoveryDemo.unity") != null)
                throw new InvalidOperationException("Demo scene already exists.");
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var canvas = Node("Recovery Demo", null);
                var display = canvas.AddComponent<Canvas>(); display.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080,1920); scaler.matchWidthOrHeight = .5f;
                canvas.AddComponent<GraphicRaycaster>();
                canvas.AddComponent<PanelManager>();
                var safe = Node("SafeArea", canvas.transform); Stretch(safe.GetComponent<RectTransform>()); safe.AddComponent<SafeArea>();
                var content = Node("Content", safe.transform); var rect = content.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(.08f,.12f); rect.anchorMax = new Vector2(.92f,.88f); rect.offsetMin = rect.offsetMax = Vector2.zero;
                var layout = content.AddComponent<VerticalLayoutGroup>(); layout.spacing=24; layout.childForceExpandHeight=false; layout.childControlHeight=layout.childControlWidth=true;
                Text("Title", content.transform, "Economy resource recovery", 42, 100);
                var status = Text("Status", content.transform, "Starting...", 28, 220);
                var silent = Button("SilentSpend", content.transform, "Spend 100 Coin (silent)");
                var prompt = Button("PromptSpend", content.transform, "Spend 100 Coin (prompt)");
                var multi = Button("MultiSpend", content.transform, "Spend 100 Coin + 10 Gem");
                var direct = Button("Direct", content.transform, "Open Gem tab directly");
                var grant = Button("DemoGrant", content.transform, "DEMO: add 100 Coin + 10 Gem");
                var panelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/NotEnoughResourcesPanel.prefab");
                var panel = (GameObject)PrefabUtility.InstantiatePrefab(panelAsset, scene);
                panel.SetActive(false);
                var shop = Node("Demo destination", canvas.transform); Stretch(shop.GetComponent<RectTransform>());
                shop.AddComponent<Image>().color = new Color(.08f,.1f,.18f,1);
                var shopSafe = Node("SafeArea",shop.transform); Stretch(shopSafe.GetComponent<RectTransform>());shopSafe.AddComponent<SafeArea>();
                var shopContent = Node("Content",shopSafe.transform); var sr=shopContent.GetComponent<RectTransform>();sr.anchorMin=new Vector2(.08f,.2f);sr.anchorMax=new Vector2(.92f,.8f);sr.offsetMin=sr.offsetMax=Vector2.zero;
                var shopLayout=shopContent.AddComponent<VerticalLayoutGroup>();shopLayout.spacing=24;shopLayout.childControlHeight=shopLayout.childControlWidth=true;shopLayout.childForceExpandHeight=false;
                var title=Text("Title",shopContent.transform,"Resource tab",30,300);
                var coin=Button("CoinTab",shopContent.transform,"Coin tab");var gem=Button("GemTab",shopContent.transform,"Gem tab");var back=Button("Return",shopContent.transform,"Return without purchase");
                var destination=shop.AddComponent<RecoveryDemoDestination>();Set(destination,"title",title);Set(destination,"coinTab",coin);Set(destination,"gemTab",gem);Set(destination,"returnButton",back);shop.SetActive(false);
                var demo=canvas.AddComponent<ResourceRecoveryDemo>();Set(demo,"panel",panel.GetComponent<Dreamy.Economy.UI.NotEnoughResourcesPanel>());Set(demo,"destination",destination);
                Set(demo,"silentButton",silent);Set(demo,"promptButton",prompt);Set(demo,"multiButton",multi);Set(demo,"directButton",direct);Set(demo,"grantButton",grant);Set(demo,"statusText",status);
                new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
                EditorSceneManager.SaveScene(scene,root + "/ResourceRecoveryDemo.unity");
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true); }
        }
        private static GameObject Node(string name,Transform parent) { var n=new GameObject(name,typeof(RectTransform));n.transform.SetParent(parent,false);return n; }
        private static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
        private static TMP_Text Text(string name,Transform parent,string text,int size,int height)
        {
            var t=Node(name,parent).AddComponent<TextMeshProUGUI>();t.text=text;t.fontSize=size;t.font=TMP_Settings.defaultFontAsset;t.color=Color.white;t.raycastTarget=false;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight=height;return t;
        }
        private static Button Button(string name,Transform parent,string caption)
        {
            var n=Node(name,parent);n.AddComponent<Image>().color=new Color(.18f,.36f,.65f);var b=n.AddComponent<Button>();n.AddComponent<LayoutElement>().preferredHeight=110;
            var t=Text("Label",n.transform,caption,28,110);Stretch(t.rectTransform);t.alignment=TextAlignmentOptions.Center;return b;
        }
        private static void Set(UnityEngine.Object target,string name,UnityEngine.Object value)
        { var s=new SerializedObject(target);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
