using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NotifySync {
	public class BatteryStatus: INotifyPropertyChanged {
		public int CurrentLevel { get; private set; } = -1;
		public bool Charging { get; private set; }
		public event PropertyChangedEventHandler PropertyChanged;

		public void HandleJson(dynamic json) {
			CurrentLevel = json.level;
			Charging = json.charging;
			NotifyPropertyChanged();
		}

		public void HandleDisconnect() {
			CurrentLevel = -1;
			Charging = false;
			NotifyPropertyChanged();
		}
		
		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
