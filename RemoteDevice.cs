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
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NotifySync.Properties;

namespace NotifySync {
	public class RemoteDevice: INotifyPropertyChanged {
		public string Name { get; set; }
        public readonly byte[] Key;
        private readonly NetworkCipher _cipher;
		private TcpClient _client;
		private NetworkStream _networkStream;
		private SemaphoreSlim _sendSemaphore;
		private SemaphoreSlim _sendConfirmSemaphore;
		private bool _sendConfirmStatus;
		private MD5 _outputHasher = MD5.Create();
		private MD5 _inputHasher = MD5.Create();
		private byte[] _packetLengthBytes = new byte[2];
		private byte[] _packetHashBytes = new byte[16];
		public bool IsConnected => _client != null;
		public IPAddress LastSeenIpAddress { get; private set; }
		public event PropertyChangedEventHandler PropertyChanged;
		
		public BatteryStatus BatteryStatus { get; }
		public NotificationList NotificationList { get; }
		public FileSender FileSender { get; }
		
		public RemoteDevice(byte[] key) {
            Key = key;
			_cipher = new NetworkCipher(key);
			// Initialize plugins
			BatteryStatus = new BatteryStatus();
			NotificationList = new NotificationList(this);
			FileSender = new FileSender(this);
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
			var packet = new Random().Next() + ":NotifySyncServer:1:" + Properties.Settings.Default.DeviceName;
			var encryptedPacket = EncryptChunk(Encoding.UTF8.GetBytes(packet));
			await udpClient.SendAsync(encryptedPacket, encryptedPacket.Length, 
				new IPEndPoint(broadcastAddress, ProtocolServer.UdpPort));
		}

		public void Connect(IPAddress address) {
			Disconnect();
			Task.Run(async () => {
				try {
					await DoConnect(address);
				} catch (Exception e) {
					App.ShowException(e);
					Disconnect();
				}
			});
		}

		public void Disconnect() {
			try {
				_client?.Close();
			} catch (ObjectDisposedException) {
			}
			_networkStream = null;
			_client = null;
			NotifyPropertyChanged("IsConnected");
		}
		
		private async Task DoConnect(IPAddress address) {
			_client = new TcpClient();
			_sendSemaphore = new SemaphoreSlim(1, 1);
			_sendConfirmSemaphore = new SemaphoreSlim(0, 1);
			
			try {
				await _client.ConnectAsync(address, ProtocolServer.TcpPort);
				_networkStream = _client.GetStream();
				await SendHandshake();
				LastSeenIpAddress = address;
				NotifyPropertyChanged("LastSeenIpAddress");
				NotifyPropertyChanged("IsConnected");
				_client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

				await HandleConnect();

				while (true) {
					var data = await ReadPacket();
					if (data == null) continue;
					await HandleReceivedData(data);
				}
			} catch (IOException) {
			} catch (ObjectDisposedException) {
			} catch (CryptographicException) {
			}
			
			if (_client != null) {
				try {
					_client.Close();
				}
				catch (ObjectDisposedException) {
				}
			}
			
			_networkStream = null;
			_client = null;
			NotifyPropertyChanged("IsConnected");
		}

		private async Task<byte[]> ReadEncryptedPacket() {
			await ReadExactNetworkBytes(_packetHashBytes);
			await ReadExactNetworkBytes(_packetLengthBytes);
			var packetLength = (ushort) IPAddress.NetworkToHostOrder(BitConverter.ToInt16(_packetLengthBytes, 0));
			var encryptedPacket = new byte[packetLength];
			await ReadExactNetworkBytes(encryptedPacket);
			var expectedMd5Bytes = _inputHasher.ComputeHash(encryptedPacket);
			var md5Error = false;
			for (var i = 0; i < _packetHashBytes.Length; i++) {
				if (_packetHashBytes[i] != expectedMd5Bytes[i]) {
					md5Error = true;
				}
			}
			return !md5Error ? encryptedPacket : null;
		}

		private async Task<string> ReadPacket() {
			var encryptedPacket = await ReadEncryptedPacket();
			var packet = DecryptChunk(encryptedPacket);
			var data = Encoding.UTF8.GetString(packet).TrimEnd('\0');
			if (data != "okay" && data != "error") return data;
			_sendConfirmStatus = data == "okay";
			_sendConfirmSemaphore.Release();
			return null;
		}

		private async Task SendHandshake() {
			var handshake = new Random().Next() + ":NotifySync:" + Properties.Settings.Default.DeviceName;
			await SendStringPacket(handshake, false, true);
		}

		private async Task HandleConnect() {
			await NotificationList.HandleConnect();
		}
		
		private async Task HandleReceivedData(string data) {
			dynamic json = JObject.Parse(data);
			string type = json.type;
			switch (type) {
				case "battery":
					BatteryStatus.HandleJson(json);
					NotifyPropertyChanged("BatteryStatus");
					break;
				case "notification":
					await NotificationList.HandleJson(this, json);
					break;
				default:
					break;
			}
		}

		private async Task SendEncryptedPacket(byte[] data, bool waitConfirmation, bool handshake) {
			var packetLength = IPAddress.HostToNetworkOrder((short) data.Length);
			var packetLengthBytes = BitConverter.GetBytes(packetLength);
			await _sendSemaphore.WaitAsync();
			try {
				do {
					if (!handshake) {
						var hashBytes = _outputHasher.ComputeHash(data);
						await _networkStream.WriteAsync(hashBytes, 0, hashBytes.Length);
					}
					await _networkStream.WriteAsync(packetLengthBytes, 0, packetLengthBytes.Length);
					await _networkStream.WriteAsync(data, 0, data.Length);
					if (!handshake && waitConfirmation) {
						await _sendConfirmSemaphore.WaitAsync();
					}
				} while (!handshake && waitConfirmation && !_sendConfirmStatus);
			} finally {
				_sendSemaphore.Release();
			}
		}

		private async Task SendBinaryPacket(byte[] data, bool waitConfirmation, bool handshake) {
			await SendEncryptedPacket(EncryptChunk(data), waitConfirmation, handshake);
		}

		private async Task SendStringPacket(string data, bool waitConfirmation, bool handshake) {
			await SendBinaryPacket(Encoding.UTF8.GetBytes(data), waitConfirmation, handshake);
		}

		private async Task SendPacket(string data) {
			await SendStringPacket(data, true, false);
		}
		
		public async Task SendJson(object data) {
			var json = JsonConvert.SerializeObject(data);
			await SendPacket(json);
		}

		private async Task ReadExactNetworkBytes(byte[] buffer) {
			await ReadExactNetworkBytes(buffer, 0, buffer.Length);
		}
		
		private async Task ReadExactNetworkBytes(byte[] buffer, int offset, int count) {
			while (offset < count) {
				var chunkSize = await _networkStream.ReadAsync(buffer, offset, count - offset);
				if (chunkSize == 0) {
					throw new IOException("Endpoint disconnected");
				}
				offset += chunkSize;
			}
		}
		
		private byte[] EncryptChunk(byte[] chunk) {
			using (var outputStream = new MemoryStream()) {
				using (var encryptor = _cipher.CreateEncryptor()) {
					using (var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write)) {
						cryptoStream.Write(chunk, 0, chunk.Length);
					}
					return outputStream.ToArray();
				}
			}
		}

		private byte[] DecryptChunk(byte[] chunk) {
			using (var outputStream = new MemoryStream()) {
				using (var decryptor = _cipher.CreateDecryptor()) {
					using (var cryptoStream = new CryptoStream(outputStream, decryptor, CryptoStreamMode.Write)) {
						cryptoStream.Write(chunk, 0, chunk.Length);
					}
					return outputStream.ToArray();
				}
			}
		}
		
		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
