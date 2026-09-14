#if UNITY_EDITOR
using Assets.Scripts.Utils;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.EditorExtensions
{
    // Dropdown s neabstraktnimi [Serializable] typy, ktere implementuji typ pole; pod nim pole
    // vybrane instance. U pole/Listu plati pro kazdy prvek.
    [CustomPropertyDrawer(typeof(TypePickerAttribute))]
    public class TypePickerDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.LabelField(position, label.text, "[TypePicker] vyzaduje [SerializeReference]");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // tlacitko PRED PropertyField - IMGUI da klik prvnimu controlu, jinak by ho sebral foldout
            float labelWidth = EditorGUIUtility.labelWidth + 2;
            var buttonRect = new Rect(position.x + labelWidth, position.y, position.width - labelWidth, EditorGUIUtility.singleLineHeight);
            var current = property.managedReferenceValue?.GetType();
            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(current?.Name ?? "(None)"), FocusType.Keyboard))
                ShowMenu(property.serializedObject, property.propertyPath, current);

            EditorGUI.PropertyField(position, property, label, true);
            EditorGUI.EndProperty();
        }

        private void ShowMenu(SerializedObject serializedObject, string propertyPath, Type current)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("(None)"), current == null, () => Assign(serializedObject, propertyPath, null));
            foreach (var type in GetCandidateTypes(ElementType(fieldInfo.FieldType)))
                menu.AddItem(new GUIContent(type.Name), type == current, () => Assign(serializedObject, propertyPath, type));
            menu.ShowAsContext();
        }

        // SerializedProperty se mezi framy recykluje - do callbacku posilame cestu, ne property.
        private static void Assign(SerializedObject serializedObject, string propertyPath, Type type)
        {
            serializedObject.Update();
            serializedObject.FindProperty(propertyPath).managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            serializedObject.ApplyModifiedProperties();
        }

        private static Type ElementType(Type fieldType)
        {
            if (fieldType.IsArray)
                return fieldType.GetElementType();
            if (fieldType.IsGenericType)
                return fieldType.GetGenericArguments()[0];
            return fieldType;
        }

        private static Type[] GetCandidateTypes(Type baseType) => TypeCache.GetTypesDerivedFrom(baseType)
            .Where(t => !t.IsAbstract && !t.IsGenericType
                && !typeof(UnityEngine.Object).IsAssignableFrom(t)
                && t.IsDefined(typeof(SerializableAttribute), false)
                && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.Name)
            .ToArray();
    }
}
#endif
