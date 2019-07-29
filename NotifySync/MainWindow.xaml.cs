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
		private RemoteDevice _selectedRemoteDevice = null;
		public RemoteDevice SelectedRemoteDevice => _selectedRemoteDevice;
		public event PropertyChangedEventHandler PropertyChanged;
		public bool CanClose = false;
		
		public MainWindow() {
			InitializeComponent(); 
			DevicesListBox.ItemsSource = App.ProtocolServer.PairedDevices;
			(App.ProtocolServer.PairedDevices as INotifyCollectionChanged).CollectionChanged += 
				PairedDevices_OnCollectionChanged;
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
		}
		
		private void GeneralSettingsButton_OnClick(object sender, RoutedEventArgs e) {
			App.ProtocolServer.CancelPairing();
			DevicesListBox.SelectedIndex = -1;
			SectionsTabControl.SelectedIndex = 0;
		}

		private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e) {
			using (
				var regKeyApp = Registry.CurrentUser.OpenSubKey(
					"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true
				)
			) {
				if (AutoStartCheckBox.IsChecked == true) {
					regKeyApp.SetValue("NotifySync", Process.GetCurrentProcess().MainModule.FileName + " --minimized");
				} else {
					regKeyApp.DeleteValue("NotifySync");
				}
			}
		}
		
		private void PairNewDeviceButton_OnClick(object sender, RoutedEventArgs e) {
			DevicesListBox.SelectedIndex = -1;
			SectionsTabControl.SelectedIndex = 1;
			GenerateNewDeviceQrCode();
		}
		
		private void DevicesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (DevicesListBox.SelectedIndex >= 0) {
				SectionsTabControl.SelectedIndex = 2;
				_selectedRemoteDevice = DevicesListBox.SelectedItem as RemoteDevice;
				NotifyPropertyChanged("SelectedRemoteDevice");
			}
		}
		
		private void PairedDevices_OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
			if (e.NewItems != null && e.NewItems.Count > 0) {
				DevicesListBox.SelectedIndex = e.NewStartingIndex;
			}
		}

		private void NewDeviceQrCodeButton_OnClick(object sender, RoutedEventArgs e) {
			GenerateNewDeviceQrCode();
		}
		
		private void UnpairDeviceButton_OnClick(object sender, RoutedEventArgs e) {
			var device = SelectedRemoteDevice;
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

		private void Window_Closing(object sender, CancelEventArgs e) {
			if (CanClose) return;
			e.Cancel = true;
			Hide();
		}

		public void ShowDevice(RemoteDevice device) {
			// TODO: Fix it because it doesn't work
			DevicesListBox.SelectedItems.Clear();
			DevicesListBox.SelectedItems.Add(device);
		}
		
		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private void SendFileButton_OnClick(object sender, RoutedEventArgs e) {
			var dialog = new OpenFileDialog {
				Multiselect = true
			};
			if (dialog.ShowDialog(this) != true) return;
			SelectedRemoteDevice.FileSender.SendFiles(dialog.FileNames);
		}

		private void DeviceTab_OnDrop(object sender, DragEventArgs e) {
			if (SelectedRemoteDevice == null) return;
			if (!SelectedRemoteDevice.IsConnected) return;
			if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
			var fileNames = (string[]) e.Data.GetData(DataFormats.FileDrop);
			SelectedRemoteDevice.FileSender.SendFiles(fileNames);
		}
	}
}
