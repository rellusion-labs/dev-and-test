using System.Threading;
using SuperNet.Netcode.Util;

namespace SuperNet.Netcode.Transport {

	/// <summary>
	/// Network message that has been sent to a connected peer.
	/// </summary>
	public class MessageSent {
		
		/// <summary>Peer that the message was sent through.</summary>
		public readonly Peer Peer;

		/// <summary>Listener used for this message or null if not provided.</summary>
		public readonly IMessageListener Listener;

		/// <summary>Message payload that is used to write to internal buffers.</summary>
		public readonly IWritable Payload;

		/// <summary>Internal message type.</summary>
		internal readonly MessageType Type;

		/// <summary>Internal message flags.</summary>
		internal readonly MessageFlags Flags;

		/// <summary>Internal sequence number of the message.</summary>
		public readonly ushort Sequence;

		/// <summary>Timestamp offset to apply when sending the message.</summary>
		public readonly short Offset;

		/// <summary>Data channel this message is sent over.</summary>
		public readonly byte Channel;

		/// <summary>Number of times this message has been sent.</summary>
		public int Attempts;

		/// <summary>Host timestamp at the moment of creation of this message.</summary>
		public HostTimestamp TimeCreated;

		/// <summary>Host timestamp at the moment the message was sent to the network socket.</summary>
		public HostTimestamp TimeSent;

		/// <summary>True if message is reliable and has been acknowledged.</summary>
		public bool Acknowledged;

		/// <summary>Cancellation token used to cancel resending of this message.</summary>
		internal readonly CancellationTokenSource Token;

		/// <summary>Sent message is timed.</summary>
		public bool Timed => Flags.HasFlag(MessageFlags.Timed);

		/// <summary>Sent message is reliable.</summary>
		public bool Reliable => Flags.HasFlag(MessageFlags.Reliable);

		/// <summary>Sent message is ordered.</summary>
		public bool Ordered => Flags.HasFlag(MessageFlags.Ordered);

		/// <summary>Sent message is unique.</summary>
		public bool Unique => Flags.HasFlag(MessageFlags.Unique);

		/// <summary>Used internally by the netcode to create a new sent message.</summary>
		internal MessageSent(
			Peer peer,
			IMessageListener listener,
			IWritable payload,
			MessageType type,
			MessageFlags flags,
			ushort sequence,
			short offset,
			byte channel
		) {
			Peer = peer;
			Listener = listener;
			Payload = payload;
			Type = type;
			Flags = flags;
			Sequence = sequence;
			Offset = offset;
			Channel = channel;
			Attempts = 0;
			TimeCreated = peer.Host.Timestamp;
			TimeSent = peer.Host.Timestamp;
			Acknowledged = false;
			Token = flags.HasFlag(MessageFlags.Reliable) ? new CancellationTokenSource() : null;
		}

		/// <summary>Stop resending this message if reliable. May cause the message to be lost.</summary>
		public void StopResending() {
			try { Token?.Cancel(); } catch { }
			try { Token?.Dispose(); } catch { }
		}
		
		/// <summary>Used internally by the netcode to notify listener.</summary>
		internal void OnMessageSend() {
			Attempts++;
			TimeSent = Peer.Host.Timestamp;
			Listener?.OnMessageSend(Peer, this);
		}

		/// <summary>Used internally by the netcode to notify listener.</summary>
		internal void OnMessageAcknowledge() {
			Acknowledged = true;
			Listener?.OnMessageAcknowledge(Peer, this);
		}

	}

}
