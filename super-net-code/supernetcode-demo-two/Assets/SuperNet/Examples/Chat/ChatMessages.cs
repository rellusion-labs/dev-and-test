using SuperNet.Netcode.Transport;
using SuperNet.Netcode.Util;

namespace SuperNet.Examples.Chat {

	public enum ServerMessageType : byte {

		Join = 1,
		Leave = 2,
		Chat = 3,

	}

	public enum ClientMessageType : byte {

		Chat = 1,

	}

	// Sent by the server to clients when a new client joins
	public struct ServerMessageJoin : IMessage {

		public byte Channel => (byte)ServerMessageType.Join;
		public bool Timed => false;
		public bool Reliable => true;
		public bool Ordered => true;
		public bool Unique => true;
		public short Offset => 0;

		public string Name;

		public void Read(Reader reader) {
			Name = reader.ReadString();
		}

		public void Write(Writer writer) {
			writer.Write(Name);
		}

	}

	// Sent by the server to clients when a client leaves
	public struct ServerMessageLeave : IMessage {

		public byte Channel => (byte)ServerMessageType.Leave;
		public bool Timed => false;
		public bool Reliable => true;
		public bool Ordered => true;
		public bool Unique => true;
		public short Offset => 0;

		public string Name;

		public void Read(Reader reader) {
			Name = reader.ReadString();
		}

		public void Write(Writer writer) {
			writer.Write(Name);
		}

	}

	// Sent by the server to clients when a client sends a chat message
	public struct ServerMessageChat : IMessage {

		public byte Channel => (byte)ServerMessageType.Chat;
		public bool Timed => false;
		public bool Reliable => true;
		public bool Ordered => false;
		public bool Unique => true;
		public short Offset => 0;

		public string Name;
		public string Message;

		public void Write(Writer writer) {
			writer.Write(Name);
			writer.Write(Message);
		}

		public void Read(Reader reader) {
			Name = reader.ReadString();
			Message = reader.ReadString();
		}

	}

	// Sent by the client to server when a chat message is sent
	public struct ClientMessageChat : IMessage {

		public byte Channel => (byte)ClientMessageType.Chat;
		public bool Timed => false;
		public bool Reliable => true;
		public bool Ordered => true;
		public bool Unique => true;
		public short Offset => 0;

		public string Message;

		public void Read(Reader reader) {
			Message = reader.ReadString();
		}

		public void Write(Writer writer) {
			writer.Write(Message);
		}

	}

}
