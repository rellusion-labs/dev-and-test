using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;

namespace SuperNet.Examples.Arena {

	public class RelayProgram : IHostListener, IPeerListener {

		// This is a relay server program that can be compiled without Unity
		// Once running, you can use it for your own server list on your own relay
		// Make sure the UDP port 44015 is open on the relay
		// You can change the port in the Main() method

		// Relay running on "superversus.com:44015" should not be used in production

		public static void Main(string[] args) {
			new RelayProgram(44015);
			Thread.Sleep(Timeout.Infinite);
		}

		private static string Date => DateTime.Now.ToString("yyyy-MM-dd H:mm:ss");
		private readonly Dictionary<IPEndPoint, Tuple<Peer, string>> Servers;
		private readonly ReaderWriterLockSlim Lock;
		private readonly Host Host;
		
		public RelayProgram(int port) {

			// Initialize variables
			Servers = new Dictionary<IPEndPoint, Tuple<Peer, string>>();
			Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

			// Create host config
			HostConfig config = new HostConfig() {
				Port = port,
				SendBufferSize = 65536,
				ReceiveBufferSize = 65536,
				ReceiveCount = 32,
				ReceiveMTU = 2048,
				AllocatorCount = 16384,
				AllocatorPooledLength = 8192,
				AllocatorPooledExpandLength = 1024,
				AllocatorExpandLength = 1024,
				AllocatorMaxLength = 65536,
			};

			// Set thread limits
			ThreadPool.SetMinThreads(32, 1);

			// Get thread limits
			ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxIOThreads);
			ThreadPool.GetMinThreads(out int minWorkerThreads, out int minIOThreads);
			ThreadPool.GetAvailableThreads(out int availWorkerThreads, out int availIOThreads);
			Console.WriteLine("Max worker threads = " + maxWorkerThreads + ", max I/O threads = " + maxIOThreads);
			Console.WriteLine("Min worker threads = " + minWorkerThreads + ", min I/O threads = " + minIOThreads);
			Console.WriteLine("Available worker threads = " + availWorkerThreads + ", available I/O threads = " + availIOThreads);

			// Create host
			Host = new Host(config, this);
			Console.WriteLine("[" + Date + "] Relay server started on " + Host.BindAddress);

		}

		void IHostListener.OnHostReceiveRequest(ConnectionRequest request, Reader message) {
			// Accept the connection
			request.Accept(new PeerConfig() {
				PingDelay = 2000,
				SendDelay = 15,
				ResendCount = 12,
				ResendDelayJitter = 80,
				ResendDelayMin = 200,
				ResendDelayMax = 800,
				FragmentTimeout = 16000,
				DuplicateTimeout = 2000,
				DisconnectDelay = 500,
			}, this);
		}

		void IPeerListener.OnPeerConnect(Peer peer) {
			Console.WriteLine("[" + Date + "] [" + peer.Remote + "] Connected.");
		}

		void IPeerListener.OnPeerReceive(Peer peer, Reader message, MessageReceived info) {

			// Convert channel to message type
			RelayMessageType type = (RelayMessageType)info.Channel;

			// Process the message based on type
			switch (type) {
				case RelayMessageType.ServerListAdd:
					// Peer has requested to be added to server list
					OnPeerReceiveListAdd(peer, message, info);
					break;
				case RelayMessageType.ServerListRequest:
					// Peer has requested current server list
					OnPeerReceiveListRequest(peer, message, info);
					break;
				case RelayMessageType.Connect:
					// Peer has requested to connect to a server on this relay
					// Notify the server if it exists
					OnPeerReceiveConnect(peer, message, info);
					break;
				default:
					Console.WriteLine("[" + Date + "] [" + peer.Remote + "] Unknown message type " + info.Channel + " received.");
					break;
			}

		}

		private void OnPeerReceiveListAdd(Peer peer, Reader reader, MessageReceived info) {

			// Read the message
			RelayServerListAdd message = new RelayServerListAdd();
			message.Read(reader);

			// Add to server list
			try {
				Lock.EnterWriteLock();
				Servers[peer.Remote] = new Tuple<Peer, string>(peer, message.Address);
			} finally {
				Lock.ExitWriteLock();
			}

			// Notify console
			Console.WriteLine("[" + Date + "] [" + peer.Remote + "] Adding to server list with local address " + message.Address + ".");
			
		}

		private void OnPeerReceiveListRequest(Peer peer, Reader reader, MessageReceived info) {

			// Read the message
			RelayServerListRequest message = new RelayServerListRequest();
			message.Read(reader);

			// Create server list to send
			List<string> servers = new List<string>();
			try {
				Lock.EnterReadLock();
				foreach (Tuple<Peer, string> server in Servers.Values) {
					if (server.Item1.Remote.Address.Equals(peer.Remote.Address)) {
						// Server is on the same local network as the requester
						// Send the local address so they can connect to it directly
						if (server.Item2 == null) {
							servers.Add("127.0.0.1:" + server.Item1.Remote.Port);
						} else {
							servers.Add(server.Item2);
						}
					} else {
						servers.Add(server.Item1.Remote.ToString());
					}
				}
			} finally {
				Lock.ExitReadLock();
			}

			// Send server list
			peer.Send(new RelayServerListResponse() { Servers = servers });

			// Notify console
			Console.WriteLine("[" + Date + "] [" + peer.Remote + "] Sending server list. Servers = " + servers.Count);
			
		}

		private void OnPeerReceiveConnect(Peer peer, Reader reader, MessageReceived info) {

			// Read the message
			RelayConnect message = new RelayConnect();
			message.Read(reader);

			// Parse connect address
			IPEndPoint remote = IPResolver.TryParse(message.Address);
			if (remote == null) {
				Console.WriteLine("[" + Date + "] [" + peer.Remote + "] Bad connect address '" + message.Address + "' received.");
				return;
			}

			// Find server if it exists
			Tuple<Peer, string> server = null;
			try {
				Lock.EnterReadLock();
				Servers.TryGetValue(remote, out server);
			} finally {
				Lock.ExitReadLock();
			}

			// Notify server about the connection
			if (server == null) {
				Console.WriteLine("[" + Date + "] [" + peer.Remote + "] Server " + remote + " is not on this relay.");
			} else {
				server.Item1.Send(new RelayConnect() { Address = peer.Remote.ToString() });
				Console.WriteLine("[" + Date + "] [" + peer.Remote + "] Notifying server " + remote + " about the connection attempt.");
			}

		}

		void IPeerListener.OnPeerDisconnect(Peer peer, Reader message, DisconnectReason reason, Exception exception) {

			// Remove from server list if exists
			bool removed = false;
			try {
				Lock.EnterWriteLock();
				removed = Servers.Remove(peer.Remote);
			} finally {
				Lock.ExitWriteLock();
			}

			// Notify console
			if (removed) {
				Console.WriteLine("[" + Date + "] [" + peer.Remote + "] Removed from server list and disconnected: " + reason);
			} else {
				Console.WriteLine("[" + Date + "] [" + peer.Remote + "] Disconnected: " + reason);
			}
			
		}

		void IHostListener.OnHostException(IPEndPoint remote, Exception exception) {
			Console.WriteLine("[" + Date + "] Host exception: " + exception.Message);
		}

		void IHostListener.OnHostShutdown() {
			Console.WriteLine("[" + Date + "] Host shut down.");
		}

		void IPeerListener.OnPeerException(Peer peer, Exception exception) {
			Console.WriteLine("[" + Date + "] [" + peer.Remote + "] Peer exception: " + exception.ToString());
		}

		void IPeerListener.OnPeerUpdateRTT(Peer peer, ushort rtt) {
			// We don't care about RTT
		}

		void IHostListener.OnHostReceiveSocket(IPEndPoint remote, byte[] buffer, int length) {
			// We don't care about raw socket packets
		}

		void IHostListener.OnHostReceiveUnconnected(IPEndPoint remote, Reader message) {
			// We don't care about unconnected packets
		}
		
		void IHostListener.OnHostReceiveBroadcast(IPEndPoint remote, Reader message) {
			// We don't care about broadcast packets	
		}

	}

}
