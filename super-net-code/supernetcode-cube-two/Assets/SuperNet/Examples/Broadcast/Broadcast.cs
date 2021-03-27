using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace SuperNet.Examples.Broadcast {

	// Empty message, no data is sent
	public struct BroadcastMessage : IWritable {
		public void Write(Writer writer) {}
	}

	public class Broadcast : MonoBehaviour {

		// UI Elements
		public Button SearchButton;
		public InputField SearchPort;
		public Text SearchTemplate;
		public Text SearchStatus;
		public InputField HostPort;
		public Button HostStart;
		public Button HostShutdown;
		public Text HostStatus;

		// Netcode
		private Host Host;

		// Queue used to run actions on the main Unity thread
		private static readonly Thread RunThread = Thread.CurrentThread;
		private readonly ConcurrentQueue<Action> RunQueue = new ConcurrentQueue<Action>();

		public void Start() {

			// Register button listeners
			SearchButton.onClick.AddListener(OnSearchClick);
			HostStart.onClick.AddListener(OnHostStartClick);
			HostShutdown.onClick.AddListener(OnHostShutdownClick);

			// Hide server template
			SearchTemplate.gameObject.SetActive(false);

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

		private void AddServer(string server) {
			// Instantiate a new template, enable it and assign text and color
			Text text = Instantiate(SearchTemplate, SearchTemplate.transform.parent);
			text.gameObject.SetActive(true);
			text.text = server;
		}

		private void ClearServers() {
			foreach (Transform child in SearchTemplate.transform.parent) {
				if (child == SearchTemplate.transform) continue;
				Destroy(child.gameObject);
			}
		}

		private void OnSearchClick() {

			// Make sure search port exists
			if (SearchPort.text == "") {
				SearchStatus.text = "Search port is required.";
				SearchStatus.color = Color.red;
				return;
			}

			// Parse search port
			if (!int.TryParse(SearchPort.text, out int port) || port <= 0 || port >= 65536) {
				SearchStatus.text = "Search port is invalid.";
				SearchStatus.color = Color.red;
				return;
			}

			// Create a host if not created yet
			OnHostStartClick();

			// If host failed to start, update status and exit
			if (Host == null) {
				SearchStatus.text = "Host failed to start.";
				SearchStatus.color = Color.red;
				return;
			}

			// Send broadcast
			Host.SendBroadcast(port, new BroadcastMessage());

			// Clear servers
			ClearServers();

			// Update status
			SearchStatus.text = "Broadcast sent on port " + port + ".";
			SearchStatus.color = Color.green;

		}

		private void OnHostStartClick() {

			// Parse port
			bool portParsed = int.TryParse(HostPort.text, out int port);
			if (HostPort.text != "" && !portParsed || port < 0 || port >= 65536) {
				HostStatus.text = "Bad port: " + HostPort.text;
				HostStatus.color = Color.red;
				return;
			}

			// If host not yet started, create one
			if (Host == null) {

				// Create host config with broadcast enabled
				HostConfig config = new HostConfig() {
					Broadcast = true,
					Port = port,
				};

				// Register host listeners
				HostEvents events = new HostEvents();
				events.OnReceiveBroadcast += OnHostReceiveBroadcast;
				events.OnReceiveUnconnected += OnHostReceiveUnconnected;
				events.OnException += OnHostException;

				try {
					Host = new Host(config, events);
					HostStatus.text = "Listening on port: " + Host.BindAddress.Port;
					HostStatus.color = Color.green;
				} catch (Exception exception) {
					Debug.LogException(exception);
					HostStatus.text = "Exception: " + exception.Message;
					HostStatus.color = Color.red;
				}
				
			}

		}

		private void OnHostShutdownClick() {

			// Dispose of the host
			if (Host != null) Host.Dispose();
			Host = null;

			// Update status
			HostStatus.text = "Status: Not listening";
			HostStatus.color = Color.white;

		}

		private void OnHostReceiveBroadcast(IPEndPoint remote, Reader message) {
			// Broadcast received, answer it with an unconnected message
			Host.SendUnconnected(remote, new BroadcastMessage());
		}

		private void OnHostReceiveUnconnected(IPEndPoint remote, Reader message) {
			Run(() => {
				// Unconnected answer received, add to server list
				AddServer(remote.ToString());
			});
		}

		private void OnHostException(IPEndPoint remote, Exception exception) {
			// Exceptions don't usually indicate any errors, we can ignore them
			Debug.LogException(exception);
		}

	}

}
