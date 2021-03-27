using SuperNet.Netcode.Util;
using System;
using System.Net;
using UnityEngine;
using UnityEngine.UI;

namespace SuperNet.Examples.Arena {

	public class Menu : MonoBehaviour {

		// UI objects
		public GameObject MenuCanvas;
		public InputField MenuServersRelay;
		public Text MenuServersStatus;
		public Button MenuServersRefresh;
		public GameObject MenuServersTemplate;
		public InputField MenuHostRelay;
		public InputField MenuHostPort;
		public Button MenuHostLaunch;
		public InputField MenuConnectRelay;
		public InputField MenuConnectAddress;
		public Button MenuConnectLaunch;
		public GameObject EscapeCanvas;
		public Button EscapeBackground;
		public Button EscapeResume;
		public Button EscapeExit;
		public GameObject GameCanvas;
		public Text GameStatus;
		public GameObject ErrorCanvas;
		public Button ErrorConfirm;
		public Text ErrorStatus;
		public GameObject LoadCanvas;
		public Button LoadCancel;
		public Text LoadStatus;

		// Network handlers
		public ArenaSpawners ArenaSpawners;
		public ArenaClient ArenaClient;
		public ArenaServer ArenaServer;
		public ArenaRelay ArenaRelay;
		
		private void Reset() {
			MenuCanvas = transform.Find("Menu").gameObject;
			MenuServersRelay = transform.Find("Menu/Left/RelayInput").GetComponent<InputField>();
			MenuServersStatus = transform.Find("Menu/Left/Status").GetComponent<Text>();
			MenuServersRefresh = transform.Find("Menu/Left/Refresh").GetComponent<Button>();
			MenuServersTemplate = transform.Find("Menu/Left/Table/Viewport/Content/Template").gameObject;
			MenuHostRelay = transform.Find("Menu/Right/Host/Relay/Input").GetComponent<InputField>();
			MenuHostPort = transform.Find("Menu/Right/Host/Port/Input").GetComponent<InputField>();
			MenuHostLaunch = transform.Find("Menu/Right/Host/Launch").GetComponent<Button>();
			MenuConnectRelay = transform.Find("Menu/Right/Connect/Relay/Input").GetComponent<InputField>();
			MenuConnectAddress = transform.Find("Menu/Right/Connect/Address/Input").GetComponent<InputField>();
			MenuConnectLaunch = transform.Find("Menu/Right/Connect/Launch").GetComponent<Button>();
			EscapeCanvas = transform.Find("Escape").gameObject;
			EscapeBackground = transform.Find("Escape/Background").GetComponent<Button>();
			EscapeResume = transform.Find("Escape/Layout/Resume").GetComponent<Button>();
			EscapeExit = transform.Find("Escape/Layout/Exit").GetComponent<Button>();
			GameCanvas = transform.Find("Game").gameObject;
			GameStatus = transform.Find("Game/Status/Text").GetComponent<Text>();
			ErrorCanvas = transform.Find("Error").gameObject;
			ErrorConfirm = transform.Find("Error/Confirm").GetComponent<Button>();
			ErrorStatus = transform.Find("Error/Status").GetComponent<Text>();
			LoadCanvas = transform.Find("Load").gameObject;
			LoadCancel = transform.Find("Load/Cancel").GetComponent<Button>();
			LoadStatus = transform.Find("Load/Status").GetComponent<Text>();
			ArenaSpawners = FindObjectOfType<ArenaSpawners>();
			ArenaClient = FindObjectOfType<ArenaClient>();
			ArenaServer = FindObjectOfType<ArenaServer>();
			ArenaRelay = FindObjectOfType<ArenaRelay>();
		}

		private void Start() {

			// Register button listeners
			MenuServersRefresh.onClick.AddListener(OnClickServersRefresh);
			MenuHostLaunch.onClick.AddListener(OnClickHostLaunch);
			MenuConnectLaunch.onClick.AddListener(OnClickConnectLaunch);
			EscapeBackground.onClick.AddListener(OnClickEscapeBackground);
			EscapeResume.onClick.AddListener(OnClickEscapeResume);
			EscapeExit.onClick.AddListener(OnClickEscapeExit);
			ErrorConfirm.onClick.AddListener(OnClickErrorConfirm);
			LoadCancel.onClick.AddListener(OnClickLoadCancel);

			// Clear server list and open menu
			ServerListDeleteAll("No connection");
			OpenCanvasMenu();

		}

		private void Update() {

			// If escape is pressed while in game, open escape menu
			if (Input.GetKeyDown(KeyCode.Escape) && GameCanvas.activeInHierarchy) {
				OpenCanvasEscape();
			}

		}

		private void OpenCanvasMenu() {
			// Open the main menu and close everything else
			MenuCanvas.SetActive(true);
			EscapeCanvas.SetActive(false);
			GameCanvas.SetActive(false);
			ErrorCanvas.SetActive(false);
			LoadCanvas.SetActive(false);
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		private void OpenCanvasEscape() {
			// Open the escape menu and close everything else
			MenuCanvas.SetActive(false);
			EscapeCanvas.SetActive(true);
			GameCanvas.SetActive(false);
			ErrorCanvas.SetActive(false);
			LoadCanvas.SetActive(false);
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		public void OpenCanvasGame() {
			// Open the game ui and close everything else
			MenuCanvas.SetActive(false);
			EscapeCanvas.SetActive(false);
			GameCanvas.SetActive(true);
			ErrorCanvas.SetActive(false);
			LoadCanvas.SetActive(false);
			GameStatus.text = ArenaClient.GetConnectionStatus();
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}

		public void OpenCanvasError(string error) {
			// Open error screen and close everything else
			MenuCanvas.SetActive(false);
			EscapeCanvas.SetActive(false);
			GameCanvas.SetActive(false);
			ErrorCanvas.SetActive(true);
			LoadCanvas.SetActive(false);
			ErrorStatus.text = error;
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		private void OpenCanvasLoad(string status) {
			// Open loading screen and close everything else
			MenuCanvas.SetActive(false);
			EscapeCanvas.SetActive(false);
			GameCanvas.SetActive(false);
			ErrorCanvas.SetActive(false);
			LoadCanvas.SetActive(true);
			LoadStatus.text = status;
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		public void ServerListDeleteAll(string status) {

			// Hide template
			MenuServersTemplate.SetActive(false);

			// Delete all template instances
			foreach (Transform child in MenuServersTemplate.transform.parent) {
				if (child == MenuServersTemplate.transform) continue;
				Destroy(child.gameObject);
			}

			// Set status
			MenuServersStatus.text = status;

		}

		public void ServerListCreate(RelayServerListResponse message, IPEndPoint relay) {
			if (message.Servers.Count <= 0) {
				// No servers
				ServerListDeleteAll("No servers");
			} else {
				// Create server rows from template
				ServerListDeleteAll("");
				foreach (string server in message.Servers) {
					GameObject instance = Instantiate(MenuServersTemplate, MenuServersTemplate.transform.parent);
					Button connect = instance.transform.Find("Connect").GetComponent<Button>();
					Text address = instance.transform.Find("Address").GetComponent<Text>();
					address.text = server;
					connect.onClick.AddListener(() => OnClickConnectLaunch(server, relay));
					instance.SetActive(true);
				}
			}
		}

		private void OnClickServersRefresh() {

			// Resolve relay address
			IPEndPoint relay = ResolveAddress(MenuServersRelay.text, out Exception relayException);
			if (relay == null || relayException != null) {
				ServerListDeleteAll("Bad relay address");
				return;
			}

			// Request server list from the relay
			if (!ArenaRelay.RequestServerList(relay)) {
				ServerListDeleteAll("Failed to request server list");
				return;
			}

			// Show notice in the server list
			ServerListDeleteAll("Requesting servers...");

		}

		private void OnClickHostLaunch() {

			// Parse port
			bool portEmpty = string.IsNullOrEmpty(MenuHostPort.text);
			bool portParsed = int.TryParse(MenuHostPort.text, out int portParseValue);
			int port = portEmpty ? 0 : (portParsed ? portParseValue : -1);
			if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort) {
				OpenCanvasError("Bad server port");
				return;
			}

			// Resolve host relay address
			IPEndPoint relay = ResolveAddress(MenuHostRelay.text, out Exception relayException);
			if (relay == null && relayException != null) {
				OpenCanvasError("Bad relay address");
				return;
			}

			// Launch server
			if (!ArenaServer.LaunchServer(port)) {
				OpenCanvasError("Server failed to start.");
				return;
			}
			
			// Get local and loopback addresses
			IPEndPoint loopback = ArenaServer.Server.GetLoopbackAddress();
			IPEndPoint local = ArenaServer.Server.GetLocalAddress();

			if (relay == null) {

				// No relay provided, disconnect
				ArenaRelay.Disconnect();

			} else {

				// Add server to relay server list
				if (!ArenaRelay.AddServer(relay, local)) {
					ArenaServer.Shutdown();
					OpenCanvasError("Failed to add server to list");
					return;
				}

			}

			// Connect client to server we just launched
			if (!ArenaClient.LaunchClient(loopback)) {
				ArenaServer.Shutdown();
				OpenCanvasError("Failed to launch client.");
				return;
			}

			// Open loading screen
			OpenCanvasLoad("Launching server...");

		}

		private void OnClickConnectLaunch(string connect, IPEndPoint relay) {

			// Resolve connect address
			IPEndPoint server = ResolveAddress(connect, out _);
			if (server == null) {
				OpenCanvasError("Bad server address");
				return;
			}

			// Connect to server
			OnClickConnectLaunch(server, relay);

		}

		private void OnClickConnectLaunch() {

			// Resolve connect address
			IPEndPoint server = ResolveAddress(MenuConnectAddress.text, out _);
			if (server == null) {
				OpenCanvasError("Bad server address");
				return;
			}

			// Resolve client relay address
			IPEndPoint relay = ResolveAddress(MenuConnectRelay.text, out Exception relayException);
			if (relay == null && relayException != null) {
				OpenCanvasError("Bad relay address");
				return;
			}

			// Connect
			OnClickConnectLaunch(server, relay);

		}

		private void OnClickConnectLaunch(IPEndPoint server, IPEndPoint relay) {
			
			if (relay == null) {

				// No relay provided, disconnect
				ArenaRelay.Disconnect();

			} else {

				// Notify server via relay
				if (!ArenaRelay.NotifyServer(relay, server)) {
					OpenCanvasError("Failed to notify relay.");
					return;
				}

			}

			// Launch client
			if (!ArenaClient.LaunchClient(server)) {
				OpenCanvasError("Failed to launch client.");
				return;
			}

			// Open connecting screen
			OpenCanvasLoad("Connecting...");

		}

		private void OnClickEscapeBackground() {
			// Resume game
			OpenCanvasGame();
		}

		private void OnClickEscapeResume() {
			// Resume game
			OpenCanvasGame();
		}

		private void OnClickEscapeExit() {
			// Shutdown game and open menu
			ArenaRelay.RemoveServer();
			ArenaClient.Shutdown();
			ArenaServer.Shutdown();
			OpenCanvasMenu();
		}

		private void OnClickErrorConfirm() {
			// Shutdown game and open menu
			ArenaRelay.RemoveServer();
			ArenaClient.Shutdown();
			ArenaServer.Shutdown();
			OpenCanvasMenu();
		}

		private void OnClickLoadCancel() {
			// Shutdown game and open menu
			ArenaRelay.RemoveServer();
			ArenaClient.Shutdown();
			ArenaServer.Shutdown();
			OpenCanvasMenu();
		}

		private IPEndPoint ResolveAddress(string host, out Exception exception) {

			// If nothing to resolve, return null
			if (string.IsNullOrWhiteSpace(host)) {
				exception = null;
				return null;
			}

			// Resolve address with IPResolver
			// This uses IPResolver.Resolve() which blocks until the resolution finishes
			// A better way to do this would be via a callback
			try {
				exception = null;
				return IPResolver.Resolve(host);
			} catch (Exception e) {
				Debug.LogException(e);
				exception = e;
				return null;
			}

		}

	}

}
