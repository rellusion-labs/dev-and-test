using SuperNet.Unity.Core;
using SuperNet.Netcode.Util;
using System;
using UnityEditor;
using UnityEngine;

namespace SuperNet.Unity.Editor {

	[CanEditMultipleObjects]
	[CustomEditor(typeof(NetworkHost))]
	public sealed class NetworkHostEditor : UnityEditor.Editor {

		private string ConnectAddress = "";
		private bool[] Foldouts = null;
		private long LastBytesSent = 0;
		private long LastBytesReceived = 0;
		private float LastTimestamp = 0f;
		private float BandwidthSend = 0f;
		private float BandwidthReceive = 0f;

		public override void OnInspectorGUI() {

			EditorGUI.BeginDisabledGroup(Application.isPlaying);
			base.OnInspectorGUI();
			EditorGUI.EndDisabledGroup();

			if (serializedObject.isEditingMultipleObjects) {
				return;
			}

			NetworkHost target = (NetworkHost)serializedObject.targetObject;

			if (!Application.IsPlaying(target)) {
				return;
			}

			EditorGUILayout.Space();

			Netcode.Transport.Host host = target.GetHost();

			if (host == null || host.Disposed) {

				EditorGUILayout.LabelField("State", EditorStyles.boldLabel);
				{
					EditorGUILayout.HelpBox("Not listening.", MessageType.Info);
					if (GUILayout.Button("Startup")) {
						target.Startup();
					}
				}

			} else {

				EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
				EditorGUI.BeginDisabledGroup(true);
				{
					long bytesSent = host.Statistics.SocketSendBytes;
					long bytesReceived = host.Statistics.SocketReceiveBytes;
					float now = Time.realtimeSinceStartup;
					float timeDelta = now - LastTimestamp;
					if (timeDelta > 1f) {
						long bytesSentDelta = bytesSent - LastBytesSent;
						long bytesReceivedDelta = bytesReceived - LastBytesReceived;
						LastBytesSent = bytesSent;
						LastBytesReceived = bytesReceived;
						LastTimestamp = now;
						BandwidthSend = bytesSentDelta / (timeDelta * 125000f);
						BandwidthReceive = bytesReceivedDelta / (timeDelta * 125000f);
					}
					EditorGUILayout.IntField("Bind Port", host.BindAddress.Port);
					EditorGUILayout.TextField("Bind Address", host.BindAddress.Address.ToString());
					EditorGUILayout.TextField("Upload Bandwidth", BandwidthSend + " mbps");
					EditorGUILayout.TextField("Download Bandwidth", BandwidthReceive + " mbps");
					EditorGUILayout.LongField("Bytes Sent", bytesSent);
					EditorGUILayout.LongField("Bytes Received", bytesReceived);
					EditorGUILayout.LongField("Packets Sent", host.Statistics.SocketSendCount);
					EditorGUILayout.LongField("Packets Received", host.Statistics.SocketReceiveCount);
					EditorGUILayout.LongField("Ticks", host.Ticks);
					EditorGUILayout.IntField("Connections", target.Connections);
				}
				EditorGUI.EndDisabledGroup();

				EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
				{
					if (GUILayout.Button("Shutdown")) {
						target.Shutdown();
					}

					EditorGUILayout.BeginHorizontal();
					{
						ConnectAddress = EditorGUILayout.TextField(ConnectAddress);
						EditorGUI.BeginDisabledGroup(IPResolver.TryParse(ConnectAddress) == null);
						if (GUILayout.Button("Connect")) target.Connect(ConnectAddress);
						EditorGUI.EndDisabledGroup();
					}
					EditorGUILayout.EndHorizontal();
				}

				EditorGUILayout.LabelField("Peers", EditorStyles.boldLabel);
				{
					Netcode.Transport.Peer[] peers = target.GetPeers();
					Array.Resize(ref Foldouts, peers.Length);

					if (peers.Length <= 0) {
						EditorGUILayout.HelpBox("No peers.", MessageType.Info);
					}

					for (int i = 0; i < peers.Length; i++) {
						Netcode.Transport.Peer peer = peers[i];
						Foldouts[i] = EditorGUILayout.Foldout(Foldouts[i], peer.Remote.ToString());
						if (Foldouts[i]) {

							EditorGUI.BeginDisabledGroup(true);
							{
								EditorGUILayout.IntField("RTT", peer.RTT);
								EditorGUILayout.TextField("Connected", peer.Connected ? "true" : "false");
								EditorGUILayout.TextField("Connecting", peer.Connecting ? "true" : "false");
								EditorGUILayout.TextField("Remote Address", peer.Remote.ToString());
								EditorGUILayout.LongField("Receive Bytes", peer.Statistics.PacketReceiveBytes);
								EditorGUILayout.LongField("Receive Packets", peer.Statistics.PacketReceiveCount);
								EditorGUILayout.LongField("Receive Messages", peer.Statistics.MessageReceiveTotal);
								EditorGUILayout.LongField("Receive Duplicated", peer.Statistics.MessageReceiveDuplicated);
								EditorGUILayout.LongField("Receive Unreliables", peer.Statistics.MessageReceiveUnreliable);
								EditorGUILayout.LongField("Receive Acknowledgments", peer.Statistics.MessageReceiveAcknowledge);
								EditorGUILayout.LongField("Receive Pings", peer.Statistics.MessageReceivePing);
								EditorGUILayout.LongField("Sent Bytes", peer.Statistics.PacketSendBytes);
								EditorGUILayout.LongField("Sent Packets", peer.Statistics.PacketSendCount);
								EditorGUILayout.LongField("Sent Messages", peer.Statistics.MessageSendTotal);
								EditorGUILayout.LongField("Sent Duplicated", peer.Statistics.MessageSendDuplicated);
								EditorGUILayout.LongField("Sent Unreliables", peer.Statistics.MessageSendUnreliable);
								EditorGUILayout.LongField("Sent Acknowledgments", peer.Statistics.MessageSendAcknowledge);
								EditorGUILayout.LongField("Sent Pings", peer.Statistics.MessageSendPing);
							}
							EditorGUI.EndDisabledGroup();

							EditorGUILayout.BeginHorizontal();
							{
								EditorGUILayout.PrefixLabel("Tools");
								if (GUILayout.Button("Disconnect")) {
									peer.Disconnect();
								}
								if (GUILayout.Button("Dispose")) {
									peer.Dispose();
								}
							}
							EditorGUILayout.EndHorizontal();

						}
					}

				}

			}

			EditorUtility.SetDirty(target);

		}

	}

}
