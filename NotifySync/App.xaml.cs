using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Shell;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace NotifySync {
	public partial class App : Application, ISingleInstanceApp {
		public static NotifyIcon NotifyIcon;
		public static ISystemNotifier SystemNotifier;
		public static ProtocolServer ProtocolServer;
		public static IPCServer ipcServer;
		public static bool StartMinimized { get; private set; }

		protected override void OnStartup(StartupEventArgs e) {
			base.OnStartup(e);
			foreach (var arg in e.Args) {
				if (arg == "--minimized") {
					StartMinimized = true;
				}
			}
			InitSettings();
			InitNotifyIcon();
			SystemNotifier = new UwpToastNotifier(); // new BalloonSystemNotifier();
			ProtocolServer = new ProtocolServer();
			ipcServer = new IPCServer();
		}

		protected override void OnExit(ExitEventArgs e) {
			ProtocolServer.SavePairedDevices();
			NotifySync.Properties.Settings.Default.Save();
			NotifyIcon.Dispose();
			NotifyIcon = null;
			base.OnExit(e);
		}

		private static void InitSettings() {
			if (NotifySync.Properties.Settings.Default.UpgradeNeeded) {
				NotifySync.Properties.Settings.Default.Upgrade();
				NotifySync.Properties.Settings.Default.Save();
				NotifySync.Properties.Settings.Default.Reload();
				NotifySync.Properties.Settings.Default.UpgradeNeeded = false;
			}
			if (NotifySync.Properties.Settings.Default.DeviceName.Length == 0) {
				NotifySync.Properties.Settings.Default.DeviceName = Environment.MachineName;
			}
		}

		private static void InitNotifyIcon() {
			var contextMenu = new ContextMenu() {
				MenuItems = {
					{
						NotifySync.Properties.Resources.Show,
						(sender, e) => ShowMainWindow()
					}, {
						NotifySync.Properties.Resources.Exit,
						(sender, e) => {
							NotifySync.MainWindow.Instance.CanClose = true;
							NotifySync.MainWindow.Instance.Close();
						}
					}
				}
			};
			contextMenu.MenuItems[0].DefaultItem = true;
			NotifyIcon = new NotifyIcon {
				Icon = NotifySync.Properties.Resources.TrayIcon,
				Text = NotifySync.Properties.Resources.AppName,
				ContextMenu = contextMenu,
				Visible = true
			};
			NotifyIcon.Click += (sender, e) => ShowMainWindow();
			NotifyIcon.DoubleClick += (sender, e) => ShowMainWindow();
		}

		private static void ShowMainWindow() {
			NotifySync.MainWindow.Instance.Show();
			NotifySync.MainWindow.Instance.Activate();
		}

		public static void ShowDeviceWindow(RemoteDevice device) {
			NotifySync.MainWindow.Instance.ShowDevice(device);
			ShowMainWindow();
		}

		public static async Task RunOnUiThreadAsync(Action action) {
			await Current.Dispatcher.InvokeAsync(action);
		}

		public static void ShowException(Exception e) {
			MessageBox.Show(e.GetType() + ": " + e.Message + "\n" + e.StackTrace);
		}

		#region ISingleInstanceApp Members
		public bool SignalExternalCommandLineArgs(IList<string> args) {
			var minimized = false;
			foreach (var arg in args) {
				if (arg == "--minimized") {
					minimized = true;
				}
			}

			if (!minimized) {
				ShowMainWindow();
			}
			return true;
		}
		#endregion
		
		[STAThread]
		public static void Main() {
			if (!SingleInstance<App>.InitializeAsFirstInstance("NotifySync")) return;
			
			var app = new App();
			app.InitializeComponent();
			app.Run();
			
			SingleInstance<App>.Cleanup();
		}
	}
}
