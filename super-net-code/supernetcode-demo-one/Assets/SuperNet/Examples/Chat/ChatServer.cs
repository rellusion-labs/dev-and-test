using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace SuperNet.Examples.Chat {

	public class ChatServer : MonoBehaviour {

		// UI Elements
		public Text MenuChatTemplate;
		public Text MenuStatus;
		public Text MenuPeerCount;
		public InputField MenuServerPort;
		public Button MenuStart;
		public Button MenuShutdown;

		// Netcode
		private Host Host;
		private int Clients;

		// Queue used to run actions on the main Unity thread
		private static readonly Thread RunThread = Thread.CurrentThread;
		private readonly ConcurrentQueue<Action> RunQueue = new ConcurrentQueue<Action>();

		public void Start() {

			// Register button listeners
			MenuStart.onClick.AddListener(OnStartClick);
			MenuShutdown.onClick.AddListener(OnShutdownClick);

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

		private void OnStartClick() {

			// If already listening, do nothing
			if (Host != null) {
				AddChat("Cannot launch while already listening.", Color.red);
				return;
			}

			// Parse port
			bool portParsed = int.TryParse(MenuServerPort.text, out int port);
			if (MenuServerPort.text != "" && !portParsed || port < 0 || port >= 65536) {
				AddChat("Bad server port '" + MenuServerPort.text + "'.", Color.red);
				return;
			}

			// Create host config
			HostConfig config = new HostConfig();
			config.Port = port;

			// Register host events
			HostEvents events = new HostEvents();
			events.OnException += OnHostException;
			events.OnReceiveRequest += OnHostReceiveRequest;

			// Start listening
			try {
				Host = new Host(config, events);
				Clients = 0;
			} catch (Exception exception) {
				AddChat("Launch exception: " + exception.Message, Color.red);
				Debug.LogException(exception);
				return;
			}

			// Notify console and update status
			AddChat("Server started listening on " + Host.BindAddress, Color.white);
			MenuStatus.text = "Listening on " + Host.BindAddress;
			MenuPeerCount.text = "Clients: " + Clients;

		}

		private void OnShutdownClick() {

			// If not listening, do nothing
			if (Host == null) {
				AddChat("Server is already not listening.", Color.red);
				return;
			}

			// Start shutting down
			Host.Shutdown();
			Host = null;

			// Notify console and update status
			AddChat("Shutting down server.", Color.white);
			MenuStatus.text = "Shutting down.";

		}

		private void OnHostException(IPEndPoint remote, Exception exception) {
			// Exceptions don't usually indicate any errors, we can ignore them
			Debug.LogException(exception);
			Run(() => AddChat("Server exception: " + exception.ToString(), Color.red));
		}

		private void OnHostReceiveRequest(ConnectionRequest request, Reader message) {

			// Register peer events
			PeerEvents events = new PeerEvents();
			events.OnConnect += OnPeerConnect;
			events.OnDisconnect += OnPeerDisconnect;
			events.OnException += OnPeerException;
			events.OnReceive += OnPeerReceive;

			// Accept connection
			request.Accept(new PeerConfig(), events);

		}

		private void OnPeerConnect(Peer peer) {

			// Send join message to all clients
			ServerMessageJoin join = new ServerMessageJoin();
			join.Name = peer.Remote.ToString();
			Host.SendAll(join, peer);
			
			// Notify console and update status
			Run(() => {
				Clients++;
				AddChat(peer.Remote + " connected.", Color.white);
				MenuPeerCount.text = "Clients: " + Clients;
			});

		}

		private void OnPeerDisconnect(Peer peer, Reader message, DisconnectReason reason, Exception exception) {

			// Send leave message to all clients
			ServerMessageLeave leave = new ServerMessageLeave();
			leave.Name = peer.Remote.ToString();
			Host.SendAll(leave, peer);

			// Notify console and update status
			Run(() => {
				Clients--;
				AddChat(peer.Remote + " disconnected: " + reason, Color.white);
				MenuPeerCount.text = "Clients: " + Clients;
			});

			// Display any exceptions
			if (exception != null) {
				Debug.LogException(exception);
				Run(() => AddChat("Disconnect Exception:" + exception.ToString(), Color.red));
			}

		}

		private void OnPeerReceive(Peer peer, Reader message, MessageReceived info) {

			// Convert channel to message type
			ClientMessageType type = (ClientMessageType)info.Channel;

			// Process message based on type
			switch (type) {
				case ClientMessageType.Chat:
					OnPeerReceiveChat(peer, message, info);
					break;
				default:
					Run(() => {
						AddChat(peer.Remote + " sent an unknown message type " + info.Channel + ".", Color.red);
					});
					break;
			}

		}

		private void OnPeerReceiveChat(Peer peer, Reader reader, MessageReceived info) {

			// Read the message
			ClientMessageChat message = new ClientMessageChat();
			message.Read(reader);

			// Send message to all clients including the one who sent it
			ServerMessageChat chat = new ServerMessageChat();
			chat.Name = peer.Remote.ToString();
			chat.Message = message.Message;
			Host.SendAll(chat);

			// Add message to console
			Run(() => {
				AddChat(peer.Remote + ": " + message.Message, Color.white);
			});

		}

		private void OnPeerException(Peer peer, Exception exception) {
			// Exceptions don't usually indicate any errors, we can ignore them
			Debug.LogException(exception);
			Run(() => AddChat("Client " + peer.Remote + " exception: " + exception.ToString(), Color.red));
		}

	}

}
