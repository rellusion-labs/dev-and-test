using SuperNet.Netcode.Util;

namespace SuperNet.Unity.Core {

	/// <summary>
	/// A network message sent betweeen network behaviours.
	/// </summary>
	public interface INetworkMessage : IWritable {

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
		/// Message is guaranteed not to be duplicated.
		/// </summary>
		bool Unique { get; }

	}
	
}
