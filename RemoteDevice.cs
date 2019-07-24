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
		private readonly byte[] _packetLengthBytes = new byte[2];
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
			NotificationList = new NotificationList();
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
					while (await DoConnect(address)) {
						await Task.Delay(1000);
					}
				} catch (Exception e) {
					App.ShowException(e);
					Disconnect();
				}
			});
		}

		public void Disconnect() {
			_client?.Close();
		}
		
		private async Task<bool> DoConnect(IPAddress address) {
			var wasConnected = false;
			_client = new TcpClient();
			_sendSemaphore = new SemaphoreSlim(1, 1);
			
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
					wasConnected = true;
					await HandleReceivedData(data);
				}
			} catch (IOException) {
			} catch (SocketException) {
			} catch (ObjectDisposedException) {
			} catch (CryptographicException) {
			}
			
			if (_client != null) {
				await _sendSemaphore.WaitAsync();
				_networkStream = null;
				_sendSemaphore.Release();
				try {
					_client.Close();
				}
				catch (ObjectDisposedException) {
				}
			}
			
			_client = null;
			NotifyPropertyChanged("IsConnected");
			
			return wasConnected;
		}

		private async Task<byte[]> ReadEncryptedPacket() {
			await ReadExactNetworkBytes(_packetLengthBytes);
			var packetLength = (ushort) IPAddress.NetworkToHostOrder(BitConverter.ToInt16(_packetLengthBytes, 0));
			var encryptedPacket = new byte[packetLength];
			await ReadExactNetworkBytes(encryptedPacket);
			return encryptedPacket;
		}

		private async Task<string> ReadPacket() {
			var encryptedPacket = await ReadEncryptedPacket();
			var packet = DecryptChunk(encryptedPacket);
			var data = Encoding.UTF8.GetString(packet).TrimEnd('\0');
			return data;
		}

		private async Task SendHandshake() {
			var handshake = new Random().Next() + ":NotifySync:" + Properties.Settings.Default.DeviceName;
			await SendStringPacket(handshake);
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

		private async Task SendEncryptedPacket(byte[] data) {
			var packetLength = IPAddress.HostToNetworkOrder((short) data.Length);
			var packetLengthBytes = BitConverter.GetBytes(packetLength);
			await _sendSemaphore.WaitAsync();
			try {
				if (_networkStream == null) {
					throw new IOException(Resources.DeviceNotConnected);
				}
				await _networkStream.WriteAsync(packetLengthBytes, 0, packetLengthBytes.Length);
				await _networkStream.WriteAsync(data, 0, data.Length);
			} finally {
				_sendSemaphore.Release();
			}
		}

		private async Task SendBinaryPacket(byte[] data) {
			await SendEncryptedPacket(EncryptChunk(data));
		}

		private async Task SendStringPacket(string data) {
			await SendBinaryPacket(Encoding.UTF8.GetBytes(data));
		}

		private async Task SendPacket(string data) {
			await SendStringPacket(data);
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
