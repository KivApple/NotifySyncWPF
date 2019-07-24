using System;

namespace NotifySync {
	public interface ISystemNotifier {
		void ShowNotification(SystemNotification notification);
		void DismissNotification(string tag);
	}

	public class SystemNotification {
		public string Tag;
		public string Title;
		public string Text;
		public string AppName;
		public byte[] IconData;
		public DateTime? Timestamp;
		public Action[] Actions = new Action[0];
		public event Action<SystemNotification> Dismissed;
		public event Action<SystemNotification> Activated;
		public event Action<SystemNotification, int, string> ActionActivated;

		public void Dismiss() {
            Dismissed?.Invoke(this);
        }
		
		public void Activate() {
			Activated?.Invoke(this);
		}

		public void ActivateAction(int index, string text) {
			ActionActivated?.Invoke(this, index, text);
		}
		
		public class Action {
			public int Index;
			public string Title;
			public bool IsTextInput;
		}
	}
}
