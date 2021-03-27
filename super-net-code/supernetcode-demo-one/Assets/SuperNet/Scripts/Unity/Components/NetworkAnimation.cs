using SuperNet.Netcode.Transport;
using SuperNet.Unity.Core;
using SuperNet.Netcode.Util;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

namespace SuperNet.Unity.Components {

	/// <summary>
	/// Synchronizes legacy animation over the network. 
	/// </summary>
	[AddComponentMenu("SuperNet/NetworkAnimation")]
	[HelpURL("https://superversus.com/netcode/api/SuperNet.Unity.Components.NetworkAnimation.html")]
	public sealed class NetworkAnimation : NetworkComponent {

		/// <summary>
		/// Animation component to synchronize. Required.
		/// </summary>
		[FormerlySerializedAs("Animation")]
		[Tooltip("Animation component to synchronize. Required.")]
		public Animation Animation;

		/// <summary>
		/// Which method to synchronize in.
		/// </summary>
		[FormerlySerializedAs("SyncMethod")]
		[Tooltip("Which method to synchronize in.")]
		public NetworkSyncModeMethod SyncMethod;

		/// <summary>
		/// Send updates to remote peers.
		/// </summary>
		[FormerlySerializedAs("Authority")]
		[Tooltip("Send updates to remote peers.")]
		public bool Authority;

		[Header("Receive Configuration")]

		/// <summary>
		/// How many seconds into the past we see the animation at.
		/// </summary>
		[FormerlySerializedAs("ReceiveDelay")]
		[Tooltip("How many seconds into the past we see the animation at.")]
		public float ReceiveDelay;

		[Header("Send Configuration")]

		/// <summary>
		/// Minimum number of seconds to wait before sending an update.
		/// </summary>
		[FormerlySerializedAs("SendIntervalMin")]
		[Tooltip("Minimum number of seconds to wait before sending an update.")] 
		public float SendIntervalMin;

		// Resources
		private AnimationState[] States;
		private System.Diagnostics.Stopwatch Stopwatch;
		private ReaderWriterLockSlim SendLock;
		private bool[] SendEnabled;
		private float[] SendWeight;
		private WrapMode[] SendWrap;
		private float[] SendTime;
		private float[] SendSpeed;
		private int[] SendLayer;
		private string[] SendName;
		private AnimationBlendMode[] SendBlend;
		private float SendLastTime;

		private void Reset() {
			ResetNetworkIdentity();
			Animation = GetComponentInChildren<Animation>();
			SyncMethod = NetworkSyncModeMethod.Update;
			Authority = false;
			ReceiveDelay = 0.1f;
			SendIntervalMin = 0.03f;
		}

		private void Awake() {

			// Check if animation is set
			if (Animation == null) {
				Debug.LogWarning("[SuperNet] [NetworkAnimation] Animation not set.", this);
				enabled = false;
				return;
			}

			// Allocate resources
			List<AnimationState> states = new List<AnimationState>();
			foreach (AnimationState state in Animation) states.Add(state);
			States = states.ToArray();
			Stopwatch = System.Diagnostics.Stopwatch.StartNew();
			SendLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			SendEnabled = new bool[States.Length];
			SendWeight = new float[States.Length];
			SendWrap = new WrapMode[States.Length];
			SendTime = new float[States.Length];
			SendSpeed = new float[States.Length];
			SendLayer = new int[States.Length];
			SendName = new string[States.Length];
			SendBlend = new AnimationBlendMode[States.Length];
			SendLastTime = 0f;

			// Initialize resources
			for (int state = 0; state < States.Length; state++) {
				SendEnabled[state] = States[state].enabled;
				SendWeight[state] = States[state].weight;
				SendWrap[state] = States[state].wrapMode;
				SendTime[state] = States[state].time;
				SendSpeed[state] = States[state].speed;
				SendLayer[state] = States[state].layer;
				SendName[state] = States[state].name;
				SendBlend[state] = States[state].blendMode;
			}

			if (States.Length * 8 >= ushort.MaxValue) {
				Debug.LogWarning("[SuperNet] [NetworkAnimation] Animation has too many states.", this);
				enabled = false;
			}

		}

		private void Update() {
			if (SyncMethod == NetworkSyncModeMethod.Update) SyncUpdate();
		}

		private void LateUpdate() {
			if (SyncMethod == NetworkSyncModeMethod.LateUpdate) SyncUpdate();
		}

		private void FixedUpdate() {
			if (SyncMethod == NetworkSyncModeMethod.FixedUpdate) SyncUpdate();
		}

		private void SyncUpdate() {

			// Check if animation is set
			if (Animation == null) {
				Debug.LogWarning("[SuperNet] [NetworkAnimation] Animation removed.", this);
				enabled = false;
				return;
			}

			// Send updates
			if (Authority) {
				SyncUpdateSend();
			}

		}

		private void SyncUpdateSend() {
			try {
				SendLock.EnterUpgradeableReadLock();

				// Get current time
				float now = (float)Stopwatch.Elapsed.TotalSeconds;

				// If not enough time has passed to send an update, do nothing
				if (now - SendLastTime < SendIntervalMin) {
					return;
				}

				for (int state = 0; state < States.Length; state++) {

					// Get current state
					bool enabled = States[state].enabled;
					float weight = States[state].weight;
					WrapMode wrap = States[state].wrapMode;
					float time = States[state].time;
					float speed = States[state].speed;
					int layer = States[state].layer;
					string name = States[state].name;
					AnimationBlendMode blend = States[state].blendMode;
					float timeNow = time - (now - SendLastTime) * speed;

					// Check enabled
					if (enabled != SendEnabled[state]) {
						ushort header = (ushort)(state * 8 + 0);
						SendNetworkMessageAll(new NetworkMessageAnimationEnabled(header, enabled));
						try {
							SendLock.EnterWriteLock();
							SendEnabled[state] = States[state].enabled;
							SendLastTime = now;
						} finally {
							SendLock.ExitWriteLock();
						}
					}

					// Check weight
					if (Mathf.Abs(weight - SendWeight[state]) > 0.001f) {
						ushort header = (ushort)(state * 8 + 1);
						SendNetworkMessageAll(new NetworkMessageAnimationWeight(header, weight));
						try {
							SendLock.EnterWriteLock();
							SendWeight[state] = weight;
							SendLastTime = now;
						} finally {
							SendLock.ExitWriteLock();
						}
					}

					// Check wrap mode
					if (wrap != SendWrap[state]) {
						ushort header = (ushort)(state * 8 + 2);
						SendNetworkMessageAll(new NetworkMessageAnimationWrap(header, wrap));
						try {
							SendLock.EnterWriteLock();
							SendWrap[state] = wrap;
							SendLastTime = now;
						} finally {
							SendLock.ExitWriteLock();
						}
					}

					// Check time
					if (Mathf.Abs(timeNow - SendTime[state]) > 0.5f) {
						ushort header = (ushort)(state * 8 + 3);
						SendNetworkMessageAll(new NetworkMessageAnimationTime(header, time));
						try {
							SendLock.EnterWriteLock();
							SendTime[state] = time;
							SendLastTime = now;
						} finally {
							SendLock.ExitWriteLock();
						}
					}

					// Check speed
					if (Mathf.Abs(speed - SendSpeed[state]) > 0.001f) {
						ushort header = (ushort)(state * 8 + 4);
						SendNetworkMessageAll(new NetworkMessageAnimationSpeed(header, speed));
						try {
							SendLock.EnterWriteLock();
							SendSpeed[state] = speed;
							SendLastTime = now;
						} finally {
							SendLock.ExitWriteLock();
						}
					}

					// Check layer
					if (layer != SendLayer[state]) {
						ushort header = (ushort)(state * 8 + 5);
						SendNetworkMessageAll(new NetworkMessageAnimationLayer(header, layer));
						try {
							SendLock.EnterWriteLock();
							SendLayer[state] = layer;
							SendLastTime = now;
						} finally {
							SendLock.ExitWriteLock();
						}
					}


					// Check name
					if (string.Compare(name, SendName[state]) != 0) {
						ushort header = (ushort)(state * 8 + 6);
						SendNetworkMessageAll(new NetworkMessageAnimationName(header, name));
						try {
							SendLock.EnterWriteLock();
							SendName[state] = name;
							SendLastTime = now;
						} finally {
							SendLock.ExitWriteLock();
						}
					}


					// Check blend mode
					if (blend != SendBlend[state]) {
						ushort header = (ushort)(state * 8 + 7);
						SendNetworkMessageAll(new NetworkMessageAnimationBlendMode(header, blend));
						try {
							SendLock.EnterWriteLock();
							SendBlend[state] = blend;
							SendLastTime = now;
						} finally {
							SendLock.ExitWriteLock();
						}
					}

				}

			} finally {
				SendLock.ExitUpgradeableReadLock();
			}
		}

		public override void OnNetworkMessage(Peer peer, Reader reader, HostTimestamp timestamp) {

			// If receiving is not enabled, do nothing
			if (Authority) {
				Debug.LogWarning(string.Format(
					"[SuperNet] [NetworkAnimation] Recieved NetworkID {0} update from {1}. Ignoring.",
					NetworkIdentity, peer.Remote
				), this);
				return;
			}

			// Get message time
			float now = (float)Stopwatch.Elapsed.TotalSeconds;
			float age = Mathf.Max(0f, (float)timestamp.ElapsedSeconds);
			float delay = Mathf.Max(0f, ReceiveDelay - age);

			// Read header
			ushort header = reader.ReadUInt16();
			int state = header / 8;

			// Update animation
			switch (header % 8) {
				case 0:
					bool enabled = reader.ReadBoolean();
					Run(() => States[state].enabled = enabled, delay);
					break;
				case 1:
					float weight = reader.ReadSingle();
					Run(() => States[state].weight = weight, delay);
					break;
				case 2:
					WrapMode wrap = (WrapMode)reader.ReadByte();
					Run(() => States[state].wrapMode = wrap, delay);
					break;
				case 3:
					float time = reader.ReadSingle();
					Run(() => States[state].time = time + age * States[state].speed, delay);
					break;
				case 4:
					float speed = reader.ReadSingle();
					Run(() => States[state].speed = speed, delay);
					break;
				case 5:
					int layer = reader.ReadInt32();
					Run(() => States[state].layer = layer, delay);
					break;
				case 6:
					string name = reader.ReadString();
					Run(() => States[state].name = name, delay);
					break;
				case 7:
					AnimationBlendMode blend = (AnimationBlendMode)reader.ReadByte();
					Run(() => States[state].blendMode = blend, delay);
					break;
			}

		}

		public override void OnNetworkPeerRegister(Peer peer) {

			// If sending is not enabled, do nothing
			if (!Authority) {
				return;
			}

			// If local connection, ignore
			if (Host.IsLocal(peer.Remote)) {
				return;
			}

			try {
				SendLock.EnterReadLock();

				for (int state = 0; state < States.Length; state++) {

					// Get current state
					bool enabled = SendEnabled[state];
					float weight = SendWeight[state];
					WrapMode wrap = SendWrap[state];
					float time = SendTime[state];
					float speed = SendSpeed[state];
					int layer = SendLayer[state];
					string name = SendName[state];
					AnimationBlendMode blend = SendBlend[state];
					ushort header = (ushort)(state * 8);

					// Send state
					SendNetworkMessage(peer, new NetworkMessageAnimationEnabled((ushort)(header + 0), enabled));
					SendNetworkMessage(peer, new NetworkMessageAnimationWeight((ushort)(header + 1), weight));
					SendNetworkMessage(peer, new NetworkMessageAnimationWrap((ushort)(header + 2), wrap));
					SendNetworkMessage(peer, new NetworkMessageAnimationTime((ushort)(header + 3), time));
					SendNetworkMessage(peer, new NetworkMessageAnimationSpeed((ushort)(header + 4), speed));
					SendNetworkMessage(peer, new NetworkMessageAnimationLayer((ushort)(header + 5), layer));
					SendNetworkMessage(peer, new NetworkMessageAnimationName((ushort)(header + 6), name));
					SendNetworkMessage(peer, new NetworkMessageAnimationBlendMode((ushort)(header + 7), blend));

				}

			} finally {
				SendLock.ExitReadLock();
			}

		}

		public override void OnNetworkRegister() {

			// If sending is not enabled, do nothing
			if (!Authority) {
				return;
			}

			try {
				SendLock.EnterReadLock();

				for (int state = 0; state < States.Length; state++) {

					// Get current state
					bool enabled = SendEnabled[state];
					float weight = SendWeight[state];
					WrapMode wrap = SendWrap[state];
					float time = SendTime[state];
					float speed = SendSpeed[state];
					int layer = SendLayer[state];
					string name = SendName[state];
					AnimationBlendMode blend = SendBlend[state];
					ushort header = (ushort)(state * 8);

					// Send state
					SendNetworkMessageAll(new NetworkMessageAnimationEnabled((ushort)(header + 0), enabled));
					SendNetworkMessageAll(new NetworkMessageAnimationWeight((ushort)(header + 1), weight));
					SendNetworkMessageAll(new NetworkMessageAnimationWrap((ushort)(header + 2), wrap));
					SendNetworkMessageAll(new NetworkMessageAnimationTime((ushort)(header + 3), time));
					SendNetworkMessageAll(new NetworkMessageAnimationSpeed((ushort)(header + 4), speed));
					SendNetworkMessageAll(new NetworkMessageAnimationLayer((ushort)(header + 5), layer));
					SendNetworkMessageAll(new NetworkMessageAnimationName((ushort)(header + 6), name));
					SendNetworkMessageAll(new NetworkMessageAnimationBlendMode((ushort)(header + 7), blend));

				}

			} finally {
				SendLock.ExitReadLock();
			}

		}

		private struct NetworkMessageAnimationEnabled : INetworkMessage {
			
			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly ushort Header;
			public readonly bool Enabled;

			public NetworkMessageAnimationEnabled(ushort header, bool enabled) {
				Header = header;
				Enabled = enabled;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write(Enabled);
			}

		}

		private struct NetworkMessageAnimationWeight : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly ushort Header;
			public readonly float Weight;

			public NetworkMessageAnimationWeight(ushort header, float weight) {
				Header = header;
				Weight = weight;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write(Weight);
			}

		}

		private struct NetworkMessageAnimationWrap : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly ushort Header;
			public readonly WrapMode Wrap;

			public NetworkMessageAnimationWrap(ushort header, WrapMode wrap) {
				Header = header;
				Wrap = wrap;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write((byte)Wrap);
			}

		}

		private struct NetworkMessageAnimationTime : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly ushort Header;
			public readonly float Time;

			public NetworkMessageAnimationTime(ushort header, float time) {
				Header = header;
				Time = time;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write(Time);
			}

		}

		private struct NetworkMessageAnimationSpeed : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly ushort Header;
			public readonly float Speed;

			public NetworkMessageAnimationSpeed(ushort header, float speed) {
				Header = header;
				Speed = speed;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write(Speed);
			}

		}

		private struct NetworkMessageAnimationLayer : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly ushort Header;
			public readonly int Layer;

			public NetworkMessageAnimationLayer(ushort header, int layer) {
				Header = header;
				Layer = layer;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write(Layer);
			}

		}

		private struct NetworkMessageAnimationName : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly ushort Header;
			public readonly string Name;

			public NetworkMessageAnimationName(ushort header, string name) {
				Header = header;
				Name = name;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write(Name);
			}

		}

		private struct NetworkMessageAnimationBlendMode : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly ushort Header;
			public readonly AnimationBlendMode Mode;

			public NetworkMessageAnimationBlendMode(ushort header, AnimationBlendMode mode) {
				Header = header;
				Mode = mode;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write((byte)Mode);
			}

		}

	}

}
