using SuperNet.Netcode.Util;

namespace SuperNet.Netcode.Transport {

	/// <summary>
	/// Implements a message that can be sent by the netcode to a connected peer.
	/// </summary>
	public interface IMessage : IWritable {

		/// <summary>
		/// Which data channel to send the message on.
		/// </summary>
		byte Channel { get; }

		/// <summary>
		/// Message includes a timestamp of the moment of creation.
		/// <para>If false, received timestamp might be innacurate due to message delays.</para>
		/// </summary>
		bool Timed { get; }

		/// <summary>
		/// Message requires an acknowledgment and needs to be resent until acknowledged.
		/// <para>This makes sure the message will never be lost.</para>
		/// </summary>
		bool Reliable { get; }

		/// <summary>
		/// Message must be delivered in order within the channel.
		/// <para>Any unreliable messages that arrive out of order are dropped.</para>
		/// <para>Any reliable messages that arrive out of order are reordered automatically.</para>
		/// </summary>
		bool Ordered { get; }

		/// <summary>
		/// Message is guaranteed not to be duplicated.
		/// </summary>
		bool Unique { get; }

		/// <summary>
		/// Timestamp offset in milliseconds to apply when sending the message. Set to 0 for no offset.
		/// <para>If message is not timed, this is ignored.</para>
		/// </summary>
		short Offset { get; }

	}
	
}
