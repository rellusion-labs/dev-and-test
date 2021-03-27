using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using SuperNet.Unity.Components;
using SuperNet.Unity.Core;
using UnityEngine;

namespace SuperNet.Examples.Arena {

	public class PlayerController : NetworkComponent {

		// Movement settings
		private const float AccelerationStandingForward = 45f;
		private const float AccelerationStandingBackward = 25f;
		private const float AccelerationStandingSideways = 40f;
		private const float AccelerationCrouchingForward = 16f;
		private const float AccelerationCrouchingBackward = 14f;
		private const float AccelerationCrouchingSideways = 14f;
		private const float AccelerationFallingForward = 4f;
		private const float AccelerationFallingBackward = 2f;
		private const float AccelerationFallingSideways = 2f;
		private const float MaxSpeedStandingForward = 4.5f;
		private const float MaxSpeedStandingBackward = 2.5f;
		private const float MaxSpeedStandingSideways = 4f;
		private const float MaxSpeedCrouchingForward = 1.5f;
		private const float MaxSpeedCrouchingBackward = 1.4f;
		private const float MaxSpeedCrouchingSideways = 1.4f;
		private const float BodyJumpVelocity = 6f;
		private const float BodyDragStanding = 8f;
		private const float BodyDragCrouching = 8f;
		private const float BodyDragFalling = 0.5f;
		private const float BodyGravity = 10f;

		// Scene objects
		public Rigidbody Body;
		public NetworkAnimator NetworkAnimator;
		public NetworkTransform NetworkTransform;
		public Transform FloorRaycastSource;
		public Transform FloorRaycastTarget;
		public Transform ViewTarget;
		public PlayerView View;

		// Resources
		private PlayerAnimation Animation;
		private Vector3 Force;
		private Peer Owner;
		private bool Local;

		private void Reset() {
			ResetNetworkIdentity();
			Body = GetComponentInChildren<Rigidbody>();
			NetworkAnimator = GetComponentInChildren<NetworkAnimator>();
			NetworkTransform = GetComponentInChildren<NetworkTransform>();
			FloorRaycastSource = transform.Find("RaycastSource").transform;
			FloorRaycastTarget = transform.Find("RaycastTarget").transform;
			ViewTarget = transform.Find("ViewTarget").transform;
			View = null;
		}

		private void Awake() {

			// Initialize resources
			View = FindObjectOfType<PlayerView>();
			Animation = PlayerAnimation.Standing;
			Force = Vector3.zero;
			Owner = null;
			Local = false;

			// Set initial animation
			SetAnimation(PlayerAnimation.Standing);

		}

		private void Update() {
			if (Local) {
				// This is a local player, perform input based movement
				UpdateLocal();
			} else {
				// This is a remote player, just apply animator values
				SetAnimationValues(transform.rotation);
			}
		}

		private void FixedUpdate() {

			// Apply gravity
			Body.AddForce(Vector3.down * BodyGravity, ForceMode.Acceleration);

			// Apply input force
			if (Local) Body.AddForce(Force, ForceMode.Acceleration);

		}

		public void OnServerSpawn(Peer peer) {

			// Called on the server by the menu when the player is first spawned

			if (Host.IsLocal(peer.Remote)) {
				// Peer is connected locally, so just mark this player as local
				ClaimAuthority();
			} else {
				// Peer is connected remotely, save it for later
				Owner = peer;
			}

		}

		private void ClaimAuthority() {
			// Claim this player as a local player
			NetworkAnimator.Authority = true;
			NetworkTransform.Authority = true;
			View.Target = ViewTarget;
			Local = true;
		}

		public override void OnNetworkPeerRegister(Peer peer) {
			if (peer == Owner) {
				// Notify remote peer that they are the owner
				SendNetworkMessage(peer, new NetworkMessageGiveAuthority());
			}
		}

		public override void OnNetworkPeerUnregister(Peer peer) {
			if (peer == Owner) {
				// Owner has unregisted this player, despawn it
				Run(() => Destroy(gameObject));
			}
		}

		public override void OnNetworkMessage(Peer peer, Reader reader, HostTimestamp timestamp) {
			// Server has notified us we are the owner
			Run(() => ClaimAuthority());
		}

		private void UpdateLocal() {

			// Check if touching floor
			Vector3 rOrigin = FloorRaycastSource.position;
			Vector3 rDirection = FloorRaycastTarget.position - rOrigin;
			bool isOnFloor = Physics.Raycast(rOrigin, rDirection, out RaycastHit rHit, rDirection.magnitude);
			
			// If travelling upwards, we're not on the floor
			float speedUpwards = Vector3.Dot(Body.velocity, transform.up);
			if (speedUpwards > BodyJumpVelocity * 0.5f) {
				isOnFloor = false;
			}

			// Get input direction
			PlayerView view = View;
			Vector3 inputDirection = Vector3.zero;
			Quaternion viewRotation = transform.rotation;
			if (view != null) {
				viewRotation = Quaternion.Euler(0, view.RotationY, 0);
				if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) inputDirection += viewRotation * Vector3.forward;
				if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) inputDirection += viewRotation * Vector3.back;
				if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inputDirection += viewRotation * Vector3.left;
				if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputDirection += viewRotation * Vector3.right;
			}

			// Get acceleration values
			GetAcceleration(out float AccelerationForward, out float AccelerationBackward, out float AccelerationSideways);

			// Convert input direction to force
			if (inputDirection.magnitude > 0.5f) {
				Vector3 directionLocal = Quaternion.Inverse(transform.rotation) * inputDirection.normalized;
				float z = (directionLocal.z > 0f ? AccelerationForward : AccelerationBackward);
				Vector3 forceLocal = new Vector3(directionLocal.x * AccelerationSideways, 0f, directionLocal.z * z);
				Force = transform.rotation * forceLocal;
			} else {
				Force = Vector3.zero;
			}

			// Set animator values
			SetAnimationValues(viewRotation);

			// Rotate towards view direction
			transform.rotation = Quaternion.Slerp(transform.rotation, viewRotation, 1 - Mathf.Pow(0.02f, Time.deltaTime));

			// Jump
			if (isOnFloor && Input.GetKeyDown(KeyCode.Space) && Animation != PlayerAnimation.Falling) {
				SetAnimation(PlayerAnimation.Falling);
				Body.AddForce(transform.up * BodyJumpVelocity, ForceMode.VelocityChange);
				isOnFloor = false;
			}

			// Change animation based on input
			switch (Animation) {
				default:
				case PlayerAnimation.Standing:
					if (isOnFloor && Input.GetKeyDown(KeyCode.Space)) {
						SetAnimation(PlayerAnimation.Falling);
						Body.AddForce(transform.up * BodyJumpVelocity, ForceMode.VelocityChange);
					} else if (isOnFloor && Input.GetKey(KeyCode.LeftShift)) {
						SetAnimation(PlayerAnimation.Crouching);
					} else if (!isOnFloor) {
						SetAnimation(PlayerAnimation.Falling);
					}
					break;
				case PlayerAnimation.Crouching:
					if (isOnFloor && Input.GetKeyDown(KeyCode.Space)) {
						SetAnimation(PlayerAnimation.Falling);
						Body.AddForce(transform.up * BodyJumpVelocity, ForceMode.VelocityChange);
					} else if (isOnFloor && !Input.GetKey(KeyCode.LeftShift)) {
						SetAnimation(PlayerAnimation.Standing);
					} else if (!isOnFloor) {
						SetAnimation(PlayerAnimation.Falling);
					}
					break;
				case PlayerAnimation.Falling:
					if (isOnFloor && Input.GetKey(KeyCode.LeftShift)) {
						SetAnimation(PlayerAnimation.Crouching);
					} else if (isOnFloor) {
						SetAnimation(PlayerAnimation.Standing);
					}
					break;
			}

		}

		private void SetAnimationValues(Quaternion viewRotation) {

			// Get max speeds
			GetMaxSpeed(out float MaxSpeedForward, out float MaxSpeedBackward, out float MaxSpeedSideways);

			// Set animator values
			Vector3 velocity = Vector3.ProjectOnPlane(transform.InverseTransformDirection(Body.velocity), Vector3.up);
			float movingSpeedX = velocity.x / MaxSpeedSideways;
			float movingSpeedZ = velocity.z / (velocity.z > 0f ? MaxSpeedForward : MaxSpeedBackward);
			float movingSpeed = Mathf.Clamp(new Vector2(movingSpeedX, movingSpeedZ).magnitude, 0, 1);
			float rotateSpeed = (1f - movingSpeed) * Vector3.SignedAngle(transform.forward, viewRotation * Vector3.forward, transform.up) / 40f;
			float idleAmount = Mathf.Clamp(new Vector2(movingSpeed, rotateSpeed).magnitude, 0, 1);
			NetworkAnimator.Animator.SetFloat("RunIdle", Mathf.Clamp(1f - idleAmount, 0, 1));
			NetworkAnimator.Animator.SetFloat("RunForward", Mathf.Clamp(movingSpeedZ, 0, 1) * idleAmount);
			NetworkAnimator.Animator.SetFloat("RunBackward", Mathf.Clamp(-movingSpeedZ, 0, 1) * idleAmount);
			NetworkAnimator.Animator.SetFloat("RunLeft", Mathf.Clamp(-movingSpeedX, 0, 1) * idleAmount);
			NetworkAnimator.Animator.SetFloat("RunRight", Mathf.Clamp(movingSpeedX, 0, 1) * idleAmount);
			NetworkAnimator.Animator.SetFloat("RotatingLeft", Mathf.Clamp(-rotateSpeed, 0, 1) * idleAmount);
			NetworkAnimator.Animator.SetFloat("RotatingRight", Mathf.Clamp(rotateSpeed, 0, 1) * idleAmount);

		}

		private void SetAnimation(PlayerAnimation animation) {

			// Change animation
			NetworkAnimator.SetTrigger(animation.ToString());
			Animation = animation;

			// Change rigidbody drag
			switch (animation) {
				default:
				case PlayerAnimation.Standing:
					Body.drag = BodyDragStanding;
					break;
				case PlayerAnimation.Crouching:
					Body.drag = BodyDragCrouching;
					break;
				case PlayerAnimation.Falling:
					Body.drag = BodyDragFalling;
					break;
			}

		}

		private void GetMaxSpeed(out float MaxSpeedForward, out float MaxSpeedBackward, out float MaxSpeedSideways) {
			switch (Animation) {
				default:
				case PlayerAnimation.Standing:
					MaxSpeedForward = MaxSpeedStandingForward;
					MaxSpeedBackward = MaxSpeedStandingBackward;
					MaxSpeedSideways = MaxSpeedStandingSideways;
					break;
				case PlayerAnimation.Crouching:
					MaxSpeedForward = MaxSpeedCrouchingForward;
					MaxSpeedBackward = MaxSpeedCrouchingBackward;
					MaxSpeedSideways = MaxSpeedCrouchingSideways;
					break;
				case PlayerAnimation.Falling:
					MaxSpeedForward = MaxSpeedStandingForward;
					MaxSpeedBackward = MaxSpeedStandingBackward;
					MaxSpeedSideways = MaxSpeedStandingSideways;
					break;
			}
		}

		private void GetAcceleration(out float AccelerationForward, out float AccelerationBackward, out float AccelerationSideways) {
			switch (Animation) {
				default:
				case PlayerAnimation.Standing:
					AccelerationForward = AccelerationStandingForward;
					AccelerationBackward = AccelerationStandingBackward;
					AccelerationSideways = AccelerationStandingSideways;
					break;
				case PlayerAnimation.Crouching:
					AccelerationForward = AccelerationCrouchingForward;
					AccelerationBackward = AccelerationCrouchingBackward;
					AccelerationSideways = AccelerationCrouchingSideways;
					break;
				case PlayerAnimation.Falling:
					AccelerationForward = AccelerationFallingForward;
					AccelerationBackward = AccelerationFallingBackward;
					AccelerationSideways = AccelerationFallingSideways;
					break;
			}
		}

		private struct NetworkMessageGiveAuthority : INetworkMessage {
			public bool Timed => false;
			public bool Reliable => true;
			public bool Unique => true;
			public void Write(Writer writer) { }
		}

	}

}
