using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace NotifySync {
	public class ClipboardListener {
		private readonly RemoteDevice _device;
		private long _currentTimestamp;
		private string _currentText = "";

		public ClipboardListener(RemoteDevice device) {
			_device = device;
			_currentTimestamp = (long) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
			_currentText = Clipboard.ContainsText() ? Clipboard.GetText() : "";
		}

		public void HandleConnect() {
			if (!Properties.Settings.Default.ShareClipboard) return;
			_device.CurrentConnection?.SendJson(new JObject {
				["type"] = "clipboard",
				["text"] = _currentText,
				["timestamp"] = _currentTimestamp
			});
		}

		public async Task HandleJson(dynamic json) {
			if (!Properties.Settings.Default.ShareClipboard) return;
			long timestamp = json.timestamp;
			if (timestamp <= _currentTimestamp) return;
			_currentTimestamp = timestamp;
			string text = json.text;
			if (text.Length > 0) {
				if (text == _currentText) return;
				_currentText = text;
				await App.RunOnUiThreadAsync(() => {
					Clipboard.SetText(text);
				});
			}
		}

		public void SetText(string text) {
			if (!Properties.Settings.Default.ShareClipboard) return;
			if (text == null) {
				text = "";
			}
			_currentTimestamp = (long) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
			if (text.Length > 5120) text = text.Substring(0, 5120);
			if (text == _currentText) return;
			_currentText = text;
			Task.Run(async () => {
				await _device.CurrentConnection?.SendJson(new JObject {
					["type"] = "clipboard",
					["text"] = text,
					["timestamp"] = _currentTimestamp
				});
			});
		}
	}
}
