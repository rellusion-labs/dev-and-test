using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SuperNet.Unity.Core {

	/// <summary>
	/// Base class for synchronized network components.
	/// </summary>
	[AddComponentMenu("SuperNet/NetworkComponent")]
	[HelpURL("https://superversus.com/netcode/api/SuperNet.Unity.Core.NetworkComponent.html")]
	public abstract class NetworkComponent : MonoBehaviour {

		/// <summary>
		/// Network ID used to identify same components across the network.
		/// </summary>
		public NetworkIdentity NetworkIdentity {
			get {
				NetworkIdentity registered = Identity;
				if (registered.IsInvalid) {
					return NetworkID;
				} else {
					return registered;
				}
			}
		}

		/// <summary>
		/// True if this component is registered on the network.
		/// </summary>
		public bool NetworkIsRegistered => !Identity.IsInvalid;

		/// <summary>
		/// Network ID used to identify same components across the network.
		/// </summary>
		[SerializeField]
		[FormerlySerializedAs("NetworkIdentity")]
		[Tooltip("Network ID used to identify same components across the network.")]
		private NetworkIdentity NetworkID;

		/// <summary>
		/// Registered network ID.
		/// </summary>
		[NonSerialized]
		private NetworkIdentity Identity = NetworkIdentity.VALUE_INVALID;

		/// <summary>
		/// Automatically registers the component if it is static. Do not override.
		/// </summary>
		protected virtual void Start() {
			if (NetworkID.IsStatic) {
				NetworkManager.Register(this);
			}
		}

		protected void ResetNetworkIdentity() {
		#if UNITY_EDITOR

			// Get prefab information
			var stage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
			bool isPrefabPart = UnityEditor.PrefabUtility.IsPartOfAnyPrefab(this);
			bool isPrefabAsset = UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this);
			bool isPrefabInstance = UnityEditor.PrefabUtility.IsPartOfNonAssetPrefabInstance(this);

			// Don't change anything on prefab instances
			if (isPrefabPart && !isPrefabAsset && isPrefabInstance) {
				return;
			}

			// Set to invalid value on prefab assets
			if ((stage != null && stage.scene == gameObject.scene) || (isPrefabPart && isPrefabAsset && !isPrefabInstance)) {
				NetworkID = NetworkIdentity.VALUE_INVALID;
				return;
			}

			// All others, set to instance ID
			uint ID = (uint)GetInstanceID();
			uint range = NetworkIdentity.VALUE_MAX_STATIC - NetworkIdentity.VALUE_MIN_STATIC;
			NetworkID = NetworkIdentity.VALUE_MIN_STATIC + (ID % range);

		#endif
		}

		/// <summary>
		/// Automatically unregisters the component. Do not override.
		/// </summary>
		protected virtual void OnDestroy() {
			NetworkManager.Unregister(this);
		}

		/// <summary>
		/// Called when the component is registered on the network.
		/// </summary>
		/// <param name="identity">Identity assigned to the component.</param>
		internal void OnComponentRegister(NetworkIdentity identity) {

			// Update identity
			Identity = identity;

			// Notify component
			OnNetworkRegister();

		}

		/// <summary>
		/// Called when the component is unregistered from the network.
		/// </summary>
		internal void OnComponentUnregister() {

			// Update identity
			Identity = NetworkIdentity.VALUE_INVALID;

			// Notify component
			OnNetworkUnregister();

		}

		/// <summary>
		/// Send a component message to all peers with the component.
		/// </summary>
		/// <param name="message">Message to send.</param>
		/// <param name="exclude">Peer to exclude.</param>
		public void SendNetworkMessageAll(INetworkMessage message, Peer exclude = null) {
			NetworkManager.GetInstance().SendComponentMessageAll(Identity, message, exclude);
		}

		/// <summary>
		/// Send a component message to a specific peer.
		/// </summary>
		/// <param name="peer">Peer to send to.</param>
		/// <param name="message">Message to send.</param>
		/// <param name="listener">Message listener to use or null if not used.</param>
		/// <returns>Sent message handle.</returns>
		public MessageSent SendNetworkMessage(Peer peer, INetworkMessage message, IMessageListener listener = null) {
			return NetworkManager.GetInstance().SendComponentMessage(Identity, peer, message, listener);
		}

		/// <summary>
		/// Queue action to be ran on the main unity thread.
		/// </summary>
		/// <param name="action">Action to run.</param>
		public void Run(Action action) {
			NetworkManager.GetInstance().RunEnqueue(action);
		}

		/// <summary>
		/// Queue action to be ran on the main unity thread after a delay.
		/// </summary>
		/// <param name="action">Action to run.</param>
		/// <param name="seconds">Delay in seconds.</param>
		public void Run(Action action, float seconds) {
			NetworkManager.GetInstance().RunEnqueue(action, seconds);
		}

		/// <summary>
		/// Called when the component is registered on the network.
		/// </summary>
		public virtual void OnNetworkRegister() { }

		/// <summary>
		/// Called when the component is unregistered from the network.
		/// </summary>
		public virtual void OnNetworkUnregister() { }

		/// <summary>
		/// Called when the component is registered on a remote peer.
		/// This can be called multiple times by the same peer.
		/// </summary>
		/// <param name="peer">Remote peer.</param>
		public virtual void OnNetworkPeerRegister(Peer peer) { }

		/// <summary>
		/// Called when the component is unregistered on a remote peer.
		/// </summary>
		/// <param name="peer">Remote peer.</param>
		public virtual void OnNetworkPeerUnregister(Peer peer) { }

		/// <summary>
		/// Called when a remote peer joins the network.
		/// </summary>
		/// <param name="peer">Remote peer.</param>
		public virtual void OnNetworkPeerConnect(Peer peer) { }

		/// <summary>
		/// Called when a remote peer leaves the network.
		/// </summary>
		/// <param name="peer">Remote peer.</param>
		public virtual void OnNetworkPeerDisconnect(Peer peer) { }

		/// <summary>
		/// Called when a remote peer sends a message to this component.
		/// </summary>
		/// <param name="peer">Peer that sent the message.</param>
		/// <param name="reader">Reader containing the message.</param>
		/// <param name="timestamp">Timestamp of when the message was created.</param>
		public virtual void OnNetworkMessage(Peer peer, Reader reader, HostTimestamp timestamp) { }

	}

}
