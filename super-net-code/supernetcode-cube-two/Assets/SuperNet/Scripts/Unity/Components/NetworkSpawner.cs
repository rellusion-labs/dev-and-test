using SuperNet.Netcode.Transport;
using SuperNet.Unity.Core;
using SuperNet.Netcode.Util;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

namespace SuperNet.Unity.Components {

	/// <summary>
	/// Manages spawnable network prefabs.
	/// </summary>
	[AddComponentMenu("SuperNet/NetworkSpawner")]
	[HelpURL("https://superversus.com/netcode/api/SuperNet.Unity.Components.NetworkSpawner.html")]
	public sealed class NetworkSpawner : NetworkComponent {

		/// <summary>
		/// The prefab template to spawn.
		/// </summary>
		[FormerlySerializedAs("Prefab")]
		[Tooltip("The prefab template to spawn.")]
		public NetworkPrefab Prefab;

		/// <summary>
		/// Ignore spawn messages from remote peers.
		/// </summary>
		[FormerlySerializedAs("IgnoreRemoteSpawns")]
		[Tooltip("Ignore spawn messages from remote peers.")]
		public bool IgnoreRemoteSpawns;

		/// <summary>
		/// Ignore despawn messages from remote peers.
		/// </summary>
		[FormerlySerializedAs("IgnoreRemoteDespawns")]
		[Tooltip("Ignore despawn messages from remote peers.")]
		public bool IgnoreRemoteDespawns;

		// Resources
		private readonly ReaderWriterLockSlim Lock;
		private readonly Dictionary<NetworkIdentity, NetworkPrefab> Instances;

		private enum Header : byte {
			Spawn = 1,
			Despawn = 2,
		}

		public NetworkSpawner() {
			Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			Instances = new Dictionary<NetworkIdentity, NetworkPrefab>();
		}

		private void Reset() {
			ResetNetworkIdentity();
			Prefab = null;
		}

		/// <summary>
		/// Get all spawned instances as an array.
		/// </summary>
		/// <returns>All spawned instances.</returns>
		public NetworkPrefab[] GetSpawnedPrefabs() {
			try {
				Lock.EnterReadLock();
				NetworkPrefab[] instances = new NetworkPrefab[Instances.Count];
				Instances.Values.CopyTo(instances, 0);
				return instances;
			} finally {
				Lock.ExitReadLock();
			}
		}

		/// <summary>
		/// Spawn a new instance on the network.
		/// </summary>
		/// <returns>The spawned instance.</returns>
		public NetworkPrefab Spawn() {
			NetworkPrefab prefab = Instantiate(Prefab, transform);
			Spawn(prefab);
			return prefab;
		}

		/// <summary>
		/// Spawn a new instance on the network.
		/// </summary>
		/// <param name="position">Position for the new object.</param>
		/// <param name="rotation">Orientation of the new object.</param>
		/// <returns>The spawned instance.</returns>
		public NetworkPrefab Spawn(Vector3 position, Quaternion rotation) {
			NetworkPrefab prefab = Instantiate(Prefab, position, rotation, transform);
			Spawn(prefab);
			return prefab;
		}

		/// <summary>
		/// Spawn an already instantiated instance on the network.
		/// </summary>
		/// <param name="instance">Instance to spawn.</param>
		public void Spawn(NetworkPrefab instance) {
			try {
				Lock.EnterWriteLock();

				// Get identity
				NetworkIdentity identity = instance.NetworkIdentity;

				// Check existing instance with the same identity
				Instances.TryGetValue(identity, out NetworkPrefab found);
				if (found == instance) {
					return;
				}

				// Initialize and save the new identity
				instance.OnSpawnLocal(this);
				identity = instance.NetworkIdentity;

				// Save the instance
				Instances.Add(identity, instance);

				// Create message
				NetworkSpawnerMessage message = new NetworkSpawnerMessage(Header.Spawn, identity);

				// Send spawn message to all peers
				SendNetworkMessageAll(message);

			} finally {
				Lock.ExitWriteLock();
			}
		}

		/// <summary>
		/// Despawn an instantiated instance and destroy it.
		/// </summary>
		/// <param name="instance">Instance to despawn.</param>
		public void Despawn(NetworkPrefab instance) {

			try {
				Lock.EnterWriteLock();

				// Get instance identity
				NetworkIdentity identity = instance.NetworkIdentity;

				// Make sure instance is actually spawned
				Instances.TryGetValue(identity, out NetworkPrefab found);
				if (found != instance) {
					return;
				}

				// Remove the instance
				Instances.Remove(identity);

				// Create message
				NetworkSpawnerMessage message = new NetworkSpawnerMessage(Header.Despawn, identity);

				// Send despawn message to all peers
				SendNetworkMessageAll(message);

			} finally {
				Lock.ExitWriteLock();
			}

			// Destroy the instance
			Run(() => Destroy(instance));
		}

		public override void OnNetworkRegister() {
			try {
				Lock.EnterReadLock();
				foreach (NetworkPrefab instance in Instances.Values) {

					// Create message
					NetworkIdentity identity = instance.NetworkIdentity;
					NetworkSpawnerMessage message = new NetworkSpawnerMessage(Header.Spawn, identity);

					// Send message
					SendNetworkMessageAll(message);

				}
			} finally {
				Lock.ExitReadLock();
			}
		}

		public override void OnNetworkPeerRegister(Peer peer) {
			
			// If local connection, ignore
			if (Host.IsLocal(peer.Remote)) {
				return;
			}

			try {
				Lock.EnterReadLock();
				foreach (NetworkPrefab instance in Instances.Values) {

					// Create message
					NetworkIdentity identity = instance.NetworkIdentity;
					NetworkSpawnerMessage message = new NetworkSpawnerMessage(Header.Spawn, identity);

					// Send message
					SendNetworkMessage(peer, message);

				}
			} finally {
				Lock.ExitReadLock();
			}

		}

		public override void OnNetworkMessage(Peer peer, Reader reader, HostTimestamp timestamp) {

			// Read message
			Header header = reader.ReadEnum<Header>();
			NetworkIdentity identity = reader.ReadUint32();
			
			// Make sure received identity is valid
			if (identity.IsInvalid) {
				Debug.LogWarning(string.Format(
					"[SuperNet] [NetworkSpawner] Received invalid identity {0} from '{1}' on spawner {2}. Ignoring.",
					identity, peer.Remote, NetworkIdentity
				), this);
				return;
			}

			// Process message
			switch (header) {
				case Header.Spawn:
					if (IgnoreRemoteSpawns) {
						Debug.LogWarning(string.Format(
							"[SuperNet] [NetworkSpawner] Received identity {0} spawn from '{1}' on spawner {2}. Ignoring.",
							identity, peer.Remote, NetworkIdentity
						), this);
					} else {
						OnNetworkMessageSpawn(identity);
					}
					break;
				case Header.Despawn:
					if (IgnoreRemoteDespawns) {
						Debug.LogWarning(string.Format(
							"[SuperNet] [NetworkSpawner] Received identity {0} despawn from '{1}' on spawner {2}. Ignoring.",
							identity, peer.Remote, NetworkIdentity
						), this);
					} else {
						OnNetworkMessageDespawn(identity);
					}
					break;
				default:
					Debug.LogWarning(string.Format(
						"[SuperNet] [NetworkSpawner] Received invalid header {0} with identity {1} from '{2}' on spawner {3}. Ignoring.",
						header, identity, peer.Remote, NetworkIdentity
					), this);
					break;
			}

		}

		private void OnNetworkMessageDespawn(NetworkIdentity identity) {

			// Find and remove instance
			NetworkPrefab instance = null;
			try {
				Lock.EnterWriteLock();
				Instances.TryGetValue(identity, out instance);
				if (instance != null) {
					Instances.Remove(identity); 
				} else {
					return;
				}
			} finally {
				Lock.ExitWriteLock();
			}

			// Destroy instance
			Run(() => Destroy(instance.gameObject));

		}

		private void OnNetworkMessageSpawn(NetworkIdentity identity) {
			Run(() => {
				try {
					Lock.EnterWriteLock();

					// Check if instance already exists
					Instances.TryGetValue(identity, out NetworkPrefab instance);
					if (instance != null) {
						return;
					}

					// Spawn and add
					instance = Instantiate(Prefab, transform);
					instance.OnSpawnRemote(this, identity);
					Instances.Add(identity, instance);

				} finally {
					Lock.ExitWriteLock();
				}
			});
		}

		private struct NetworkSpawnerMessage : INetworkMessage {
			
			public bool Timed => false;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly Header Request;
			public readonly NetworkIdentity Identity;
			
			public NetworkSpawnerMessage(Header request, NetworkIdentity identity) {
				Request = request;
				Identity = identity;
			}

			public void Write(Writer writer) {
				writer.WriteEnum(Request);
				writer.Write(Identity.Value);
			}

		}

	}

}
