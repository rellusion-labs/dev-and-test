using SuperNet.Netcode.Transport;
using SuperNet.Unity.Core;
using SuperNet.Netcode.Util;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

namespace SuperNet.Unity.Components {

	/// <summary>
	/// Synchronizes an animator over the network. 
	/// </summary>
	[AddComponentMenu("SuperNet/NetworkAnimator")]
	[HelpURL("https://superversus.com/netcode/api/SuperNet.Unity.Components.NetworkAnimator.html")]
	public sealed class NetworkAnimator : NetworkComponent {

		/// <summary>
		/// Animator component to synchronize. Required.
		/// </summary>
		[FormerlySerializedAs("Animator")]
		[Tooltip("Animator component to synchronize. Required.")]
		public Animator Animator;

		/// <summary>
		/// Which method to synchronize in.
		/// </summary>
		[FormerlySerializedAs("SyncMethod")]
		[Tooltip("Which method to synchronize in.")]
		public NetworkSyncModeMethod SyncMethod;

		/// <summary>
		/// Syncronize animator parameters.
		/// </summary>
		[FormerlySerializedAs("SyncParameters")]
		[Tooltip("Syncronize animator parameters.")]
		public bool SyncParameters;

		/// <summary>
		/// Syncronize animator states.
		/// </summary>
		[FormerlySerializedAs("SyncStates")]
		[Tooltip("Syncronize animator states.")]
		public bool SyncStates;

		/// <summary>
		/// Send updates to remote peers.
		/// </summary>
		[FormerlySerializedAs("Authority")]
		[Tooltip("Send updates to remote peers.")]
		public bool Authority;

		[Header("Receive Configuration")]

		/// <summary>
		/// How many seconds into the past we see the animator at.
		/// </summary>
		[FormerlySerializedAs("ReceiveDelay")]
		[Tooltip("How many seconds into the past we see the animator at.")]
		public float ReceiveDelay;

		[Header("Send Configuration")]

		/// <summary>
		/// Minimum number of seconds to wait before sending an update.
		/// </summary>
		[FormerlySerializedAs("SendIntervalMin")]
		[Tooltip("Minimum number of seconds to wait before sending an update.")]
		public float SendIntervalMin;

		// Resources
		private int Layers;
		private AnimatorControllerParameter[] Parameters;
		private System.Diagnostics.Stopwatch Stopwatch;
		private ReaderWriterLockSlim SendLock;
		private float[] SendParametersFloat;
		private bool[] SendParametersBool;
		private int[] SendParametersInt;
		private int[] SendStates;
		private float SendTime;

		private void Reset() {
			ResetNetworkIdentity();
			Animator = GetComponentInChildren<Animator>();
			SyncMethod = NetworkSyncModeMethod.Update;
			SyncParameters = true;
			SyncStates = true;
			Authority = false;
			ReceiveDelay = 0.1f;
			SendIntervalMin = 0.03f;
		}

		private void Awake() {

			// Check if animator is set
			if (Animator == null) {
				Debug.LogWarning("[SuperNet] [NetworkAnimator] Animator not set.", this);
				enabled = false;
				return;
			}

			// Initialize
			Layers = Animator.layerCount;
			List<AnimatorControllerParameter> parameters = new List<AnimatorControllerParameter>();
			foreach (AnimatorControllerParameter parameter in Animator.parameters) {
				if (Animator.IsParameterControlledByCurve(parameter.nameHash)) continue;
				bool isInt = parameter.type == AnimatorControllerParameterType.Int;
				bool isBool = parameter.type == AnimatorControllerParameterType.Bool;
				bool isFloat = parameter.type == AnimatorControllerParameterType.Float;
				if (isInt || isBool || isFloat) parameters.Add(parameter);
			}
			Parameters = parameters.ToArray();
			Stopwatch = System.Diagnostics.Stopwatch.StartNew();
			SendLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			SendParametersFloat = new float[Parameters.Length];
			SendParametersBool = new bool[Parameters.Length];
			SendParametersInt = new int[Parameters.Length];
			SendStates = new int[Layers];
			SendTime = 0f;

			// Get current local state
			for (int layer = 0; layer < Layers; layer++) SendStates[layer] = GetAnimatorState(layer).fullPathHash;
			for (int i = 0; i < Parameters.Length; i++) {
				if (Parameters[i].type == AnimatorControllerParameterType.Int)
					SendParametersInt[i] = Animator.GetInteger(Parameters[i].nameHash);
				if (Parameters[i].type == AnimatorControllerParameterType.Bool)
					SendParametersBool[i] = Animator.GetBool(Parameters[i].nameHash);
				if (Parameters[i].type == AnimatorControllerParameterType.Float)
					SendParametersFloat[i] = Animator.GetFloat(Parameters[i].nameHash);
			}

			// Make sure parameters and layer states can be sent over network
			if (Layers + Parameters.Length > 254) {
				Debug.LogWarning("[SuperNet] [NetworkAnimator] Animator has too many parameters.", this);
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

			// Check if transform was removed
			if (Animator == null) {
				Debug.LogWarning("[SuperNet] [NetworkAnimator] Animator removed.", this);
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
				if (now - SendTime < SendIntervalMin) {
					return;
				}

				// Check if any layer states changed
				if (SyncStates) {
					for (int layer = 0; layer < Layers; layer++) {
						AnimatorStateInfo info = GetAnimatorState(layer);
						int hash = info.fullPathHash;
						if (hash != SendStates[layer]) {
							byte header = (byte)layer;
							float time = info.normalizedTime;
							SendNetworkMessageAll(new NetworkMessageAnimatorHash(header, hash, time));
							try {
								SendLock.EnterWriteLock();
								SendStates[layer] = hash;
								SendTime = now;
							} finally {
								SendLock.ExitWriteLock();
							}
						}
					}
				}

				// Check if any parameters changed
				if (SyncParameters) {
					for (int i = 0; i < Parameters.Length; i++) {
						if (Parameters[i].type == AnimatorControllerParameterType.Int) {
							int value = Animator.GetInteger(Parameters[i].nameHash);
							if (value != SendParametersInt[i]) {
								byte header = (byte)(Layers + i);
								SendNetworkMessageAll(new NetworkMessageAnimatorInt(header, value));
								try {
									SendLock.EnterWriteLock();
									SendParametersInt[i] = value;
									SendTime = now;
								} finally {
									SendLock.ExitWriteLock();
								}
							}
						} else if (Parameters[i].type == AnimatorControllerParameterType.Bool) {
							bool value = Animator.GetBool(Parameters[i].nameHash);
							if (value != SendParametersBool[i]) {
								byte header = (byte)(Layers + i);
								SendNetworkMessageAll(new NetworkMessageAnimatorBool(header, value));
								try {
									SendLock.EnterWriteLock();
									SendParametersBool[i] = value;
									SendTime = now;
								} finally {
									SendLock.ExitWriteLock();
								}
							}
						} else if (Parameters[i].type == AnimatorControllerParameterType.Float) {
							float value = Animator.GetFloat(Parameters[i].nameHash);
							if (Mathf.Abs(value - SendParametersFloat[i]) > 0.001f) {
								byte header = (byte)(Layers + i);
								SendNetworkMessageAll(new NetworkMessageAnimatorFloat(header, value));
								try {
									SendLock.EnterWriteLock();
									SendParametersFloat[i] = value;
									SendTime = now;
								} finally {
									SendLock.ExitWriteLock();
								}
							}
						}
					}
				}
				
			} finally {
				SendLock.ExitUpgradeableReadLock();
			}
		}

		/// <summary>
		/// Sets a trigger locally and sends it to everybody on the network regardless of authority.
		/// </summary>
		/// <param name="triggerName">Trigger name.</param>
		public void SetTrigger(string triggerName) {
			SetTrigger(Animator.StringToHash(triggerName));
		}

		/// <summary>
		/// Sets a trigger locally and sends it to everybody on the network regardless of authority.
		/// </summary>
		/// <param name="id">Trigger hash ID.</param>
		public void SetTrigger(int id) {

			// Set trigger
			Run(() => Animator.SetTrigger(id));

			// Send trigger to everybody
			SendNetworkMessageAll(new NetworkMessageAnimatorInt(0xFF, id));

		}

		public override void OnNetworkMessage(Peer peer, Reader reader, HostTimestamp timestamp) {

			// If receiving is not enabled, do nothing
			if (Authority) {
				Debug.LogWarning(string.Format(
					"[SuperNet] [NetworkAnimator] Recieved NetworkID {0} update from {1}. Ignoring.",
					NetworkIdentity, peer.Remote
				), this);
				return;
			}

			// Get message time
			float now = (float)Stopwatch.Elapsed.TotalSeconds;
			float delay = Mathf.Max(0f, ReceiveDelay - Mathf.Max(0f, (float)timestamp.ElapsedSeconds));

			// Read header
			byte header = reader.ReadByte();

			if (header == 0xFF) {

				// Update trigger
				int hash = reader.ReadInt32();
				Run(() => Animator.SetTrigger(hash), delay);

			} else if (header < Layers) {

				// Update layer state
				if (SyncStates) {
					int hash = reader.ReadInt32();
					if (reader.Available >= 4) {
						float normalizedTime = reader.ReadSingle();
						Run(() => Animator.Play(hash, header, normalizedTime), delay);
					} else {
						Run(() => Animator.Play(hash, header), delay);
					}
				}

			} else if (header - Layers < Parameters.Length) {

				// Update parameter
				if (SyncParameters) {
					AnimatorControllerParameter parameter = Parameters[header - Layers];
					if (parameter.type == AnimatorControllerParameterType.Int) {
						int value = reader.ReadInt32();
						Run(() => Animator.SetInteger(parameter.nameHash, value), delay);
					} else if (parameter.type == AnimatorControllerParameterType.Bool) {
						bool value = reader.ReadBoolean();
						Run(() => Animator.SetBool(parameter.nameHash, value), delay);
					} else if (parameter.type == AnimatorControllerParameterType.Float) {
						float value = reader.ReadSingle();
						Run(() => Animator.SetFloat(parameter.nameHash, value), delay);
					}
				}

			} else {

				// Header extends beyond the last parameter
				Debug.LogWarning(string.Format(
					"[SuperNet] [NetworkAnimator] Recieved NetworkID {0} update from {1} with invalid header {2}. Ignoring.",
					NetworkIdentity, peer.Remote, header
				), this);

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

				// Send layer states
				for (int layer = 0; layer < Layers; layer++) {
					byte header = (byte)layer;
					int hash = SendStates[layer];
					SendNetworkMessage(peer, new NetworkMessageAnimatorInt(header, hash));
				}

				// Send parameters
				for (int i = 0; i < Parameters.Length; i++) {
					byte header = (byte)(Layers + i);
					if (Parameters[i].type == AnimatorControllerParameterType.Int) {
						int value = SendParametersInt[i];
						SendNetworkMessage(peer, new NetworkMessageAnimatorInt(header, value));
					} else if (Parameters[i].type == AnimatorControllerParameterType.Bool) {
						bool value = SendParametersBool[i];
						SendNetworkMessage(peer, new NetworkMessageAnimatorBool(header, value));
					} else if (Parameters[i].type == AnimatorControllerParameterType.Float) {
						float value = SendParametersFloat[i];
						SendNetworkMessage(peer, new NetworkMessageAnimatorFloat(header, value));
					}
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

				// Send layer states
				for (int layer = 0; layer < Layers; layer++) {
					byte header = (byte)layer;
					int hash = SendStates[layer];
					SendNetworkMessageAll(new NetworkMessageAnimatorInt(header, hash));
				}

				// Send parameters
				for (int i = 0; i < Parameters.Length; i++) {
					byte header = (byte)(Layers + i);
					if (Parameters[i].type == AnimatorControllerParameterType.Int) {
						int value = SendParametersInt[i];
						SendNetworkMessageAll(new NetworkMessageAnimatorInt(header, value));
					} else if (Parameters[i].type == AnimatorControllerParameterType.Bool) {
						bool value = SendParametersBool[i];
						SendNetworkMessageAll(new NetworkMessageAnimatorBool(header, value));
					} else if (Parameters[i].type == AnimatorControllerParameterType.Float) {
						float value = SendParametersFloat[i];
						SendNetworkMessageAll(new NetworkMessageAnimatorFloat(header, value));
					}
				}

			} finally {
				SendLock.ExitReadLock();
			}

		}

		private AnimatorStateInfo GetAnimatorState(int layer) {
			if (Animator.IsInTransition(layer)) {
				return Animator.GetNextAnimatorStateInfo(layer);
			} else {
				return Animator.GetCurrentAnimatorStateInfo(layer);
			}
		}

		private struct NetworkMessageAnimatorFloat : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly byte Header;
			public readonly float Value;

			public NetworkMessageAnimatorFloat(byte header, float value) {
				Header = header;
				Value = value;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write(Value);
			}

		}

		private struct NetworkMessageAnimatorBool : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly byte Header;
			public readonly bool Value;

			public NetworkMessageAnimatorBool(byte header, bool value) {
				Header = header;
				Value = value;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write(Value);
			}

		}

		private struct NetworkMessageAnimatorInt : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly byte Header;
			public readonly int Value;

			public NetworkMessageAnimatorInt(byte header, int value) {
				Header = header;
				Value = value;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write(Value);
			}

		}

		private struct NetworkMessageAnimatorHash : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => true;
			public bool Unique => true;
			public readonly byte Header;
			public readonly int Hash;
			public readonly float Time;

			public NetworkMessageAnimatorHash(byte header, int hash, float time) {
				Header = header;
				Hash = hash;
				Time = time;
			}

			public void Write(Writer writer) {
				writer.Write(Header);
				writer.Write(Hash);
				writer.Write(Time);
			}

		}

	}

}
