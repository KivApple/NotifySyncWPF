using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using DesktopNotifications;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using MS.WindowsAPICodePack.Internal;
using NotifySync.Properties;

namespace NotifySync {
	public class UwpToastNotifier : ISystemNotifier {
		private const string AppId = "NotifySync";
		
		private static UwpToastNotifier Instance { get; set; }
		private readonly string _appName = Resources.AppName;
		private readonly ToastNotifier _toastNotifier;
		private readonly Dictionary<string, ToastNotification> _toasts = new Dictionary<string, ToastNotification>();
		private readonly Dictionary<string, SystemNotification> _notifications = new Dictionary<string, SystemNotification>();
		private readonly SHA512 _iconHashFunc = new SHA512CryptoServiceProvider();
		private readonly ObjectIDGenerator _toastIdGenerator = new ObjectIDGenerator();
		
		public UwpToastNotifier() {
			Instance = this;
			CreateShortcut<MyNotificationActivator>(AppId, _appName, true);
			DesktopNotificationManagerCompat.RegisterAumidAndComServer<MyNotificationActivator>(AppId);
			DesktopNotificationManagerCompat.RegisterActivator<MyNotificationActivator>();
			_toastNotifier = DesktopNotificationManagerCompat.CreateToastNotifier();
			DesktopNotificationManagerCompat.History.Clear();
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
			if (notification.Tag == null) {
				notification.Tag = "#" + _toastIdGenerator.GetId(notification, out _);
			}
			if (notification.Actions.Length > 0) {
				var actionsElement = toastXml.CreateElement("actions");
				foreach (var action in notification.Actions) {
					var actionElement = toastXml.CreateElement("action");
					if (action.IsTextInput) {
						var inputElement = toastXml.CreateElement("input");
						inputElement.SetAttribute("id", "input" + action.Index);
						inputElement.SetAttribute("type", "text");
						inputElement.SetAttribute("placeHolderContent", action.Title);
						actionsElement.AppendChild(inputElement);
						
						actionElement.SetAttribute("hint-inputId", "input" + action.Index);
						actionElement.SetAttribute("content", Resources.Send);
					} else { 
						actionElement.SetAttribute("content", action.Title);
					}
					var arguments = $"tag={Uri.EscapeDataString(notification.Tag)}&{(action.IsTextInput ? "text-input" : "click")}={action.Index}";
					actionElement.SetAttribute("arguments", arguments);
					actionElement.SetAttribute("activationType", "background");
					actionsElement.AppendChild(actionElement);
				}
				toastXml.DocumentElement.AppendChild(actionsElement);
			}
			var toast = new ToastNotification(toastXml) {
				Tag = notification.Tag
			};
			toast.Dismissed += OnDismissed;
			if (_toasts.TryGetValue(notification.Tag, out var oldToast)) {
				_toastNotifier.Hide(oldToast);
			}
			_toasts[notification.Tag] = toast;
			_notifications[notification.Tag] = notification;
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

		private void HandleNotificationActivated(string invokedArgs, NotificationUserInput userInput) {
			var args = new Dictionary<string, string>();
			foreach (var arg in invokedArgs.Split('&')) {
				var parts = arg.Split(new [] {'='}, 2);
				var key = Uri.UnescapeDataString(parts[0]);
				var value = Uri.UnescapeDataString(parts[1]);
				args[key] = value;
			}

			if (!args.TryGetValue("tag", out var tag)) return;
			if (!_notifications.TryGetValue(tag, out var notification)) return;
			
			if (args.TryGetValue("click", out var clickIndexStr)) {
				notification.ActivateAction(int.Parse(clickIndexStr), null);
			} else if (args.TryGetValue("text-input", out var inputIndexStr)) {
				var inputIndex = int.Parse(inputIndexStr);
				var inputText = userInput["input" + inputIndex];
				notification.ActivateAction(inputIndex, inputText);
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
		
		private void CreateShortcut<T>(string appId, string appName, bool overrideIfExists)
			where T: NotificationActivator
		{
			var shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + 
			                   "\\Microsoft\\Windows\\Start Menu\\Programs\\" + appName + ".lnk";
			if (!overrideIfExists && File.Exists(shortcutPath)) return;
			// ReSharper disable once PossibleNullReferenceException
			var executablePath = Process.GetCurrentProcess().MainModule.FileName;
			var toastActivatorClsidStr = $"{{{typeof(T).GUID.ToString().ToUpper()}}}";
			
			// ReSharper disable once SuspiciousTypeConversion.Global
			var shortcut = (IShellLinkW) new CShellLink();
			RequireSuccess(shortcut.SetPath(executablePath));
			RequireSuccess(shortcut.SetArguments(""));

			// ReSharper disable once SuspiciousTypeConversion.Global
			var shortcutProperties = (IPropertyStore) shortcut;
			using (var applicationId = new PropVariant(appId))
			using (var toastActivatorClsid = new PropVariant(toastActivatorClsidStr)) {
				RequireSuccess(shortcutProperties.SetValue(SystemProperties.System.AppUserModel.ID, applicationId));
				RequireSuccess(shortcutProperties.SetValue(SystemAppUserModelToastActivatorClsid, toastActivatorClsid));
				RequireSuccess(shortcutProperties.Commit());
			}
			
			// ReSharper disable once SuspiciousTypeConversion.Global
			var shortcutFile = (IPersistFile) shortcut;
			RequireSuccess(shortcutFile.Save(shortcutPath, true));
		}

		private static void RequireSuccess(UInt32 hResult) {
			if (hResult <= 1) return;
			throw new Exception("Failed with HRESULT: " + hResult.ToString("X"));
		}

		// System.AppUserModel.ToastActivatorCLSID
		private static PropertyKey SystemAppUserModelToastActivatorClsid => 
			new PropertyKey(new Guid("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}"), 26);
		
		[ClassInterface(ClassInterfaceType.None)]
		[ComSourceInterfaces(typeof(INotificationActivationCallback))]
		[Guid("DC92FD25-8C9F-4824-9A1A-B4E88BD2A1F9"), ComVisible(true)]
		public class MyNotificationActivator: NotificationActivator {
			public override void OnActivated(string invokedArgs, NotificationUserInput userInput, string appUserModelId) {
				Application.Current.Dispatcher.Invoke(() => {
					Instance?.HandleNotificationActivated(invokedArgs, userInput);
				});
			}
		}
	}
}
