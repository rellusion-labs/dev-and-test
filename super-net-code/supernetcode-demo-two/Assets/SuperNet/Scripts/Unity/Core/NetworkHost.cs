using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

namespace SuperNet.Unity.Core {

	/// <summary>
	/// Manages a network socket and all network communication between peers.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("SuperNet/NetworkHost")]
	[HelpURL("https://superversus.com/netcode/api/SuperNet.Unity.Core.NetworkHost.html")]
	public sealed class NetworkHost : MonoBehaviour, IHostListener, IPeerListener {

		[Header("Configuration")]

		/// <summary>
		/// The maximum number of concurrent network connections to support.
		/// </summary>
		[FormerlySerializedAs("MaxConnections")]
		[Tooltip("Maximum number of concurrent connections.")]
		public int MaxConnections;

		/// <summary>
		/// Do not destroy the host when loading a new Scene.
		/// </summary>
		[FormerlySerializedAs("PersistAcrossScenes")]
		[Tooltip("Do not destroy the host when loading a new Scene.")]
		public bool PersistAcrossScenes;

		/// <summary>
		/// Should events be logged to the debug console.
		/// </summary>
		[FormerlySerializedAs("LogEvents")]
		[Tooltip("Should events be logged to the debug console.")]
		public bool LogEvents;

		/// <summary>
		/// Host configuration values.
		/// </summary>
		[FormerlySerializedAs("HostConfiguration")]
		[Tooltip("Netcode configuration for the host.")]
		public HostConfig HostConfiguration;

		/// <summary>
		/// Peer configuration values.
		/// </summary>
		[FormerlySerializedAs("HostConfiguration")]
		[Tooltip("Netcode configuration for peers on this host.")]
		public PeerConfig PeerConfiguration;

		[Header("Startup Actions")]

		/// <summary>
		/// Should the host start listening on startup.
		/// </summary>
		[FormerlySerializedAs("AutoStartup")]
		[Tooltip("Should the host start listening on startup.")]
		public bool AutoStartup;

		/// <summary>
		/// Remote address to connect to on startup or empty to disable.
		/// </summary>
		[FormerlySerializedAs("AutoConnectAddress")]
		[Tooltip("Remote address to connect to on startup or empty to disable.")]
		public string AutoConnectAddress;

		/// <summary>
		/// Peer events for all peers.
		/// </summary>
		public PeerEvents PeerEvents { get; private set; }

		/// <summary>
		/// Host events for this host.
		/// </summary>
		public HostEvents HostEvents { get; private set; }

		/// <summary>
		/// True if host is active and listening.
		/// </summary>
		public bool Listening => !(Host?.Disposed ?? true);

		/// <summary>
		/// Number of peers on this host.
		/// </summary>
		public int Connections => PeersAll.Count;

		/// <summary>
		/// Host allocator or an empty allocator if not listening.
		/// </summary>
		public Allocator Allocator => Host?.Allocator ?? EmptyAllocator;

		// Resources
		private readonly ReaderWriterLockSlim Lock;
		private readonly Dictionary<IPEndPoint, Peer> PeersAll;
		private readonly Dictionary<IPEndPoint, Peer> PeersTracked;
		private readonly Dictionary<IPEndPoint, IPeerListener> Listeners;
		private readonly Allocator EmptyAllocator;
		private Host Host;

		public NetworkHost() {
			PeerEvents = new PeerEvents();
			HostEvents = new HostEvents();
			Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			PeersAll = new Dictionary<IPEndPoint, Peer>();
			PeersTracked = new Dictionary<IPEndPoint, Peer>();
			Listeners = new Dictionary<IPEndPoint, IPeerListener>();
			EmptyAllocator = new Allocator();
			Host = null;
		}

		private void Reset() {
			MaxConnections = 32;
			PersistAcrossScenes = true;
			LogEvents = true;
			HostConfiguration = new HostConfig();
			PeerConfiguration = new PeerConfig();
			AutoStartup = true;
			AutoConnectAddress = "";
		}

		private void Awake() {

			// Make sure the host isn't destroyed when the scene changes
			if (PersistAcrossScenes) {
				transform.parent = null;
				DontDestroyOnLoad(this);
			}

			// Create network manager if needed
			NetworkManager.GetInstance();

		}

		private void Start() {
			if (AutoStartup) Startup();
		}

		private void OnDestroy() {

			// Get host to dispose
			Host host = null;
			try {
				Lock.EnterWriteLock();
				host = Host;
				Host = null;
			} finally {
				Lock.ExitWriteLock();
			}

			// Log
			if (host != null && LogEvents) {
				Debug.Log(string.Format(
				   "[SuperNet] [NetworkHost] Destroying host listening on '{0}'.",
				   host.BindAddress
			   ));
			}

			// Dispose host
			host?.Dispose();

		}

		/// <summary>
		/// Get all peers on this host.
		/// </summary>
		/// <returns>Array of all peers on this host.</returns>
		public Peer[] GetPeers() {
			try {
				Lock.EnterReadLock();
				Peer[] array = new Peer[PeersAll.Count];
				PeersAll.Values.CopyTo(array, 0);
				return array;
			} finally {
				Lock.ExitReadLock();
			}
		}

		/// <summary>
		/// Get netcode host or null if not listening.
		/// </summary>
		/// <returns>Netcode host.</returns>
		public Host GetHost() {
			try {
				Lock.EnterReadLock();
				return Host;
			} finally {
				Lock.ExitReadLock();
			}
		}

		/// <summary>
		/// Enable or disable component synchronization for a specific peer.
		/// </summary>
		/// <param name="remote">Peer remote address.</param>
		/// <param name="enabled">True if enabled, false if not.</param>
		/// <returns>True if changed, false if peer doesn't exist.</returns>
		public bool SetTracking(IPEndPoint remote, bool enabled) {

			Peer peer = null;
			bool connect = false;
			bool disconnect = false;

			// Set tracking
			try {
				Lock.EnterWriteLock();
				PeersAll.TryGetValue(remote, out peer);
				if (peer != null) {
					PeersTracked.TryGetValue(remote, out Peer tracked);
					if (enabled) {
						PeersTracked[remote] = peer;
						connect = peer.Connected && tracked != peer;
					} else {
						PeersTracked[remote] = null;
						disconnect = peer.Connected && tracked == peer;
					}
				}
			} finally {
				Lock.ExitWriteLock();
			}

			// Notify manager
			if (connect) NetworkManager.GetInstance().OnPeerConnect(peer);
			if (disconnect) NetworkManager.GetInstance().OnPeerDisconnect(peer);
			
			// Return true if peer was found
			return peer != null;

		}

		/// <summary>
		/// Replace listener for a specific peer.
		/// </summary>
		/// <param name="remote">Peer remote address.</param>
		/// <param name="listener">Listener to replace with.</param>
		/// <returns>True if replaced, false if peer doesn't exist.</returns>
		public bool ReplaceListener(IPEndPoint remote, IPeerListener listener) {
			try {
				Lock.EnterWriteLock();
				PeersAll.TryGetValue(remote, out Peer peer);
				if (peer == null) {
					return false;
				} else {
					Listeners[remote] = listener;
					return true;
				}
			} finally {
				Lock.ExitWriteLock();
			}
		}

		/// <summary>
		/// Get address this host is listening on or loopback if none.
		/// This is usually <c>0.0.0.0</c> with the listen port.
		/// </summary>
		/// <returns>A valid address.</returns>
		public IPEndPoint GetBindAddress() {
			try {
				Lock.EnterReadLock();
				if (Host != null) {
					return Host.BindAddress;
				} else if (HostConfiguration.DualMode) {
					return new IPEndPoint(IPAddress.IPv6Loopback, 0);
				} else {
					return new IPEndPoint(IPAddress.Loopback, 0);
				}
			} finally {
				Lock.ExitReadLock();
			}
		}

		/// <summary>
		/// Create a loopback address for this host.
		/// This is usually <c>127.0.0.1</c> with the listen port.
		/// </summary>
		/// <returns>Loopback address.</returns>
		public IPEndPoint GetLoopbackAddress() {
			IPEndPoint address = GetBindAddress();
			if (HostConfiguration.DualMode) {
				return new IPEndPoint(IPAddress.IPv6Loopback, address.Port);
			} else {
				return new IPEndPoint(IPAddress.Loopback, address.Port);
			}
		}

		/// <summary>
		/// Creates a LAN address for this host.
		/// An example is <c>192.168.1.10</c> with the listen port.
		/// </summary>
		/// <returns>Local address.</returns>
		public IPEndPoint GetLocalAddress() {
			IPEndPoint address = GetBindAddress();
			if (HostConfiguration.DualMode) {
				return new IPEndPoint(IPResolver.GetLocalAddressIPv6(), address.Port);
			} else {
				return new IPEndPoint(IPResolver.GetLocalAddress(), address.Port);
			}
		}

		/// <summary>
		/// Start listening.
		/// </summary>
		/// <returns>True on success, false on failure.</returns>
		public bool Startup() {
			try {
				Lock.EnterWriteLock();

				// Check if already listening
				if (Host != null && !Host.Disposed) {
					return true;
				}

				// Start listening
				try {
					Host = new Host(HostConfiguration, this);
				} catch (Exception exception) {
					Debug.LogException(exception);
					return false;
				}

				// Log
				if (LogEvents) {
					Debug.Log(string.Format(
						"[SuperNet] [NetworkHost] Host started listening on '{0}'",
						Host.BindAddress
					));
				}

				// Start resolving connect address
				if (!string.IsNullOrWhiteSpace(AutoConnectAddress)) {
					IPResolver.Resolve(AutoConnectAddress, OnConnectResolve);
				}

			} finally {
				Lock.ExitWriteLock();
			}

			// Notify manager
			NetworkManager.GetInstance().OnHostStartup(this);
			return true;

		}

		/// <summary>
		/// Instantly dispose all resources held by this host and connected peers.
		/// </summary>
		public void Dispose() {

			// Get host to dispose
			Host host = null;
			try {
				Lock.EnterWriteLock();
				host = Host;
				Host = null;
			} finally {
				Lock.ExitWriteLock();
			}

			// Log
			if (host != null && LogEvents) {
				Debug.Log(string.Format(
					"[SuperNet] [NetworkHost] Disposing host listening on '{0}' with {1} peers.",
					host.BindAddress, PeersAll.Count
				));
			}

			// Dispose
			host?.Dispose();

		}

		/// <summary>
		/// Gracefully disconnect all peers and perform a shutdown.
		/// </summary>
		public void Shutdown() {

			// Get host to shutdown
			Host host = null;
			try {
				Lock.EnterWriteLock();
				host = Host;
				Host = null;
			} finally {
				Lock.ExitWriteLock();
			}

			// Log
			if (host != null && LogEvents) {
				Debug.Log(string.Format(
					"[SuperNet] [NetworkHost] Shutting down host listening on '{0}' with {1} peers.",
					host.BindAddress, PeersAll.Count
				));
			}

			// Shutdown
			host?.Shutdown();

		}

		/// <summary>
		/// Send a global message to all connected peers.
		/// </summary>
		/// <param name="message">Message to send.</param>
		/// <param name="exclude">Peers to exclude.</param>
		public void SendAll(IMessage message, params Peer[] exclude) {
			try {
				Lock.EnterReadLock();
				Host?.SendAll(message, exclude);
			} finally {
				Lock.ExitReadLock();
			}
		}

		/// <summary>
		/// Send a component message to all tracked peers.
		/// </summary>
		/// <param name="message">Message to send.</param>
		/// <param name="exclude">Peer to exclude.</param>
		internal void SendInternal(IMessage message, Peer exclude) {
			try {
				Lock.EnterReadLock();
				foreach (Peer peer in PeersTracked.Values) {
					if (peer == null) continue;
					if (!peer.Connected) continue;
					if (peer == exclude) continue;
					if (Host.IsLocal(peer.Remote)) continue;
					try {
						peer.Send(message);
					} catch (Exception exception) {
						Debug.LogException(exception);
					}
				}
			} finally {
				Lock.ExitReadLock();
			}
		}

		/// <summary>
		/// Send a component message to all tracked peers.
		/// </summary>
		/// <param name="message">Message to send.</param>
		internal void SendInternal(IMessage message) {
			try {
				Lock.EnterReadLock();
				foreach (Peer peer in PeersTracked.Values) {
					if (peer == null) continue;
					if (!peer.Connected) continue;
					if (Host.IsLocal(peer.Remote)) continue;
					try {
						peer.Send(message);
					} catch (Exception exception) {
						Debug.LogException(exception);
					}
				}
			} finally {
				Lock.ExitReadLock();
			}
		}

		/// <summary>Create a local peer and start connecting to an active remote host.</summary>
		/// <param name="remote">Remote address to connect to.</param>
		/// <param name="listener">Peer listener to use or null for none.</param>
		/// <param name="message">Connect message to use.</param>
		/// <returns>Local peer that attempts to connect or null on failure.</returns>
		public Peer Connect(IPEndPoint remote, IPeerListener listener = null, IWritable message = null) {
			return Connect(remote, true, listener, message);
		}

		/// <summary>Create a local peer and start connecting to an active remote host.</summary>
		/// <param name="remote">Remote address to connect to.</param>
		/// <param name="tracked">True if this peer should synchronize components.</param>
		/// <param name="listener">Peer listener to use or null for none.</param>
		/// <param name="message">Connect message to use.</param>
		/// <returns>Local peer that attempts to connect or null on failure.</returns>
		public Peer Connect(IPEndPoint remote, bool tracked, IPeerListener listener = null, IWritable message = null) {

			// Validate
			if (remote == null) {
				Debug.LogWarning("[SuperNet] [NetworkHost] Remote connect address is null.");
				return null;
			}

			try {
				Lock.EnterWriteLock();

				// Check if listening
				if (Host == null || Host.Disposed) {
					Debug.LogWarning(string.Format(
						"[SuperNet] [NetworkHost] Not listening, cannot connect to '{0}'.", remote
					));
					return null;
				}

				// Check if maximum connections was reached
				if (PeersAll.Count >= MaxConnections) {
					Debug.LogWarning(string.Format(
						"[SuperNet] [NetworkHost] Maximum connections {0} reached, cannot connect to '{1}'.",
						MaxConnections, remote
					));
					return null;
				}

				// Check if peer already exists
				PeersAll.TryGetValue(remote, out Peer found);
				if (found != null && found.Connected) {
					Debug.LogWarning(string.Format("[SuperNet] [NetworkHost] Already connected to '{0}'.", remote));
					return found;
				} else if (found != null && found.Connecting) {
					Debug.LogWarning(string.Format("[SuperNet] [NetworkHost] Already connecting to '{0}'.", remote));
					return found;
				}

				// Dispose any existing connections
				if (found != null) found.Dispose();

				// Log
				if (LogEvents) Debug.Log(string.Format("[SuperNet] [NetworkHost] Connecting to '{0}'.", remote));

				// Start connecting
				Peer peer = Host.Connect(remote, PeerConfiguration, this, message);

				// Save peer & listener
				PeersAll[remote] = peer;
				Listeners[remote] = listener;
				PeersTracked[remote] = tracked ? peer : null;

				// Return peer
				return peer;

			} finally {
				Lock.ExitWriteLock();
			}
		}

		/// <summary>
		/// Create a new peer and start connecting to the provided address.
		/// </summary>
		/// <param name="address">Address to connect to.</param>
		public void Connect(string address) {
			IPResolver.Resolve(address, OnConnectResolve);
		}

		private void OnConnectResolve(IPEndPoint remote, Exception exception) {
			if (remote == null) {
				Debug.LogException(exception);
			} else {
				Connect(remote);
			}
		}

		private IPeerListener FindListener(Peer peer) {
			try {
				Lock.EnterReadLock();
				Listeners.TryGetValue(peer.Remote, out IPeerListener listener);
				return listener ?? PeerEvents;
			} finally {
				Lock.ExitReadLock();
			}
		}

		void IHostListener.OnHostReceiveRequest(ConnectionRequest request, Reader message) {

			// Invoke event
			try {
				(HostEvents as IHostListener).OnHostReceiveRequest(request, message);
			} catch (Exception exception) {
				Debug.LogException(exception);
			}

			// If request has been disposed, do nothing
			if (request.Disposed) {
				return;
			}

			try {
				Lock.EnterWriteLock();

				// Check if maximum connections was reached
				if (PeersAll.Count >= MaxConnections) {
					Debug.LogWarning(string.Format(
						"[SuperNet] [NetworkHost] Maximum connections {0} reached. Connection request from '{1}' rejected.",
						MaxConnections, request.Remote
					));
					request.Reject();
					return;
				}

				// Log
				if (LogEvents) {
					Debug.Log(string.Format(
						"[SuperNet] [NetworkHost] Connection request from '{0}' accepted.", request.Remote
					));
				}

				// Check if already connected
				PeersAll.TryGetValue(request.Remote, out Peer found);
				if (found != null && (found.Connected || found.Connecting)) {
					Debug.LogWarning(string.Format(
						"[SuperNet] [NetworkHost] Duplicated connection request from '{0}'. Ignoring.",
						request.Remote
					));
					return;
				}

				// Accept the connection
				Peer peer = request.Accept(PeerConfiguration, this);
				
				// Save peer
				PeersAll[request.Remote] = peer;
				PeersTracked[request.Remote] = peer;
				
			} finally {
				Lock.ExitWriteLock();
			}

		}

		void IPeerListener.OnPeerConnect(Peer peer) {

			// Log
			if (LogEvents) {
				Debug.Log(string.Format(
					"[SuperNet] [NetworkHost] Connection to '{0}' established.", peer.Remote
				));
			}

			// Invoke listener
			try {
				IPeerListener listener = FindListener(peer);
				listener?.OnPeerConnect(peer);
			} catch (Exception exception) {
				Debug.LogException(exception);
			}

			bool connect = false;
			try {
				Lock.EnterWriteLock();

				// Add peer if not added yet
				PeersAll.TryGetValue(peer.Remote, out Peer found);
				if (found == null) PeersAll[peer.Remote] = peer;

				// Check if peer is tracked
				PeersTracked.TryGetValue(peer.Remote, out Peer tracked);
				connect = tracked == peer;
				
			} finally {
				Lock.ExitWriteLock();
			}

			// Notify manager
			if (connect) NetworkManager.GetInstance().OnPeerConnect(peer);

		}

		void IPeerListener.OnPeerDisconnect(Peer peer, Reader message, DisconnectReason reason, Exception exception) {

			// Invoke listener
			try {
				IPeerListener listener = FindListener(peer);
				listener?.OnPeerDisconnect(peer, message, reason, exception);
			} catch (Exception e) {
				Debug.LogException(e);
			}

			bool disconnect = false;
			try {
				Lock.EnterWriteLock();

				// Log
				if (LogEvents) {
					if (exception != null) Debug.LogException(exception);
					Debug.Log(string.Format(
						"[SuperNet] [NetworkHost] Disconnected from '{0}': {1}",
						peer.Remote, reason
					));
				}
				
				// Check if peer is tracked
				PeersTracked.TryGetValue(peer.Remote, out Peer tracked);
				disconnect = tracked == peer;

				// Remove peer & listener
				PeersAll.Remove(peer.Remote);
				Listeners.Remove(peer.Remote);
				PeersTracked.Remove(peer.Remote);

			} finally {
				Lock.ExitWriteLock();
			}

			// Notify manager
			if (disconnect) NetworkManager.GetInstance().OnPeerDisconnect(peer);

		}

		void IHostListener.OnHostShutdown() {

			// Invoke event
			try {
				(HostEvents as IHostListener).OnHostShutdown();
			} catch (Exception exception) {
				Debug.LogException(exception);
			}

			// Notify manager
			NetworkManager.GetInstance().OnHostShutdown(this);

			try {
				Lock.EnterWriteLock();

				// Log
				if (LogEvents) {
					Debug.Log(string.Format(
						"[SuperNet] [NetworkHost] Host listening on '{0}' was shut down.",
						Host.BindAddress
					));
				}

				// Remove host
				Host = null;

				// Remove all peers
				PeersAll.Clear();
				Listeners.Clear();
				PeersTracked.Clear();
				
			} finally {
				Lock.ExitWriteLock();
			}

		}

		void IPeerListener.OnPeerReceive(Peer peer, Reader reader, MessageReceived info) {

			// Check if peer is tracked and find listener
			bool tracked = false;
			IPeerListener listener = null;
			try {
				Lock.EnterReadLock();
				Listeners.TryGetValue(peer.Remote, out listener);
				PeersTracked.TryGetValue(peer.Remote, out Peer found);
				tracked = peer == found;
			} finally {
				Lock.ExitReadLock();
			}

			// Process the message if tracked
			if (tracked) {
				NetworkManager.GetInstance().OnPeerReceive(this, peer, reader, info);
			}

			// Invoke listener
			try {
				if (listener == null) listener = PeerEvents;
				listener?.OnPeerReceive(peer, reader, info);
			} catch (Exception exception) {
				Debug.LogException(exception);
			}

		}

		void IPeerListener.OnPeerUpdateRTT(Peer peer, ushort rtt) {

			// Invoke listener
			try {
				IPeerListener listener = FindListener(peer);
				listener?.OnPeerUpdateRTT(peer, rtt);
			} catch (Exception exception) {
				Debug.LogException(exception);
			}

		}

		void IHostListener.OnHostException(IPEndPoint remote, Exception exception) {

			// Log
			Debug.LogException(exception);

			// Invoke event
			try {
				(HostEvents as IHostListener).OnHostException(remote, exception);
			} catch (Exception e) {
				Debug.LogException(e);
			}

		}

		void IPeerListener.OnPeerException(Peer peer, Exception exception) {

			// Log
			Debug.LogException(exception);

			// Invoke listener
			try {
				IPeerListener listener = FindListener(peer);
				listener?.OnPeerException(peer, exception);
			} catch (Exception e) {
				Debug.LogException(e);
			}

		}

		void IHostListener.OnHostReceiveSocket(IPEndPoint remote, byte[] buffer, int length) {
			(HostEvents as IHostListener).OnHostReceiveSocket(remote, buffer, length);
		}

		void IHostListener.OnHostReceiveUnconnected(IPEndPoint remote, Reader message) {
			(HostEvents as IHostListener).OnHostReceiveUnconnected(remote, message);
		}
		
		void IHostListener.OnHostReceiveBroadcast(IPEndPoint remote, Reader message) {
			(HostEvents as IHostListener).OnHostReceiveBroadcast(remote, message);
		}
		
	}

}
