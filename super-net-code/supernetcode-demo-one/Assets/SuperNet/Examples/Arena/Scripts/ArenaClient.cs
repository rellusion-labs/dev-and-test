using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using SuperNet.Unity.Core;
using System;
using System.Net;
using UnityEngine;

namespace SuperNet.Examples.Arena {

	public class ArenaClient : MonoBehaviour {

		// Scene objects
		public Menu Menu;
		public PlayerView View;
		public NetworkHost Client;
		public ArenaSpawners ArenaSpawners;
		public ArenaServer ArenaServer;
		public ArenaRelay ArenaRelay;
		
		// Resources
		private Peer Peer = null;

		private void Reset() {
			Menu = transform.Find("/Canvas").GetComponent<Menu>();
			View = FindObjectOfType<PlayerView>();
			Client = transform.Find("/Network/Client").GetComponent<NetworkHost>();
			ArenaSpawners = FindObjectOfType<ArenaSpawners>();
			ArenaServer = FindObjectOfType<ArenaServer>();
			ArenaRelay = FindObjectOfType<ArenaRelay>();
		}

		public string GetConnectionStatus() {

			// Get address client is connected to
			IPEndPoint address = Peer?.Remote;

			// Convert address to status
			if (address == null) {
				return "Not connected";
			} else if (Host.IsLocal(address)) {
				return "Server listening on port " + address.Port;
			} else {
				return "Connected to " + address;
			}

		}

		public bool LaunchClient(IPEndPoint address) {

			// Start client host
			if (!Client.Startup()) {
				return false;
			}

			// Check if connecting to local server
			if (Host.IsLocal(address)) {

				// We're connecting to a local server
				// Let the server handle spawners

			} else {

				// We're connecting to a remote server

				// Despawn any existing objects
				ArenaSpawners.DespawnAllCubes();
				ArenaSpawners.DespawnAllSpheres();
				ArenaSpawners.DespawnAllPlayers();

				// Enable remote spawning/despawning
				ArenaSpawners.SetRemoteSpawning(true);

			}

			// Create and register events
			PeerEvents events = new PeerEvents();
			events.OnConnect += OnClientConnect;
			events.OnDisconnect += OnClientDisconnect;
			events.OnReceive += OnClientReceive;

			// Start connecting
			Peer = Client.Connect(address, events);
			if (Peer == null) {
				return false;
			}

			// Log
			Debug.Log("[Arena] [Client] Connecting to server " + address + ".");

			// Success
			return true;

		}

		public void Shutdown() {

			// Disconnect peer and shutdown host
			if (Peer != null) {
				Debug.Log("[Arena] [Client] Disconnecting and shutting down.");
				Peer.Disconnect();
				Client.Shutdown();
				Peer = null;
			}

			// Remove camera target
			View.Target = null;

		}

		private void OnClientConnect(Peer peer) {

			// Successfully connected to game server

			// Log
			Debug.Log("[Arena] [Client] Connected to " + peer.Remote + ".");

			// Open game UI
			NetworkManager.Run(() => Menu.OpenCanvasGame());

		}

		private void OnClientDisconnect(Peer peer, Reader message, DisconnectReason reason, Exception exception) {

			// Disconnected from game server

			// Log exception if included
			if (exception != null) {
				Debug.LogWarning(string.Format("[Arena] [Client] Exception: {0}", exception.Message));
			}

			// Log
			Debug.Log("[Arena] [Client] Disconnected from server: " + reason);
			
			NetworkManager.Run(() => {
				
				// Shutdown connection
				Shutdown();

				// If already in the menu do nothing
				if (Menu.MenuCanvas.activeSelf) {
					return;
				}

				// Show error
				Menu.OpenCanvasError("Connection to game server failed.");

			});

		}

		private void OnClientReceive(Peer peer, Reader message, MessageReceived info) {
			
			// Global messages from server to client are received here

		}

	}

}
