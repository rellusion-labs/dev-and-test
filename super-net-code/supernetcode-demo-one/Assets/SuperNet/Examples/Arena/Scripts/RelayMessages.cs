using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;
using System.Collections.Generic;

namespace SuperNet.Examples.Arena {

	public enum RelayMessageType : byte {

		// Sent by the client to the relay to request server list
		ServerListRequest = 254,

		// Sent by the relay to the client who requested it
		ServerListResponse = 253,

		// Sent by the server to the relay to add itself to the server list
		ServerListAdd = 252,

		// Sent by the client to the relay to request a p2p connection
		// Sent by the relay to the server to request a p2p connection
		Connect = 251,

	}

	public struct RelayServerListRequest : IMessage {

		byte IMessage.Channel => (byte)RelayMessageType.ServerListRequest;
		bool IMessage.Timed => false;
		bool IMessage.Reliable => true;
		bool IMessage.Ordered => false;
		bool IMessage.Unique => true;
		short IMessage.Offset => 0;

		public void Read(Reader reader) { }
		public void Write(Writer writer) { }

	}

	public struct RelayServerListResponse : IMessage {

		byte IMessage.Channel => (byte)RelayMessageType.ServerListResponse;
		bool IMessage.Timed => false;
		bool IMessage.Reliable => true;
		bool IMessage.Ordered => false;
		bool IMessage.Unique => true;
		short IMessage.Offset => 0;

		public List<string> Servers;

		public void Read(Reader reader) {
			Servers = new List<string>();
			int count = reader.ReadInt32();
			for (int i = 0; i < count; i++) {
				Servers.Add(reader.ReadString());
			}
		}

		public void Write(Writer writer) {
			writer.Write(Servers.Count);
			foreach (string server in Servers) {
				writer.Write(server);
			}
		}

	}

	public struct RelayServerListAdd : IMessage {

		byte IMessage.Channel => (byte)RelayMessageType.ServerListAdd;
		bool IMessage.Timed => false;
		bool IMessage.Reliable => true;
		bool IMessage.Ordered => false;
		bool IMessage.Unique => true;
		short IMessage.Offset => 0;

		public string Address;

		public void Read(Reader reader) {
			if (reader.Available > 0) {
				Address = reader.ReadString();
			} else {
				Address = null;
			}
		}

		public void Write(Writer writer) {
			writer.Write(Address);
		}

	}

	public struct RelayConnect : IMessage {

		byte IMessage.Channel => (byte)RelayMessageType.Connect;
		bool IMessage.Timed => false;
		bool IMessage.Reliable => true;
		bool IMessage.Ordered => false;
		bool IMessage.Unique => true;
		short IMessage.Offset => 0;

		public string Address;

		public void Read(Reader reader) {
			Address = reader.ReadString();
		}

		public void Write(Writer writer) {
			writer.Write(Address);
		}

	}

}
