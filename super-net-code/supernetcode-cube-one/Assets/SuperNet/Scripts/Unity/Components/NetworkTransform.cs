using SuperNet.Netcode.Transport;
using SuperNet.Unity.Core;
using SuperNet.Netcode.Util;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

namespace SuperNet.Unity.Components {

	/// <summary>
	/// Synchronizes a transform over the network. 
	/// </summary>
	[AddComponentMenu("SuperNet/NetworkTransform")]
	[HelpURL("https://superversus.com/netcode/api/SuperNet.Unity.Components.NetworkTransform.html")]
	public sealed class NetworkTransform : NetworkComponent {

		/// <summary>
		/// Transform component to synchronize. Required.
		/// </summary>
		[FormerlySerializedAs("Transform")]
		[Tooltip("Transform component to synchronize. Required.")]
		public Transform Transform;
		
		/// <summary>
		/// Rigidbody component to synchronize. Optional.
		/// </summary>
		[FormerlySerializedAs("Rigidbody")]
		[Tooltip("Rigidbody component to synchronize. Optional.")]
		public Rigidbody Rigidbody;

		/// <summary>
		/// Rigidbody2D component to synchronize. Required.
		/// </summary>
		[FormerlySerializedAs("Rigidbody2D")]
		[Tooltip("Rigidbody2D component to synchronize. Optional.")]
		public Rigidbody2D Rigidbody2D;

		/// <summary>
		/// RectTransform component to synchronize. Optional.
		/// </summary>
		[FormerlySerializedAs("RectTransform")]
		[Tooltip("RectTransform component to synchronize. Optional.")]
		public RectTransform RectTransform;

		/// <summary>
		/// Which method to synchronize in.
		/// </summary>
		[FormerlySerializedAs("SyncMethod")]
		[Tooltip("Which method to synchronize in.")]
		public NetworkSyncModeMethod SyncMethod;

		/// <summary>
		/// Which rotation components to synchronize.
		/// </summary>
		[FormerlySerializedAs("SyncRotation")]
		[Tooltip("Which rotation components to synchronize.")]
		public NetworkSyncModeVector3 SyncRotation;

		/// <summary>
		/// Which position components to synchronize.
		/// </summary>
		[FormerlySerializedAs("SyncPosition")]
		[Tooltip("Which position components to synchronize.")]
		public NetworkSyncModeVector3 SyncPosition;

		/// <summary>
		/// Which scale components to synchronize.
		/// </summary>
		[FormerlySerializedAs("SyncScale")]
		[Tooltip("Which scale components to synchronize.")]
		public NetworkSyncModeVector3 SyncScale;

		/// <summary>
		/// Which velocity components to synchronize.
		/// </summary>
		[FormerlySerializedAs("SyncVelocity")]
		[Tooltip("Which velocity components to synchronize.")]
		public NetworkSyncModeVector3 SyncVelocity;

		/// <summary>
		/// Which angular velocity components to synchronize.
		/// </summary>
		[FormerlySerializedAs("SyncAngularVelocity")]
		[Tooltip("Which angular velocity components to synchronize.")]
		public NetworkSyncModeVector3 SyncAngularVelocity;

		/// <summary>
		/// Should RectTransform anchorMin be synchronized.
		/// </summary>
		[FormerlySerializedAs("SyncRectAnchorMin")]
		[Tooltip("Should RectTransform anchorMin be synchronized.")]
		public NetworkSyncModeVector2 SyncRectAnchorMin;

		/// <summary>
		/// Should RectTransform anchorMax be synchronized.
		/// </summary>
		[FormerlySerializedAs("SyncRectAnchorMax")]
		[Tooltip("Should RectTransform anchorMax be synchronized.")]
		public NetworkSyncModeVector2 SyncRectAnchorMax;

		/// <summary>
		/// Should RectTransform sizeDelta be synchronized.
		/// </summary>
		[FormerlySerializedAs("SyncRectSizeDelta")]
		[Tooltip("Should RectTransform sizeDelta be synchronized.")]
		public NetworkSyncModeVector2 SyncRectSizeDelta;

		/// <summary>
		/// Should RectTransform pivot be synchronized.
		/// </summary>
		[FormerlySerializedAs("SyncRectPivot")]
		[Tooltip("Should RectTransform pivot be synchronized.")]
		public NetworkSyncModeVector2 SyncRectPivot;

		/// <summary>
		/// Synchronize position and rotation in local space.
		/// </summary>
		[FormerlySerializedAs("SyncLocalTransform")]
		[Tooltip("Synchronize position and rotation in local space.")]
		public bool SyncLocalTransform;

		/// <summary>
		/// Send updates to remote peers.
		/// </summary>
		[FormerlySerializedAs("Authority")]
		[Tooltip("Send updates to remote peers.")]
		public bool Authority;

		[Header("Receive Configuration")]

		/// <summary>
		/// How many seconds into the past we see the transform at.
		/// Smaller values make the transform more correct to where it actually is but make it more jittery.
		/// If updates we receive are older than this value (high ping), they are extrapolated instead of interpolated.
		/// </summary>
		[FormerlySerializedAs("ReceiveDelay")]
		[Tooltip("How many seconds into the past we see the transform at.")]
		public float ReceiveDelay;

		/// <summary>
		/// Seconds after the last update is received to extrapolate for.
		/// Bigger values make the transform less likely to jitter during lag spikes but can introduce rubber banding.
		/// </summary>
		[FormerlySerializedAs("ReceiveExtrapolate")]
		[Tooltip("Seconds after the last update is received to extrapolate for.")]
		public float ReceiveExtrapolate;

		/// <summary>
		/// Minimum rotation angle still allowed to interpolate before snapping. Zero to disable.
		/// </summary>
		[FormerlySerializedAs("ReceiveSnapRotation")]
		[Tooltip("Minimum rotation angle still allowed to interpolate before snapping. Zero to disable.")]
		public float ReceiveSnapRotation;

		/// <summary>
		/// Minimum distance still allowed to interpolate before snapping. Zero to disable.
		/// </summary>
		[FormerlySerializedAs("ReceiveSnapPosition")]
		[Tooltip("Minimum distance still allowed to interpolate before snapping. Zero to disable.")]
		public float ReceiveSnapPosition;

		/// <summary>
		/// Minimum scale difference still allowed to interpolate before snapping. Zero to disable.
		/// </summary>
		[FormerlySerializedAs("ReceiveSnapScale")]
		[Tooltip("Minimum scale difference still allowed to interpolate before snapping. Zero to disable.")]
		public float ReceiveSnapScale;

		/// <summary>
		/// Minimum velocity difference still allowed to interpolate before snapping. Zero to disable.
		/// </summary>
		[FormerlySerializedAs("ReceiveSnapVelocity")]
		[Tooltip("Minimum velocity difference still allowed to interpolate before snapping. Zero to disable.")]
		public float ReceiveSnapVelocity;

		/// <summary>
		/// Minimum angular velocity difference still allowed to interpolate before snapping. Zero to disable.
		/// </summary>
		[FormerlySerializedAs("ReceiveSnapAngularVelocity")]
		[Tooltip("Minimum angular velocity difference still allowed to interpolate before snapping. Zero to disable.")]
		public float ReceiveSnapAngularVelocity;

		/// <summary>
		/// Minimum RectTransform anchor difference still allowed to interpolate before snapping. Zero to disable.
		/// </summary>
		[FormerlySerializedAs("ReceiveSnapRectAnchors")]
		[Tooltip("Minimum RectTransform anchor difference still allowed to interpolate before snapping. Zero to disable.")]
		public float ReceiveSnapRectAnchors;

		/// <summary>
		/// Minimum RectTransform size difference still allowed to interpolate before snapping. Zero to disable.
		/// </summary>
		[FormerlySerializedAs("ReceiveSnapRectSizeDelta")]
		[Tooltip("Minimum RectTransform size difference still allowed to interpolate before snapping. Zero to disable.")]
		public float ReceiveSnapRectSizeDelta;

		/// <summary>
		/// Minimum RectTransform pivot difference still allowed to interpolate before snapping. Zero to disable.
		/// </summary>
		[FormerlySerializedAs("ReceiveSnapRectPivot")]
		[Tooltip("Minimum RectTransform pivot difference still allowed to interpolate before snapping. Zero to disable.")]
		public float ReceiveSnapRectPivot;

		[Header("Send Configuration")]

		/// <summary>
		/// Minimum number of seconds to wait before sending an update.
		/// </summary>
		[FormerlySerializedAs("SendIntervalMin")]
		[Tooltip("Minimum number of seconds to wait before sending an update.")]
		public float SendIntervalMin;

		/// <summary>
		/// Maximum number of seconds to wait before sending an update.
		/// </summary>
		[FormerlySerializedAs("SendIntervalMax")]
		[Tooltip("Maximum number of seconds to wait before sending an update.")]
		public float SendIntervalMax;

		/// <summary>
		/// Minimum amount rotation angle is able to change before an update is sent.
		/// </summary>
		[FormerlySerializedAs("SendThresholdRotation")]
		[Tooltip("Minimum amount rotation angle is able to change before an update is sent.")]
		public float SendThresholdRotation;

		/// <summary>
		/// Minimum distance transform is able to move before an update is sent.
		/// </summary>
		[FormerlySerializedAs("SendThresholdPosition")]
		[Tooltip("Minimum distance transform is able to move before an update is sent.")]
		public float SendThresholdPosition;

		/// <summary>
		/// Minimum amount transform is able to scale before an update is sent.
		/// </summary>
		[FormerlySerializedAs("SendThresholdScale")]
		[Tooltip("Minimum amount transform is able to scale before an update is sent.")]
		public float SendThresholdScale;

		/// <summary>
		/// Minimum amount velocity is able to change before an update is sent.
		/// </summary>
		[FormerlySerializedAs("SendThresholdVelocity")]
		[Tooltip("Minimum amount velocity is able to change before an update is sent.")]
		public float SendThresholdVelocity;

		/// <summary>
		/// Minimum amount angular velocity is able to change before an update is sent.
		/// </summary>
		[FormerlySerializedAs("SendThresholdAngularVelocity")]
		[Tooltip("Minimum amount angular velocity is able to change before an update is sent.")]
		public float SendThresholdAngularVelocity;

		/// <summary>
		/// Minimum RectTransform anchor is able to change before an update is sent.
		/// </summary>
		[FormerlySerializedAs("SendThresholdRectAnchors")]
		[Tooltip("Minimum amount RectTransform anchor is able to change before an update is sent.")]
		public float SendThresholdRectAnchors;

		/// <summary>
		/// Minimum amount RectTransform size is able to change before an update is sent.
		/// </summary>
		[FormerlySerializedAs("SendThresholdRectSizeDelta")]
		[Tooltip("Minimum amount RectTransform size is able to change before an update is sent.")]
		public float SendThresholdRectSizeDelta;

		/// <summary>
		/// Minimum amount RectTransform pivot is able to change before an update is sent.
		/// </summary>
		[FormerlySerializedAs("SendThresholdRectPivot")]
		[Tooltip("Minimum amount RectTransform pivot is able to change before an update is sent.")]
		public float SendThresholdRectPivot;

		/// <summary>
		/// Should remote peer extrapolation be taken into account when checking for thresholds.
		/// </summary>
		[FormerlySerializedAs("SendThresholdExtrapolate")]
		[Tooltip("Should remote peer extrapolation be taken into account when checking for thresholds.")]
		public bool SendThresholdExtrapolate;

		/// <summary>
		/// True if a rigidbody is attached.
		/// </summary>
		public bool HasRigidbody => Rigidbody != null || Rigidbody2D != null;

		/// <summary>
		/// True if a RectTransform is attached.
		/// </summary>
		public bool HasRectTransform => RectTransform != null;

		// State sent over the network
		private struct SyncState {
			public Vector3 Euler;
			public Vector3 Position;
			public Vector3 Scale;
			public Vector3 Velocity;
			public Vector3 AngularVelocity;
			public Vector2 RectAnchorMin;
			public Vector2 RectAnchorMax;
			public Vector2 RectSizeDelta;
			public Vector2 RectPivot;
			public float Time;
		}

		[Flags]
		private enum SyncHeader : byte {
			None            = 0b00000000,
			Rotation        = 0b00000001,
			Position        = 0b00000010,
			Scale           = 0b00000100,
			Velocity        = 0b00001000,
			AngularVelocity = 0b00010000,
			RectAnchors     = 0b00100000,
			RectSizeDelta   = 0b01000000,
			RectPivot       = 0b10000000,
			All             = 0b11111111,
		}

		// Resources
		private System.Diagnostics.Stopwatch Stopwatch;
		private ReaderWriterLockSlim ReceiveLock;
		private SyncState[] ReceiveArray;
		private int ReceiveIndexSave;
		private int ReceiveIndexSync;
		private ReaderWriterLockSlim SendLock;
		private SyncState SendPrevState;
		private SyncState SendLastState;
		private float SendTimeRotation;
		private float SendTimePosition;
		private float SendTimeScale;
		private float SendTimeVelocity;
		private float SendTimeAngularVelocity;
		private float SendTimeRectAnchors;
		private float SendTimeRectSizeDelta;
		private float SendTimeRectPivot;

		private void Reset() {
			ResetNetworkIdentity();
			Transform = transform;
			RectTransform = GetComponent<RectTransform>();
			Rigidbody = GetComponent<Rigidbody>();
			Rigidbody2D = GetComponent<Rigidbody2D>();
			SyncMethod = NetworkSyncModeMethod.Update;
			SyncRotation = NetworkSyncModeVector3.XYZ;
			SyncPosition = NetworkSyncModeVector3.XYZ;
			SyncScale = NetworkSyncModeVector3.XYZ;
			SyncVelocity = HasRigidbody ? NetworkSyncModeVector3.XYZ : NetworkSyncModeVector3.None;
			SyncAngularVelocity = HasRigidbody ? NetworkSyncModeVector3.XYZ : NetworkSyncModeVector3.None;
			SyncRectAnchorMin = HasRectTransform ? NetworkSyncModeVector2.XY : NetworkSyncModeVector2.None;
			SyncRectAnchorMax = HasRectTransform ? NetworkSyncModeVector2.XY : NetworkSyncModeVector2.None;
			SyncRectSizeDelta = HasRectTransform ? NetworkSyncModeVector2.XY : NetworkSyncModeVector2.None;
			SyncRectPivot = HasRectTransform ? NetworkSyncModeVector2.XY : NetworkSyncModeVector2.None;
			SyncLocalTransform = false;
			Authority = false;
			ReceiveDelay = 0.1f;
			ReceiveExtrapolate = 0.3f;
			ReceiveSnapRotation = 40f;
			ReceiveSnapPosition = 0.5f;
			ReceiveSnapScale = 0.5f;
			ReceiveSnapVelocity = 1f;
			ReceiveSnapAngularVelocity = 10f;
			ReceiveSnapRectAnchors = 0.05f;
			ReceiveSnapRectSizeDelta = 0.25f;
			ReceiveSnapRectPivot = 0.05f;
			SendIntervalMin = 0.030f;
			SendIntervalMax = 2.000f;
			SendThresholdRotation = 1f;
			SendThresholdPosition = 0.02f;
			SendThresholdScale = 0.01f;
			SendThresholdVelocity = 0.01f;
			SendThresholdAngularVelocity = 0.1f;
			SendThresholdRectAnchors = 0.001f;
			SendThresholdRectSizeDelta = 0.01f;
			SendThresholdRectPivot = 0.001f;
			SendThresholdExtrapolate = false;
		}

		private void Awake() {

			// Check if transform is set
			if (Transform == null) {
				Debug.LogWarning("[SuperNet] [NetworkTransform] Transform not set.", this);
				enabled = false;
				return;
			}

			// Initialize resources
			Stopwatch = System.Diagnostics.Stopwatch.StartNew();
			ReceiveLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			ReceiveArray = new SyncState[] { CreateLocalState(0f) };
			ReceiveIndexSave = 0;
			ReceiveIndexSync = 0;
			SendLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			SendPrevState = CreateLocalState(0f);
			SendLastState = CreateLocalState(0f);
			SendTimeRotation = 0f;
			SendTimePosition = 0f;
			SendTimeScale = 0f;
			SendTimeVelocity = 0f;
			SendTimeAngularVelocity = 0f;
			SendTimeRectAnchors = 0f;
			SendTimeRectSizeDelta = 0f;
			SendTimeRectPivot = 0f;

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
			if (Transform == null) {
				Debug.LogWarning("[SuperNet] [NetworkTransform] Transform removed.", this);
				enabled = false;
				return;
			}

			// Send or receive updates
			if (Authority) {
				SyncUpdateSend();
			} else {
				SyncUpdateReceive();
			}

		}

		private void SyncUpdateSend() {
			try {
				SendLock.EnterUpgradeableReadLock();

				// Get current time
				float now = (float)Stopwatch.Elapsed.TotalSeconds;

				// If not enough time has passed to send an update, do nothing
				if (now - SendLastState.Time < SendIntervalMin) {
					return;
				}

				// Get local state
				SyncState local = CreateLocalState(now);
				SyncHeader header = SyncHeader.None;

				// Calculate extrapolation factor for remote peers to check thresholds
				float factor = 1f;
				if (SendThresholdExtrapolate && SendLastState.Time > SendPrevState.Time) {
					factor = (local.Time - SendPrevState.Time) / (SendLastState.Time - SendPrevState.Time);
				}

				// Check rotation threshold
				if (SyncRotation != NetworkSyncModeVector3.None) {
					Quaternion rotationLocal = Quaternion.Euler(local.Euler);
					Quaternion rotationPrev = Quaternion.Euler(SendPrevState.Euler);
					Quaternion rotationLast = Quaternion.Euler(SendLastState.Euler);
					Quaternion rotationValue = Quaternion.SlerpUnclamped(rotationPrev, rotationLast, factor);
					float rotationDelta = Quaternion.Angle(rotationLocal, rotationValue);
					float timeDelta = local.Time - SendTimeRotation;
					if (rotationDelta > SendThresholdRotation) header |= SyncHeader.Rotation;
					if (timeDelta > SendIntervalMax) header |= SyncHeader.Rotation;
				}

				// Check position threshold
				if (SyncPosition != NetworkSyncModeVector3.None) {
					Vector3 positionValue = Vector3.LerpUnclamped(SendPrevState.Position, SendLastState.Position, factor); 
					float positionDelta = Vector3.Distance(local.Position, positionValue);
					float timeDelta = local.Time - SendTimePosition;
					if (positionDelta > SendThresholdPosition) header |= SyncHeader.Position;
					if (timeDelta > SendIntervalMax) header |= SyncHeader.Position;
				}

				// Check scale threshold
				if (SyncScale != NetworkSyncModeVector3.None) {
					Vector3 scaleValue = Vector3.LerpUnclamped(SendPrevState.Scale, SendLastState.Scale, factor);
					float scaleDelta = Vector3.Distance(local.Scale, scaleValue);
					float timeDelta = local.Time - SendTimeScale; 
					if (scaleDelta > SendThresholdScale) header |= SyncHeader.Scale;
					if (timeDelta > SendIntervalMax) header |= SyncHeader.Scale;
				}

				// Check velocity threshold
				if (SyncVelocity != NetworkSyncModeVector3.None && HasRigidbody) {
					Vector3 velocityValue = Vector3.LerpUnclamped(SendPrevState.Velocity, SendLastState.Velocity, factor);
					float velocityDelta = Vector3.Distance(local.Velocity, velocityValue);
					float timeDelta = local.Time - SendTimeVelocity;
					if (velocityDelta > SendThresholdVelocity) header |= SyncHeader.Velocity;
					if (timeDelta > SendIntervalMax) header |= SyncHeader.Velocity;
				}

				// Check angular velocity threshold
				if (SyncAngularVelocity != NetworkSyncModeVector3.None && HasRigidbody) {
					Vector3 angularVelocityValue = Vector3.LerpUnclamped(SendPrevState.AngularVelocity, SendLastState.AngularVelocity, factor);
					float angularVelocityDelta = Vector3.Distance(local.AngularVelocity, angularVelocityValue);
					float timeDelta = local.Time - SendTimeAngularVelocity;
					if (angularVelocityDelta > SendThresholdAngularVelocity) header |= SyncHeader.AngularVelocity;
					if (timeDelta > SendIntervalMax) header |= SyncHeader.AngularVelocity;
				}

				// Check rect anchors
				if ((SyncRectAnchorMax != NetworkSyncModeVector2.None || SyncRectAnchorMin != NetworkSyncModeVector2.None) && HasRectTransform) {
					Vector2 anchorMinValue = Vector2.LerpUnclamped(SendPrevState.RectAnchorMin, SendLastState.RectAnchorMin, factor);
					Vector2 anchorMaxValue = Vector2.LerpUnclamped(SendPrevState.RectAnchorMax, SendLastState.RectAnchorMax, factor);
					float anchorMinDelta = Vector2.Distance(local.RectAnchorMin, anchorMinValue);
					float anchorMaxDelta = Vector2.Distance(local.RectAnchorMax, anchorMaxValue);
					float anchorDelta = Math.Max(anchorMinDelta, anchorMaxDelta);
					float timeDelta = local.Time - SendTimeRectAnchors;
					if (anchorDelta > SendThresholdRectAnchors) header |= SyncHeader.RectAnchors;
					if (timeDelta > SendIntervalMax) header |= SyncHeader.RectAnchors;
				}

				// Check rect size
				if (SyncRectSizeDelta != NetworkSyncModeVector2.None && HasRectTransform) {
					Vector2 sizeValue = Vector2.LerpUnclamped(SendPrevState.RectSizeDelta, SendLastState.RectSizeDelta, factor);
					float sizeDelta = Vector2.Distance(local.RectSizeDelta, sizeValue);
					float timeDelta = local.Time - SendTimeRectSizeDelta;
					if (sizeDelta > SendThresholdRectSizeDelta) header |= SyncHeader.RectSizeDelta;
					if (timeDelta > SendIntervalMax) header |= SyncHeader.RectSizeDelta;
				}

				// Check rect pivot
				if (SyncRectPivot != NetworkSyncModeVector2.None && HasRectTransform) {
					Vector2 sizeValue = Vector2.LerpUnclamped(SendPrevState.RectPivot, SendLastState.RectPivot, factor);
					float sizeDelta = Vector2.Distance(local.RectPivot, sizeValue);
					float timeDelta = local.Time - SendTimeRectPivot;
					if (sizeDelta > SendThresholdRectPivot) header |= SyncHeader.RectPivot;
					if (timeDelta > SendIntervalMax) header |= SyncHeader.RectPivot;
				}

				// Send message if needed
				if (header != SyncHeader.None) {
					try {
						SendLock.EnterWriteLock();

						// Update send times
						if (header.HasFlag(SyncHeader.Velocity)) SendTimeRotation = now;
						if (header.HasFlag(SyncHeader.Position)) SendTimePosition = now;
						if (header.HasFlag(SyncHeader.Scale)) SendTimeScale = now;
						if (header.HasFlag(SyncHeader.Velocity)) SendTimeVelocity = now;
						if (header.HasFlag(SyncHeader.AngularVelocity)) SendTimeAngularVelocity = now;
						if (header.HasFlag(SyncHeader.RectAnchors)) SendTimeRectAnchors = now;
						if (header.HasFlag(SyncHeader.RectSizeDelta)) SendTimeRectSizeDelta = now;
						if (header.HasFlag(SyncHeader.RectPivot)) SendTimeRectPivot = now;

						// Save state
						SendPrevState = SendLastState;
						SendLastState = local;

						// Send message to all peers
						SendNetworkMessageAll(new NetworkMessageTransform(this, local, header));

					} finally {
						SendLock.ExitWriteLock();
					}
				}

			} finally {
				SendLock.ExitUpgradeableReadLock();
			}
		}

		private void SyncUpdateReceive() {
			try {
				ReceiveLock.EnterReadLock();

				// Get current time
				float now = (float)Stopwatch.Elapsed.TotalSeconds;

				// Synchronization time and indices
				float syncTime = now - ReceiveDelay;
				int prev = (ReceiveIndexSync + 0) % ReceiveArray.Length;
				int next = (ReceiveIndexSync + 1) % ReceiveArray.Length;

				// Move index to the latest state in the array
				while (ReceiveArray[next].Time > ReceiveArray[prev].Time && ReceiveArray[next].Time < syncTime) {
					ReceiveIndexSync = (ReceiveIndexSync + 1) % ReceiveArray.Length;
					prev = (ReceiveIndexSync + 0) % ReceiveArray.Length;
					next = (ReceiveIndexSync + 1) % ReceiveArray.Length;
				}

				// Check if end of array reached
				if (ReceiveArray[next].Time <= ReceiveArray[prev].Time) {
					prev = (ReceiveIndexSync - 1 + ReceiveArray.Length) % ReceiveArray.Length;
					next = (ReceiveIndexSync + 0) % ReceiveArray.Length;
				}

				// Get interpolation/extrapolation factor
				float prevTime = ReceiveArray[prev].Time;
				float nextTime = ReceiveArray[next].Time;
				float factor = (syncTime - prevTime) / (nextTime - prevTime);
				if (prev == next) factor = 0f;

				// Stop extrapolating if enough time passed without an update
				if (syncTime - nextTime > ReceiveExtrapolate) {
					return;
				}

				// Get local state
				SyncState local = CreateLocalState(now);

				// Interplate/Extrapolate rotation
				if (SyncRotation != NetworkSyncModeVector3.None) {
					Vector3 prevEuler = ReceiveArray[prev].Euler;
					Vector3 nextEuler = ReceiveArray[next].Euler;
					if (!SyncRotation.HasFlag(NetworkSyncModeVector3.X)) prevEuler.x = local.Euler.x;
					if (!SyncRotation.HasFlag(NetworkSyncModeVector3.Y)) prevEuler.y = local.Euler.y;
					if (!SyncRotation.HasFlag(NetworkSyncModeVector3.Z)) prevEuler.z = local.Euler.z;
					if (!SyncRotation.HasFlag(NetworkSyncModeVector3.X)) nextEuler.x = local.Euler.x;
					if (!SyncRotation.HasFlag(NetworkSyncModeVector3.Y)) nextEuler.y = local.Euler.y;
					if (!SyncRotation.HasFlag(NetworkSyncModeVector3.Z)) nextEuler.z = local.Euler.z;
					Quaternion prevRotation = Quaternion.Euler(prevEuler);
					Quaternion nextRotation = Quaternion.Euler(nextEuler);
					if (ReceiveSnapRotation > 0f && Quaternion.Angle(prevRotation, nextRotation) > ReceiveSnapRotation) {
						if (SyncLocalTransform) {
							Transform.localRotation = nextRotation;
						} else {
							Transform.rotation = nextRotation;
						}
					} else {
						Quaternion rotation = Quaternion.SlerpUnclamped(prevRotation, nextRotation, factor);
						if (SyncLocalTransform) {
							Transform.rotation = nextRotation;
						} else if (Rigidbody != null) {
							Rigidbody.MoveRotation(rotation);
						} else if (Rigidbody2D != null) {
							Rigidbody2D.MoveRotation(rotation.x);
						} else {
							Transform.localRotation = rotation;
						}
					}
				}

				// Interplate/Extrapolate position
				if (SyncPosition != NetworkSyncModeVector3.None) {
					Vector3 prevPosition = ReceiveArray[prev].Position;
					Vector3 nextPosition = ReceiveArray[next].Position;
					if (!SyncPosition.HasFlag(NetworkSyncModeVector3.X)) prevPosition.x = local.Position.x;
					if (!SyncPosition.HasFlag(NetworkSyncModeVector3.Y)) prevPosition.y = local.Position.y;
					if (!SyncPosition.HasFlag(NetworkSyncModeVector3.Z)) prevPosition.z = local.Position.z;
					if (!SyncPosition.HasFlag(NetworkSyncModeVector3.X)) nextPosition.x = local.Position.x;
					if (!SyncPosition.HasFlag(NetworkSyncModeVector3.Y)) nextPosition.y = local.Position.y;
					if (!SyncPosition.HasFlag(NetworkSyncModeVector3.Z)) nextPosition.z = local.Position.z;
					if (ReceiveSnapPosition > 0f && Vector3.Distance(prevPosition, nextPosition) > ReceiveSnapPosition) {
						if (SyncLocalTransform) {
							Transform.localPosition = nextPosition;
						} else {
							Transform.position = nextPosition;
						}
					} else {
						Vector3 position = Vector3.LerpUnclamped(prevPosition, nextPosition, factor);
						if (SyncLocalTransform) {
							Transform.localPosition = nextPosition;
						} else if (Rigidbody != null) {
							Rigidbody.MovePosition(position);
						} else if (Rigidbody2D != null) {
							Rigidbody2D.MovePosition(position);
						} else {
							Transform.position = position;
						}
					}
				}

				// Interplate/Extrapolate scale
				if (SyncScale != NetworkSyncModeVector3.None) {
					Vector3 prevScale = ReceiveArray[prev].Scale;
					Vector3 nextScale = ReceiveArray[next].Scale;
					if (!SyncScale.HasFlag(NetworkSyncModeVector3.X)) prevScale.x = local.Scale.x;
					if (!SyncScale.HasFlag(NetworkSyncModeVector3.Y)) prevScale.y = local.Scale.y;
					if (!SyncScale.HasFlag(NetworkSyncModeVector3.Z)) prevScale.z = local.Scale.z;
					if (!SyncScale.HasFlag(NetworkSyncModeVector3.X)) nextScale.x = local.Scale.x;
					if (!SyncScale.HasFlag(NetworkSyncModeVector3.Y)) nextScale.y = local.Scale.y;
					if (!SyncScale.HasFlag(NetworkSyncModeVector3.Z)) nextScale.z = local.Scale.z;
					if (ReceiveSnapScale > 0f && Vector3.Distance(prevScale, nextScale) > ReceiveSnapScale) {
						Transform.localScale = nextScale;
					} else {
						Transform.localScale = Vector3.LerpUnclamped(prevScale, nextScale, factor);
					}
				}

				// Interplate/Extrapolate velocity
				if (SyncVelocity != NetworkSyncModeVector3.None && HasRigidbody) {
					Vector3 prevVelocity = ReceiveArray[prev].Velocity;
					Vector3 nextVelocity = ReceiveArray[next].Velocity;
					if (!SyncVelocity.HasFlag(NetworkSyncModeVector3.X)) prevVelocity.x = local.Velocity.x;
					if (!SyncVelocity.HasFlag(NetworkSyncModeVector3.Y)) prevVelocity.y = local.Velocity.y;
					if (!SyncVelocity.HasFlag(NetworkSyncModeVector3.Z)) prevVelocity.z = local.Velocity.z;
					if (!SyncVelocity.HasFlag(NetworkSyncModeVector3.X)) nextVelocity.x = local.Velocity.x;
					if (!SyncVelocity.HasFlag(NetworkSyncModeVector3.Y)) nextVelocity.y = local.Velocity.y;
					if (!SyncVelocity.HasFlag(NetworkSyncModeVector3.Z)) nextVelocity.z = local.Velocity.z;
					if (ReceiveSnapVelocity > 0f && Vector3.Distance(prevVelocity, nextVelocity) > ReceiveSnapVelocity) {
						if (Rigidbody != null) Rigidbody.velocity = nextVelocity;
						if (Rigidbody2D != null) Rigidbody2D.velocity = nextVelocity;
					} else {
						Vector3 velocity = Vector3.LerpUnclamped(prevVelocity, nextVelocity, factor);
						if (Rigidbody != null) Rigidbody.velocity = velocity;
						if (Rigidbody2D != null) Rigidbody2D.velocity = velocity;
					}
				}

				// Interplate/Extrapolate angular velocity
				if (SyncAngularVelocity != NetworkSyncModeVector3.None && HasRigidbody) {
					Vector3 prevAngularVelocity = ReceiveArray[prev].AngularVelocity;
					Vector3 nextAngularVelocity = ReceiveArray[next].AngularVelocity;
					if (!SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.X)) prevAngularVelocity.x = local.AngularVelocity.x;
					if (!SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.Y)) prevAngularVelocity.y = local.AngularVelocity.y;
					if (!SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.Z)) prevAngularVelocity.z = local.AngularVelocity.z;
					if (!SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.X)) nextAngularVelocity.x = local.AngularVelocity.x;
					if (!SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.Y)) nextAngularVelocity.y = local.AngularVelocity.y;
					if (!SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.Z)) nextAngularVelocity.z = local.AngularVelocity.z;
					if (ReceiveSnapAngularVelocity > 0f && Vector3.Distance(prevAngularVelocity, nextAngularVelocity) > ReceiveSnapAngularVelocity) {
						if (Rigidbody != null) Rigidbody.angularVelocity = nextAngularVelocity;
						if (Rigidbody2D != null) Rigidbody2D.angularVelocity = nextAngularVelocity.x;
					} else {
						Vector3 angularVelocity = Vector3.LerpUnclamped(prevAngularVelocity, nextAngularVelocity, factor);
						if (Rigidbody != null) Rigidbody.angularVelocity = angularVelocity;
						if (Rigidbody2D != null) Rigidbody2D.angularVelocity = angularVelocity.x;
					}
				}

				// Interplate/Extrapolate rect anchors
				if ((SyncRectAnchorMin != NetworkSyncModeVector2.None || SyncRectAnchorMax != NetworkSyncModeVector2.None) && HasRectTransform) {
					Vector2 prevAnchorMin = ReceiveArray[prev].RectAnchorMin;
					Vector2 prevAnchorMax = ReceiveArray[prev].RectAnchorMax;
					Vector2 nextAnchorMin = ReceiveArray[next].RectAnchorMin;
					Vector2 nextAnchorMax = ReceiveArray[next].RectAnchorMax;
					float distanceAnchorMin = Vector2.Distance(prevAnchorMin, nextAnchorMin);
					float distanceAnchorMax = Vector2.Distance(prevAnchorMax, nextAnchorMax);
					if (ReceiveSnapRectAnchors > 0f && Mathf.Max(distanceAnchorMin, distanceAnchorMax) > ReceiveSnapRectAnchors) {
						RectTransform.anchorMin = nextAnchorMin;
						RectTransform.anchorMax = nextAnchorMax;
					} else {
						Vector2 anchorMin = Vector2.LerpUnclamped(prevAnchorMin, nextAnchorMin, factor);
						Vector2 anchorMax = Vector2.LerpUnclamped(prevAnchorMax, nextAnchorMax, factor);
						RectTransform.anchorMin = anchorMin;
						RectTransform.anchorMax = anchorMax;
					}
				}

				// Interplate/Extrapolate rect size
				if (SyncRectSizeDelta != NetworkSyncModeVector2.None && HasRectTransform) {
					Vector2 prevSizeDelta = ReceiveArray[prev].RectSizeDelta;
					Vector2 nextSizeDelta = ReceiveArray[next].RectSizeDelta;
					if (ReceiveSnapRectSizeDelta > 0f && Vector2.Distance(prevSizeDelta, nextSizeDelta) > ReceiveSnapRectSizeDelta) {
						RectTransform.sizeDelta = nextSizeDelta;
						RectTransform.sizeDelta = nextSizeDelta;
					} else {
						Vector2 sizeDelta = Vector2.LerpUnclamped(prevSizeDelta, nextSizeDelta, factor);
						RectTransform.sizeDelta = sizeDelta;
						RectTransform.sizeDelta = sizeDelta;
					}
				}

				// Interplate/Extrapolate rect pivot
				if (SyncRectPivot != NetworkSyncModeVector2.None && HasRectTransform) {
					Vector2 prevPivot = ReceiveArray[prev].RectPivot;
					Vector2 nextPivot = ReceiveArray[next].RectPivot;
					if (ReceiveSnapRectPivot > 0f && Vector2.Distance(prevPivot, nextPivot) > ReceiveSnapRectPivot) {
						RectTransform.pivot = nextPivot;
						RectTransform.pivot = nextPivot;
					} else {
						Vector2 pivot = Vector2.LerpUnclamped(prevPivot, nextPivot, factor);
						RectTransform.pivot = pivot;
						RectTransform.pivot = pivot;
					}
				}

			} finally {
				ReceiveLock.ExitReadLock();
			}
		}

		public override void OnNetworkMessage(Peer peer, Reader reader, HostTimestamp timestamp) {

			// If receiving is not enabled, do nothing
			if (Authority) {
				Debug.LogWarning(string.Format(
					"[SuperNet] [NetworkTransform] Recieved NetworkID {0} update from {1}. Ignoring.",
					NetworkIdentity, peer.Remote
				), this);
				return;
			}

			// Get message time
			float now = (float)Stopwatch.Elapsed.TotalSeconds;
			float time = now - Mathf.Max(0f, (float)timestamp.ElapsedSeconds);

			try {
				ReceiveLock.EnterWriteLock();

				// Read state from message
				SyncState state = ReadState(reader, ReceiveArray[ReceiveIndexSave], time);

				// Discard the update if we already have one that is newer
				if (ReceiveArray[ReceiveIndexSave].Time > time) {
					Debug.LogWarning(string.Format(
						"[SuperNet] [NetworkTransform] Recieved NetworkID {0} outdated update from {1} for {2} seconds. Ignoring.",
						NetworkIdentity, peer.Remote, ReceiveArray[ReceiveIndexSave].Time - time
					), this);
					return;
				}

				// Resize receive array if needed
				int prev = (ReceiveIndexSave + 1) % ReceiveArray.Length;
				int next = (ReceiveIndexSave + 2) % ReceiveArray.Length;
				if (now - ReceiveArray[next].Time <= ReceiveDelay * 2f) {
					SyncState[] copy = new SyncState[ReceiveArray.Length + 1];
					for (int i = 0; i <= ReceiveIndexSave; i++) copy[i] = ReceiveArray[i];
					for (int i = ReceiveIndexSave + 1; i < copy.Length; i++) copy[i] = ReceiveArray[i - 1];
					if (ReceiveIndexSync > ReceiveIndexSave) ReceiveIndexSync++;
					prev = (ReceiveIndexSave + 1) % copy.Length;
					ReceiveArray = copy;
				}

				// Update index and save state
				ReceiveIndexSave = prev;
				ReceiveArray[ReceiveIndexSave] = state;

			} finally {
				ReceiveLock.ExitWriteLock();
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

			// Copy last sent state
			SyncState state;
			try {
				SendLock.EnterReadLock();
				state = SendLastState;
			} finally {
				SendLock.ExitReadLock();
			}

			// Send message to newly connected peer
			SendNetworkMessage(peer, new NetworkMessageTransform(this, state, SyncHeader.All));

		}

		public override void OnNetworkRegister() {

			// If sending is not enabled, do nothing
			if (!Authority) {
				return;
			}

			// Copy last sent state
			SyncState state;
			try {
				SendLock.EnterReadLock();
				state = SendLastState;
			} finally {
				SendLock.ExitReadLock();
			}

			// Send newly created state to all peers
			SendNetworkMessageAll(new NetworkMessageTransform(this, state, SyncHeader.All));

		}

		private SyncState CreateLocalState(float time) {

			SyncState state;

			// Get transform state
			if (SyncLocalTransform) {
				state.Euler = Transform.localRotation.eulerAngles;
				state.Position = Transform.localPosition;
			} else {
				state.Euler = Transform.rotation.eulerAngles;
				state.Position = Transform.position;
			}
			state.Scale = Transform.localScale;

			// Get rigidbody state
			state.Velocity = Vector3.zero;
			state.AngularVelocity = Vector3.zero;
			if (Rigidbody != null) {
				state.Velocity = Rigidbody.velocity;
				state.AngularVelocity = Rigidbody.angularVelocity;
			} else if (Rigidbody2D != null) {
				state.Velocity = Rigidbody2D.velocity;
				state.AngularVelocity = new Vector3(Rigidbody2D.angularVelocity, 0f, 0f);
			}

			// Get rect state
			state.RectAnchorMin = Vector2.zero;
			state.RectAnchorMax = Vector2.zero;
			state.RectSizeDelta = Vector2.zero;
			state.RectPivot = Vector2.zero;
			if (RectTransform != null) {
				state.RectAnchorMin = RectTransform.anchorMin;
				state.RectAnchorMax = RectTransform.anchorMax;
				state.RectSizeDelta = RectTransform.sizeDelta;
				state.RectPivot = RectTransform.pivot;
			}

			// Return state
			state.Time = time;
			return state;

		}

		private SyncState ReadState(Reader reader, SyncState previous, float time) {

			// Read header
			SyncState state;
			SyncHeader header = (SyncHeader)reader.ReadByte();

			// Read rotation
			state.Euler = previous.Euler;
			if (header.HasFlag(SyncHeader.Rotation)) {
				if (SyncRotation.HasFlag(NetworkSyncModeVector3.X)) state.Euler.x = reader.ReadSingle();
				if (SyncRotation.HasFlag(NetworkSyncModeVector3.Y)) state.Euler.y = reader.ReadSingle();
				if (SyncRotation.HasFlag(NetworkSyncModeVector3.Z)) state.Euler.z = reader.ReadSingle();
			}

			// Read position
			state.Position = previous.Position;
			if (header.HasFlag(SyncHeader.Position)) {
				if (SyncPosition.HasFlag(NetworkSyncModeVector3.X)) state.Position.x = reader.ReadSingle();
				if (SyncPosition.HasFlag(NetworkSyncModeVector3.Y)) state.Position.y = reader.ReadSingle();
				if (SyncPosition.HasFlag(NetworkSyncModeVector3.Z)) state.Position.z = reader.ReadSingle();
			}

			// Read scale
			state.Scale = previous.Scale;
			if (header.HasFlag(SyncHeader.Scale)) {
				if (SyncScale.HasFlag(NetworkSyncModeVector3.X)) state.Scale.x = reader.ReadSingle();
				if (SyncScale.HasFlag(NetworkSyncModeVector3.Y)) state.Scale.y = reader.ReadSingle();
				if (SyncScale.HasFlag(NetworkSyncModeVector3.Z)) state.Scale.z = reader.ReadSingle();
			}

			// Read velocity
			state.Velocity = previous.Velocity;
			if (header.HasFlag(SyncHeader.Velocity)) {
				if (SyncVelocity.HasFlag(NetworkSyncModeVector3.X)) state.Velocity.x = reader.ReadSingle();
				if (SyncVelocity.HasFlag(NetworkSyncModeVector3.Y)) state.Velocity.y = reader.ReadSingle();
				if (SyncVelocity.HasFlag(NetworkSyncModeVector3.Z)) state.Velocity.z = reader.ReadSingle();
			}

			// Read angular velocity
			state.AngularVelocity = previous.AngularVelocity;
			if (header.HasFlag(SyncHeader.AngularVelocity)) {
				if (SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.X)) state.AngularVelocity.x = reader.ReadSingle();
				if (SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.Y)) state.AngularVelocity.y = reader.ReadSingle();
				if (SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.Z)) state.AngularVelocity.z = reader.ReadSingle();
			}

			// Read rect anchors
			state.RectAnchorMin = previous.RectAnchorMin;
			state.RectAnchorMax = previous.RectAnchorMax;
			if (header.HasFlag(SyncHeader.RectAnchors)) {
				if (SyncRectAnchorMin.HasFlag(NetworkSyncModeVector2.X)) state.RectAnchorMin.x = reader.ReadSingle();
				if (SyncRectAnchorMin.HasFlag(NetworkSyncModeVector2.Y)) state.RectAnchorMin.y = reader.ReadSingle();
				if (SyncRectAnchorMax.HasFlag(NetworkSyncModeVector2.X)) state.RectAnchorMax.x = reader.ReadSingle();
				if (SyncRectAnchorMax.HasFlag(NetworkSyncModeVector2.Y)) state.RectAnchorMax.y = reader.ReadSingle();
			}

			// Read rect size
			state.RectSizeDelta = previous.RectSizeDelta;
			if (header.HasFlag(SyncHeader.RectSizeDelta)) {
				if (SyncRectSizeDelta.HasFlag(NetworkSyncModeVector2.X)) state.RectSizeDelta.x = reader.ReadSingle();
				if (SyncRectSizeDelta.HasFlag(NetworkSyncModeVector2.Y)) state.RectSizeDelta.y = reader.ReadSingle();
			}

			// Read rect pivot
			state.RectPivot = previous.RectPivot;
			if (header.HasFlag(SyncHeader.RectPivot)) {
				if (SyncRectPivot.HasFlag(NetworkSyncModeVector2.X)) state.RectPivot.x = reader.ReadSingle();
				if (SyncRectPivot.HasFlag(NetworkSyncModeVector2.Y)) state.RectPivot.y = reader.ReadSingle();
			}

			// Return state
			state.Time = time;
			return state;

		}

		private void WriteState(Writer writer, SyncState state, SyncHeader header) {

			// Write header
			writer.Write((byte)header);

			// Write rotation
			if (header.HasFlag(SyncHeader.Rotation)) {
				if (SyncRotation.HasFlag(NetworkSyncModeVector3.X)) writer.Write(state.Euler.x);
				if (SyncRotation.HasFlag(NetworkSyncModeVector3.Y)) writer.Write(state.Euler.y);
				if (SyncRotation.HasFlag(NetworkSyncModeVector3.Z)) writer.Write(state.Euler.z);
			}

			// Write position
			if (header.HasFlag(SyncHeader.Position)) {
				if (SyncPosition.HasFlag(NetworkSyncModeVector3.X)) writer.Write(state.Position.x);
				if (SyncPosition.HasFlag(NetworkSyncModeVector3.Y)) writer.Write(state.Position.y);
				if (SyncPosition.HasFlag(NetworkSyncModeVector3.Z)) writer.Write(state.Position.z);
			}

			// Write scale
			if (header.HasFlag(SyncHeader.Scale)) {
				if (SyncScale.HasFlag(NetworkSyncModeVector3.X)) writer.Write(state.Scale.x);
				if (SyncScale.HasFlag(NetworkSyncModeVector3.Y)) writer.Write(state.Scale.y);
				if (SyncScale.HasFlag(NetworkSyncModeVector3.Z)) writer.Write(state.Scale.z);
			}
			
			// Write velocity
			if (header.HasFlag(SyncHeader.Velocity)) {
				if (SyncVelocity.HasFlag(NetworkSyncModeVector3.X)) writer.Write(state.Velocity.x);
				if (SyncVelocity.HasFlag(NetworkSyncModeVector3.Y)) writer.Write(state.Velocity.y);
				if (SyncVelocity.HasFlag(NetworkSyncModeVector3.Z)) writer.Write(state.Velocity.z);
			}

			// Write angular velocity
			if (header.HasFlag(SyncHeader.AngularVelocity)) {
				if (SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.X)) writer.Write(state.AngularVelocity.x);
				if (SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.Y)) writer.Write(state.AngularVelocity.y);
				if (SyncAngularVelocity.HasFlag(NetworkSyncModeVector3.Z)) writer.Write(state.AngularVelocity.z);
			}

			// Write rect anchors
			if (header.HasFlag(SyncHeader.RectAnchors)) {
				if (SyncRectAnchorMin.HasFlag(NetworkSyncModeVector2.X)) writer.Write(state.RectAnchorMin.x);
				if (SyncRectAnchorMin.HasFlag(NetworkSyncModeVector2.Y)) writer.Write(state.RectAnchorMin.y);
				if (SyncRectAnchorMax.HasFlag(NetworkSyncModeVector2.X)) writer.Write(state.RectAnchorMax.x);
				if (SyncRectAnchorMax.HasFlag(NetworkSyncModeVector2.Y)) writer.Write(state.RectAnchorMax.y);
			}

			// Write rect size
			if (header.HasFlag(SyncHeader.RectSizeDelta)) {
				if (SyncRectSizeDelta.HasFlag(NetworkSyncModeVector2.X)) writer.Write(state.RectSizeDelta.x);
				if (SyncRectSizeDelta.HasFlag(NetworkSyncModeVector2.Y)) writer.Write(state.RectSizeDelta.y);
			}

			// Write rect pivot
			if (header.HasFlag(SyncHeader.RectPivot)) {
				if (SyncRectPivot.HasFlag(NetworkSyncModeVector2.X)) writer.Write(state.RectPivot.x);
				if (SyncRectPivot.HasFlag(NetworkSyncModeVector2.Y)) writer.Write(state.RectPivot.y);
			}

		}

		private struct NetworkMessageTransform : INetworkMessage {

			public bool Timed => true;
			public bool Reliable => false;
			public bool Unique => false;
			public readonly NetworkTransform Transform;
			public readonly SyncState State;
			public readonly SyncHeader Header;

			public NetworkMessageTransform(NetworkTransform transform, SyncState state, SyncHeader header) {
				Transform = transform;
				State = state;
				Header = header;
			}

			public void Write(Writer writer) {
				Transform.WriteState(writer, State, Header);
			}

		}

	}

}
