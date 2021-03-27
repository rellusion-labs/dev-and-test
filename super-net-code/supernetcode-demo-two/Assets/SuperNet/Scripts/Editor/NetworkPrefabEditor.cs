using SuperNet.Unity.Components;
using UnityEditor;
using UnityEngine;

namespace SuperNet.Unity.Editor {

	[CanEditMultipleObjects]
	[CustomEditor(typeof(NetworkPrefab))]
	public sealed class NetworkPrefabEditor : UnityEditor.Editor {

		public override void OnInspectorGUI() {

			EditorGUI.BeginDisabledGroup(Application.isPlaying);
			base.OnInspectorGUI();
			EditorGUI.EndDisabledGroup();

			bool warningNoSpawner = false;
			bool isPlaying = false;

			foreach (Object target in serializedObject.targetObjects) {

				NetworkPrefab prefab = (NetworkPrefab)target;
				if (prefab == null) {
					continue;
				}

				bool isPrefabPart = PrefabUtility.IsPartOfAnyPrefab(target);
				bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(target);
				bool isPrefabInstance = PrefabUtility.IsPartOfNonAssetPrefabInstance(target);
				
				if (isPrefabPart && !isPrefabAsset && isPrefabInstance && prefab.NetworkSpawner == null) {
					warningNoSpawner = true;
				}

				if (Application.IsPlaying(target)) {
					isPlaying = true;
				}

			}

			if (warningNoSpawner) {
				EditorGUILayout.Space();
				if (serializedObject.isEditingMultipleObjects) {
					EditorGUILayout.HelpBox("Spawner not set on one of the prefabs.", MessageType.Warning);
				} else {
					EditorGUILayout.HelpBox("Spawner not set.", MessageType.Warning);
				}
				EditorGUILayout.Space();
			}

			if (isPlaying && GUILayout.Button("Despawn")) {
				foreach (Object target in serializedObject.targetObjects) {
					NetworkPrefab instance = (NetworkPrefab)target;
					if (instance != null && Application.IsPlaying(target)) {
						Destroy(instance.gameObject);
					}
				}
			}

		}

	}

}
