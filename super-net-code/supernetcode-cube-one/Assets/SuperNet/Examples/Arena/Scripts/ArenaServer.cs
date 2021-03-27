using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using SuperNet.Unity.Core;
using System;
using System.Net;
using UnityEngine;

namespace SuperNet.Examples.Arena {

	public class ArenaServer : MonoBehaviour {

		// Scene objects
		public Menu Menu;
		public NetworkHost Server;
		public ArenaSpawners ArenaSpawners;
		public ArenaClient ArenaClient;
		public ArenaRelay ArenaRelay;

		private void Reset() {
			Menu = transform.Find("/Canvas").GetComponent<Menu>();
			Server = transform.Find("/Network/Server").GetComponent<NetworkHost>();
			ArenaSpawners = FindObjectOfType<ArenaSpawners>();
			ArenaClient = FindObjectOfType<ArenaClient>();
			ArenaRelay = FindObjectOfType<ArenaRelay>();
		}

		private void Awake() {

			// Register server events
			Server.PeerEvents.OnConnect += OnServerPeerConnect;
			Server.PeerEvents.OnDisconnect += OnServerPeerDisconnect;
			Server.PeerEvents.OnReceive += OnServerPeerReceive;

		}

		public bool LaunchServer(int port) {

			// Disable remote spawning/despawning
			ArenaSpawners.SetRemoteSpawning(false);

			// Despawn all players
			ArenaSpawners.DespawnAllPlayers();

			// Claim authority on all existing movables
			// If you want to reset all movables to their original place
			// This is where you can do that
			ArenaSpawners.ClaimAuthorityOnAllCubes();
			ArenaSpawners.ClaimAuthorityOnAllSpheres();

			// Start server host
			Server.HostConfiguration.Port = port;
			if (!Server.Startup()) {
				return false;
			}

			// Log
			IPEndPoint address = Server.GetBindAddress();
			Debug.Log("[Arena] [Server] Server launched on port " + address.Port + ".");

			// Success
			return true;

		}

		public void Shutdown() {

			// Despawn all objects
			ArenaSpawners.DespawnAllCubes();
			ArenaSpawners.DespawnAllSpheres();
			ArenaSpawners.DespawnAllPlayers();

			// Shutdown server host
			Server.Shutdown();

		}

		public void ConnectP2P(IPEndPoint address) {

			// This method just starts connecting to the provided address
			// The address is received from the relay when a P2P connection is requested
			// If the server and the client both start connecting to eachother at the same time
			// Then a P2P connection is established

			// Start connecting
			Server.Connect(address);

		}

		private void OnServerPeerConnect(Peer peer) {

			// Client connected to server

			// Log
			Debug.Log(string.Format("[Arena] [Server] [{0}] Client connected.", peer.Remote));

			NetworkManager.Run(() => {

				// Spawn and notify player
				PlayerController player = ArenaSpawners.SpawnPlayer();

				// Notify player, which will give authority to the right peer
				player.OnServerSpawn(peer);

				// Spawn a random cube and claim authority
				Movable cube = ArenaSpawners.SpawnCube();
				cube.ClaimAuthority();

				// Spawn a random sphere and claim authority
				Movable sphere = ArenaSpawners.SpawnSphere();
				sphere.ClaimAuthority();

			});

		}

		private void OnServerPeerDisconnect(Peer peer, Reader message, DisconnectReason reason, Exception exception) {

			// Client disconnected from server

			// Log exception if included
			if (exception != null) {
				Debug.LogWarning(string.Format(
					"[Arena] [Server] [{0}] Exception: {1}",
					peer.Remote, exception.Message
				));
			}

			// Log
			Debug.Log(string.Format("[Arena] [Server] [{0}] Client disconnected: {1}", peer.Remote, reason));

		}

		private void OnServerPeerReceive(Peer peer, Reader message, MessageReceived info) {

			// Global messages from client to server are received here

		}

	}

}

