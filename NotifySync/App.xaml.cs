using System;
using System.Collections.Generic;
using System.Diagnostics;
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
				switch (arg) {
					case "--minimized":
						StartMinimized = true;
						break;
					case "--installer":
						NotifySync.MainWindow.EnableAutoStart(true);
						Process.Start(Process.GetCurrentProcess().MainModule.FileName);
						Environment.Exit(0);
						return;
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
						(sender, e) => ShowMainWindow(false)
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
			NotifyIcon.Click += (sender, e) => ShowMainWindow(true);
			NotifyIcon.DoubleClick += (sender, e) => ShowMainWindow(false);
		}

		private static void ShowMainWindow(bool popup) {
			var window = NotifySync.MainWindow.Instance;
			var scale = PresentationSource.FromVisual(window).CompositionTarget.TransformToDevice.M11;
			var position = Control.MousePosition;
			var workingArea = Screen.FromPoint(position).WorkingArea;
			workingArea.Width = (int) (workingArea.Width / scale);
			workingArea.Height = (int) (workingArea.Height / scale);
			workingArea.X = (int) (workingArea.X / scale);
			workingArea.Y = (int) (workingArea.Y / scale);

			if (popup) {
				window.WindowStyle = WindowStyle.None;
			} else {
				window.WindowStyle = WindowStyle.SingleBorderWindow;
			}

			double left;
			double top;

			if (popup) {
				left = position.X / scale - window.Width / 2;
				top = position.Y / scale - window.Height;
				if (top + window.Height > workingArea.Bottom) {
					top = workingArea.Bottom - window.Height;
				}

				if (top < workingArea.Top) {
					top = workingArea.Top;
				}

				if (left + window.Width > workingArea.Right) {
					left = workingArea.Right - window.Width;
				}

				if (left < workingArea.Left) {
					left = workingArea.Left;
				}
			} else {
				left = workingArea.Left + (workingArea.Width - window.Width) / 2;
				top = workingArea.Top + (workingArea.Height - window.Height) / 2;
			}

			window.Left = left;
			window.Top = top;
			window.Show();
			window.Activate();
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
				ShowMainWindow(false);
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
