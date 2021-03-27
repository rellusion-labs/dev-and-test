using System;

namespace SuperNet.Unity.Core {
	
	/// <summary>
	/// Identity used to syncronize components over network.
	/// </summary>
	[Serializable]
	public struct NetworkIdentity : IComparable, IComparable<NetworkIdentity>, IEquatable<NetworkIdentity>, IFormattable {

		/// <summary>
		/// Invalid value.
		/// </summary>
		public const uint VALUE_INVALID = 0;

		/// <summary>
		/// Minimum value for static components.
		/// </summary>
		public const uint VALUE_MIN_STATIC = 1;

		/// <summary>
		/// Maximum value for static components.
		/// </summary>
		public const uint VALUE_MAX_STATIC = 2147483647;

		/// <summary>
		/// Minimum value for dynamic components.
		/// </summary>
		public const uint VALUE_MIN_DYNAMIC = 2147483648;

		/// <summary>
		/// Maximum value for dynamic components.
		/// </summary>
		public const uint VALUE_MAX_DYNAMIC = 4294967295;

		/// <summary>
		/// Raw network ID.
		/// </summary>
		public uint Value;

		/// <summary>
		/// This identity is invalid.
		/// </summary>
		public bool IsInvalid => Value == VALUE_INVALID;

		/// <summary>
		/// This identity if for a static component.
		/// </summary>
		public bool IsStatic => Value >= VALUE_MIN_STATIC && Value <= VALUE_MAX_STATIC;

		/// <summary>
		/// This identity is for a dynamic component.
		/// </summary>
		public bool IsDynamic => Value >= VALUE_MIN_DYNAMIC && Value <= VALUE_MAX_DYNAMIC;

		/// <summary>
		/// Create a new network ID.
		/// </summary>
		/// <param name="value">Raw network ID.</param>
		public NetworkIdentity(uint value) {
			Value = value;
		}
		
		public override bool Equals(object obj) {
			if (obj is NetworkIdentity id) {
				return Value.Equals(id.Value);
			} else {
				return false;
			}
		}

		public bool Equals(NetworkIdentity other) {
			return Value.Equals(other.Value);
		}

		public int CompareTo(NetworkIdentity other) {
			return Value.CompareTo(other.Value);
		}

		public int CompareTo(object obj) {
			if (obj is NetworkIdentity id) {
				return Value.CompareTo(id.Value);
			} else {
				return 1;
			}
		}

		public override int GetHashCode() {
			return Value.GetHashCode();
		}

		public override string ToString() {
			return Value.ToString();
		}

		public string ToString(string format, IFormatProvider provider) {
			return Value.ToString(format, provider);
		}

		public static bool operator ==(NetworkIdentity lhs, NetworkIdentity rhs) {
			return lhs.Value == rhs.Value;
		}

		public static bool operator !=(NetworkIdentity lhs, NetworkIdentity rhs) {
			return lhs.Value != rhs.Value;
		}

		public static explicit operator uint(NetworkIdentity id) {
			return id.Value;
		}

		public static implicit operator NetworkIdentity(uint value) {
			return new NetworkIdentity(value);
		}

	}

}
