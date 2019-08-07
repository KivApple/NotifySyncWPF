using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QRCoder;

namespace NotifySync {
	public partial class MainWindow: Window, INotifyPropertyChanged {
		public static MainWindow Instance { get; private set; }
		public event PropertyChangedEventHandler PropertyChanged;
		public bool CanClose = false;
		private bool _shown = false;
		
		public MainWindow() {
			InitializeComponent(); 
			
			DevicesListBox.ItemsSource = App.ProtocolServer.PairedDevices;
			(App.ProtocolServer.PairedDevices as INotifyCollectionChanged).CollectionChanged += 
				PairedDevices_OnCollectionChanged;
			Deactivated += Window_Deactivated;
			Closing += Window_Closing;
			if (App.StartMinimized) {
				Hide();
			}
			Instance = this;

			using (
				var regKey = Registry.CurrentUser.OpenSubKey(
					"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false
				)
			) AutoStartCheckBox.IsChecked = regKey.GetValue("NotifySync") != null;
			AutoStartCheckBox.Checked += AutoStartCheckBox_Checked;

			if (App.ProtocolServer.PairedDevices.Count == 0) {
				PairNewDevicePanel.Visibility = Visibility.Visible;
				GenerateNewDeviceQrCode();
			}
		}

		protected override void OnContentRendered(EventArgs e) {
			base.OnContentRendered(e);
			if (_shown) return;
			_shown = true;
			App.ProtocolServer.SendBroadcasts();
		}
		
		private void Window_Closing(object sender, CancelEventArgs e) {
			_shown = false;
			if (CanClose) return;
			e.Cancel = true;
			Hide();
		}

		private void Window_Deactivated(object sender, EventArgs e) {
			if (WindowStyle == WindowStyle.None) {
				Hide();
			}
		}
		
		private void GeneralSettingsButton_OnClick(object sender, RoutedEventArgs e) {
			if (GeneralSettingsPanel.Visibility != Visibility.Visible) {
				GeneralSettingsPanel.Visibility = Visibility.Visible;
			} else {
				GeneralSettingsPanel.Visibility = Visibility.Collapsed;
			}
		}

		private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e) {
			EnableAutoStart(AutoStartCheckBox.IsChecked.Value);
		}

		public static void EnableAutoStart(bool enable) {
			using (
				var regKeyApp = Registry.CurrentUser.OpenSubKey(
					"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true
				)
			) {
				if (enable) {
					regKeyApp.SetValue("NotifySync", Process.GetCurrentProcess().MainModule.FileName + " --minimized");
				} else {
					regKeyApp.DeleteValue("NotifySync");
				}
			}
		}

		private void PairNewDeviceButton_OnClick(object sender, RoutedEventArgs e) {
			if (PairNewDevicePanel.Visibility != Visibility.Visible) {
				GenerateNewDeviceQrCode();
				PairNewDevicePanel.Visibility = Visibility.Visible;
			} else {
				PairNewDevicePanel.Visibility = Visibility.Collapsed;
				App.ProtocolServer.CancelPairing();
			}
		}

		private void NewDeviceQrCodeButton_OnClick(object sender, RoutedEventArgs e) {
			GenerateNewDeviceQrCode();
		}
		
		private void UnpairDeviceButton_OnClick(object sender, RoutedEventArgs e) {
			var device = (sender as Button).DataContext as RemoteDevice;
			if (device == null) return;
			var result = MessageBox.Show(
				String.Format(Properties.Resources.AreYouSureUnpair, device.Name), 
				Properties.Resources.UnpairDeviceButton,
				MessageBoxButton.YesNo,
				MessageBoxImage.Warning
			);
			if (result != MessageBoxResult.Yes) return;
			GeneralSettingsButton_OnClick(null, null); 
			App.ProtocolServer.UnPair(device);
		}
		
		private void GenerateNewDeviceQrCode() {
			var dataText = App.ProtocolServer.StartPairing();
			var qrCodeGenerator = new QRCodeGenerator();
			var qrCodeData = qrCodeGenerator.CreateQrCode(dataText, QRCodeGenerator.ECCLevel.Q);
			var qrCode = new QRCode(qrCodeData);
			var qrCodeBitmap = qrCode.GetGraphic(20);
			using (var memory = new MemoryStream()) {
				qrCodeBitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
				memory.Position = 0;
				var bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.StreamSource = memory;
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.EndInit();
				bitmapImage.Freeze();
				NewDeviceQrCodeImage.Source = bitmapImage;
			}
		}

		private void SendFileButton_OnClick(object sender, RoutedEventArgs e) {
			var dialog = new OpenFileDialog {
				Multiselect = true
			};
			if (dialog.ShowDialog(this) != true) return;
			((sender as Button).DataContext as RemoteDevice).FileSender.SendFiles(dialog.FileNames);
		}

		private void DeviceItemGrid_OnDrop(object sender, DragEventArgs e) {
			if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
			var fileNames = (string[]) e.Data.GetData(DataFormats.FileDrop);
			((sender as Grid).DataContext as RemoteDevice).FileSender.SendFiles(fileNames);
		}
		
		private void PairedDevices_OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
			if (e.NewItems != null && e.NewItems.Count > 0) {
				PairNewDevicePanel.Visibility = Visibility.Collapsed;
			}
		}
		
		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
