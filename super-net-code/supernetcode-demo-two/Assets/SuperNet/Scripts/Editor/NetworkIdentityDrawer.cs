using SuperNet.Unity.Core;
using UnityEditor;
using UnityEngine;

namespace SuperNet.Unity.Editor {

	[CustomPropertyDrawer(typeof(NetworkIdentity))]
	public sealed class NetworkIdentityDrawer : PropertyDrawer {

		private const int HeightMargin = 1;
		private float HeightControl = 0f;
		private float HeightMessage = 0f;
		private string MessageText = null;
		private MessageType MessageType = MessageType.None;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			
			label = EditorGUI.BeginProperty(position, label, property);
			
			// Show control label
			Rect rectControl = new Rect(position.x, position.y, position.width, HeightControl);
			Rect rectPrefix = EditorGUI.PrefixLabel(rectControl, GUIUtility.GetControlID(FocusType.Passive), label);

			// Disable control input in play mode
			foreach (Object target in property.serializedObject.targetObjects) {
				if (Application.IsPlaying(target)) {
					GUI.enabled = false;
				}
			}

			// Show control input
			SerializedProperty value = property.FindPropertyRelative(nameof(NetworkIdentity.Value));
			if (value == null) {
				Debug.LogError("Value not found in property " + property.propertyPath);
			} else {
				EditorGUI.showMixedValue = value.hasMultipleDifferentValues;
				EditorGUI.PropertyField(rectPrefix, value, GUIContent.none);
			}

			// Show message
			if (MessageText != null) {
				float y = position.y + HeightControl + HeightMargin;
				Rect rectMessage = new Rect(position.x, y, position.width, HeightMessage);
				EditorGUI.HelpBox(rectMessage, MessageText, MessageType);
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {

			// Get base control height
			HeightControl = base.GetPropertyHeight(property, label);
			
			// Get message
			MessageText = GetMessage(property, out MessageType);

			// Calculate total height
			if (MessageText != null) {
				GUIStyle style = GUI.skin.GetStyle("helpbox");
				GUIContent content = new GUIContent(MessageText);
				float height = style.CalcHeight(content, EditorGUIUtility.currentViewWidth);
				HeightMessage = Mathf.Max(height + HeightMargin * 2, 40);
				return HeightControl + HeightMessage;
			} else {
				HeightMessage = 0f;
				return HeightControl;
			}

		}

		private static string GetMessage(SerializedProperty property, out MessageType type) {

			// Make sure only one object is selected
			if (property.serializedObject.isEditingMultipleObjects) {
				type = MessageType.Warning;
				return "Multiple components selected.";
			}

			// Get identity
			Object target = property.serializedObject.targetObject;
			SerializedProperty value = property.FindPropertyRelative(nameof(NetworkIdentity.Value));
			NetworkIdentity identity = (uint)value.longValue;
			NetworkComponent component = target is NetworkComponent ? target as NetworkComponent : null;

			// Make sure this is a network component
			if (component == null) {
				type = MessageType.Warning;
				return "NetworkID is not used on a network component.";
			}

			if (Application.IsPlaying(target)) {

				// If component is playing, it should be registered
				if (component.NetworkIsRegistered) {
					type = MessageType.Info;
					return "Registered NetworkID: " + component.NetworkIdentity;
				} else {
					type = MessageType.Warning;
					return "Not registered on the network.";
				}

			} else {

				if (IsPrefabInstance(component.gameObject)) {

					// Prefab instances can be either invalid or static
					if (identity.IsDynamic) {
						type = MessageType.Warning;
						return string.Format(
							"NetworkID on prefabs must be {0}.",
							NetworkIdentity.VALUE_INVALID
						);
					} else {
						type = MessageType.None;
						return null;
					}

				} else if (IsPrefabAsset(component.gameObject)) {

					// Prefab assets must have an invalid ID
					if (identity.IsInvalid) {
						type = MessageType.None;
						return null;
					} else {
						type = MessageType.Warning;
						return string.Format(
							"NetworkID on prefabs must be {0}.",
							NetworkIdentity.VALUE_INVALID
						);
					}

				} else {

					// Non prefabs must have a static ID
					if (identity.IsStatic) {
						type = MessageType.None;
						return null;
					} else {
						type = MessageType.Warning;
						return string.Format(
							"NetworkID must be between {0} and {1}.",
							NetworkIdentity.VALUE_MIN_STATIC, NetworkIdentity.VALUE_MAX_STATIC
						);
					}

				}

			}

		}

		private static bool IsPrefabAsset(GameObject obj) {
			var stage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
			bool isPrefabPart = PrefabUtility.IsPartOfAnyPrefab(obj);
			bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(obj);
			bool isPrefabInstance = PrefabUtility.IsPartOfNonAssetPrefabInstance(obj);
			return stage != null && stage.scene == obj.scene || isPrefabPart && isPrefabAsset && !isPrefabInstance;
		}

		private static bool IsPrefabInstance(GameObject obj) {
			bool isPrefabPart = PrefabUtility.IsPartOfAnyPrefab(obj);
			bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(obj);
			bool isPrefabInstance = PrefabUtility.IsPartOfNonAssetPrefabInstance(obj);
			return isPrefabPart && !isPrefabAsset && isPrefabInstance;
		}

	}

}
