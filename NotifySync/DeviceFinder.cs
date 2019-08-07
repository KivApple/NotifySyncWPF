using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NotifySync {
	public class DeviceFinder: INotifyPropertyChanged {
		private readonly RemoteDevice _device;
		public event PropertyChangedEventHandler PropertyChanged;

		private bool _finding;
		public bool Finding {
			get { return _finding; }
			set {
				if (_finding == value) return;
				if (value) {
					Find();
				} else {
					Cancel();
				}
			}
		}

		public DeviceFinder(RemoteDevice device) {
			_device = device;
		}

		private void Find() {
			Task.Run(async () => {
				await _device.CurrentConnection?.SendJson(new JObject {
					["type"] = "find-device"
				});
			});
			_finding = true;
			NotifyPropertyChanged("Finding");
		}

		private void Cancel() {
			Task.Run(async () => {
				await _device.CurrentConnection?.SendJson(new JObject {
					["type"] = "find-device",
					["cancel"] = true
				});
			});
			_finding = false;
			NotifyPropertyChanged("Finding");
		}
		
		public void DeviceFound() {
			_finding = false;
			Finding = false;
			NotifyPropertyChanged("Finding");
		}

		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
