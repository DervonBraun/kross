#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UI.Flow.Editor
{
    /// <summary>
    /// Отображает PageId как простое текстовое поле вместо раскрывающегося struct.
    /// </summary>
    [CustomPropertyDrawer(typeof(PageId))]
    public sealed class PageIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var idProp = property.FindPropertyRelative("_id");
            idProp.stringValue = EditorGUI.TextField(position, label, idProp.stringValue);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;
    }
}
#endif