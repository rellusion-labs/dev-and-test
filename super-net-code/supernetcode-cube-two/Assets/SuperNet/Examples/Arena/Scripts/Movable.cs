using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using SuperNet.Unity.Components;
using SuperNet.Unity.Core;
using UnityEngine;

namespace SuperNet.Examples.Arena {

	public class Movable : NetworkComponent {

		// Scene objects
		public Rigidbody Body;
		public NetworkTransform NetworkTransform;

		private void Reset() {
			ResetNetworkIdentity();
			Body = GetComponent<Rigidbody>();
			NetworkTransform = GetComponentInChildren<NetworkTransform>();
		}

		public void ClaimAuthority() {
			// Notify all peers with this object that we are claiming authority over it
			SendNetworkMessageAll(new NetworkMessageClaimAuthority());
			NetworkTransform.Authority = true;
		}

		public override void OnNetworkPeerRegister(Peer peer) {
			// A remote peer has registered this object
			// If we have authority, notify them about it
			if (NetworkTransform.Authority) {
				SendNetworkMessage(peer, new NetworkMessageClaimAuthority());
			}
		}

		public override void OnNetworkMessage(Peer peer, Reader reader, HostTimestamp timestamp) {

			// If this message is from a local connection, ignore
			if (Host.IsLocal(peer.Remote)) return;

			// Remote peer has claimed authority over this object
			// Remove authority from our own object to start receiving updates
			NetworkTransform.Authority = false;

		}

		/// <summary>Message sent when authority is claimed.</summary>
		private struct NetworkMessageClaimAuthority : INetworkMessage {
			public bool Timed => false;
			public bool Reliable => true;
			public bool Unique => true;
			public void Write(Writer writer) {}
		}

	}

}
