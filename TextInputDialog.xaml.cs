using System;
using System.Windows;
using System.Windows.Input;

namespace NotifySync {
	public partial class TextInputDialog : Window {
		public string Message { get; set; }
		public string ButtonText { get; set; }
		public string Text { get; set; }
		private bool _isShown;
		
		public TextInputDialog() {
			InitializeComponent();
		}
		
		private void OkButton_OnClick(object sender, RoutedEventArgs e) {
			DialogResult = true;
			Close();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e) {
			WindowStartupLocation =
				Owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
		}
		
		private void Window_OnContentRendered(object sender, EventArgs e) {
			if (_isShown) return;
			_isShown = true;
				
			TextBox.Text = Text;
			TextBox.CaretIndex = TextBox.Text.Length;

			Activate();
		}
		
		private void TextBox_OnKeyUp(object sender, KeyEventArgs e) {
			switch (e.Key) {
				case Key.Enter:
					e.Handled = true;
					DialogResult = true;
					Close();
					break;
				case Key.Escape:
					Close();
					break;
			}
		}

		public static string Show(string title, string message = "", string okButtonText = "OK", string defaultText = "") {
			var dialog = new TextInputDialog {
				Title = title,
				Message = message,
				ButtonText = okButtonText,
				Text = defaultText
			};
			var result = dialog.ShowDialog();
			return result == true ? dialog.Text : null;
		}
	}
}
