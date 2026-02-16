using UnityEditor;
using UnityEngine;
using OutOfPhase.Dimension;

namespace OutOfPhase.Editor
{
    [CustomEditor(typeof(DimensionLockVolume))]
    public class DimensionLockVolumeEditor : UnityEditor.Editor
    {
        private SerializedProperty _lockSwitching;
        private SerializedProperty _forceDimension;
        private SerializedProperty _targetDimension;
        private SerializedProperty _lockedDimensions;
        private SerializedProperty _gizmoColor;

        private void OnEnable()
        {
            _lockSwitching = serializedObject.FindProperty("lockSwitching");
            _forceDimension = serializedObject.FindProperty("forceDimension");
            _targetDimension = serializedObject.FindProperty("targetDimension");
            _lockedDimensions = serializedObject.FindProperty("lockedDimensions");
            _gizmoColor = serializedObject.FindProperty("gizmoColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_lockSwitching);
            EditorGUILayout.PropertyField(_forceDimension);
            if (_forceDimension.boolValue)
            {
                EditorGUILayout.PropertyField(_targetDimension);
            }

            DrawLockedDimensions();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_gizmoColor);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLockedDimensions()
        {
            if (_lockedDimensions == null)
                return;

            string[] names = GetDimensionNames(_lockedDimensions.arraySize);

            EditorGUILayout.LabelField("Locked Dimensions", EditorStyles.boldLabel);

            for (int i = 0; i < _lockedDimensions.arraySize; i++)
            {
                var element = _lockedDimensions.GetArrayElementAtIndex(i);
                string label = i < names.Length ? names[i] : $"Dimension {i + 1}";
                element.boolValue = EditorGUILayout.ToggleLeft(label, element.boolValue);
            }
        }

        private string[] GetDimensionNames(int count)
        {
            var manager = FindFirstObjectByType<DimensionManager>();
            if (manager == null || count <= 0)
                return new string[0];

            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = manager.GetDimensionName(i);
            }
            return names;
        }
    }
}
