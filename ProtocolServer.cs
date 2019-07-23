using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using NotifySync.Properties;

namespace NotifySync {
	public class ProtocolServer {
		public const int UdpPort = 5397;
		public const int TcpPort = 5397;
		
		private RemoteDevice _pairingDevice;
		private readonly ObservableCollection<RemoteDevice> _pairedDevices = new ObservableCollection<RemoteDevice>();
		public readonly ReadOnlyObservableCollection<RemoteDevice> PairedDevices;
		
		public ProtocolServer() {
			PairedDevices = new ReadOnlyObservableCollection<RemoteDevice>(_pairedDevices);
			if (Settings.Default.PairedDevices != null) {
				foreach (var deviceSpec in Settings.Default.PairedDevices)
				{
					var parts = deviceSpec.Split(new[] { ':' }, 2);
					var device = new RemoteDevice(Convert.FromBase64String(parts[0])) {
						Name = parts[1]
					};
					_pairedDevices.Add(device);
				}
			}
			NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
			Task.Run(async () => {
				try {
					await AcceptBroadcast();
				} catch (Exception e) {
					App.ShowException(e);
				}
			});
			SendBroadcasts();
		}
		
		public void SavePairedDevices() {
			if (Settings.Default.PairedDevices == null) {
				Settings.Default.PairedDevices = new System.Collections.Specialized.StringCollection();
			} else {
				Settings.Default.PairedDevices.Clear();
			}
			foreach (var device in _pairedDevices) {
				Settings.Default.PairedDevices.Add(Convert.ToBase64String(device.Key) + ":" + device.Name);
			}
		}

		public string StartPairing() {
			var key = new byte[32];
			new Random().NextBytes(key);
			_pairingDevice = new RemoteDevice(key);
			return "NotifySync:" + Convert.ToBase64String(key) + ":" + Settings.Default.DeviceName;
		}

		public void CancelPairing() {
			_pairingDevice = null;
		}

		public void UnPair(RemoteDevice device) {
			device.Disconnect();
			_pairedDevices.Remove(device);
		}
		
		private async Task AcceptBroadcast() {
			using (var udpClient = new UdpClient(UdpPort)) {
				while (true) {
					UdpReceiveResult packet;
					try {
						packet = await udpClient.ReceiveAsync();
					} catch (ObjectDisposedException) {
						break;
					}
					if (_pairingDevice != null) {
						var device = _pairingDevice;
						if (device.AcceptBroadcast(packet.Buffer)) {
							_pairingDevice = null;
							await Application.Current.Dispatcher.InvokeAsync(() => {
								_pairedDevices.Add(device);
								NotifyAboutNewDevice(device);
							});
							device.Connect(packet.RemoteEndPoint.Address);
							continue;
						}
					}
					foreach (var device in _pairedDevices) {
						if (!device.AcceptBroadcast(packet.Buffer)) continue;
						device.Connect(packet.RemoteEndPoint.Address);
						break;
					}
				}
			}
		}

		private void SendBroadcasts() {
			var addresses = NetworkInterface.GetAllNetworkInterfaces()
				.SelectMany(it => it.GetIPProperties().UnicastAddresses
					.Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
					.Select(address => {
						var addr = address.Address.GetAddressBytes();
						var mask = address.IPv4Mask.GetAddressBytes();
						var broadcast = new byte[addr.Length];
						for (var i = 0; i < broadcast.Length; i++) {
							broadcast[i] = (byte)(addr[i] | (byte) ~mask[i]);
						}
						return new IPAddress(broadcast);
					})
				).ToArray();
			Task.Run(async () => {
				var udpClient = new UdpClient {
					EnableBroadcast = true
				};
				foreach (var device in _pairedDevices) {
					foreach (var address in addresses) {
						await device.SendBroadcast(udpClient, address);
					}
					await Task.Delay(100);
				}
			});
		}

		private void OnNetworkAddressChanged(object sender, EventArgs e) {
			foreach (var device in _pairedDevices) {
				device.Disconnect();
			}
			SendBroadcasts();
		}

		private static void NotifyAboutNewDevice(RemoteDevice device) {
			App.SystemNotifier.ShowNotification(new SystemNotification {
				Title = device.Name,
				Text = Resources.NewDeviceNotification
			});
		}
	}
}
