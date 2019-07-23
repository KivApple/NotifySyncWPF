using System;
using System.Windows.Forms;

namespace NotifySync {
	public class BalloonSystemNotifier: ISystemNotifier {
		private SystemNotification _lastNotification;

		public BalloonSystemNotifier() {
			App.NotifyIcon.BalloonTipClicked += OnClicked;
			App.NotifyIcon.BalloonTipClosed += OnClosed;
		}
		
		public void ShowNotification(SystemNotification notification) {
			_lastNotification = notification;
			App.NotifyIcon.ShowBalloonTip(
				5000, 
				notification.AppName == null || notification.Title == notification.AppName ? 
					notification.Title :
					notification.Title + " - " + notification.AppName, 
				notification.Text, 
				ToolTipIcon.None
			);
		}

		public void DismissNotification(string tag) {
		}

		private void OnClicked(object sender, EventArgs e) {
			_lastNotification.Activate();
		}

		private void OnClosed(object sender, EventArgs e) {
			_lastNotification.Dismiss();
		}
	}
}
