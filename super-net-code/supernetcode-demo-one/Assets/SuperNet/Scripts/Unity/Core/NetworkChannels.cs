using UnityEngine;

namespace SuperNet.Unity.Core {

	/// <summary>
	/// Channels used by unity components.
	/// </summary>
	public enum NetworkChannels : byte {

		/// <summary>
		/// A component was registered on a peer.
		/// </summary>
		ComponentRegister = 254,

		/// <summary>
		/// A component was unregistered on a peer.
		/// </summary>
		ComponentUnregister = 253,

		/// <summary>
		/// A message sent by a component to other components on the network.
		/// </summary>
		ComponentMessage = 252,

	}

}
