using System;
using System.Net;
using SuperNet.Netcode.Util;

namespace SuperNet.Netcode.Transport {

	/// <summary>
	/// A connection request received by an active host.
	/// </summary>
	public class ConnectionRequest : IDisposable {

		/// <summary>The host that received this request.</summary>
		public readonly Host Host;

		/// <summary>Remote address that the request was received from.</summary>
		public readonly IPEndPoint Remote;

		/// <summary>Exchange key received in the request or empty if no encryption.</summary>
		internal readonly ArraySegment<byte> Key;

		/// <summary>Random data to be signed or empty if authentication is disabled.</summary>
		internal readonly ArraySegment<byte> Random;

		/// <summary>True if the underlying buffers for the request have been repurposed for something else.</summary>
		public bool Disposed { get; private set; }

		/// <summary>True if remote peer requires encryption.</summary>
		public bool Encrypted => Key.Count > 0;

		/// <summary>True if remote peer requires us to authenticate.</summary>
		public bool Authenticate => Random.Count > 0;

		/// <summary>Used internally by the netcode to create a new connection request.</summary>
		/// <param name="host">Host that received the request.</param>
		/// <param name="remote">Remote address that the request was received from.</param>
		/// <param name="key">Exchange key received in the request.</param>
		/// <param name="random">Random data to be signed.</param>
		internal ConnectionRequest(Host host, IPEndPoint remote, ArraySegment<byte> key, ArraySegment<byte> random) {
			Host = host;
			Remote = remote;
			Key = key;
			Random = random;
			Disposed = false;
		}

		/// <summary>
		/// Used internally by the netcode to invalidate the request, making it unable to be accepted.
		/// <para>This is called when the underlying buffers have been repurposed for something else.</para>
		/// </summary>
		public void Dispose() {
			Disposed = true;
		}

		/// <summary>Reject the request by sending a reject message.</summary>
		/// <param name="message">Message to reject with.</param>
		public void Reject(IWritable message = null) {
			Host.Reject(this, message);
			Disposed = true;
		}

		/// <summary>Accept the request, create a new peer and establish a connection.</summary>
		/// <param name="config">Peer configuration values. If null, default is used.</param>
		/// <param name="listener">Peer listener. If null, event based listener is created.</param>
		/// <returns>The created peer.</returns>
		public Peer Accept(PeerConfig config, IPeerListener listener) {
			return Host.Accept(this, config, listener);
		}

	}

}
