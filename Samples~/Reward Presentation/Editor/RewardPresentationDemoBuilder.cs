using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Dreamy.Economy.UI;
using Dreamy.UI;

namespace Dreamy.Economy.RewardSample.Editor
{
    public static class RewardPresentationDemoBuilder
    {
        public static string Root
        {
            get
            {
                foreach(var guid in AssetDatabase.FindAssets("RewardPresentationDemoBuilder t:MonoScript"))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);
                    if(path.EndsWith("/Editor/RewardPresentationDemoBuilder.cs",StringComparison.Ordinal))
                        return path.Substring(0,path.Length-"/Editor/RewardPresentationDemoBuilder.cs".Length);
                }
                throw new InvalidOperationException("Import the Reward Presentation sample before building it.");
            }
        }
        private static readonly Color Navy=new(0.035f,0.05f,0.09f,1), Card=new(0.075f,0.10f,0.16f,1), Gold=new(1,0.76f,0.29f,1);
        // Writes only this sample's assets, in an additive scene, restoring the user's active scene.
        [MenuItem("Dreamy/Economy/Samples/Build Reward Presentation Demo")]
        public static void Build()
        {
            if(SceneManager.GetSceneByPath(Root+"/Scenes/RewardPresentationDemo.unity").isLoaded)
                throw new InvalidOperationException("Close the demo scene before regenerating sample assets.");
            Directory.CreateDirectory(Root+"/Prefabs"); Directory.CreateDirectory(Root+"/Scenes");
            Directory.CreateDirectory(Root+"/Resources/RewardPresentation");
            AssetDatabase.Refresh();
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var camera=new GameObject("Main Camera",typeof(Camera)); camera.tag="MainCamera";
                camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
                camera.GetComponent<Camera>().backgroundColor=Navy;
                new GameObject("Directional Light",typeof(Light)).GetComponent<Light>().type=LightType.Directional;
                var events=new GameObject("EventSystem",typeof(EventSystem));
                var input=Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if(input != null) events.AddComponent(input); else events.AddComponent<StandaloneInputModule>();
                var canvas=new GameObject("Reward Demo Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(PanelManager));
                canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=1;
                var screenLayer=Rect("Layer Screen",canvas.transform);Stretch(screenLayer);screenLayer.gameObject.AddComponent<UILayerRoot>().SetLayer(UILayer.Screen);
                var background=Box("Background",screenLayer,Navy);Stretch(background);
                var safe=Rect("Demo Safe Area",screenLayer);Stretch(safe);safe.gameObject.AddComponent<SafeArea>();
                var body=Box("Demo Card",safe,new Color(0.055f,0.075f,0.12f));Anchor(body,new Vector2(0.08f,0.1f),new Vector2(0.92f,0.9f));
                var title=Text("Title",body,"REWARD LAB",64,Color.white);Place(title.rectTransform,0.08f,0.86f,0.92f,0.95f);
                var subtitle=Text("Subtitle",body,"ONE SOURCE. THREE PRESENTATION FLOWS.",20,Gold);Place(subtitle.rectTransform,0.08f,0.81f,0.92f,0.85f);
                var balance=Text("Balances",body,"COINS  0     HAMMERS  0",29,Gold);Place(balance.rectTransform,0.08f,0.73f,0.92f,0.79f);
                var buttons=new Button[6];string[] labels={"1  EARN REWARD","2  OPEN CLAIM PANEL","3  CLAIM WITHOUT PANEL","4  SHOW RECEIVED NOTICE","RELOAD / RECOVER","INJECT FINAL SAVE FAILURE"};
                for(int i=0;i<labels.Length;i++)
                {buttons[i]=Button("Action"+i,body,labels[i],i==1?Gold:new Color(0.13f,0.17f,0.25f));Place((RectTransform)buttons[i].transform,0.08f,0.62f-i*0.085f,0.92f,0.685f-i*0.085f);}
                var status=Text("Status",body,"Initializing persistent demo...",23,new Color(0.65f,0.73f,0.85f));Place(status.rectTransform,0.08f,0.05f,0.92f,0.17f);
                var catalog=AssetDatabase.LoadAssetAtPath<ResourceDisplayCatalog>(Root+"/Resources/RewardPresentation/DisplayCatalog.asset");
                if(catalog==null){catalog=ScriptableObject.CreateInstance<ResourceDisplayCatalog>();AssetDatabase.CreateAsset(catalog,Root+"/Resources/RewardPresentation/DisplayCatalog.asset");}
                var catalogSo=new SerializedObject(catalog);var resources=catalogSo.FindProperty("resources");resources.arraySize=2;
                string[] ids={"currency.coin","item.booster.hammer"},names={"Coins","Hammer booster"};
                for(int i=0;i<2;i++){var entry=resources.GetArrayElementAtIndex(i);entry.FindPropertyRelative("resourceId").stringValue=ids[i];entry.FindPropertyRelative("displayName").stringValue=names[i];}
                catalogSo.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(catalog);
                var item=Box("Reward Item",canvas.transform,new Color(0.12f,0.16f,0.23f));item.sizeDelta=new Vector2(600,110);
                item.gameObject.AddComponent<LayoutElement>().preferredHeight=110;
                var itemView=item.gameObject.AddComponent<RewardItemView>();
                var icon=Box("Icon",item,Gold);Place(icon,0.05f,0.22f,0.16f,0.78f);
                var name=Text("Name",item,"Coins",27,Color.white);Place(name.rectTransform,0.20f,0.1f,0.73f,0.9f);
                var amount=Text("Amount",item,"100",32,Gold);Place(amount.rectTransform,0.73f,0.1f,0.95f,0.9f);amount.alignment=TextAlignmentOptions.Right;
                Set(itemView,"nameText",name);Set(itemView,"amountText",amount);Set(itemView,"icon",icon.GetComponent<Image>());
                var itemAsset=PrefabUtility.SaveAsPrefabAsset(item.gameObject,Root+"/Prefabs/RewardItem.prefab");UnityEngine.Object.DestroyImmediate(item.gameObject);
                var overlay=Box("Reward Panel",canvas.transform,new Color(0,0,0,0.82f));Stretch(overlay);
                var panel=overlay.gameObject.AddComponent<RewardPanel>();
                var panelSafe=Rect("Safe Area",overlay);Stretch(panelSafe);panelSafe.gameObject.AddComponent<SafeArea>();
                var card=Box("Card",panelSafe,Card);Place(card,0.12f,0.25f,0.88f,0.75f);
                var stripe=Box("Accent",card,Gold);Place(stripe,0,0.984f,1,1);
                var panelTitle=Text("Title",card,"REWARDS",42,Color.white);Place(panelTitle.rectTransform,0.08f,0.84f,0.80f,0.95f);
                var close=Button("Close",card,"CLOSE",new Color(0.16f,0.2f,0.29f));Place((RectTransform)close.transform,0.73f,0.855f,0.94f,0.93f);
                var scrollBox=Rect("Scroll",card);Place(scrollBox,0.07f,0.31f,0.93f,0.79f);
                var scroll=scrollBox.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;
                var viewport=Box("Viewport",scrollBox,new Color(1,1,1,0.01f));Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();
                var content=Rect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(0.5f,1);content.sizeDelta=Vector2.zero;
                var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=15;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=content;
                var panelStatus=Text("Status",card,"Tap Claim to receive these rewards.",22,new Color(0.65f,0.73f,0.85f));Place(panelStatus.rectTransform,0.08f,0.20f,0.92f,0.29f);
                var claim=Button("Claim",card,"CLAIM REWARDS",Gold);Place((RectTransform)claim.transform,0.08f,0.06f,0.92f,0.17f);
                Set(panel,"titleText",panelTitle);Set(panel,"statusText",panelStatus);Set(panel,"claimButton",claim);Set(panel,"claimLabel",claim.GetComponentInChildren<TMP_Text>());
                Set(panel,"closeButton",close);Set(panel,"itemContainer",content);Set(panel,"itemPrefab",itemAsset.GetComponent<RewardItemView>());Set(panel,"displayCatalog",catalog);
                Set(panel,"canvasGroup",overlay.GetComponent<CanvasGroup>());
                PrefabUtility.SaveAsPrefabAsset(overlay.gameObject,Root+"/Prefabs/RewardPanel.prefab");
                var demo=canvas.AddComponent<RewardPresentationDemo>();Set(demo,"panel",panel);Set(demo,"balanceText",balance);Set(demo,"statusText",status);
                string[] fields={"earnButton","confirmButton","silentButton","noticeButton","reloadButton","faultButton"};for(int i=0;i<6;i++)Set(demo,fields[i],buttons[i]);
                if(!EditorSceneManager.SaveScene(scene,Root+"/Scenes/RewardPresentationDemo.unity"))throw new InvalidOperationException("Sample scene save failed.");AssetDatabase.SaveAssets();
            }
            finally { SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true); }
        }
        private static RectTransform Rect(string name,Transform parent)
        {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
        private static RectTransform Box(string name,Transform parent,Color color)
        {var rect=Rect(name,parent);rect.gameObject.AddComponent<Image>().color=color;return rect;}
        private static void Stretch(RectTransform rect)=>Place(rect,0,0,1,1);
        private static void Anchor(RectTransform rect,Vector2 min,Vector2 max){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;}
        private static void Place(RectTransform rect,float x,float y,float x2,float y2)=>Anchor(rect,new Vector2(x,y),new Vector2(x2,y2));
        private static TMP_Text Text(string name,Transform parent,string value,float size,Color color)
        {
            var rect=Rect(name,parent);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.text=value;text.fontSize=size;text.color=color;
            text.font=TMP_Settings.defaultFontAsset;text.raycastTarget=false;text.alignment=TextAlignmentOptions.MidlineLeft;return text;
        }
        private static Button Button(string name,Transform parent,string value,Color color)
        {
            var rect=Box(name,parent,color);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
            var label=Text("Label",rect,value,23,color==Gold?Navy:Color.white);Stretch(label.rectTransform);label.alignment=TextAlignmentOptions.Center;return button;
        }
        private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
        {var so=new SerializedObject(target);var property=so.FindProperty(field);if(property==null)throw new InvalidOperationException(field);property.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
