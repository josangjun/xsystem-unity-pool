#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XSystem
{
    [CustomEditor(typeof(GameObjectPoolDebugView))]
    internal sealed class GameObjectPoolDebugViewEditor : Editor
    {
        private readonly List<PrefabInstancePool> _pools = new List<PrefabInstancePool>();
        private SerializedProperty _sortByIdleCountDescendingProperty;

        private void OnEnable()
        {
            _sortByIdleCountDescendingProperty = serializedObject.FindProperty("_sortByIdleCountDescending");
            EditorApplication.update += RepaintInspector;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintInspector;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _pools.Clear();
            foreach (PrefabInstancePool pool in PrefabInstancePool.ActivePools)
            {
                if (pool != null)
                    _pools.Add(pool);
            }

            int totalActiveCount = 0;
            int totalIdleCount = 0;
            for (int i = 0; i < _pools.Count; i++)
            {
                totalActiveCount += _pools[i].ActiveCount;
                totalIdleCount += _pools[i].IdleCount;
            }

            if (_sortByIdleCountDescendingProperty != null)
            {
                EditorGUILayout.PropertyField(
                    _sortByIdleCountDescendingProperty,
                    new GUIContent("Sort by Idle Count (Descending)"));

                if (_sortByIdleCountDescendingProperty.boolValue)
                    _pools.Sort(CompareByIdleCountDescending);
            }
            int totalCount = totalActiveCount + totalIdleCount;
            EditorGUILayout.LabelField("Prefab Instance Pools", _pools.Count.ToString());
            EditorGUILayout.LabelField("Total Count (Active / Idle)", $"{totalCount} ({totalActiveCount} / {totalIdleCount})");
            EditorGUILayout.LabelField("Status", "Live Editor view");

            serializedObject.ApplyModifiedProperties();

            if (_pools.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No prefab instance pool has been initialized in this Editor session.",
                    MessageType.Info);
                return;
            }

            for (int i = 0; i < _pools.Count; i++)
                DrawPool(_pools[i], i + 1);
        }

        private void RepaintInspector()
        {
            if (target == null)
            {
                EditorApplication.update -= RepaintInspector;
                return;
            }

            Repaint();
        }

        private static int CompareByIdleCountDescending(PrefabInstancePool left, PrefabInstancePool right)
        {
            int idleCountComparison = right.IdleCount.CompareTo(left.IdleCount);
            if (idleCountComparison != 0)
                return idleCountComparison;

            string leftPrefabName = left.Prefab != null ? left.Prefab.name : string.Empty;
            string rightPrefabName = right.Prefab != null ? right.Prefab.name : string.Empty;
            return string.CompareOrdinal(leftPrefabName, rightPrefabName);
        }

        private static void DrawPool(PrefabInstancePool pool, int index)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string prefabName = pool.Prefab != null ? pool.Prefab.name : "(Missing Prefab)";
                EditorGUILayout.LabelField($"Pool {index}: {prefabName}", EditorStyles.boldLabel);
                EditorGUILayout.ObjectField("Prefab", pool.Prefab, typeof(GameObject), false);
                EditorGUILayout.LabelField("Size", pool.Size.ToString());
                EditorGUILayout.LabelField("Active", pool.ActiveCount.ToString());
                EditorGUILayout.LabelField("Idle", $"{pool.IdleCount} / {pool.MaxSize}");
                EditorGUILayout.LabelField("Default Capacity", pool.ConfiguredDefaultCapacity.ToString());

                EditorGUILayout.LabelField("Idle Objects", pool.IdleObjectCount.ToString());
                foreach (GameObject idleObject in pool.IdleObjects)
                {
                    if (idleObject != null)
                        EditorGUILayout.ObjectField(idleObject, typeof(GameObject), true);
                }
            }
        }
    }
}
#endif
