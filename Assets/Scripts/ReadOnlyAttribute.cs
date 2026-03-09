using UnityEngine;

/// <summary>
/// Помечает SerializeField как read-only в инспекторе Unity.
/// Поле отображается, но недоступно для редактирования — удобно для дебага runtime-значений.
/// </summary>
public sealed class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
namespace UnityEditor
{
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public sealed class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }

        public override float GetPropertyHeight(UnityEditor.SerializedProperty property, GUIContent label)
            => UnityEditor.EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif