using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NotifySync {
	public class RemoteDevice: INotifyPropertyChanged {
		public string Name { get; set; }
        public readonly byte[] Key;
        private readonly NetworkCipher _cipher;
		private TcpClient _client = null;
		private NetworkStream _networkStream = null;
		public bool IsConnected => _client != null;
		public IPAddress LastSeenIpAddress { get; private set; }
		public event PropertyChangedEventHandler PropertyChanged;

		public BatteryStatus BatteryStatus { get; }
		public NotificationList NotificationList { get; }
		
		public RemoteDevice(byte[] key) {
            Key = key;
			_cipher = new NetworkCipher(key);
			BatteryStatus = new BatteryStatus();
			NotificationList = new NotificationList(this);
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
					MessageBox.Show(e.Message + "\n" + e.StackTrace);
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
			
			try {
				await _client.ConnectAsync(address, ProtocolServer.TcpPort);
				_networkStream = _client.GetStream();
				await SendHandshake();
				LastSeenIpAddress = address;
				NotifyPropertyChanged("LastSeenIpAddress");
				NotifyPropertyChanged("IsConnected");
				_client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

				await HandleConnect();
				
				var packetLengthBytes = new byte[2];
				while (true) {
					if (!await ReadExactNetworkBytes(packetLengthBytes)) {
						break;
					}

					var packetLength = (ushort) IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packetLengthBytes, 0));
					var encryptedPacket = new byte[packetLength];
					if (!await ReadExactNetworkBytes(encryptedPacket)) {
						break;
					}

					var data = Encoding.UTF8.GetString(DecryptChunk(encryptedPacket)).TrimEnd('\0');
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

		private async Task SendHandshake() {
			var handshake = new Random().Next() + ":NotifySync:" + Properties.Settings.Default.DeviceName;
			await SendPacket(Encoding.UTF8.GetBytes(handshake));
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
			}
		}

		private async Task SendPacket(byte[] data) {
			if (_networkStream == null) return;
			var packet = EncryptChunk(data);
			var packetLength = IPAddress.HostToNetworkOrder((short) packet.Length);
			var packetLengthBytes = BitConverter.GetBytes(packetLength);
			await _networkStream.WriteAsync(packetLengthBytes, 0, packetLengthBytes.Length);
			await _networkStream.WriteAsync(packet, 0, packet.Length);
		}

		public async Task SendJson(object data) {
			var json = JsonConvert.SerializeObject(data);
			await SendPacket(Encoding.UTF8.GetBytes(json));
		}

		private async Task<bool> ReadExactNetworkBytes(byte[] buffer) {
			return await ReadExactNetworkBytes(buffer, 0, buffer.Length);
		}
		
		private async Task<bool> ReadExactNetworkBytes(byte[] buffer, int offset, int count) {
			while (offset < count) {
				var chunkSize = await _networkStream.ReadAsync(buffer, offset, count - offset);
				if (chunkSize == 0) {
					return false;
				}
				offset += chunkSize;
			}
			return true;
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
