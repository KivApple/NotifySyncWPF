using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NotifySync.Properties;

namespace NotifySync {
	public class RemoteDevice: INotifyPropertyChanged {
		public string Name { get; set; }
        public readonly byte[] Key;
		public readonly string Id;
        private readonly NetworkCipher _cipher;
		public Connection CurrentConnection { get; private set; }
        public bool IsConnected => CurrentConnection != null;
		public IPAddress CurrentIpAddress { get; private set; }
		public event PropertyChangedEventHandler PropertyChanged;
		
		public BatteryStatus BatteryStatus { get; }
		public NotificationList NotificationList { get; }
		public FileSender FileSender { get; }
		public FileReceiver FileReceiver { get; }
		
		public RemoteDevice(byte[] key) {
            Key = key;
			Id = BitConverter.ToString(SHA1.Create().ComputeHash(Key)).Replace("-", "");
			_cipher = new NetworkCipher(key);
			// Initialize plugins
			BatteryStatus = new BatteryStatus();
			NotificationList = new NotificationList();
			FileSender = new FileSender(this);
			FileReceiver = new FileReceiver(this);
		}

		public bool AcceptBroadcast(byte[] packetData) {
			try {
				var data = Encoding.UTF8.GetString(DecryptChunk(packetData))
					.TrimEnd('\0')
					.Split(new [] {':'}, 4);
				if (data.Length == 4 && data[1] == "NotifySync" && data[3].Length > 0) {
					if (Name != data[3]) {
						Name = data[3];
						NotifyPropertyChanged("Name");
					}
					return !IsConnected || data[2] != "0";
				}
			} catch (CryptographicException) {
			}
			return false;
		}

		public async Task SendBroadcast(UdpClient udpClient, IPAddress broadcastAddress) {
			var packet = new Random().Next() + ":NotifySyncServer:1:" + Settings.Default.DeviceName;
			var encryptedPacket = EncryptChunk(Encoding.UTF8.GetBytes(packet));
			await udpClient.SendAsync(encryptedPacket, encryptedPacket.Length, 
				new IPEndPoint(broadcastAddress, ProtocolServer.UdpPort));
		}

		public void Connect(IPAddress address) {
			Disconnect();
			CurrentConnection = new Connection(this, address, Settings.Default.EnableEncryption);
		}

		public void Disconnect() {
			CurrentConnection?.Disconnect();
		}

		private byte[] EncryptChunk(byte[] chunk) {
			lock (_cipher) {
				using (var outputStream = new MemoryStream()) {
					using (var encryptor = _cipher.CreateEncryptor()) {
						using (var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write)) {
							cryptoStream.Write(chunk, 0, chunk.Length);
						}

						return outputStream.ToArray();
					}
				}
			}
		}

		private byte[] DecryptChunk(byte[] chunk) {
			lock (_cipher) {
				using (var outputStream = new MemoryStream()) {
					using (var decryptor = _cipher.CreateDecryptor()) {
						using (var cryptoStream = new CryptoStream(outputStream, decryptor, CryptoStreamMode.Write)) {
							cryptoStream.Write(chunk, 0, chunk.Length);
						}

						return outputStream.ToArray();
					}
				}
			}
		}
		
		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public class Connection {
			//private const int ReconnectDelay = 2000;
			
			public readonly RemoteDevice RemoteDevice;
			//private bool _wasConnected;
			private TcpClient _tcpClient;
			private NetworkStream _networkStream;
			private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1);
			private readonly byte[] _packetLengthBytes = new byte[2];
			private readonly bool _encryptionEnabled;
			
			public Connection(RemoteDevice device, IPAddress ipAddress, bool encryptionEnabled) {
				RemoteDevice = device;
				_encryptionEnabled = encryptionEnabled;
				Task.Run(async () => {
					try {
						await Connect(ipAddress);
					} catch (Exception e) {
						App.ShowException(e);
					}
					/* if (_wasConnected) {
						await Task.Delay(ReconnectDelay);
						device.Connect(ipAddress);
					} */
				});
			}

			private async Task Connect(IPAddress ipAddress) {
				try {
					_tcpClient = new TcpClient();
					await _tcpClient.ConnectAsync(ipAddress, ProtocolServer.TcpPort);
					_tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
					_networkStream = _tcpClient.GetStream();
					await SendHandshake();
					RemoteDevice.CurrentIpAddress = ipAddress;
					RemoteDevice.NotifyPropertyChanged("CurrentIpAddress");
					RemoteDevice.NotifyPropertyChanged("IsConnected");
					while (_tcpClient.Connected) {
						dynamic json;
						try {
							json = await ReceiveJson();
						} catch (ObjectDisposedException) {
							break;
						}
						//_wasConnected = true;
						await HandleJson(json);
					}
				} catch (SocketReadFailedException) {
				} catch (SocketException) {
				} catch (IOException) {
				} finally {
					await HandleDisconnect();
				}
			}

			public void Disconnect() {
				_tcpClient.Close();
			}

			private async Task HandleDisconnect() {
				RemoteDevice.CurrentConnection = null;
				RemoteDevice.CurrentIpAddress = null;
				RemoteDevice.NotifyPropertyChanged("IsConnected");
				RemoteDevice.NotifyPropertyChanged("CurrentIpAddress");

				await RemoteDevice.FileReceiver.HandleDisconnect();
				await RemoteDevice.NotificationList.HandleDisconnect();
				RemoteDevice.BatteryStatus.HandleDisconnect();
				RemoteDevice.NotifyPropertyChanged("BatteryStatus");
			}

			private async Task HandleJson(dynamic json) {
				string type = json.type;
				switch (type) {
					case "battery":
						RemoteDevice.BatteryStatus.HandleJson(json);
						RemoteDevice.NotifyPropertyChanged("BatteryStatus");
						break;
					case "notification":
						await RemoteDevice.NotificationList.HandleJson(this, json);
						break;
					case "cancel-file":
						RemoteDevice.FileSender.CancelSending();
						break;
					case "file":
						await RemoteDevice.FileReceiver.HandleJson(json);
						break;
				}
			}

			private async Task SendHandshake() {
				var handshake = new Random().Next() + ":NotifySync:" + (_encryptionEnabled ? "1" : "0") + 
				                ":" + Settings.Default.DeviceName;
				await SendString(handshake, true);
			}

			public async Task SendJson(object data) {
				var json = JsonConvert.SerializeObject(data);
				await SendString(json);
			}

			private async Task SendString(string packet, bool handshake = false) {
				var bytes = Encoding.UTF8.GetBytes(packet);
				await SendBytes(bytes, handshake);
			}

			private async Task SendBytes(byte[] packet, bool handshake = false) {
				var encryptedPacket = handshake || _encryptionEnabled ? EncryptChunk(packet) : packet;
				await SendEncrypted(encryptedPacket);
			}
			
			private async Task SendEncrypted(byte[] packet) {
				var packetLength = IPAddress.HostToNetworkOrder((short) packet.Length);
				var packetLengthBytes = BitConverter.GetBytes(packetLength);
				await _sendSemaphore.WaitAsync();
				try {
					if (_networkStream == null) {
						throw new IOException(Resources.DeviceNotConnected);
					}
					await _networkStream.WriteAsync(packetLengthBytes, 0, packetLengthBytes.Length);
					await _networkStream.WriteAsync(packet, 0, packet.Length);
				} finally {
					_sendSemaphore.Release();
				}
			}

			private async Task<dynamic> ReceiveJson() {
				var text = await ReceiveString();
				return JObject.Parse(text);
			}
			
			private async Task<string> ReceiveString() {
				var bytes = await ReceiveBytes();
				return Encoding.UTF8.GetString(bytes);
			}

			private async Task<byte[]> ReceiveBytes() {
				var encryptedPacket = await ReceiveEncrypted();
				var packet = _encryptionEnabled ? DecryptChunk(encryptedPacket) : encryptedPacket;
				return packet;
			}
			
			private async Task<byte[]> ReceiveEncrypted() {
				await ReceiveExactNetworkBytes(_packetLengthBytes);
				var packetLength = (ushort) IPAddress.NetworkToHostOrder(BitConverter.ToInt16(_packetLengthBytes, 0));
				var encryptedPacket = new byte[packetLength];
				await ReceiveExactNetworkBytes(encryptedPacket);
				return encryptedPacket;
			}
			
			private async Task ReceiveExactNetworkBytes(byte[] buffer) {
				await ReceiveExactNetworkBytes(buffer, 0, buffer.Length);
			}
		
			private async Task ReceiveExactNetworkBytes(byte[] buffer, int offset, int count) {
				while (offset < count) {
					var chunkSize = await _networkStream.ReadAsync(buffer, offset, count - offset);
					if (chunkSize == 0) {
						throw new SocketReadFailedException();
					}
					offset += chunkSize;
				}
			}

			private byte[] EncryptChunk(byte[] chunk) {
				return RemoteDevice.EncryptChunk(chunk);
			}

			private byte[] DecryptChunk(byte[] chunk) {
				return RemoteDevice.DecryptChunk(chunk);
			}

			private class SocketReadFailedException : Exception {
			}
		}
	}
}
