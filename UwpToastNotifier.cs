using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using MS.WindowsAPICodePack.Internal;
using NotifySync.Properties;

namespace NotifySync {
	public class UwpToastNotifier : ISystemNotifier {
		private const string AppId = "NotifySync";
		private readonly string _appName = Properties.Resources.AppName;
		private readonly ToastNotifier _toastNotifier;
		private readonly Dictionary<string, ToastNotification> _toasts = new Dictionary<string, ToastNotification>();
		private readonly Dictionary<string, SystemNotification> _notifications = new Dictionary<string, SystemNotification>();
		private readonly SHA512 _iconHashFunc = new SHA512CryptoServiceProvider();
		private readonly ObjectIDGenerator _toastIdGenerator = new ObjectIDGenerator();
		
		public UwpToastNotifier() {
			CreateShortcut(AppId, _appName, false);
			CreateRegistryKey(AppId);
			_toastNotifier = ToastNotificationManager.CreateToastNotifier(AppId);
		}
		
		public void ShowNotification(SystemNotification notification) {
			ToastTemplateType toastTemplateType;
			if (notification.IconData != null) {
				toastTemplateType = notification.AppName != null ? 
					ToastTemplateType.ToastImageAndText04 : 
					ToastTemplateType.ToastImageAndText02;
			} else {
				toastTemplateType = notification.AppName != null ? 
					ToastTemplateType.ToastText04 : 
					ToastTemplateType.ToastText02;
			}
			var toastXml = ToastNotificationManager.GetTemplateContent(toastTemplateType);
			var toastStrings = toastXml.GetElementsByTagName("text");
			toastStrings[0].AppendChild(toastXml.CreateTextNode(notification.Title));
			toastStrings[1].AppendChild(toastXml.CreateTextNode(notification.Text));
			if (notification.AppName != null) {
				toastStrings[2].AppendChild(toastXml.CreateTextNode(notification.AppName));
				((XmlElement) toastStrings[2]).SetAttribute("placement", "attribution");
			}
			if (notification.IconData != null) {
				var toastImages = toastXml.GetElementsByTagName("image");
				var fileName = CreateIconFile(notification.IconData);
				toastImages[0].Attributes.GetNamedItem("src").NodeValue = "file:///" + fileName;
			}
			if (notification.Timestamp != null && Math.Abs((DateTime.Now - notification.Timestamp.Value).TotalMinutes) > 1) {
				toastXml.DocumentElement.SetAttribute("displayTimestamp",
					notification.Timestamp.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
			}
			if (notification.Actions.Length > 0) {
				var actionsElement = toastXml.CreateElement("actions");
				foreach (var action in notification.Actions) {
					var actionElement = toastXml.CreateElement("action");
					actionElement.SetAttribute("content", action.Title);
					actionElement.SetAttribute("arguments", 
						 (action.IsTextInput ? "text-input" : "click") + "=" + action.Index);
					actionElement.SetAttribute("activationType", 
						action.IsTextInput ? "foreground" : "background");
					actionsElement.AppendChild(actionElement);
				}
				toastXml.DocumentElement.AppendChild(actionsElement);
			}
			if (notification.Tag == null) {
				notification.Tag = "#" + _toastIdGenerator.GetId(notification, out _);
			} else if (notification.Tag.StartsWith("#")) {
				notification.Tag = "#" + notification.Tag;
			}
			var toast = new ToastNotification(toastXml) {
				Tag = notification.Tag
			};
			_notifications[notification.Tag] = notification;
			_toasts[notification.Tag] = toast;
			toast.Activated += OnActivated;
			toast.Dismissed += OnDismissed;
			_toastNotifier.Show(toast);
		}

		public void DismissNotification(string tag) {
			if (!_toasts.TryGetValue(tag, out var toast)) return;
			if (_notifications.TryGetValue(tag, out var notification)) {
				notification.Dismiss();
				_notifications.Remove(tag);
			}
			_toasts.Remove(tag);
			_toastNotifier.Hide(toast);
		}

		private void OnActivated(ToastNotification sender, object e) {
			var args = (ToastActivatedEventArgs) e;
			if (!_notifications.TryGetValue(sender.Tag, out var notification)) return;
			var parts = args.Arguments.Split('=');
			if (parts.Length == 2) {
				var index = int.Parse(parts[1]);
				string text = null;
				if (parts[0] == "text-input") {
					App.Current.Dispatcher.Invoke(() => {
						text = TextInputDialog.Show(
							notification.AppName,
							notification.Actions.First(action => action.Index == index).Title,
							Resources.Send
						);
					});
				}
				notification.ActivateAction(index, text);
			} else {
				notification.Activate();
			}
		}
		
		private void OnDismissed(ToastNotification sender, ToastDismissedEventArgs e) {
			var found = _notifications.TryGetValue(sender.Tag, out var notification);
			// TODO: When remove notification from our collections?
			//_notifications.Remove(sender.Tag);
			//_toasts.Remove(sender.Tag);
			if (!found) return;
			notification.Dismiss();
		}
		
		private string CreateIconFile(byte[] iconData) {
			var hash = _iconHashFunc.ComputeHash(iconData);
			var directory = Path.GetTempPath() + "NotifySync";
			if (!Directory.Exists(directory)) {
				Directory.CreateDirectory(directory);
			}
			var hashStr = BitConverter.ToString(hash).Replace("-", "").ToLower();
			var fileName = directory + "\\icon_" + hashStr + ".png";
			if (!File.Exists(fileName)) {
				File.WriteAllBytes(fileName, iconData);
			}
			return fileName;
		}
		
		private static void CreateRegistryKey(string appId) {
			using (
				var regKey = Registry.CurrentUser.OpenSubKey(
					"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings",
					true
				)
			) {
				using (var regSubKey = regKey.CreateSubKey(appId)) {
					regSubKey.SetValue("ShowInActionCenter", 1, RegistryValueKind.DWord);
				}
			}
		}

		private void CreateShortcut(string appId, string appName, bool overrideIfExists) {
			var shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + 
			                   "\\Microsoft\\Windows\\Start Menu\\Programs\\" + appName + ".lnk";
			if (!overrideIfExists && File.Exists(shortcutPath)) return;
			var executablePath = Process.GetCurrentProcess().MainModule.FileName;
			
			var shortcut = (IShellLinkW) new CShellLink();
			RequireSuccess(shortcut.SetPath(executablePath));
			RequireSuccess(shortcut.SetArguments(""));

			var shortcutProperties = (IPropertyStore) shortcut;
			using (var applicationId = new PropVariant(appId)) {
				RequireSuccess(shortcutProperties.SetValue(SystemProperties.System.AppUserModel.ID, applicationId));
				RequireSuccess(shortcutProperties.Commit());
			}

			var shortcutFile = (IPersistFile) shortcut;
			RequireSuccess(shortcutFile.Save(shortcutPath, true));
		}

		private void RequireSuccess(UInt32 hResult) {
			if (hResult <= 1) return;
			throw new Exception("Failed with HRESULT: " + hResult.ToString("X"));
		}
	}

	internal enum STGM : long {
		STGM_READ = 0x00000000L,
		STGM_WRITE = 0x00000001L,
		STGM_READWRITE = 0x00000002L,
		STGM_SHARE_DENY_NONE = 0x00000040L,
		STGM_SHARE_DENY_READ = 0x00000030L,
		STGM_SHARE_DENY_WRITE = 0x00000020L,
		STGM_SHARE_EXCLUSIVE = 0x00000010L,
		STGM_PRIORITY = 0x00040000L,
		STGM_CREATE = 0x00001000L,
		STGM_CONVERT = 0x00020000L,
		STGM_FAILIFTHERE = 0x00000000L,
		STGM_DIRECT = 0x00000000L,
		STGM_TRANSACTED = 0x00010000L,
		STGM_NOSCRATCH = 0x00100000L,
		STGM_NOSNAPSHOT = 0x00200000L,
		STGM_SIMPLE = 0x08000000L,
		STGM_DIRECT_SWMR = 0x00400000L,
		STGM_DELETEONRELEASE = 0x04000000L,
	}

	internal static class ShellIIDGuid {
		internal const string IShellLinkW = "000214F9-0000-0000-C000-000000000046";
		internal const string CShellLink = "00021401-0000-0000-C000-000000000046";
		internal const string IPersistFile = "0000010b-0000-0000-C000-000000000046";
		internal const string IPropertyStore = "886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99";
	}

	[ComImport,
	 Guid(ShellIIDGuid.IShellLinkW),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IShellLinkW {
		UInt32 GetPath(
			[Out(), MarshalAs(UnmanagedType.LPWStr)]
			StringBuilder pszFile,
			int cchMaxPath,
			//ref _WIN32_FIND_DATAW pfd,
			IntPtr pfd,
			uint fFlags);

		UInt32 GetIDList(out IntPtr ppidl);
		UInt32 SetIDList(IntPtr pidl);

		UInt32 GetDescription(
			[Out(), MarshalAs(UnmanagedType.LPWStr)]
			StringBuilder pszFile,
			int cchMaxName);

		UInt32 SetDescription(
			[MarshalAs(UnmanagedType.LPWStr)] string pszName);

		UInt32 GetWorkingDirectory(
			[Out(), MarshalAs(UnmanagedType.LPWStr)]
			StringBuilder pszDir,
			int cchMaxPath
		);

		UInt32 SetWorkingDirectory(
			[MarshalAs(UnmanagedType.LPWStr)] string pszDir);

		UInt32 GetArguments(
			[Out(), MarshalAs(UnmanagedType.LPWStr)]
			StringBuilder pszArgs,
			int cchMaxPath);

		UInt32 SetArguments(
			[MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

		UInt32 GetHotKey(out short wHotKey);
		UInt32 SetHotKey(short wHotKey);
		UInt32 GetShowCmd(out uint iShowCmd);
		UInt32 SetShowCmd(uint iShowCmd);

		UInt32 GetIconLocation(
			[Out(), MarshalAs(UnmanagedType.LPWStr)]
			out StringBuilder pszIconPath,
			int cchIconPath,
			out int iIcon);

		UInt32 SetIconLocation(
			[MarshalAs(UnmanagedType.LPWStr)] string pszIconPath,
			int iIcon);

		UInt32 SetRelativePath(
			[MarshalAs(UnmanagedType.LPWStr)] string pszPathRel,
			uint dwReserved);

		UInt32 Resolve(IntPtr hwnd, uint fFlags);

		UInt32 SetPath(
			[MarshalAs(UnmanagedType.LPWStr)] string pszFile);
	}

	[ComImport,
	 Guid(ShellIIDGuid.IPersistFile),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IPersistFile {
		UInt32 GetCurFile(
			[Out(), MarshalAs(UnmanagedType.LPWStr)]
			StringBuilder pszFile
		);

		UInt32 IsDirty();

		UInt32 Load(
			[MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
			[MarshalAs(UnmanagedType.U4)] STGM dwMode);

		UInt32 Save(
			[MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
			bool fRemember);

		UInt32 SaveCompleted(
			[MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
	}

	[ComImport]
	[Guid(ShellIIDGuid.IPropertyStore)]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	interface IPropertyStore {
		UInt32 GetCount([Out] out uint propertyCount);
		UInt32 GetAt([In] uint propertyIndex, out PropertyKey key);
		UInt32 GetValue([In] ref PropertyKey key, [Out] PropVariant pv);
		UInt32 SetValue([In] ref PropertyKey key, [In] PropVariant pv);
		UInt32 Commit();
	}


	[ComImport,
	 Guid(ShellIIDGuid.CShellLink),
	 ClassInterface(ClassInterfaceType.None)]
	internal class CShellLink {
	}
}
