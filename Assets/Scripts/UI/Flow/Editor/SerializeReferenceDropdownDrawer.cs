#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UI.Flow.Editor
{
    /// <summary>
    /// Рисует кнопку "Set Type" для [SerializeReference] полей типа IFlowCondition.
    /// Показывает выпадающий список всех конкретных реализаций в проекте.
    ///
    /// Только для IFlowCondition — IFlowStep теперь хранятся как FlowStepAsset (SO-ассеты).
    /// </summary>
    [CustomPropertyDrawer(typeof(IFlowCondition), useForChildren: true)]
    public sealed class SerializeReferenceDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var managedRef  = property.managedReferenceValue;
            var currentType = managedRef?.GetType();
            var typeName    = currentType != null ? currentType.Name : "< None >";

            float btnWidth  = 100f;
            var   labelRect = new Rect(position.x, position.y, position.width - btnWidth - 4f, EditorGUIUtility.singleLineHeight);
            var   btnRect   = new Rect(position.x + position.width - btnWidth, position.y, btnWidth, EditorGUIUtility.singleLineHeight);

            if (managedRef != null)
                EditorGUI.PropertyField(labelRect, property, new GUIContent($"{label.text}  [{typeName}]"), true);
            else
                EditorGUI.LabelField(labelRect, label.text, $"[ {typeName} ]");

            if (GUI.Button(btnRect, "Set Type"))
            {
                var types = GetConcreteTypes(typeof(IFlowCondition));
                var menu  = new GenericMenu();

                menu.AddItem(new GUIContent("None"), currentType == null, () =>
                {
                    property.managedReferenceValue = null;
                    property.serializedObject.ApplyModifiedProperties();
                });

                menu.AddSeparator("");

                foreach (var t in types)
                {
                    var captured   = t;
                    bool isSelected = currentType == captured;
                    menu.AddItem(new GUIContent(captured.Name), isSelected, () =>
                    {
                        property.managedReferenceValue = Activator.CreateInstance(captured);
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }

                menu.ShowAsContext();
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.managedReferenceValue == null)
                return EditorGUIUtility.singleLineHeight;

            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        private static readonly Dictionary<Type, List<Type>> _cache = new();

        private static List<Type> GetConcreteTypes(Type interfaceType)
        {
            if (_cache.TryGetValue(interfaceType, out var cached)) return cached;

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => !t.IsAbstract
                         && !t.IsInterface
                         && !t.IsGenericType
                         && interfaceType.IsAssignableFrom(t)
                         && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToList();

            _cache[interfaceType] = types;
            return types;
        }
    }
}
#endif