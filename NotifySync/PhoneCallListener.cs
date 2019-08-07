using System.Threading.Tasks;
using NotifySync.Properties;

namespace NotifySync {
	public class PhoneCallListener {
		private readonly RemoteDevice _device;

		public PhoneCallListener(RemoteDevice device) {
			_device = device;
		}
		
		public async Task HandleCall(dynamic json) {
			string number = json.number;
			string displayName = json.displayName;
			
			await App.RunOnUiThreadAsync(() => {
				App.SystemNotifier.ShowNotification(new SystemNotification {
					Tag = "phone-call",
					AppName = _device.Name,
					Title = Resources.IncomingCall,
					Text = displayName != null ? displayName + "\n" + number : number,
					Ongoing = true
				});
			});
		}

		public async Task HandleCallEnded() {
			await App.RunOnUiThreadAsync(() => {
				App.SystemNotifier.DismissNotification("phone-call");
			});
		}
	}
}
