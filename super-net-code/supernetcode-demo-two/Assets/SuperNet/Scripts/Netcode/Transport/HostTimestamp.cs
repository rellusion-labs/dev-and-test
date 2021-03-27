using System;

namespace SuperNet.Netcode.Transport {

	/// <summary>
	/// Stores a local timestamp of an event accurate down to a millisecond.
	/// </summary>
	public struct HostTimestamp {

		/// <summary>Host that created this timestamp.</summary>
		public readonly Host Host;

		/// <summary>Raw host ticks.</summary>
		public readonly long Ticks;

		/// <summary>Number of days since the creation of this timestamp.</summary>
		public double ElapsedDays => (Host.Ticks - Ticks) / 86400000d;

		/// <summary>Number of hours since the creation of this timestamp.</summary>
		public double ElapsedHours => (Host.Ticks - Ticks) / 3600000d;

		/// <summary>Number of minutes since the creation of this timestamp.</summary>
		public double ElapsedMinutes => (Host.Ticks - Ticks) / 60000d;

		/// <summary>Number of seconds since the creation of this timestamp.</summary>
		public double ElapsedSeconds => (Host.Ticks - Ticks) / 1000d;

		/// <summary>Number of milliseconds since the creation of this timestamp.</summary>
		public long ElapsedMilliseconds => Host.Ticks - Ticks;

		/// <summary>Create a new timestamp at the current host time.</summary>
		/// <param name="host">Host to use.</param>
		internal HostTimestamp(Host host) {
			Host = host ?? throw new ArgumentNullException(nameof(host), "Host is null");
			Ticks = host.Ticks;
		}

		/// <summary>Create a new timestamp from the provided host ticks.</summary>
		/// <param name="host"></param>
		/// <param name="ticks"></param>
		internal HostTimestamp(Host host, long ticks) {
			Host = host ?? throw new ArgumentNullException(nameof(host), "Host is null");
			Ticks = ticks;
		}

	}

}
