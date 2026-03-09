#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// Отображает [ReadOnly]-поля в Inspector как disabled (только для чтения).
    /// Полезно для отображения runtime-состояния WebUIManager.
    /// </summary>
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public sealed class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(position, property, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif
