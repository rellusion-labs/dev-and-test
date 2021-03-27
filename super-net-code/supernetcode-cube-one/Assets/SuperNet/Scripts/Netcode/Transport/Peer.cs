using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SuperNet.Netcode.Crypto;
using SuperNet.Netcode.Util;

namespace SuperNet.Netcode.Transport {

	/// <summary>
	/// Manages an active network connection.
	/// </summary>
	public class Peer : IDisposable {

		//////////////////////////////// Peer state ////////////////////////////////

		/// <summary>True if peer has been disposed.</summary>
		public bool Disposed => StateDisposed == 1;

		/// <summary>True if messages can be sent.</summary>
		public bool Connected => !Disposed && StateConnected == 1;

		/// <summary>True if peer is in the process of connecting.</summary>
		public bool Connecting => !Disposed && StateConnected == 0;

		/// <summary>Host used to manage this peer.</summary>
		public readonly Host Host;

		/// <summary>Address this peer is connected to.</summary>
		public readonly IPEndPoint Remote;

		/// <summary>Configuration values for this peer.</summary>
		public readonly PeerConfig Config;

		/// <summary>Listener used by this peer.</summary>
		public readonly IPeerListener Listener;

		/// <summary>Packet statistics.</summary>
		public readonly PeerStatistics Statistics;

		/// <summary>Cancelled when peer is disposed.</summary>
		private readonly CancellationTokenSource DisposeToken;

		/// <summary>Current connection state. Interlocked.</summary>.
		private int StateConnected;

		/// <summary>Current dispose state. Interlocked.</summary>.
		private int StateDisposed;

		//////////////////////////////// Timings ////////////////////////////////

		/// <summary>Current round trip time (ping) in milliseconds.</summary>
		public ushort RTT => (ushort)TimeRTT;

		/// <summary>Difference in last 16 bits of ticks between peers. Interlocked.</summary>
		private int TimeDelta;

		/// <summary>Current round trip time (ping) in milliseconds. Interlocked.</summary>
		private int TimeRTT;

		//////////////////////////////// Received unique messages ///////////////////////////////

		/// <summary>Lock used for access to received unique messages.</summary>
		private readonly object UniqueLock;

		/// <summary>Received channel/sequence for unique messages to check for duplicates.</summary>
		private HashSet<Tuple<byte, ushort>> UniqueReceived;

		//////////////////////////////// Received fragment ///////////////////////////////

		/// <summary>Lock used for access to the received fragment.</summary>
		private readonly object FragmentLock;

		/// <summary>Used to cancel fragment timeout task.</summary>
		private CancellationTokenSource FragmentCancel;

		/// <summary>Received bitfield.</summary>
		private byte[] FragmentReceived;

		/// <summary>Fragment data.</summary>
		private byte[] FragmentDataParts;

		/// <summary>Last fragment data.</summary>
		private byte[] FragmentDataLast;

		/// <summary>Fragment length.</summary>
		private int FragmentLengthParts;

		/// <summary>Last fragment length.</summary>
		private int FragmentLengthLast;

		/// <summary>Fragment ID</summary>
		private ushort FragmentID;

		/// <summary>Last fragment part.</summary>
		private ushort FragmentLast;

		//////////////////////////////// Received ordered messages ////////////////////////////////

		/// <summary>Lock used for access to ordered messages.</summary>
		private readonly object OrderedLock;

		/// <summary>Used to signal delayed ordered messages to check their sequences.</summary>
		private CancellationTokenSource[] OrderedSignal;
		
		/// <summary>Current receive sequence for all channels for ordered messages.</summary>
		private int[] OrderedSequence;

		//////////////////////////////// Sequences ////////////////////////////////

		/// <summary>Lock used for access to received sequence.</summary>
		private readonly object SequenceReceiveLock;
		
		/// <summary>Received sequence for all channels. Interlocked.</summary>
		private int[] SequenceReceive;

		/// <summary>Lock used for access to sent sequence.</summary>
		private readonly object SequenceSendLock;

		/// <summary>Sent sequence for all channels. Interlocked.</summary>
		private int[] SequenceSend;

		/// <summary>Lock used for access to sent consecutive unsequenced messages.</summary>
		private readonly object SequenceUnsequencedLock;

		/// <summary>Sent consecutive unsequenced messages for all channels. Interlocked.</summary>
		private int[] SequenceUnsequenced;
		
		/// <summary>Sent fragment ID. Interlocked.</summary>
		private int SequenceFragment;

		//////////////////////////////// Sent reliable messages ////////////////////////////////

		/// <summary>Lock used for access to sent reliable messages.</summary>
		private readonly object ReliablesLock;

		/// <summary>Queued reliable messages to be resent. Key is channel/sequence tuple.</summary>
		private Dictionary<Tuple<byte, ushort>, MessageSent> ReliablesSent;

		//////////////////////////////// Flush task ////////////////////////////////

		/// <summary>Lock used for access to flush queue.</summary>
		private readonly object FlushLock;

		/// <summary>Writer to write messages to before they are flushed.</summary>
		private Writer FlushWriter;
		
		/// <summary>Flush task that compresses, encrypts and fragments all queued messages.</summary>
		private Task FlushTask;

		/// <summary>If true, some messages in flush queue are timed.</summary>
		private bool FlushTimed;

		/// <summary>Number of messages in queue.</summary>
		private int FlushCount;

		//////////////////////////////// Connection ////////////////////////////////

		/// <summary>Lock used for access to connection state fields.</summary>
		private readonly object ConnectLock;

		/// <summary>Cancellation token that stops connector task when cancelled.</summary>
		private CancellationTokenSource ConnectCancel;
		
		/// <summary>Key exchanger used for the connection.</summary>
		private ICryptoExchanger ConnectExchanger;

		/// <summary>Encryptor used for the connection.</summary>
		private ICryptoEncryptor ConnectEncryptor;

		/// <summary>Random data that the remote peer has to sign.</summary>
		private byte[] ConnectRandom;

		/// <summary>Pinger task that periodically sends ping messages.</summary>
		private Task ConnectPinger;

		//////////////////////////////// Constants ////////////////////////////////

		private const byte ChannelDefault = 0;
		private const byte ChannelDisconnect = 0;
		private const byte ChannelPinger = 0;

		//////////////////////////////// Implementation ////////////////////////////////

		/// <summary>Used internally by the netcode to create a new peer.</summary>
		internal Peer(Host host, IPEndPoint remote, PeerConfig config, IPeerListener listener) {

			// Peer state
			Host = host ?? throw new ArgumentNullException(nameof(host), "Host is null");
			Remote = remote ?? throw new ArgumentNullException(nameof(remote), "Remote address is null");
			Config = config ?? new PeerConfig();
			Listener = listener ?? new PeerEvents();
			Statistics = new PeerStatistics();
			DisposeToken = new CancellationTokenSource();
			StateConnected = 0;
			StateDisposed = 0;
			
			// Timings
			TimeDelta = 0;
			TimeRTT = 0;

			// Received unique messages
			UniqueLock = new object();
			UniqueReceived = Host.Allocator.CreateSet();

			// Received fragment
			FragmentLock = new object();
			FragmentCancel = null;
			FragmentReceived = null;
			FragmentDataParts = null;
			FragmentDataLast = null;
			FragmentLengthParts = 0;
			FragmentLengthLast = 0;
			FragmentID = 0;
			FragmentLast = 0;

			// Received ordered messages
			OrderedLock = new object();
			OrderedSignal = Host.Allocator.TokensNew(256);
			OrderedSequence = Host.Allocator.SequenceNew(256);
			Array.Clear(OrderedSignal, 0, 256);
			Array.Clear(OrderedSequence, 0, 256);

			// Sequences
			SequenceReceiveLock = new object();
			SequenceSendLock = new object();
			SequenceUnsequencedLock = new object();
			SequenceReceive = Host.Allocator.SequenceNew(256);
			SequenceSend = Host.Allocator.SequenceNew(256);
			SequenceUnsequenced = Host.Allocator.SequenceNew(256);
			SequenceFragment = 0;
			Array.Clear(SequenceReceive, 0, 256);
			Array.Clear(SequenceSend, 0, 256);
			Array.Clear(SequenceUnsequenced, 0, 256);

			// Sent reliable messages
			ReliablesLock = new object();
			ReliablesSent = new Dictionary<Tuple<byte, ushort>, MessageSent>();

			// Flush task
			FlushLock = new object();
			FlushTask = Task.CompletedTask;
			FlushWriter = null;
			FlushTimed = false;
			FlushCount = 0;
			
			// Connection
			ConnectLock = new object();
			ConnectCancel = null;
			ConnectExchanger = null;
			ConnectEncryptor = null;
			ConnectRandom = null;
			ConnectPinger = Task.CompletedTask;

		}

		/// <summary>Instantly dispose of all resources held by this peer.</summary>
		public void Dispose() {

			// Update disposed state and do nothing if already disposed
			try {
				int disposed = Interlocked.Exchange(ref StateDisposed, 1);
				if (disposed == 1) return;
			} catch { }

			// Update connected state and notify listener if still connected
			try {
				int connected = Interlocked.Exchange(ref StateConnected, 0);
				if (connected == 1) {
					Task.Factory.StartNew(
						() => Listener.OnPeerDisconnect(this, null, DisconnectReason.Disposed, null),
						TaskCreationOptions.PreferFairness
					);
				}
			} catch { }

			// Remove peer from host
			try { Host.OnPeerDispose(this); } catch { }

			// Dispose token
			try { DisposeToken.Cancel(); } catch { }
			try { DisposeToken.Dispose(); } catch { }

			// Remove all received unique messages
			try {
				lock (UniqueLock) {
					Host.Allocator.ReturnSet(ref UniqueReceived);
				}
			} catch { }
			
			// Delete received fragment
			try {
				lock (FragmentLock) {
					FragmentDispose();
				}
			} catch { }
			
			// Remove all delayed ordered messages
			try {
				lock (OrderedLock) {
					Host.Allocator.TokensReturn(ref OrderedSignal);
					Host.Allocator.SequenceReturn(ref OrderedSequence);
				}
			} catch { }

			// Return all sequence arrays
			try { lock (SequenceReceiveLock) Host.Allocator.SequenceReturn(ref SequenceReceive); } catch { }
			try { lock (SequenceSendLock) Host.Allocator.SequenceReturn(ref SequenceSend); } catch { }
			try { lock (SequenceUnsequencedLock) Host.Allocator.SequenceReturn(ref SequenceUnsequenced); } catch { }

			// Stop resending all reliable messages
			try {
				lock (ReliablesLock) {
					Host.Allocator.ReturnSent(ref ReliablesSent);
				}
			} catch { }

			// Delete all queued messages
			try {
				lock (FlushLock) {
					FlushWriter?.Dispose();
					FlushWriter = null;
					FlushTimed = false;
					FlushCount = 0;
				}
			} catch { }
			
			// Dispose connection resources
			try {
				lock (ConnectLock) {
					ConnectCancel?.Cancel();
					ConnectCancel?.Dispose();
					ConnectExchanger?.Dispose();
					ConnectEncryptor?.Dispose();
					Host.Allocator.ReturnPacket(ref ConnectRandom);
					ConnectCancel = null;
					ConnectExchanger = null;
				}
			} catch { }
			
		}

		/// <summary>Used internally by the netcode to connect to an active remote host.</summary>
		internal void Connect(IWritable message = null) {

			// Validate
			if (StateDisposed == 1) throw new ObjectDisposedException(GetType().FullName);

			// If already connected, do nothing
			if (StateConnected == 1) return;

			// Disposable resources
			byte[] requestPacket = null;
			Writer writer = null;
			
			try {

				// Serialize custom message if not null
				writer = message == null ? null : new Writer(Host.Allocator, 0);
				message?.Write(writer);

				lock (ConnectLock) {

					// Create a new key exchanger
					ConnectEncryptor?.Dispose();
					ConnectExchanger?.Dispose();
					ConnectExchanger = Host.Config.Encryption ? Host.Exchanger.Invoke() : null;

					// Create random signature data
					bool HasRemotePublicKey = Config.RemotePublicKey != null && Config.RemotePublicKey.Length > 0;
					Host.Allocator.ReturnPacket(ref ConnectRandom);
					if (HasRemotePublicKey) {
						ConnectRandom = Host.Allocator.CreatePacket(Host.Authenticator.SignatureLength);
						Host.Random.GetBytes(ConnectRandom, 0, Host.Authenticator.SignatureLength);
					}

					// Allocate the request packet
					int lengthHeader = 5 + (Host.Config.CRC32 ? 4 : 0);
					int lengthKey = Host.Config.Encryption ? ConnectExchanger.KeyLength : 0;
					int lengthRandom = HasRemotePublicKey ? Host.Authenticator.SignatureLength : 0;
					int lengthMessage = writer?.Position ?? 0;
					int lengthTotal = lengthHeader + lengthKey + lengthRandom + lengthMessage;
					requestPacket = Host.Allocator.CreatePacket(lengthTotal);

					// Write header
					requestPacket[0] = (byte)PacketType.Request;
					Serializer.Write16(requestPacket, lengthHeader - 4, (ushort)lengthKey);
					Serializer.Write16(requestPacket, lengthHeader - 2, (ushort)lengthRandom);

					// Write exchange key
					if (lengthKey > 0) {
						int offset = lengthHeader;
						ConnectExchanger.ExportKey(new ArraySegment<byte>(requestPacket, offset, lengthKey));
					}

					// Write random data
					if (lengthRandom > 0) {
						int offset = lengthHeader + lengthKey;
						Array.Copy(ConnectRandom, 0, requestPacket, offset, lengthRandom);
					}

					// Write message
					if (lengthMessage > 0) {
						int offset = lengthHeader + lengthKey + lengthRandom;
						Array.Copy(writer.Buffer, 0, requestPacket, offset, lengthMessage);
					}

					// Write CRC32
					if (Host.Config.CRC32) {
						uint crc32 = CRC32.Compute(requestPacket, 5, lengthTotal - 5);
						Serializer.Write32(requestPacket, 1, crc32);
						requestPacket[0] |= (byte)PacketFlags.Verified;
					}

					// Dispose the writer
					writer?.Dispose();

					// Start connector task
					ConnectCancel?.Cancel();
					ConnectCancel?.Dispose();
					ConnectCancel = new CancellationTokenSource();
					Task.Factory.StartNew(
						() => ConnectAsync(requestPacket, lengthTotal, ConnectCancel),
						TaskCreationOptions.PreferFairness
					);

				}

			} catch (Exception exception) {

				// Exception while initiating connector task
				Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
				Dispose();

				// Dispose resources
				Host.Allocator.ReturnPacket(ref requestPacket);
				writer?.Dispose();
				
			}

		}

		/// <summary>Sends connection requests.</summary>
		private async Task ConnectAsync(byte[] packet, int length, CancellationTokenSource token) {
			try {

				// Keep sending connection requests until accepted or rejected
				for (int attempt = 0; attempt < Config.ConnectAttempts; attempt++) {
					await Host.SendSocketAsync(Remote, packet, 0, length);
					bool cancelled = await DelayAsync(Config.ConnectDelay, token);
					if (cancelled) return;
				}

				// All connection requests were ignored, disconnect
				Listener.OnPeerDisconnect(this, null, DisconnectReason.Timeout, null);
				Dispose();

			} catch (ObjectDisposedException) {

				// Peer was disposed while sending connection requests
				// This can happen if the connection request is rejected
				// Only trigger the disconnect event if not disposed yet
				if (StateDisposed != 1) Listener.OnPeerDisconnect(this, null, DisconnectReason.Disposed, null);
				Dispose();

			} catch (Exception exception) {

				// Exception while sending connection requests
				Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
				Dispose();

			} finally {

				// Return packet back to allocator
				Host.Allocator.ReturnPacket(ref packet);

			}
		}

		/// <summary>Used internally by the netcode to accept an incoming connection request.</summary>
		internal void Accept(ConnectionRequest request) {

			// Validate
			if (StateDisposed == 1) throw new ObjectDisposedException(GetType().FullName);

			// Disposable resources
			byte[] acceptPacket = null;

			try {

				// If already received connected messages, this could be a spoofed packet
				if (Statistics.MessageReceiveTotal > 0) {
					throw new InvalidOperationException(string.Format(
						"Connection request from {0} on peer {1} after already received {2} messages",
						request?.Remote?.ToString() ?? "null", Remote, Statistics.MessageReceiveTotal
					));
				}
				
				// Allocate accept packet
				int lengthHeader = 5 + (Host.Config.CRC32 ? 4 : 0);
				int lengthKey = request.Key.Count;
				int lengthRandom = request.Random.Count;
				int lengthTotal = lengthHeader + lengthKey + lengthRandom;
				acceptPacket = Host.Allocator.CreatePacket(lengthTotal);

				// Extract key and random
				ArraySegment<byte> key = new ArraySegment<byte>(acceptPacket, lengthHeader, lengthKey);
				ArraySegment<byte> random = new ArraySegment<byte>(acceptPacket, lengthHeader + lengthKey, lengthRandom);

				// Write header
				acceptPacket[0] = (byte)PacketType.Accept;
				Serializer.Write16(acceptPacket, lengthHeader - 4, (ushort)lengthKey);
				Serializer.Write16(acceptPacket, lengthHeader - 2, (ushort)lengthRandom);

				lock (ConnectLock) {

					// Stop sending connection requests
					ConnectCancel?.Cancel();
					ConnectCancel?.Dispose();
					ConnectCancel = null;

					// Create encryptor and write exchange key to the packet
					if (lengthKey > 0) {
						if (ConnectExchanger == null) ConnectExchanger = Host.Exchanger.Invoke();
						if (ConnectEncryptor == null) ConnectEncryptor = ConnectExchanger.DeriveEncryptor(request.Key);
						ConnectExchanger.ExportKey(key);
					}

					// Sign random data
					if (lengthRandom > 0) {
						Host.Authenticator.Sign(request.Random, random);
					}

					// Write CRC32
					if (Host.Config.CRC32) {
						uint crc32 = CRC32.Compute(acceptPacket, 5, lengthTotal - 5);
						Serializer.Write32(acceptPacket, 1, crc32);
						acceptPacket[0] |= (byte)PacketFlags.Verified;
					}

					// Send packet
					Task.Factory.StartNew(
						() => AcceptAsync(acceptPacket, lengthTotal),
						TaskCreationOptions.PreferFairness
					);

				}

			} catch (Exception exception) {

				// Exception while accepting a connection request
				// If already connected only notify listener, otherwise disconnect
				if (StateConnected == 1) {
					Listener.OnPeerException(this, exception);
				} else {
					Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
					Dispose();
				}

				// Dispose resources
				Host.Allocator.ReturnPacket(ref acceptPacket);

			}

		}

		/// <summary>Send an accept packet.</summary>
		private async Task AcceptAsync(byte[] packet, int length) {
			try {

				// Send packet
				await Host.SendSocketAsync(Remote, packet, 0, length);

				// Start pinger task if not started yet
				lock (ConnectLock) {
					if (ConnectPinger.IsCompleted) {
						ConnectPinger = Task.Factory.StartNew(() => PingerAsync(), TaskCreationOptions.PreferFairness);
					}
				}
				
				// Update connection state and notify listener
				int state = Interlocked.Exchange(ref StateConnected, 1);
				if (state == 0) Listener.OnPeerConnect(this);

			} catch (Exception exception) {

				// Exception while accepting connection request
				Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
				Dispose();
				
			} finally {

				// Return packet back to allocator
				Host.Allocator.ReturnPacket(ref packet);

			}
		}

		/// <summary>Disconnect by sending a disconnect message.</summary>
		/// <param name="message">Disconnect message to include or null if none.</param>
		public void Disconnect(IWritable message = null) {

			// Start disconnecting
			Task.Factory.StartNew(() => DisconnectAsync(message), TaskCreationOptions.PreferFairness);

		}

		/// <summary>Disconnect by sending a disconnect message.</summary>
		/// <param name="message">Disconnect message to include or null if none.</param>
		public async Task DisconnectAsync(IWritable message = null) {

			// Set connection state to disconnected ahead of time
			int state = Interlocked.Exchange(ref StateConnected, 0);
			
			try {

				// Send disconnect message
				if (state == 1) {
					await SendReliableAsync(new MessageSent(
						this, null, message,
						MessageType.Disconnect, MessageFlags.Reliable | MessageFlags.Sequenced |
						(ChannelDisconnect == ChannelDefault ? MessageFlags.None : MessageFlags.Channeled),
						NextSendSequence(ChannelDisconnect), 0, ChannelDisconnect
					));
				}

				// Delay the disconnect
				if (StateDisposed == 0) {
					await DelayAsync(Config.DisconnectDelay);
				}

				// Wait for flush task to complete
				Task flush = Task.CompletedTask;
				lock (FlushLock) flush = FlushTask;
				await flush;

				// Notify listener and disconnect
				Listener.OnPeerDisconnect(this, null, DisconnectReason.Disconnected, null);
				Dispose();

			} catch (ObjectDisposedException) {

				// Peer was disposed during delay
				Listener.OnPeerDisconnect(this, null, DisconnectReason.Disconnected, null);
				Dispose();

			} catch (Exception exception) {

				// Notify listener and disconnect
				Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
				Dispose();

			}

		}

		/// <summary>Queue a message for sending and return a sent message handle.</summary>
		/// <param name="message">Message to send.</param>
		/// <param name="listener">Message listener to use or null if not used.</param>
		/// <returns>Sent message handle.</returns>
		public MessageSent Send(IMessage message, IMessageListener listener = null) {

			// Validate
			if (message == null) {
				throw new ArgumentNullException(nameof(message), "Message to send is null");
			} else if (StateDisposed == 1) {
				throw new ObjectDisposedException(GetType().FullName);
			} else if (StateConnected == 0) {
				throw new InvalidOperationException("Not connected");
			}

			// Extract message information
			bool timed = message.Timed;
			bool reliable = message.Reliable;
			bool ordered = message.Ordered;
			bool unique = message.Unique;
			byte channel = message.Channel;
			short offset = message.Offset;

			// Create message flags
			MessageFlags flags = MessageFlags.None;
			if (timed) flags |= MessageFlags.Timed;
			if (reliable) flags |= MessageFlags.Reliable;
			if (ordered) flags |= MessageFlags.Ordered;
			if (unique) flags |= MessageFlags.Unique;
			if (channel != ChannelDefault) flags |= MessageFlags.Channeled;

			// Check if message needs to include the sequence
			ushort sequence = NextSendSequence(channel);
			int unsequenced = NextSendUnsequenced(channel, reliable || ordered || unique);
			bool sequenced = reliable || ordered || unique || unsequenced > Config.UnsequencedMax;
			if (sequenced) flags |= MessageFlags.Sequenced;

			// Create sent message
			MessageSent sent = new MessageSent(
				this, listener, message,
				MessageType.Custom, flags,
				sequence, offset, channel
			);

			// Send message
			if (reliable) {
				Task.Factory.StartNew(() => SendReliableAsync(sent), TaskCreationOptions.PreferFairness);
			} else {
				Task.Factory.StartNew(() => SendUnreliableAsync(sent), TaskCreationOptions.PreferFairness);
			}

			// Return sent message
			return sent;

		}
		
		/// <summary>Periodically send ping messages.</summary>
		private async Task PingerAsync() {
			try {

				while (true) {

					// Send ping packet
					try {
						await SendReliableAsync(new MessageSent(
							this, null, null,
							MessageType.Ping,
							MessageFlags.Timed | MessageFlags.Sequenced | MessageFlags.Reliable |
							(ChannelPinger == ChannelDefault ? MessageFlags.None : MessageFlags.Channeled),
							NextSendSequence(ChannelPinger), 0, ChannelPinger
						));
					} catch (Exception exception) {
						Listener.OnPeerException(this, exception);
					}

					// Delay before sending again
					await DelayAsync(Config.PingDelay);

				}

			} catch (ObjectDisposedException exception) {

				// Peer was disposed while sending pings
				int state = Interlocked.Exchange(ref StateConnected, 0);
				if (state == 1) Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
				if (state == 0 && StateDisposed != 1) Listener.OnPeerException(this, exception);
				Dispose();

			} catch (Exception exception) {

				// Exception while sending pings
				int state = Interlocked.Exchange(ref StateConnected, 0);
				if (state == 1) Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
				if (state == 0) Listener.OnPeerException(this, exception);
				Dispose();

			}
		}

		/// <summary>Increment send sequence.</summary>
		private ushort NextSendSequence(byte channel) {
			lock (SequenceSendLock) {
				if (SequenceSend == null) {
					return 0;
				} else {
					ushort sequence = (ushort)Interlocked.Increment(ref SequenceSend[channel]);
					return sequence;
				}
			}
		}

		/// <summary>Increment unsequenced count.</summary>
		private int NextSendUnsequenced(byte channel, bool reset) {
			lock (SequenceUnsequencedLock) {
				if (SequenceUnsequenced == null) {
					return 0;
				} else if (reset) {
					Interlocked.Exchange(ref SequenceUnsequenced[channel], 0);
					return 0;
				} else {
					int unsequenced = Interlocked.Increment(ref SequenceUnsequenced[channel]);
					if (unsequenced > Config.UnsequencedMax) {
						Interlocked.Exchange(ref SequenceUnsequenced[channel], 0);
						return 0;
					} else {
						return unsequenced;
					}
				}
			}
		}

		/// <summary>Send a reliable message.</summary>
		private async Task SendReliableAsync(MessageSent message) {

			try {

				// Create reliable key
				Tuple<byte, ushort> tuple = new Tuple<byte, ushort>(message.Channel, message.Sequence);

				// Save reliable message
				lock (ReliablesLock) ReliablesSent?.Add(tuple, message);

				// Keep sending until acknowledged
				for (byte attempt = 0; attempt < Config.ResendCount; attempt++) {

					// Send message and notify listener
					await SendFlushAsync(message, attempt);
					message.OnMessageSend();

					// Delay before resending
					int delay = RTT <= 0 ? Config.ResendDelayMax : RTT + Config.ResendDelayJitter;
					if (delay < Config.ResendDelayMin) delay = Config.ResendDelayMin;
					if (delay > Config.ResendDelayMax) delay = Config.ResendDelayMax;
					bool cancelled = await DelayAsync(delay, message.Token);
					if (cancelled) return;

				}

				// No acknowledgement received, disconnect
				message.StopResending();
				int state = Interlocked.Exchange(ref StateConnected, 0);
				if (state == 1) Listener.OnPeerDisconnect(this, null, DisconnectReason.Timeout, null);
				Dispose();

			} catch (ObjectDisposedException exception) {

				// Peer was disposed while sending message
				int state = Interlocked.Exchange(ref StateConnected, 0);
				if (state == 1) Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
				if (state == 0 && StateDisposed != 1) Listener.OnPeerException(this, exception);
				Dispose();

			} catch (Exception exception) {

				// Exception while sending message
				int state = Interlocked.Exchange(ref StateConnected, 0);
				if (state == 1) Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
				if (state == 0) Listener.OnPeerException(this, exception);
				Dispose();

			}

		}

		/// <summary>Send an unreliable message.</summary>
		private async Task SendUnreliableAsync(MessageSent message) {
			try {

				// Send message and notify listener
				await SendFlushAsync(message, 0);
				message.OnMessageSend();

			} catch (Exception exception) {

				// Exception while sending message
				Listener.OnPeerException(this, exception);

			}
		}

		/// <summary>Write message to the flush queue and return the flush task.</summary>
		private Task SendFlushAsync(MessageSent message, byte attempt) {
			lock (FlushLock) {

				// Allocate writer
				if (FlushWriter == null) {
					FlushWriter = new Writer(Host.Allocator, 13);
					FlushTimed = false;
					FlushCount = 0;
				}

				// Get writer position
				int position = FlushWriter.Position;

				try {

					// Write message
					FlushWriter.Skip(4);
					FlushWriter.Write((byte)(((byte)message.Type) | ((byte)message.Flags)));
					if (message.Flags.HasFlag(MessageFlags.Timed)) FlushWriter.Write((ushort)(message.TimeCreated.Ticks + message.Offset));
					if (message.Flags.HasFlag(MessageFlags.Sequenced)) FlushWriter.Write(message.Sequence);
					if (message.Flags.HasFlag(MessageFlags.Reliable)) FlushWriter.Write(attempt);
					if (message.Flags.HasFlag(MessageFlags.Channeled)) FlushWriter.Write(message.Channel);
					if (message.Flags.HasFlag(MessageFlags.Timed)) FlushTimed = true;
					message.Payload?.Write(FlushWriter);
					Serializer.Write32(FlushWriter.Buffer, position, FlushWriter.Position - position - 4);

					// Increment statistics
					Statistics.AddMessageSendBytes(FlushWriter.Position - position - 4);
					if (message.Type == MessageType.Acknowledge) Statistics.IncrementMessageSendAcknowledge();
					if (message.Type == MessageType.Ping) Statistics.IncrementMessageSendPing();
					if (attempt > 0) Statistics.IncrementMessageSendDuplicated();
					Statistics.IncrementMessageSendTotal();
					if (message.Flags.HasFlag(MessageFlags.Reliable)) {
						Statistics.IncrementMessageSendReliable();
					} else {
						Statistics.IncrementMessageSendUnreliable();
					}

					// Increment message count
					FlushCount++;

					// Start flush task if not started yet
					if (FlushTask.IsCompleted) {
						return FlushTask = Task.Factory.StartNew(() => FlushAsync(), TaskCreationOptions.PreferFairness);
					} else {
						return FlushTask;
					}

				} catch (Exception) {

					// Reset writer back
					FlushWriter.Reset(position);
					throw;

				}
				
			}
		}

		/// <summary>Send all queued messages to the host socket.</summary>
		private async Task FlushAsync() {

			// Disposable resources
			byte[] bufferCompress = null;
			byte[] bufferEncrypt = null;
			Writer flushWriter = null;
			bool flushTimed = false;
			int flushCount = 0;

			try {

				// Delay before flushing
				await DelayAsync(Config.SendDelay);

				// Save flush information
				lock (FlushLock) {
					flushWriter = FlushWriter;
					flushTimed = FlushTimed;
					flushCount = FlushCount;
					FlushWriter = null;
					FlushTimed = false;
					FlushCount = 0;
				}

				// Create packet information
				ArraySegment<byte> packet;
				PacketFlags flags = PacketFlags.None;
				PacketType type = PacketType.Connected;
				if (flushTimed) flags |= PacketFlags.Timed;
				if (Host.Config.CRC32) flags |= PacketFlags.Verified;

				if (flushCount <= 0 || flushWriter == null) {
					// If no messages, do nothing
					return;
				} else if (flushCount == 1) {
					// If only one message, ignore length
					packet = new ArraySegment<byte>(flushWriter.Buffer, 17, flushWriter.Position - 17);
				} else {
					// If multiple messages, include lengths and set flag
					packet = new ArraySegment<byte>(flushWriter.Buffer, 13, flushWriter.Position - 13);
					flags |= PacketFlags.Combined;
				}

				// Compress
				if (Host.Config.Compression) {
					int max = Host.Compressor.MaxCompressedLength(packet.Count) + packet.Offset;
					bufferCompress = Host.Allocator.CreateMessage(max);
					int len = Host.Compressor.Compress(packet, bufferCompress, packet.Offset);
					packet = new ArraySegment<byte>(bufferCompress, packet.Offset, len);
					flags |= PacketFlags.Compressed;
				}

				// Encrypt
				ICryptoEncryptor encryptor = ConnectEncryptor;
				if (encryptor != null) {
					int max = encryptor.MaxEncryptedLength(packet.Count) + packet.Offset;
					bufferEncrypt = Host.Allocator.CreateMessage(max);
					int len = encryptor.Encrypt(packet, bufferEncrypt, packet.Offset);
					packet = new ArraySegment<byte>(bufferEncrypt, packet.Offset, len);
				}

				// Check if packet needs to be fragmented
				int id = 0, last = 0;
				int mtu = Config.MTU - 1 - (Host.Config.CRC32 ? 4 : 0) - (flushTimed ? 2 : 0);
				if (packet.Count > mtu) {
					id = Interlocked.Increment(ref SequenceFragment);
					last = (packet.Count - 1) / (mtu - 6);
					flags |= PacketFlags.Fragmented;
				}

				// Send all fragments
				for (int part = 0; part <= last; part++) {

					// Create fragment segment
					int offset = part * (mtu - 6), length = mtu - 6;
					if (offset + length >= packet.Count || part == last) length = packet.Count - offset;
					ArraySegment<byte> data = new ArraySegment<byte>(packet.Array, packet.Offset + offset, length);

					// Write fragment header
					if (flags.HasFlag(PacketFlags.Fragmented)) {
						Serializer.Write16(data.Array, data.Offset - 6, (ushort)id);
						Serializer.Write16(data.Array, data.Offset - 4, (ushort)part);
						Serializer.Write16(data.Array, data.Offset - 2, (ushort)last);
						data = new ArraySegment<byte>(data.Array, data.Offset - 6, data.Count + 6);
					}

					// Write ticks
					if (flags.HasFlag(PacketFlags.Timed)) {
						Serializer.Write16(data.Array, data.Offset - 2, (ushort)Host.Ticks);
						data = new ArraySegment<byte>(data.Array, data.Offset - 2, data.Count + 2);
					}

					// Write CRC32
					if (flags.HasFlag(PacketFlags.Verified)) {
						uint crc32 = CRC32.Compute(data.Array, data.Offset, data.Count);
						Serializer.Write32(data.Array, data.Offset - 4, crc32);
						data = new ArraySegment<byte>(data.Array, data.Offset - 4, data.Count + 4);
					}

					// Write header
					data.Array[data.Offset - 1] = (byte)(((byte)type) | ((byte)flags));
					data = new ArraySegment<byte>(data.Array, data.Offset - 1, data.Count + 1);

					// Send
					int sent = await Host.SendSocketAsync(Remote, data.Array, data.Offset, data.Count);

					// Increment statistics
					Statistics.SetPacketSendTicks(Host.Ticks);
					Statistics.AddPacketSendBytes(sent);
					Statistics.IncrementPacketSendCount();

				}

			} catch (ObjectDisposedException exception) {

				// Peer was disposed while flushing messages
				int state = Interlocked.Exchange(ref StateConnected, 0);
				if (state == 1) Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
				if (state == 0 && StateDisposed != 1) Listener.OnPeerException(this, exception);
				Dispose();

			} catch (Exception exception) {

				// Exception while flushing messages
				int state = Interlocked.Exchange(ref StateConnected, 0);
				if (state == 1) Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
				if (state == 0) Listener.OnPeerException(this, exception);
				Dispose();

			} finally {

				// Dispose resources
				Host.Allocator.ReturnMessage(ref bufferCompress);
				Host.Allocator.ReturnMessage(ref bufferEncrypt);
				flushWriter?.Dispose();

			}

		}

		/// <summary>Used internally by the netcode to process network packets.</summary>
		/// <param name="buffer">Receive buffer the packet is written on.</param>
		/// <param name="length">Number of bytes in the packet.</param>
		/// <returns>Task that completes when processing is finished.</returns>
		internal async Task OnReceiveAsync(byte[] buffer, int length) {

			// Validate
			if (StateDisposed == 1) throw new ObjectDisposedException(GetType().FullName);

			// Disposable resources
			byte[] bufferFragment = null;
			byte[] bufferDecompress = null;
			byte[] bufferDecrypt = null;
			
			try {
				
				// Discard if empty
				if (length < 1) return;

				// Increment statistics
				Statistics.SetPacketReceiveTicks(Host.Ticks);
				Statistics.AddPacketReceiveBytes(length);
				Statistics.IncrementPacketReceiveCount();

				// Extract packet information
				PacketType type = (PacketType)(buffer[0] & (byte)PacketType.Mask);
				PacketFlags flags = (PacketFlags)(buffer[0] & (byte)PacketFlags.Mask);
				ArraySegment<byte> packet = new ArraySegment<byte>(buffer, 1, length - 1);
				
				// Discard if bad packet type
				if (type == PacketType.Unused1 || type == PacketType.Unused2) {
					throw new FormatException(string.Format("Bad packet {0} ({1}) from {2}", type, flags, Remote));
				} else if (type == PacketType.Request) {
					throw new FormatException(string.Format("Bad packet {0} ({1}) from {2}", type, flags, Remote));
				} else if (type == PacketType.Broadcast) {
					throw new FormatException(string.Format("Bad packet {0} ({1}) from {2}", type, flags, Remote));
				} else if (type == PacketType.Unconnected) {
					throw new FormatException(string.Format("Bad packet {0} ({1}) from {2}", type, flags, Remote));
				}

				// Verify CRC32
				if (flags.HasFlag(PacketFlags.Verified)) {
					if (packet.Count < 4) {
						throw new FormatException(string.Format(
							"Packet {0} ({1}) from {2} with length {3} is too short for CRC32",
							type, flags, Remote, length
						));
					} else if (Host.Config.CRC32) {
						uint computed = CRC32.Compute(packet.Array, packet.Offset + 4, packet.Count - 4);
						uint received = Serializer.ReadUInt32(packet.Array, packet.Offset);
						if (computed != received) {
							throw new FormatException(string.Format(
								"CRC32 {0} does not match {1} in packet {2} ({3}) from {4} with length {5}",
								received, computed, type, flags, Remote, length
							));
						}
					}
					packet = new ArraySegment<byte>(packet.Array, packet.Offset + 4, packet.Count - 4);
				}

				// Extract sent ticks
				ushort? sent = null;
				if (flags.HasFlag(PacketFlags.Timed)) {
					if (packet.Count < 2) {
						throw new FormatException(string.Format(
							"Packet {0} ({1}) from {2} with length {3} is too short for ticks",
							type, flags, Remote, length
						));
					} else {
						sent = Serializer.ReadUInt16(packet.Array, packet.Offset);
						packet = new ArraySegment<byte>(packet.Array, packet.Offset + 2, packet.Count - 2);
					}
				}

				// Process fragment
				if (flags.HasFlag(PacketFlags.Fragmented)) {

					// Check for header
					if (packet.Count < 6) {
						throw new FormatException(string.Format(
							"Fragmented packet {0} ({1}) from {2} with length {3} is too short",
							type, flags, Remote, length
						));
					}

					// Extract and validate header
					ushort id = Serializer.ReadUInt16(packet.Array, packet.Offset);
					ushort part = Serializer.ReadUInt16(packet.Array, packet.Offset + 2);
					ushort last = Serializer.ReadUInt16(packet.Array, packet.Offset + 4);
					if (last == 0 || part > last) {
						throw new FormatException(string.Format(
							"Bad fragment {0}/{1}/{2} in packet {3} ({4}) from {5}",
							id, part, last, type, flags, Remote
						));
					}

					// Extract data
					packet = new ArraySegment<byte>(packet.Array, packet.Offset + 6, packet.Count - 6);

					lock (FragmentLock) {

						try {

							// Reset if first fragment
							if (FragmentID != id || FragmentLast != last) {
								FragmentDispose();
								FragmentID = id;
								FragmentLast = last;
							}

							// Make sure saved fragments have the same length
							if (part != last && FragmentDataParts != null && FragmentLengthParts != packet.Count) {
								FragmentDispose();
								throw new InvalidOperationException(string.Format(
									"Fragment {0}/{1}/{2}/{3} from {4} in packet {5} ({6}) data length is not {7}",
									id, part, last, packet.Count, Remote, type, flags, FragmentLengthParts
								));
							}

							// Make sure last fragment data is not allocated yet
							if (part == last && FragmentDataLast != null) {
								throw new InvalidOperationException(string.Format(
									"Fragment {0}/{1}/{2}/{3} from {4} in packet {5} ({6}) is already allocated",
									id, part, last, packet.Count, Remote, type, flags, FragmentLengthParts
								));
							}

							// Start a new timeout task
							if (FragmentCancel == null) {
								FragmentCancel = new CancellationTokenSource();
								_ = Task.Factory.StartNew(
									() => FragmentTimeoutAsync(FragmentCancel),
									TaskCreationOptions.PreferFairness
								);
							}

							// Get received bitfield information
							int receivedIndex = part / 8;
							int receivedLength = (last + 7) / 8;
							int receivedBit = 1 << (part % 8);

							// Allocate received bitfield
							if (FragmentReceived == null) {
								FragmentReceived = Host.Allocator.CreateMessage(receivedLength);
								for (int i = 0; i < receivedLength; i++) FragmentReceived[i] = 0;
							}

							// If already received, ignore
							if ((FragmentReceived[receivedIndex] & receivedBit) != 0) {
								throw new InvalidOperationException(string.Format(
									"Fragment {0}/{1}/{2}/{3} from {4} in packet {5} ({6}) is duplicated",
									id, part, last, packet.Count, Remote, type, flags, FragmentLengthParts
								));
							}

							// Mark fragment as received
							FragmentReceived[receivedIndex] |= (byte)(receivedBit);

							// Copy fragment data
							if (part == last) {
								FragmentLengthLast = packet.Count;
								FragmentDataLast = Host.Allocator.CreateMessage(packet.Count);
								Array.Copy(packet.Array, packet.Offset, FragmentDataLast, 0, packet.Count);
							} else {
								FragmentLengthParts = packet.Count;
								FragmentDataParts = FragmentDataParts ?? Host.Allocator.CreateMessage(packet.Count * (last + 1));
								Array.Copy(packet.Array, packet.Offset, FragmentDataParts, part * packet.Count, packet.Count);
							}

							// If any fragment missing, return
							for (int i = 0; i <= last; i++) {
								int index = i / 8, bit = 1 << (i % 8);
								if ((FragmentReceived[index] & bit) == 0) {
									return;
								}
							}

							// Make sure both arrays exist
							if (FragmentDataLast == null) {
								throw new InvalidOperationException(string.Format(
									"Fragment {0}/{1}/{2}/{3} from {4} in packet {5} ({6}) with no last fragment",
									id, part, last, packet.Count, Remote, type, flags, FragmentLengthParts
								));
							} else if (FragmentDataParts == null) {
								throw new InvalidOperationException(string.Format(
									"Fragment {0}/{1}/{2}/{3} from {4} in packet {5} ({6}) with no fragment parts",
									id, part, last, packet.Count, Remote, type, flags, FragmentLengthParts
								));
							}

							// Reconstruct packet from fragments
							bufferFragment = Interlocked.Exchange(ref FragmentDataParts, null);
							Array.Copy(FragmentDataLast, 0, bufferFragment, last * FragmentLengthParts, FragmentLengthLast);
							packet = new ArraySegment<byte>(bufferFragment, 0, FragmentLengthParts * last + FragmentLengthLast);

							// Dispose fragment
							FragmentDispose();

						} catch (Exception) {

							// Dispose fragment and rethrow
							FragmentDispose();
							throw;

						}

					}

				}

				// Decrypt packet
				if (type == PacketType.Connected && ConnectEncryptor != null) {
					int max = ConnectEncryptor.MaxDecryptedLength(packet.Count);
					bufferDecrypt = Host.Allocator.CreateMessage(max);
					int len = ConnectEncryptor.Decrypt(packet, bufferDecrypt, 0);
					packet = new ArraySegment<byte>(bufferDecrypt, 0, len);
				}

				// Decompress packet
				if (flags.HasFlag(PacketFlags.Compressed)) {
					bufferDecompress = Host.Allocator.CreateMessage(packet.Count);
					int len = Host.Compressor.Decompress(packet, ref bufferDecompress, 0);
					packet = new ArraySegment<byte>(bufferDecompress, 0, len);
				}

				// Proces packet based on type
				switch (type) {
					case PacketType.Connected:
						if (flags.HasFlag(PacketFlags.Combined)) {
							await OnReceiveMultiple(packet, sent);
						} else {
							await OnReceiveConnected(packet, sent);
						}
						break;
					case PacketType.Accept:
						OnReceiveAccept(packet);
						break;
					case PacketType.Reject:
						OnReceiveReject(packet);
						break;
				}
				
			} catch (Exception exception) {

				// Notify listener
				Listener.OnPeerException(this, exception);

			} finally {

				// Return arrays back to allocator
				Host.Allocator.ReturnMessage(ref bufferFragment);
				Host.Allocator.ReturnMessage(ref bufferDecompress);
				Host.Allocator.ReturnMessage(ref bufferDecrypt);
				
			}

		}

		/// <summary>Process a connection reject packet.</summary>
		private void OnReceiveReject(ArraySegment<byte> packet) {

			// A connection cannot be rejected after it is already connected
			// Fake connection rejection message can be sent with IP address spoofing
			// It is best to ignore it and throw en exception for the listener to handle
			if (StateConnected == 1) {
				throw new InvalidOperationException(string.Format(
					"Connection reject from {0} with length {1} received after already connected",
					Remote, packet.Count
				));
			}

			// Notify listener and dispose
			using (Reader reader = new Reader(packet)) {
				Listener.OnPeerDisconnect(this, reader, DisconnectReason.Rejected, null);
				Dispose();
			}

		}

		/// <summary>Process a connection accept packet.</summary>
		private void OnReceiveAccept(ArraySegment<byte> packet) {

			// If already connected, ignore
			// This can happen if multiple connect messages are sent before they are accepted
			// This can also happen from a fake accept packet with IP address spoofing 
			if (StateConnected == 1) {
				throw new FormatException(string.Format(
					"Connection accept from {0} with length {1} after already connected",
					Remote, packet.Count
				));
			}

			// Make sure packet is not too short for header
			if (packet.Count < 4) {
				throw new FormatException(string.Format(
					"Connection accept from {0} with length {1} is too short for header",
					Remote, packet.Count
				));
			}

			// Extract header
			ushort lengthKey = Serializer.ReadUInt16(packet.Array, packet.Offset);
			ushort lengthSignature = Serializer.ReadUInt16(packet.Array, packet.Offset + 2);

			// Validate key and signature lengths
			if (packet.Count < 4 + lengthKey + lengthSignature) {
				throw new FormatException(string.Format(
					"Connection accept from {0} with length {1} is less than header (4) + key {2} + signature {3}",
					Remote, packet.Count, lengthKey, lengthSignature
				));
			} else if (Host.Config.Encryption && lengthKey == 0) {
				throw new FormatException(string.Format(
					"Connection accept from {0} with lengths {1}/{2}/{3} has no encryption key",
					Remote, packet.Count, lengthKey, lengthSignature
				));
			} else if (Host.Config.Encryption && lengthKey != Host.ExchangerKeyLength) {
				throw new FormatException(string.Format(
					"Connection accept from {0} with lengths {1}/{2}/{3} has bad encryption key",
					Remote, packet.Count, lengthKey, lengthSignature
				));
			} else if (!Host.Config.Encryption && lengthKey != 0) {
				throw new FormatException(string.Format(
					"Connection accept from {0} with lengths {1}/{2}/{3} has an unrequested encryption key",
					Remote, packet.Count, lengthKey, lengthSignature
				));
			} else if (lengthSignature > 0 && lengthSignature != Host.Authenticator.SignatureLength) {
				throw new FormatException(string.Format(
					"Connection accept from {0} with lengths {1}/{2}/{3} has a bad signature",
					Remote, packet.Count, lengthKey, lengthSignature
				));
			} else if (lengthSignature > 0 && (Config.RemotePublicKey == null || Config.RemotePublicKey.Length == 0)) {
				throw new FormatException(string.Format(
					"Connection accept from {0} with lengths {1}/{2}/{3} has an unrequested signature",
					Remote, packet.Count, lengthKey, lengthSignature
				));
			}

			// Extract data
			ArraySegment<byte> key = new ArraySegment<byte>(packet.Array, packet.Offset + 4, lengthKey);
			ArraySegment<byte> signature = new ArraySegment<byte>(packet.Array, packet.Offset + 4 + lengthKey, lengthSignature);

			try {

				lock (ConnectLock) {

					// Verify signature
					if (ConnectRandom != null) {
						ArraySegment<byte> random = new ArraySegment<byte>(ConnectRandom, 0, Host.Authenticator.SignatureLength);
						bool verified = Host.Authenticator.Verify(random, signature, Config.RemotePublicKey);
						if (!verified) {
							Listener.OnPeerDisconnect(this, null, DisconnectReason.BadSignature, null);
							Dispose();
							return;
						}
						Host.Allocator.ReturnPacket(ref ConnectRandom);
					}

					// Create encryptor
					if (Host.Config.Encryption && ConnectEncryptor == null) {
						if (ConnectExchanger == null) {
							throw new InvalidOperationException(string.Format(
								"Connection accept from {0} has encryption but key exchanger is missing", Remote
							));
						} else {
							ConnectEncryptor = ConnectExchanger.DeriveEncryptor(key);
						}
					}

					// Stop sending connection requests
					ConnectCancel?.Cancel();
					ConnectCancel?.Dispose();
					ConnectCancel = null;

					// Start pinger task if not started yet
					if (ConnectPinger.IsCompleted) {
						ConnectPinger = Task.Factory.StartNew(() => PingerAsync(), TaskCreationOptions.PreferFairness);
					}

					// Update connection state and notify listener
					int state = Interlocked.Exchange(ref StateConnected, 1);
					if (state == 0) Listener.OnPeerConnect(this);

				}

			} catch (Exception exception) {

				// Failed to process accept message, disconnect
				Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
				Dispose();

			}

		}

		/// <summary>Process a connected packet with multiple messages.</summary>
		private async Task OnReceiveMultiple(ArraySegment<byte> packet, ushort? sent) {

			// Process until packet is empty
			for (int count = 0; packet.Count > 0; count++) {

				// Make sure message has length
				if (packet.Count < 4) {
					throw new FormatException(string.Format(
						"Message {0} from {1} at offset {1} is too short",
						count, Remote, packet.Offset
					));
				}

				// Read and validate length
				int length = Serializer.ReadInt32(packet.Array, packet.Offset);
				if (length <= 0) {
					throw new FormatException(string.Format(
						"Message {0} length {1} from {2} at offset {3} is negative",
						count, length, Remote, packet.Offset
					));
				} else if (length > packet.Count - 4) {
					throw new FormatException(string.Format(
						"Message {0} length {1} from {2} at offset {3} extends beyond packet length {4}",
						count, length, Remote, packet.Offset, packet.Count
					));
				}

				// Extract message
				ArraySegment<byte> message = new ArraySegment<byte>(packet.Array, packet.Offset + 4, length);
				packet = new ArraySegment<byte>(packet.Array, packet.Offset + length + 4, packet.Count - length - 4);

				// Process message
				try {
					await OnReceiveConnected(message, sent);
				} catch (Exception exception) {
					Listener.OnPeerException(this, exception);
				}
				
			}

		}

		/// <summary>Process a single connected message.</summary>
		private async Task OnReceiveConnected(ArraySegment<byte> message, ushort? sent) {

			// Increment statistics
			Statistics.AddMessageReceiveBytes(message.Count);
			Statistics.IncrementMessageReceiveTotal();

			// Dispose of the key exchanger as it will no longer be used
			ConnectExchanger?.Dispose();
			ConnectExchanger = null;

			// Extract message information
			MessageType type = (MessageType)(message.Array[message.Offset] & (byte)MessageType.Mask);
			MessageFlags flags = (MessageFlags)(message.Array[message.Offset] & (byte)MessageFlags.Mask);
			message = new ArraySegment<byte>(message.Array, message.Offset + 1, message.Count - 1);

			// Extract time created
			ushort? created = null;
			if (flags.HasFlag(MessageFlags.Timed)) {
				if (message.Count < 2) {
					throw new FormatException(string.Format(
						"Message {0} ({1}) from {2} with length {3} is too short for ticks",
						type, flags, Remote, message.Count
					));
				}
				created = Serializer.ReadUInt16(message.Array, message.Offset);
				message = new ArraySegment<byte>(message.Array, message.Offset + 2, message.Count - 2);
			}

			// Extract sequence
			ushort? sequence = null;
			if (flags.HasFlag(MessageFlags.Sequenced)) {
				if (message.Count < 2) {
					throw new FormatException(string.Format(
						"Message {0} ({1}) from {2} with length {3} is too short for sequence",
						type, flags, Remote, message.Count
					));
				}
				sequence = Serializer.ReadUInt16(message.Array, message.Offset);
				message = new ArraySegment<byte>(message.Array, message.Offset + 2, message.Count - 2);
			}

			// Extract attempt
			byte attempt = 0;
			if (flags.HasFlag(MessageFlags.Reliable)) {
				if (message.Count < 1) {
					throw new FormatException(string.Format(
						"Message {0} ({1}) from {2} with length {3} is too short for send count",
						type, flags, Remote, message.Count
					));
				}
				attempt = message.Array[message.Offset];
				message = new ArraySegment<byte>(message.Array, message.Offset + 1, message.Count - 1);
			}

			// Extract channel
			byte channel = ChannelDefault;
			if (flags.HasFlag(MessageFlags.Channeled)) {
				if (message.Count < 1) {
					throw new FormatException(string.Format(
						"Message {0} ({1}) from {2} with length {3} is too short for channel",
						type, flags, Remote, message.Count
					));
				}
				channel = message.Array[message.Offset];
				message = new ArraySegment<byte>(message.Array, message.Offset + 1, message.Count - 1);
			}

			// Check if unique
			bool unique = true;
			if (flags.HasFlag(MessageFlags.Unique) && type != MessageType.Acknowledge && Config.DuplicateTimeout > 0) {
				if (sequence == null) {
					throw new FormatException(string.Format(
						"Unique message {0} ({1}) from {2} with length {3} without sequence",
						type, flags, Remote, message.Count
					));
				}
				Tuple<byte, ushort> tuple = new Tuple<byte, ushort>(channel, sequence.Value);
				lock (UniqueLock) unique = UniqueReceived?.Add(tuple) ?? false;
				if (unique) {
					_ = Task.Factory.StartNew(
						() => ReceiveUniqueTimeoutAsync(tuple),
						TaskCreationOptions.PreferFairness
					);
				}
			}

			// Increment statistics
			if (type == MessageType.Acknowledge) Statistics.IncrementMessageReceiveAcknowledge();
			if (type == MessageType.Ping) Statistics.IncrementMessageReceivePing();
			if (!unique) Statistics.IncrementMessageReceiveDuplicated();
			if (flags.HasFlag(MessageFlags.Reliable)) {
				Statistics.IncrementMessageReceiveReliable();
			} else {
				Statistics.IncrementMessageReceiveUnreliable();
			}

			// Update lost message statistics
			if (type != MessageType.Acknowledge) {
				lock (SequenceReceiveLock) {
					if (SequenceReceive != null) {
						ushort expected = (ushort)Interlocked.Increment(ref SequenceReceive[channel]);
						if (sequence != null && sequence.Value != expected) {
							ushort lossAmount = (ushort)(sequence - expected);
							ushort leadAmount = (ushort)(expected - sequence);
							if (lossAmount < leadAmount) {
								// There are missing messages before this one 
								// This can indicate either packet loss or re-ordering during transit
								Interlocked.Add(ref SequenceReceive[channel], lossAmount);
								Statistics.AddMessageReceiveLost(lossAmount);
							} else {
								// This message was previously thought to have been lost but came late
								Interlocked.Decrement(ref SequenceReceive[channel]);
								Statistics.DecrementMessageReceiveLost();
							}
						}
					}
				}
			}

			// Send acknowledge back
			if (flags.HasFlag(MessageFlags.Reliable)) {
				if (sequence == null) {
					throw new FormatException(string.Format(
						"Reliable message {0} ({1}) from {2} with length {3} without sequence",
						type, flags, Remote, message.Count
					));
				}
				await SendFlushAsync(new MessageSent(
					this, null, null,
					MessageType.Acknowledge,
					MessageFlags.Timed | MessageFlags.Sequenced | MessageFlags.Channeled,
					sequence.Value, 0, channel
				), attempt);
			}

			// Calculate message creation ticks
			long ticks = Host.Ticks - RTT / 2;
			if (created != null) {
				long now = Host.Ticks;
				long diff = ((now & ~65535) | (ushort)(created.Value + TimeDelta));
				ticks = now - Math.Abs(now - diff);
			}

			// Create received message information
			MessageReceived info = new MessageReceived(
				this, type, flags,
				channel, attempt, sequence,
				created, sent,
				new HostTimestamp(Host, ticks)
			);

			// Proces message based on type
			switch (type) {
				case MessageType.Custom:
					if (unique && flags.HasFlag(MessageFlags.Ordered)) {
						// Custom message requires ordered delivery
						bool delayable = Config.OrderedDelayMax > 0 && Config.OrderedDelayTimeout > 0;
						CancellationTokenSource signal = OnReceiveOrdered(message, info, delayable);
						if (signal != null) {
							// Copy the message and delay it
							byte[] copy = Host.Allocator.CreateMessage(message.Count);
							Array.Copy(message.Array, message.Offset, copy, 0, message.Count);
							message = new ArraySegment<byte>(copy, 0, message.Count);
							_ = Task.Factory.StartNew(
								() => OnReceiveDelayed(signal, message, info),
								TaskCreationOptions.PreferFairness
							);
						}
					} else if (unique) {
						// Custom message is not ordered, process it immediately
						OnReceiveCustom(message, info);
					}
					break;
				case MessageType.Acknowledge:
					OnReceiveAcknowledge(message, info);
					break;
				case MessageType.Disconnect:
					if (Config.DisconnectDelay <= 0) {
						// No disconnect delay
						await OnReceiveDisconnect(message, false);
					} else if (message.Count > 0) {
						// Copy the message and delay handling it
						byte[] copy = Host.Allocator.CreateMessage(message.Count);
						Array.Copy(message.Array, message.Offset, copy, 0, message.Count);
						message = new ArraySegment<byte>(copy, 0, message.Count);
						_ = Task.Factory.StartNew(
							() => OnReceiveDisconnect(message, true),
							TaskCreationOptions.PreferFairness
						);
					} else {
						// No copy needed as the message is empty
						_ = Task.Factory.StartNew(
							() => OnReceiveDisconnect(message, false),
							TaskCreationOptions.PreferFairness
						);
					}
					break;
				case MessageType.Ping:
					// Do nothing with received pings
					break;
			}

		}

		/// <summary>Process a custom unordered message.</summary>
		private void OnReceiveCustom(ArraySegment<byte> message, MessageReceived info) {

			// Just notify listener
			using (Reader reader = new Reader(message)) {
				Listener.OnPeerReceive(this, reader, info);
			}

		}

		/// <summary>Process a disconnect message.</summary>
		private async Task OnReceiveDisconnect(ArraySegment<byte> message, bool copied) {
			try {

				// Delay the disconnect
				await DelayAsync(Config.DisconnectDelay);

				// Notify listener and dispose
				using (Reader reader = new Reader(message)) {
					int state = Interlocked.Exchange(ref StateConnected, 0);
					if (state == 1) Listener.OnPeerDisconnect(this, null, DisconnectReason.Terminated, null);
					Dispose();
				}

			} catch (ObjectDisposedException exception) {

				// Peer has been disposed during delay
				int state = Interlocked.Exchange(ref StateConnected, 0);
				if (state == 1) Listener.OnPeerDisconnect(this, null, DisconnectReason.Terminated, null);
				if (state == 0 && StateDisposed != 1) Listener.OnPeerException(this, exception);
				Dispose();

			} finally {

				// Return the copied message
				if (copied) {
					byte[] copy = message.Array;
					Host.Allocator.ReturnMessage(ref copy);
				}

			}
		}

		/// <summary>Process an acknowledgment.</summary>
		private void OnReceiveAcknowledge(ArraySegment<byte> message, MessageReceived info) {

			// Make sure acknowledgment has a sequence
			if (info.Sequence == null) {
				throw new FormatException(string.Format(
					"Acknowledge {0} ({1}) from {2} with length {3} without sequence",
					info.Type, info.Flags, Remote, message.Count
				));
			}

			// Create channel/sequence tuple
			Tuple<byte, ushort> tuple = new Tuple<byte, ushort>(info.Channel, info.Sequence.Value);

			// Find reliable message
			MessageSent found = null;
			lock (ReliablesLock) {
				ReliablesSent?.TryGetValue(tuple, out found);
				ReliablesSent?.Remove(tuple);
			}

			// Stop resending reliable message
			found?.StopResending();

			// Update RTT
			bool hasTicks = info.TicksCreated != null && info.TicksSent != null;
			if (found != null && info.Attempt == 0 && found.Attempts == 1 && hasTicks) {
				ushort timeProcess = (ushort)(info.TicksSent - info.TicksCreated);
				long now = Host.Ticks;
				long rtt = now - found.TimeSent.Ticks - timeProcess;
				if (rtt > 0 && rtt < 32768) {
					ushort sliceLocal = (ushort)(now);
					ushort sliceRemote = (ushort)(info.TicksSent - rtt / 2);
					ushort timeDelta = (ushort)(sliceLocal - sliceRemote);
					Interlocked.Exchange(ref TimeDelta, timeDelta);
					Interlocked.Exchange(ref TimeRTT, (int)rtt);
					Listener.OnPeerUpdateRTT(this, (ushort)rtt);
				}
			}

			// Notify message listener
			found?.OnMessageAcknowledge();

		}

		/// <summary>Process a delayed ordered message.</summary>
		private async Task OnReceiveDelayed(
			CancellationTokenSource signal,
			ArraySegment<byte> message,
			MessageReceived info
		) {
			try {

				long ticks = Host.Ticks;
				for (int i = 0; i < Config.OrderedDelayMax; i++) {

					// If timeout delay reached zero, break and process the message
					long delay = Config.OrderedDelayTimeout + ticks - Host.Ticks;

					// Wait for signal to be triggered, or timeout
					bool cancelled = await DelayAsync((int)delay, signal);
					if (!cancelled) break;

					// Attempt to process message
					bool delayable = Config.OrderedDelayMax > 0 && Config.OrderedDelayTimeout > 0;
					signal = OnReceiveOrdered(message, info, delayable);

					// If no delay signal, message has been processed
					if (signal == null) return;

				}

				// Process the message after timeout or max signals
				OnReceiveOrdered(message, info, false);

			} catch (Exception exception) {

				if (StateConnected == 1 && info.Flags.HasFlag(MessageFlags.Reliable)) {
					// Reliable message was acknowledged but not processed, disconnect
					int state = Interlocked.Exchange(ref StateConnected, 0);
					if (state == 1) Listener.OnPeerDisconnect(this, null, DisconnectReason.Exception, exception);
					Dispose();
				} else {
					// Not connected anymore, just notify listener
					if (StateDisposed != 1) Listener.OnPeerException(this, exception);
				}

			} finally {

				// Return the copied message
				byte[] copy = message.Array;
				Host.Allocator.ReturnMessage(ref copy);

			}
		}

		/// <summary>Process an ordered message.</summary>
		private CancellationTokenSource OnReceiveOrdered(
			ArraySegment<byte> message,
			MessageReceived info,
			bool delayable
		) {

			// Make sure ordered message has a sequence
			if (info.Sequence == null) {
				throw new FormatException(string.Format(
					"Ordered message {0}/{1} from {2} with length {3} without sequence",
					info.Type, info.Flags, Remote, message.Count
				));
			}

			lock (OrderedLock) {

				// Make sure object hasn't been disposed
				if (OrderedSequence == null || OrderedSignal == null) {
					return null;
				}

				// Check if message can be processed
				bool process = false, update = false;
				bool reliable = info.Flags.HasFlag(MessageFlags.Reliable);
				ushort expected = (ushort)(OrderedSequence[info.Channel] + 1);
				ushort lossAmount = (ushort)(info.Sequence - expected);
				ushort leadAmount = (ushort)(expected - info.Sequence);
				if (info.Sequence == expected) {
					// Message is ordered properly
					update = process = true;
				} else if (lossAmount < leadAmount) {
					// There are missing messages before this one
					if (reliable && delayable) {
						// Delay the message until the misssing messages are received
						CancellationTokenSource signal = OrderedSignal[info.Channel];
						signal = signal ?? new CancellationTokenSource();
						OrderedSignal[info.Channel] = signal;
						return signal;
					} else {
						// Process the message normally because delaying is disabled
						// Missing messages can be dropped, process this one normally
						update = process = true;
					}
				} else {
					// This message was previously thought to have been lost but came late
					// If unreliable ignore, if reliable process anyway
					if (reliable) process = true;
				}

				// Update sequence
				if (update) {
					OrderedSequence[info.Channel] = info.Sequence.Value;
					OrderedSignal[info.Channel]?.Cancel();
					OrderedSignal[info.Channel]?.Dispose();
					OrderedSignal[info.Channel] = null;
				}

				// Process message and return
				if (process) OnReceiveCustom(message, info);

				// No delay
				return null;

			}

		}

		/// <summary>Automatically remove a received unique message after enough time.</summary>
		private async Task ReceiveUniqueTimeoutAsync(Tuple<byte, ushort> tuple) {
			try {

				// Delay before removing
				await DelayAsync(Config.DuplicateTimeout);

				// Remove from duplicates
				lock (UniqueLock) UniqueReceived?.Remove(tuple);

			} catch (ObjectDisposedException exception) {

				// Notify listener
				if (StateDisposed != 1) Listener.OnPeerException(this, exception);

			} catch (Exception exception) {

				// Notify listener
				Listener.OnPeerException(this, exception);

			}
		}

		/// <summary>Automatically disposes of received fragments after timeout.</summary>
		private async Task FragmentTimeoutAsync(CancellationTokenSource cancel) {
			try {

				// Delay before disposing
				bool cancelled = await DelayAsync(Config.FragmentTimeout, cancel);
				if (cancelled) return;

				// Dispose if still the same token
				lock (FragmentLock) {
					if (cancel == FragmentCancel) {
						FragmentDispose();
					} else {
						cancel.Dispose();
					}
				}

			} catch (ObjectDisposedException exception) {

				// Notify listener
				if (StateDisposed != 1) Listener.OnPeerException(this, exception);

			} catch (Exception exception) {

				// Notify listener
				Listener.OnPeerException(this, exception);

			}
		}

		/// <summary>Dispose of all received fragments.</summary>
		private void FragmentDispose() {
			Host.Allocator.ReturnMessage(ref FragmentReceived);
			Host.Allocator.ReturnMessage(ref FragmentDataParts);
			Host.Allocator.ReturnMessage(ref FragmentDataLast);
			FragmentCancel?.Cancel();
			FragmentCancel?.Dispose();
			FragmentCancel = null;
			FragmentLengthParts = 0;
			FragmentLengthLast = 0;
			FragmentLast = 0;
			FragmentID--;
		}

		/// <summary>Delay and return true if cancelled or false if completed or throw if disposed.</summary>
		private async Task<bool> DelayAsync(int delay, CancellationTokenSource token) {

			// If no token, do nothing
			if (token == null) {
				return true;
			}

			// If no delay, do nothing
			if (delay <= 0) {
				if (DisposeToken.IsCancellationRequested) {
					throw new ObjectDisposedException(GetType().FullName);
				} else {
					return token.IsCancellationRequested;
				}
			}

			// Delay
			CancellationTokenSource combined = null;
			try {
				combined = CancellationTokenSource.CreateLinkedTokenSource(token.Token, DisposeToken.Token);
				await Task.Delay(delay, combined.Token);
				return token.IsCancellationRequested;
			} catch {
				if (DisposeToken.IsCancellationRequested) {
					throw new ObjectDisposedException(GetType().FullName);
				} else if (token.IsCancellationRequested) {
					return true;
				} else {
					throw;
				}
			} finally {
				try { combined?.Dispose(); } catch { }
			}

		}

		/// <summary>Delay and throw when disposed.</summary>
		private async Task DelayAsync(int delay) {

			// If no delay, do nothing
			if (delay <= 0) {
				if (DisposeToken.IsCancellationRequested) {
					throw new ObjectDisposedException(GetType().FullName);
				} else {
					return;
				}
			}

			// Delay
			try {
				await Task.Delay(delay, DisposeToken.Token);
			} catch {
				if (DisposeToken.IsCancellationRequested) {
					throw new ObjectDisposedException(GetType().FullName);
				} else {
					throw;
				}
			}

		}

	}

}
