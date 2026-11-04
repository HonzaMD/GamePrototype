#if UNITY_EDITOR
using Assets.Scripts.Bases;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.EditorExtensions
{
    // EnergyCost na jeden radek: Jump   Cost [3]   Min [5]
    [CustomPropertyDrawer(typeof(EnergyCost))]
    public class EnergyCostDrawer : PropertyDrawer
    {
        private const float ValueLabelWidth = 32;
        private const float Gap = 4;

        private static readonly GUIContent CostLabel = new("Cost", "Kolik energie akce spotrebuje");
        private static readonly GUIContent MinLabel = new("Min", "Pod touto hladinou energie se akce odmitne");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var values = EditorGUI.PrefixLabel(position, label);

            int indent = EditorGUI.indentLevel;
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.labelWidth = ValueLabelWidth;

            var left = new Rect(values.x, values.y, (values.width - Gap) * 0.5f, values.height);
            var right = new Rect(left.xMax + Gap, values.y, left.width, values.height);
            EditorGUI.PropertyField(left, property.FindPropertyRelative(nameof(EnergyCost.Cost)), CostLabel);
            EditorGUI.PropertyField(right, property.FindPropertyRelative(nameof(EnergyCost.MinEnergy)), MinLabel);

            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
#endif
