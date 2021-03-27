using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace SuperNet.Examples.Chat {

	public class ChatClient : MonoBehaviour {

		// UI Elements
		public Text MenuChatTemplate;
		public Text MenuStatus;
		public Text MenuPing;
		public InputField MenuAddress;
		public InputField MenuChatInput;
		public Button MenuConnect;
		public Button MenuDisconnect;
		public Button MenuChatSend;

		// Netcode
		private Host Host;
		private Peer Peer;

		// Queue used to run actions on the main Unity thread
		private static readonly Thread RunThread = Thread.CurrentThread;
		private readonly ConcurrentQueue<Action> RunQueue = new ConcurrentQueue<Action>();

		public void Start() {

			// Register button listeners
			MenuConnect.onClick.AddListener(OnConnectClick);
			MenuDisconnect.onClick.AddListener(OnDisconnectClick);
			MenuChatSend.onClick.AddListener(OnChatSendClick);

			// Hide chat template
			MenuChatTemplate.gameObject.SetActive(false);

		}

		public void Update() {
			// Run all queued actions on the main Unity thread
			while (RunQueue.TryDequeue(out Action action)) {
				try {
					action.Invoke();
				} catch (Exception exception) {
					Debug.LogException(exception);
				}
			}
		}

		public void OnDestroy() {
			// Dispose host if running
			if (Host != null) Host.Dispose();
		}

		private void Run(Action action) {
			// Run action in the main Unity thread
			if (Thread.CurrentThread == RunThread) {
				action.Invoke();
			} else {
				RunQueue.Enqueue(action);
			}
		}

		private void AddChat(string message, Color color) {
			// Instantiate a new template, enable it and assign text and color
			Text text = Instantiate(MenuChatTemplate, MenuChatTemplate.transform.parent);
			text.gameObject.SetActive(true);
			text.text = message;
			text.color = color;
		}

		private void OnConnectClick() {

			// If already connected, do nothing
			if (Host != null) {
				AddChat("Cannot connect while already connected.", Color.red);
				return;
			}

			// Parse address
			IPEndPoint address = IPResolver.TryParse(MenuAddress.text);
			if (address == null) {
				AddChat("Bad server address '" + MenuAddress.text + "'.", Color.red);
				return;
			}

			// Register peer events
			PeerEvents peerEvents = new PeerEvents();
			peerEvents.OnConnect += OnPeerConnect;
			peerEvents.OnDisconnect += OnPeerDisconnect;
			peerEvents.OnException += OnPeerException;
			peerEvents.OnReceive += OnPeerReceive;
			peerEvents.OnUpdateRTT += OnPeerUpdateRTT;

			// Register host events
			HostEvents hostEvents = new HostEvents();
			hostEvents.OnException += OnHostException;
			hostEvents.OnShutdown += OnHostShutdown;

			// Start connecting
			try {
				Host = new Host(new HostConfig(), hostEvents);
				Peer = Host.Connect(address, new PeerConfig(), peerEvents);
			} catch (Exception exception) {
				AddChat("Connect exception: " + exception.Message, Color.red);
				Debug.LogException(exception);
				return;
			}

			// Notify console and update status
			AddChat("Connecting to " + address, Color.white);
			MenuStatus.text = "Connecting to " + address;

		}

		private void OnDisconnectClick() {

			// If not connected, do nothing
			if (Host == null) {
				AddChat("Cannot disconnect: Not connected.", Color.red);
				return;
			}

			// Disconnect
			Peer.Disconnect();
			Host.Shutdown();
			Host = null;
			Peer = null;

			// Notify console and update status
			AddChat("Disconnecting.", Color.white);
			MenuStatus.text = "Disconnecting.";

		}

		private void OnChatSendClick() {

			// If not connected, do nothing
			if (Peer == null || !Peer.Connected) {
				AddChat("Cannot send: Not connected.", Color.red);
				return;
			}

			// Send chat message to server
			ClientMessageChat message = new ClientMessageChat();
			message.Message = MenuChatInput.text;
			Peer.Send(message);

			// Clear the UI input
			MenuChatInput.text = "";

		}

		private void OnPeerConnect(Peer peer) {
			Run(() => {
				// Notify console and update status
				AddChat("Connected.", Color.white);
				MenuStatus.text = "Connected to " + peer.Remote;
			});
		}

		private void OnPeerDisconnect(Peer peer, Reader message, DisconnectReason reason, Exception exception) {
			Run(() => {

				// Dispose resources
				Peer.Dispose();
				Host.Dispose();
				Host = null;
				Peer = null;

				// Notify console and update status
				AddChat("Disconnected: " + reason, Color.white);
				MenuStatus.text = "Disconnected: " + reason;
				MenuPing.text = "";

				// Display any exceptions
				if (exception != null) {
					Debug.LogException(exception);
					Run(() => AddChat("Disconnect Exception:" + exception.ToString(), Color.red));
				}

			});
		}

		private void OnPeerUpdateRTT(Peer peer, ushort rtt) {
			Run(() => {
				MenuPing.text = "Ping: " + rtt;
			});
		}

		private void OnPeerReceive(Peer peer, Reader message, MessageReceived info) {

			// Convert channel to message type
			ServerMessageType type = (ServerMessageType)info.Channel;

			// Process message based on type
			switch (type) {
				case ServerMessageType.Join:
					OnPeerReceiveJoin(peer, message, info);
					break;
				case ServerMessageType.Leave:
					OnPeerReceiveLeave(peer, message, info);
					break;
				case ServerMessageType.Chat:
					OnPeerReceiveChat(peer, message, info);
					break;
				default:
					Run(() => {
						AddChat("Server sent an unknown message type " + info.Channel + ".", Color.red);
					});
					break;
			}

		}

		private void OnPeerReceiveJoin(Peer peer, Reader reader, MessageReceived info) {

			// Read the message
			ServerMessageJoin message = new ServerMessageJoin();
			message.Read(reader);

			// Notify console
			Run(() => {
				AddChat(message.Name + " joined.", Color.white);
			});

		}

		private void OnPeerReceiveLeave(Peer peer, Reader reader, MessageReceived info) {

			// Read the message
			ServerMessageLeave message = new ServerMessageLeave();
			message.Read(reader);

			// Notify console
			Run(() => {
				AddChat(message.Name + " disconnected.", Color.white);
			});

		}

		private void OnPeerReceiveChat(Peer peer, Reader reader, MessageReceived info) {

			// Read the message
			ServerMessageChat message = new ServerMessageChat();
			message.Read(reader);

			// Notify console
			Run(() => {
				AddChat(message.Name + ": " + message.Message, Color.white);
			});

		}

		private void OnPeerException(Peer peer, Exception exception) {
			// Exceptions don't usually indicate any errors, we can ignore them
			Debug.LogException(exception);
			Run(() => AddChat("Peer Exception:" + exception.ToString(), Color.red));
		}

		private void OnHostException(IPEndPoint remote, Exception exception) {
			// Exceptions don't usually indicate any errors, we can ignore them
			Debug.LogException(exception);
			Run(() => AddChat("Host Exception:" + exception.ToString(), Color.red));
		}

		private void OnHostShutdown() {
			Run(() => AddChat("Host was shut down.", Color.red));
		}

	}

}
