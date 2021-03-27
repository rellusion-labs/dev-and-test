using SuperNet.Netcode.Transport;
using SuperNet.Unity.Core;
using SuperNet.Netcode.Util;
using UnityEngine;
using UnityEngine.Serialization;

namespace SuperNet.Unity.Components {

	/// <summary>
	/// Spawnable prefab with network components.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("SuperNet/NetworkPrefab")]
	[HelpURL("https://superversus.com/netcode/api/SuperNet.Unity.Components.NetworkPrefab.html")]
	public sealed class NetworkPrefab : NetworkComponent {

		/// <summary>
		/// Spawner responsible for this prefab.
		/// </summary>
		public NetworkSpawner NetworkSpawner => Spawner;

		/// <summary>
		/// Spawner responsible for this prefab.
		/// </summary>
		[SerializeField]
		[FormerlySerializedAs("NetworkSpawner")]
		[Tooltip("Spawner responsible for this prefab.")]
		private NetworkSpawner Spawner;

		// Resources
		private NetworkComponent[] Components = null;
		private bool Registered = false;

		private void Reset() {
			ResetNetworkIdentity();
			Spawner = GetComponentInParent<NetworkSpawner>();
		}

		private void Awake() {

			// Get network components
			NetworkComponent[] componentsAll = GetComponentsInChildren<NetworkComponent>(true);
			NetworkComponent[] componentsNew = new NetworkComponent[componentsAll.Length - 1];
			for (int i = 0, j = 0; i < componentsAll.Length; i++) {
				if (componentsAll[i] != this) {
					componentsNew[j++] = componentsAll[i];
				}
			}

			// Save network components
			Components = componentsNew;

		}

		protected override void Start() {

			// Spawn prefab on the spawner
			NetworkSpawner spawner = Spawner;
			if (spawner != null) {
				Spawner.Spawn(this);
			}

		}

		protected override void OnDestroy() {

			// Despawn prefab from the spawner
			NetworkSpawner spawner = Spawner;
			if (spawner != null) {
				spawner.Despawn(this);
			}

			// Unregister prefab
			NetworkManager.Unregister(this);

		}

		internal void OnSpawnLocal(NetworkSpawner spawner) {

			// Save spawner
			Spawner = spawner;

			// Register prefab
			NetworkManager.Register(this);

			// Register components
			foreach (NetworkComponent component in Components) {
				NetworkManager.Register(component, NetworkIdentity.VALUE_INVALID);
			}

			// Mark components as registered
			Registered = true;

			// Create message
			NetworkPrefabMessage message = new NetworkPrefabMessage(this);

			// Send components identities to all peers
			SendNetworkMessageAll(message);

		}

		internal void OnSpawnRemote(NetworkSpawner spawner, NetworkIdentity identity) {

			// Save spawner
			Spawner = spawner;

			// Register prefab
			NetworkManager.Register(this, identity);

		}

		public override void OnNetworkPeerRegister(Peer peer) {

			// If components are not registered, do nothing
			if (!Registered) {
				return;
			}

			// If local connection, ignore
			if (Host.IsLocal(peer.Remote)) {
				return;
			}

			// Create message
			NetworkPrefabMessage message = new NetworkPrefabMessage(this);

			// Send component identities to the new peer
			SendNetworkMessage(peer, message);

		}

		public override void OnNetworkMessage(Peer peer, Reader reader, HostTimestamp timestamp) {

			// If components already registered, do nothing
			if (Registered) {
				return;
			}

			// Read identities and register components
			foreach (NetworkComponent component in Components) {
				NetworkIdentity identity = reader.ReadUint32();
				NetworkManager.Register(component, identity);
			}

			// Mark components as registered
			Registered = true;

		}

		private struct NetworkPrefabMessage : INetworkMessage {
			
			public bool Timed => false;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly NetworkPrefab Prefab;

			public NetworkPrefabMessage(NetworkPrefab prefab) {
				Prefab = prefab;
			}

			public void Write(Writer writer) {
				foreach (NetworkComponent component in Prefab.Components) {
					writer.Write(component.NetworkIdentity.Value);
				}
			}

		}

	}

}
