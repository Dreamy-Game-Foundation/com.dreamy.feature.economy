using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dreamy.Economy.UI.Editor
{
    [CustomEditor(typeof(ResourceUIHolder))]
    public sealed class ResourceUIHolderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty catalogProperty = serializedObject.FindProperty("catalog");
            SerializedProperty resourceIdProperty = serializedObject.FindProperty("resourceId");

            EditorGUILayout.PropertyField(catalogProperty);
            DrawResourceSelector(catalogProperty.objectReferenceValue as ResourceDisplayCatalog, resourceIdProperty);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("iconImage"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("amountText"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayNameText"));

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (Object value in targets)
                {
                    ResourceUIHolder holder = (ResourceUIHolder)value;
                    holder.SetResource(resourceIdProperty.stringValue);
                    EditorUtility.SetDirty(holder);
                }
            }
        }

        private static void DrawResourceSelector(
            ResourceDisplayCatalog catalog,
            SerializedProperty resourceIdProperty)
        {
            if (catalog == null || catalog.Resources.Count == 0)
            {
                EditorGUILayout.PropertyField(resourceIdProperty, new GUIContent("Resource ID"));
                return;
            }

            IReadOnlyList<ResourceDisplayDefinition> resources = catalog.Resources;
            string[] labels = new string[resources.Count];
            int selectedIndex = 0;
            for (int index = 0; index < resources.Count; index++)
            {
                ResourceDisplayDefinition definition = resources[index];
                labels[index] = $"{definition.DisplayName} ({definition.Id})";
                if (definition.Id == resourceIdProperty.stringValue) selectedIndex = index;
            }

            int nextIndex = EditorGUILayout.Popup("Resource Type", selectedIndex, labels);
            resourceIdProperty.stringValue = resources[nextIndex].Id;
        }
    }
}
