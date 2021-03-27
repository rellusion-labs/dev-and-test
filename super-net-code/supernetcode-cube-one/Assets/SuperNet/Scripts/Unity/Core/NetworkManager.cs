using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SuperNet.Unity.Core {

	/// <summary>
	/// Manager for network hosts and components.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("SuperNet/NetworkManager")]
	[HelpURL("https://superversus.com/netcode/api/SuperNet.Unity.Core.NetworkManager.html")]
	public sealed class NetworkManager : MonoBehaviour {

		/// <summary>
		/// Queue action to be ran on the main unity thread.
		/// </summary>
		/// <param name="action">Action to run.</param>
		public static void Run(Action action) {
			if (action == null) {
				throw new ArgumentNullException(nameof(action), "No action provided");
			} else {
				GetInstance().RunEnqueue(action);
			}
		}

		/// <summary>
		/// Queue action to be ran on the main unity thread after a delay.
		/// </summary>
		/// <param name="action">Action to run.</param>
		/// <param name="seconds">Delay in seconds.</param>
		public static void Run(Action action, float seconds) {
			if (action == null) {
				throw new ArgumentNullException(nameof(action), "No action provided");
			} else if (seconds < 0) {
				throw new ArgumentOutOfRangeException(nameof(seconds), "Delay is negative");
			} else {
				GetInstance().RunEnqueue(action, seconds);
			}
		}

		/// <summary>
		/// Register a component on the network and notify all peers.
		/// This makes the component able to receive and send messages.
		/// </summary>
		/// <param name="component">Component to register.</param>
		/// <param name="identity">Identity to assign.</param>
		public static void Register(NetworkComponent component, NetworkIdentity identity) {
			if (component == null) {
				throw new ArgumentNullException(nameof(component), "No component provided");
			} else {
				GetInstance().RegisterComponent(component, identity);
			}
		}

		/// <summary>
		/// Register a component on the network and notify all peers.
		/// This makes the component able to receive and send messages.
		/// Generates a random identity if needed.
		/// </summary>
		/// <param name="component">Component to register.</param>
		public static void Register(NetworkComponent component) {
			if (component == null) {
				throw new ArgumentNullException(nameof(component), "No component provided");
			} else {
				GetInstance().RegisterComponent(component, component.NetworkIdentity);
			}
		}

		/// <summary>
		/// Unregister a registered component from the network and notify all peers.
		/// This makes the component top being able to receive and send messages.
		/// </summary>
		/// <param name="component">Component to unregister.</param>
		public static void Unregister(NetworkComponent component) {
			if (component == null) {
				throw new ArgumentNullException(nameof(component), "No component provided");
			} else {
				GetInstance().UnregisterComponent(component);
			}
		}

		/// <summary>
		/// Check if a component is registered on the network.
		/// </summary>
		/// <param name="component">Component to check.</param>
		/// <returns>True if registered, false if not.</returns>
		public static bool IsRegistered(NetworkComponent component) {
			if (component == null) {
				return false;
			} else {
				return GetInstance().IsComponentRegistered(component);
			}
		}

		/// <summary>
		/// Find a registered component from an identity.
		/// </summary>
		/// <param name="identity">Identity to check.</param>
		/// <returns>Component if found or null if not.</returns>
		public static NetworkComponent GetNetworkComponent(NetworkIdentity identity) {
			return GetInstance().FindNetworkComponent(identity);
		}

		/// <summary>
		/// Return number of peers with this identity registered.
		/// </summary>
		/// <param name="identity">Identity to check.</param>
		/// <returns>Number of peers.</returns>
		public static int GetPeerCount(NetworkIdentity identity) {
			return GetInstance().GetComponentPeerCount(identity);
		}

		/// <summary>
		/// Return all peers with this identity registered.
		/// </summary>
		/// <param name="identity">Identity to check.</param>
		/// <returns>Array of all peers.</returns>
		public static Peer[] GetPeers(NetworkIdentity identity) {
			return GetInstance().GetComponentPeers(identity);
		}

		// Singleton instance
		private static bool Initialized = false;
		private static NetworkManager Instance = null;
		internal static NetworkManager GetInstance() {
			if (Initialized) return Instance;
			NetworkManager manager = FindObjectOfType<NetworkManager>();
			NetworkHost host = FindObjectOfType<NetworkHost>();
			if (manager != null) {
				Instance = manager;
			} else if (host != null && host.PersistAcrossScenes) {
				Instance = host.gameObject.AddComponent<NetworkManager>();
			} else {
				Instance = new GameObject(nameof(NetworkManager)).AddComponent<NetworkManager>();
			}
			Initialized = true;
			return Instance;
		}

		// Resources
		private readonly ReaderWriterLockSlim Lock;
		private readonly ArrayPool<NetworkIdentity> PoolIdentity;
		private readonly ArrayPool<NetworkComponent> PoolComponent;
		private readonly Dictionary<NetworkIdentity, Tracker> Trackers;
		private readonly ConcurrentQueue<Action> UpdateQueue;
		private readonly HashSet<NetworkHost> Hosts;
		private readonly System.Random Random;
		private Thread UpdateThread;
		private bool Destroyed;

		public NetworkManager() {
			Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			PoolIdentity = new ArrayPool<NetworkIdentity>(1, int.MaxValue);
			PoolComponent = new ArrayPool<NetworkComponent>(2, int.MaxValue);
			Trackers = new Dictionary<NetworkIdentity, Tracker>();
			UpdateQueue = new ConcurrentQueue<Action>();
			Hosts = new HashSet<NetworkHost>();
			Random = new System.Random();
			UpdateThread = Thread.CurrentThread;
			Destroyed = false;
		}

		private void Awake() {

			// Make sure the network manager isn't destroyed when the scene changes
			transform.parent = null;
			DontDestroyOnLoad(this);

			// Save the current thread for update actions
			UpdateThread = Thread.CurrentThread;

			// Set the singleton instance
			if (Initialized) {
				if (Instance != this) {
					Debug.LogWarning("[SuperNet] [NetworkManager] Another manager found. Disabling.");
					enabled = false;
				}
			} else {
				Instance = this;
				Initialized = true;
			}

		}

		private void Update() {
			while (UpdateQueue.TryDequeue(out Action action)) {
				try {
					action.Invoke();
				} catch (Exception exception) {
					Debug.LogException(exception);
				}
			}
		}

		private void OnDestroy() {
			Destroyed = true;
		}

		internal void RunEnqueue(Action action) {
			if (Thread.CurrentThread == UpdateThread) {
				action.Invoke();
			} else if (!Destroyed) {
				UpdateQueue.Enqueue(action);
			}
		}

		internal void RunEnqueue(Action action, float seconds) {
			if (Thread.CurrentThread == UpdateThread) {
				StartCoroutine(RunCoroutine(action, seconds));
			} else if (!Destroyed) {
				UpdateQueue.Enqueue(() => StartCoroutine(RunCoroutine(action, seconds)));
			}
		}

		private IEnumerator RunCoroutine(Action action, float seconds) {
			yield return new WaitForSeconds(seconds);
			try {
				action.Invoke();
			} catch (Exception exception) {
				Debug.LogException(exception);
			}
		}

		internal void OnHostStartup(NetworkHost host) {

			// Add the host
			try {
				Lock.EnterWriteLock();
				if (Hosts.Add(host)) return;
			} finally {
				Lock.ExitWriteLock();
			}

			// Warn if host was already added
			Debug.LogWarning(string.Format(
				"[SuperNet] [NetworkManager] Host listening on {0} started up again.",
				host.GetBindAddress()
			));

		}

		internal void OnHostShutdown(NetworkHost host) {

			// Remove the host
			try {
				Lock.EnterWriteLock();
				if (Hosts.Remove(host)) return;
			} finally {
				Lock.ExitWriteLock();
			}

			// Warn if the host didn't exist
			Debug.LogWarning(string.Format(
				"[SuperNet] [NetworkManager] Host listening on {0} was shutdown again.",
				host.GetBindAddress()
			));

		}

		internal void OnPeerConnect(Peer peer) {

			// Temporary array
			NetworkComponent[] components;
			int count = 0;

			try {
				Lock.EnterWriteLock();

				// Rent temporary array
				components = PoolComponent.Rent(Trackers.Count);

				// Send all tracked identities and save all components to the array
				if (!Host.IsLocal(peer.Remote)) {
					foreach (Tracker tracker in Trackers.Values) {
						if (tracker.Component != null) {
							components[count++] = tracker.Component;
							peer.Send(new MessageComponentRegister(tracker.Identity));
						}
					}
				}
				
			} finally {
				Lock.ExitWriteLock();
			}

			// Notify all components outside the lock
			for (int i = 0; i < count; i++) {
				components[i].OnNetworkPeerConnect(peer);
			}

			// Return components array
			PoolComponent.Return(components);

		}

		internal void OnPeerDisconnect(Peer peer) {

			// Temporary arrays
			NetworkIdentity[] identityArray;
			NetworkComponent[] componentAllArray;
			NetworkComponent[] componentRemovedArray;
			int componentRemovedLength = 0;
			int componentAllLength = 0;
			int identityLength = 0;

			try {
				Lock.EnterWriteLock();

				// Rent temporary arrays
				identityArray = PoolIdentity.Rent(Trackers.Count);
				componentAllArray = PoolComponent.Rent(Trackers.Count);
				componentRemovedArray = PoolComponent.Rent(Trackers.Count);

				foreach (Tracker tracker in Trackers.Values) {

					// Save component to the temporary array
					if (tracker.Component != null) {
						componentAllArray[componentAllLength++] = tracker.Component;
					}

					// Remove peer from the tracker and save component if removed
					if (tracker.Peers.Remove(peer) && tracker.Component != null) {
						componentRemovedArray[componentRemovedLength++] = tracker.Component;
					}

					// Check if tracker is empty (no peers and no component)
					if (tracker.Peers.Count <= 0 && tracker.Component == null) {
						identityArray[identityLength++] = tracker.Identity;
					}

				}

				// Remove all empty trackers and notify all other peers
				for (int i = 0; i < identityLength; i++) {
					Trackers.Remove(identityArray[i]);
					foreach (NetworkHost host in Hosts) {
						host.SendInternal(new MessageComponentUnregister(identityArray[i]), peer);
					}
				}

			} finally {
				Lock.ExitWriteLock();
			}

			// Notify all components that the peer was tracking outside the lock
			for (int i = 0; i < componentRemovedLength; i++) {
				componentRemovedArray[i].OnNetworkPeerUnregister(peer);
			}

			// Notify all components about the peer disconnect outside the lock
			for (int i = 0; i < componentAllLength; i++) {
				componentAllArray[i].OnNetworkPeerDisconnect(peer);
			}

			// Return temporary arrays
			PoolIdentity.Return(identityArray);
			PoolComponent.Return(componentAllArray);
			PoolComponent.Return(componentRemovedArray);

		}

		internal void RegisterComponent(NetworkComponent component, NetworkIdentity identity) {

			// Validate
			if (Destroyed) return;

			try {
				Lock.EnterWriteLock();

				// Unregister first if already registered
				if (identity != component.NetworkIdentity) {
					UnregisterComponentInsideLock(component);
				}

				// Get tracker
				Trackers.TryGetValue(identity, out Tracker tracker);

				// Check if tracker already has a component
				if (tracker != null && tracker.Component != null) {
					if (tracker.Component == component) {
						// Component is already tracked, do nothing
						return;
					} else {
						// Another component is tracked with the same ID
						Debug.LogWarning(string.Format(
							"[SuperNet] [NetworkManager] Another component is already registered with ID {0}.",
							identity
						));
						return;
					}
				}

				// Generate a new identity if invalid
				if (identity.IsInvalid) {
					uint value = (uint)Random.Next(int.MinValue, int.MaxValue);
					uint range = NetworkIdentity.VALUE_MAX_DYNAMIC - NetworkIdentity.VALUE_MIN_DYNAMIC;
					identity = NetworkIdentity.VALUE_MIN_DYNAMIC + value % range;
					while (Trackers.ContainsKey(identity)) {
						identity = identity.Value + 1;
						if (!identity.IsDynamic) {
							identity = NetworkIdentity.VALUE_MIN_DYNAMIC;
						}
					}
					Trackers.TryGetValue(identity, out tracker);
				}

				// Create a new tracker if it doesnt exist
				if (tracker == null) {
					tracker = new Tracker(identity);
					Trackers.Add(identity, tracker);
				}

				// Assign component
				tracker.Component = component;

				// Notify all peers
				foreach (NetworkHost host in Hosts) {
					host.SendInternal(new MessageComponentRegister(identity));
				}

			} finally {
				Lock.ExitWriteLock();
			}

			// Notify component
			component.OnComponentRegister(identity);

		}

		internal void UnregisterComponent(NetworkComponent component) {

			// Validate
			if (Destroyed) return;

			// Unregister
			try {
				Lock.EnterWriteLock();
				UnregisterComponentInsideLock(component);
			} finally {
				Lock.ExitWriteLock();
			}

			// Notify component
			component.OnNetworkUnregister();

		}

		private void UnregisterComponentInsideLock(NetworkComponent component) {

			// Get tracker
			NetworkIdentity identity = component.NetworkIdentity;
			Trackers.TryGetValue(identity, out Tracker tracker);

			// If component not tracked, do nothing
			if (tracker == null || tracker.Component != component) {
				return;
			}

			// Remove component
			tracker.Component = null;

			// Check if tracker is empty
			if (tracker.Peers.Count <= 0) {

				// Remove tracker
				Trackers.Remove(identity);

				// Notify all peers
				foreach (NetworkHost host in Hosts) {
					host.SendInternal(new MessageComponentUnregister(identity));
				}

			}

		}

		internal bool IsComponentRegistered(NetworkComponent component) {
			try {
				Lock.EnterReadLock();
				Trackers.TryGetValue(component.NetworkIdentity, out Tracker tracker);
				return tracker != null && tracker.Component == component;
			} finally {
				Lock.ExitReadLock();
			}
		}

		internal NetworkComponent FindNetworkComponent(NetworkIdentity identity) {
			try {
				Lock.EnterReadLock();
				Trackers.TryGetValue(identity, out Tracker tracker);
				return tracker?.Component;
			} finally {
				Lock.ExitReadLock();
			}
		}

		private int GetComponentPeerCount(NetworkIdentity identity) {
			try {
				Lock.EnterReadLock();
				Trackers.TryGetValue(identity, out Tracker tracker);
				return tracker?.Peers.Count ?? 0;
			} finally {
				Lock.ExitReadLock();
			}
		}

		private Peer[] GetComponentPeers(NetworkIdentity identity) {
			try {
				Lock.EnterReadLock();
				Trackers.TryGetValue(identity, out Tracker tracker);
				if (tracker == null || tracker.Peers.Count <= 0) {
					return new Peer[0];
				} else {
					Peer[] peers = new Peer[tracker.Peers.Count];
					tracker.Peers.CopyTo(peers);
					return peers;
				}
			} finally {
				Lock.ExitReadLock();
			}
		}

		internal MessageSent SendComponentMessage(NetworkIdentity identity, Peer peer, INetworkMessage message, IMessageListener listener) {
			try {
				Lock.EnterReadLock();

				// Get tracker and check if it contains the peer
				Trackers.TryGetValue(identity, out Tracker tracker);
				if (tracker == null || !tracker.Peers.Contains(peer)) {
					throw new ArgumentException(string.Format(
						"Peer {0} isn't tracking component {1}",
						peer.Remote, identity
					), nameof(peer));
				}

				// Send message
				return peer.Send(new MessageComponentMessage(identity, message), listener);
				
			} finally {
				Lock.ExitReadLock();
			}
		}

		internal void SendComponentMessageAll(NetworkIdentity identity, INetworkMessage message, Peer exclude) {
			try {
				Lock.EnterReadLock();

				// Get tracker
				Trackers.TryGetValue(identity, out Tracker tracker);
				if (tracker == null) return;

				// Send message to all peers
				foreach (Peer peer in tracker.Peers) {
					try {
						if (peer.Connected && peer != exclude) {
							peer.Send(new MessageComponentMessage(identity, message));
						}
					} catch (Exception exception) {
						Debug.LogException(exception);
					}
				}

			} finally {
				Lock.ExitReadLock();
			}
		}

		internal void OnPeerReceive(NetworkHost host, Peer peer, Reader reader, MessageReceived info) {

			// Ignore messages from local connections
			if (Host.IsLocal(peer.Remote)) {
				return;
			}

			// Save position
			int position = reader.Position;

			// Process based on channel
			if (Enum.IsDefined(typeof(NetworkChannels), info.Channel)) {
				NetworkChannels type = (NetworkChannels)info.Channel;
				switch (type) {
					case NetworkChannels.ComponentRegister:
						OnReceiveComponentRegister(peer, reader);
						break;
					case NetworkChannels.ComponentUnregister:
						OnReceiveComponentUnregister(peer, reader);
						break;
					case NetworkChannels.ComponentMessage:
						OnReceiveComponentMessage(host, peer, reader, info);
						break;
				}
			}

			// Reset the reader back
			reader.Reset(position);

		}

		private void OnReceiveComponentRegister(Peer peer, Reader reader) {

			// Read and check ID
			NetworkIdentity identity = reader.ReadUint32();
			if (identity.IsInvalid) {
				Debug.LogWarning(string.Format(
					"[SuperNet] [NetworkManager] Received component ID {0} from '{1}' is invalid.",
					identity, peer.Remote
				));
				return;
			}

			NetworkComponent component = null;
			try {
				Lock.EnterWriteLock();

				// Find tracker or create it
				Trackers.TryGetValue(identity, out Tracker tracker);
				if (tracker == null) {
					tracker = Trackers[identity] = new Tracker(identity);
				}

				// Add peer to the tracker
				tracker.Peers.Add(peer);

				// Get component
				component = tracker.Component;

				// Resend message to other peers
				foreach (NetworkHost host in Hosts) {
					host.SendInternal(new MessageComponentRegister(identity), peer);
				}

			} finally {
				Lock.ExitWriteLock();
			}

			// Notify component
			if (component != null) {
				component.OnNetworkPeerRegister(peer);
			}

		}

		private void OnReceiveComponentUnregister(Peer peer, Reader reader) {

			// Read and check ID
			NetworkIdentity identity = reader.ReadUint32();
			if (identity.IsInvalid) {
				Debug.LogWarning(string.Format(
					"[SuperNet] [NetworkManager] Received component ID {0} from '{1}' is invalid.",
					identity, peer.Remote
				));
				return;
			}

			NetworkComponent component = null;
			try {
				Lock.EnterWriteLock();

				// Find tracker
				Trackers.TryGetValue(identity, out Tracker tracker);

				// Make sure tracker exists
				if (tracker == null) {
					Debug.LogWarning(string.Format(
						"[SuperNet] [NetworkManager] Received component ID {0} unregistered on '{1}' doesn't exist.",
						identity, peer.Remote
					));
					return;
				}

				// Remove peer from the tracker
				if (!tracker.Peers.Remove(peer)) {
					Debug.LogWarning(string.Format(
						"[SuperNet] [NetworkManager] Received component ID {0} unregistered on '{1}' wasn't tracked.",
						identity, peer.Remote
					));
					return;
				}

				// Get component
				component = tracker.Component;

				// Remove tracker if empty
				bool empty = false;
				if (tracker.Peers.Count <= 0 && component == null) {
					Trackers.Remove(identity);
					empty = true;
				}

				// Resend message to other peers
				if (empty) {
					foreach (NetworkHost host in Hosts) {
						host.SendInternal(new MessageComponentUnregister(identity), peer);
					}
				}

			} finally {
				Lock.ExitWriteLock();
			}

			// Notify component if it exists
			if (component != null) {
				component.OnNetworkPeerUnregister(peer);
			}

		}

		private void OnReceiveComponentMessage(NetworkHost host, Peer peer, Reader reader, MessageReceived info) {

			// Read and check ID
			NetworkIdentity identity = reader.ReadUint32();
			if (identity.IsInvalid) {
				Debug.LogWarning(string.Format(
					"[SuperNet] [NetworkManager] Received component ID {0} from '{1}' is invalid.",
					identity, peer.Remote
				));
				return;
			}

			NetworkComponent component = null;
			try {
				Lock.EnterReadLock();

				// Find tracker
				Trackers.TryGetValue(identity, out Tracker tracker);

				// Make sure tracker exists
				if (tracker == null) {
					Debug.LogWarning(string.Format(
						"[SuperNet] [NetworkManager] Received component ID {0} message from '{1}' doesn't exist.",
						identity, peer.Remote
					));
					return;
				}

				// Get component
				if (tracker != null) component = tracker.Component;

				// Copy and resend message to peers with this component
				MessageCopy copy = null;
				foreach (Peer other in tracker.Peers) {
					if (other == peer) continue;
					if (Host.IsLocal(other.Remote)) continue;
					if (copy == null) copy = new MessageCopy(host.Allocator, reader, info);
					copy.Send(other);
				}

			} finally {
				Lock.ExitReadLock();
			}

			// Notify component
			if (component != null) {
				component.OnNetworkMessage(peer, reader, info.Timestamp);
			}

		}

		private class Tracker {

			public readonly NetworkIdentity Identity;
			public readonly HashSet<Peer> Peers;
			public NetworkComponent Component;
			
			public Tracker(NetworkIdentity identity) {
				Identity = identity;
				Peers = new HashSet<Peer>();
				Component = null;
			}

		}

		private class MessageCopy : IMessage, IMessageListener {

			public byte Channel => (byte)NetworkChannels.ComponentMessage;
			public bool Timed => Info.Timed;
			public bool Reliable => Info.Reliable;
			public bool Ordered => false;
			public bool Unique => Info.Unique;
			public short Offset => (short)(-Info.Timestamp.ElapsedMilliseconds);
			public readonly Allocator Allocator;
			public readonly MessageReceived Info;
			public readonly int Length;
			public byte[] Buffer;
			public int Count;

			public MessageCopy(Allocator allocator, Reader reader, MessageReceived info) {
				Allocator = allocator;
				Info = info;
				Length = reader.Last - reader.First;
				Buffer = allocator.CreateMessage(Length);
				Array.Copy(reader.Buffer, reader.First, Buffer, 0, Length);
				Count = 0;
			}

			public void Send(Peer peer) {
				Interlocked.Increment(ref Count);
				try {
					peer.Send(this, this);
				} catch (Exception exception) {
					Debug.LogException(exception);
				}
			}

			public void Write(Writer writer) {
				writer.WriteBytes(Buffer, 0, Length);
			}

			public void OnMessageSend(Peer peer, MessageSent message) {
				if (Info.Reliable) return;
				int count = Interlocked.Decrement(ref Count);
				if (count == 1) Allocator.ReturnMessage(ref Buffer);
			}

			public void OnMessageAcknowledge(Peer peer, MessageSent message) {
				int count = Interlocked.Decrement(ref Count);
				if (count == 1) Allocator.ReturnMessage(ref Buffer);
			}

		}

		private struct MessageComponentMessage : IMessage {

			public byte Channel => (byte)NetworkChannels.ComponentMessage;
			public bool Timed => Message.Timed;
			public bool Reliable => Message.Reliable;
			public bool Ordered => false;
			public bool Unique => Message.Unique;
			public short Offset => 0;
			public readonly NetworkIdentity Identity;
			public readonly INetworkMessage Message;

			public MessageComponentMessage(NetworkIdentity identity, INetworkMessage message) {
				Identity = identity;
				Message = message;
			}

			public void Write(Writer writer) {
				writer.Write(Identity.Value);
				Message.Write(writer);
			}

		}

		private struct MessageComponentRegister : IMessage {

			public byte Channel => (byte)NetworkChannels.ComponentRegister;
			public bool Timed => false;
			public bool Reliable => true;
			public bool Ordered => false;
			public bool Unique => true;
			public short Offset => 0;
			public readonly NetworkIdentity Identity;

			public MessageComponentRegister(NetworkIdentity identity) {
				Identity = identity;
			}

			public void Write(Writer writer) {
				writer.Write(Identity.Value);
			}

		}

		private struct MessageComponentUnregister : IMessage {

			public byte Channel => (byte)NetworkChannels.ComponentUnregister;
			public bool Timed => false;
			public bool Reliable => true;
			public bool Ordered => false;
			public bool Unique => true;
			public short Offset => 0;
			public readonly NetworkIdentity Identity;

			public MessageComponentUnregister(NetworkIdentity identity) {
				Identity = identity;
			}

			public void Write(Writer writer) {
				writer.Write(Identity.Value);
			}

		}

	}

}
