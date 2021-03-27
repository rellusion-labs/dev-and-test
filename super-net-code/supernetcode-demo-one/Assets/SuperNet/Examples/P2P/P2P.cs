using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace SuperNet.Examples.P2P {

	public class P2P : MonoBehaviour {

		// UI Elements
		public InputField HostPort;
		public Button HostCreate;
		public Button HostShutdown;
		public Text HostStatus;
		public Button IPObtain;
		public Text IPStatus;
		public InputField PunchAddress;
		public Button PunchConnect;
		public Text PunchStatus;
		public Text PunchPing;

		// Netcode
		private Host Host;
		private Peer Peer;

		// Queue used to run actions on the main Unity thread
		private static readonly Thread RunThread = Thread.CurrentThread;
		private readonly ConcurrentQueue<Action> RunQueue = new ConcurrentQueue<Action>();

		public void Start() {
			
			// Register button click events
			HostCreate.onClick.AddListener(OnHostCreateClick);
			HostShutdown.onClick.AddListener(OnHostShutdownClick);
			IPObtain.onClick.AddListener(OnIPObtainClick);
			PunchConnect.onClick.AddListener(OnPunchConnectClick);

			// Reset everything
			OnHostShutdownClick();

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

		private void OnHostCreateClick() {

			// Dispose any existing hosts
			if (Host != null) Host.Dispose();

			// Parse port
			bool portParsed = int.TryParse(HostPort.text, out int port);
			if (HostPort.text != "" && !portParsed || port < 0 || port >= 65536) {
				HostStatus.text = "Bad port: " + HostPort.text;
				HostStatus.color = Color.red;
				return;
			}

			// Create host
			try {
				Host = new Host(new HostConfig() { Port = port }, null);
				HostStatus.text = "Listening on port: " + Host.BindAddress.Port;
				HostStatus.color = Color.green;
			} catch (Exception exception) {
				Debug.LogException(exception);
				HostStatus.text = "Exception: " + exception.Message;
				HostStatus.color = Color.red;
			}

			// Enable next stage
			HostCreate.interactable = false;
			IPObtain.interactable = true;

		}

		private void OnHostShutdownClick() {

			if (Peer != null) Peer.Dispose();
			if (Host != null) Host.Dispose();
			Host = null;
			Peer = null;

			// Disable all stages
			HostCreate.interactable = true;
			IPObtain.interactable = false;
			PunchConnect.interactable = false;
			PunchAddress.interactable = false;

			// Reset all statuses
			HostStatus.text = "Status: Not listening";
			HostStatus.color = Color.white;
			IPStatus.text = "IP: Unknown";
			IPStatus.color = Color.white;
			PunchStatus.text = "Status: Not connected";
			PunchStatus.color = Color.white;
			PunchPing.text = "Ping: Unknown";

		}

		private void OnIPObtainClick() {

			// If host not running, reset
			if (Host == null) {
				OnHostShutdownClick();
				return;
			}

			// Update status
			IPStatus.text = "Obtaining IP...";
			IPStatus.color = Color.white;

			// Make sure we can only click obtain once
			IPObtain.interactable = false;

			// Start obtaining IP
			StartCoroutine(GetPublicIP());

		}

		private void OnIPObtainComplete(IPAddress ip) {

			// If host not running, do nothing
			if (Host == null) return;

			// If no ip obtained, display error
			if (ip == null) {
				IPStatus.text = "Failed to obtain IP";
				IPStatus.color = Color.red;
				IPObtain.interactable = true;
				return;
			}

			// Display obtained IP
			IPStatus.text = ip.ToString() + ":" + Host.BindAddress.Port.ToString();
			IPStatus.color = Color.green;

			// Enable next stage
			IPObtain.interactable = false;
			PunchConnect.interactable = true;
			PunchAddress.interactable = true;

		}

		private void OnPunchConnectClick() {

			// If host not running or already connecting, reset
			if (Host == null || Peer != null) {
				OnHostShutdownClick();
				return;
			}

			// Parse address
			IPEndPoint address = IPResolver.TryParse(PunchAddress.text);
			if (address == null) {
				PunchStatus.text = "Bad address: " + PunchAddress.text;
				PunchStatus.color = Color.red;
				return;
			}

			// Register peer events
			PeerEvents events = new PeerEvents();
			events.OnConnect += OnPeerConnect;
			events.OnDisconnect += OnPeerDisconnect;
			events.OnUpdateRTT += OnPeerUpdateRTT;

			// Start connecting
			Peer = Host.Connect(address, new PeerConfig() {
				ConnectAttempts = 40,
				ConnectDelay = 200,
			}, events);

			// Update status
			PunchStatus.text = "Connecting to: " + address;
			PunchStatus.color = Color.white;

			// Make sure we can't connect again while connecting
			PunchConnect.interactable = false;
			PunchAddress.interactable = false;

		}

		private void OnPeerConnect(Peer peer) {
			Run(() => {

				// Succesfully connected, update status
				PunchStatus.text = "Connected to: " + peer.Remote;
				PunchStatus.color = Color.green;

			});
		}

		private void OnPeerDisconnect(Peer peer, Reader message, DisconnectReason reason, Exception exception) {
			Run(() => {

				// Update status
				PunchStatus.text = "Disconnected: " + reason;
				PunchStatus.color = Color.red;
				Peer = null;

				// Allow connecting again
				PunchConnect.interactable = true;
				PunchAddress.interactable = true;

			});
		}

		private void OnPeerUpdateRTT(Peer peer, ushort rtt) {
			Run(() => {

				// Update ping display text in the Unity thread
				PunchPing.text = "Ping: " + rtt;

			});
		}

		private static readonly string[] PublicIPServices = new string[] {
			"https://icanhazip.com",
			"https://ipinfo.io/ip",
			"https://bot.whatismyipaddress.com/",
			"https://api.ipify.org/",
			"https://checkip.amazonaws.com/",
			"https://wtfismyip.com/text",
		};

		private IEnumerator GetPublicIP() {
			foreach (string url in PublicIPServices) {
				Debug.Log("Obtaining public IP from " + url);
				using (UnityWebRequest www = UnityWebRequest.Get(url)) {
					UnityWebRequestAsyncOperation request = www.SendWebRequest();
					yield return request;
					if (Host == null) yield break;
					if (www.isDone && string.IsNullOrWhiteSpace(www.error)) {
						string ip = www.downloadHandler.text.Trim();
						if (IPAddress.TryParse(ip, out IPAddress parsed)) {
							OnIPObtainComplete(parsed);
							yield break;
						} else {
							Debug.LogWarning("Public IP '" + ip + "' from " + url + " is invalid.");
						}
					} else {
						Debug.LogWarning("Failed to get public IP from " + url + ": " + www.error);
					}
				}
			}
			OnIPObtainComplete(null);
			yield break;
		}

	}

}
