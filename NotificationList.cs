using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using NotifySync.Properties;

namespace NotifySync {
	public class NotificationList {
		private readonly ObservableCollection<NotificationItem> _notifications = new ObservableCollection<NotificationItem>();
		public ReadOnlyObservableCollection<NotificationItem> Notifications { get; }

		public NotificationList() {
			Notifications = new ReadOnlyObservableCollection<NotificationItem>(_notifications);
		}

		public async Task HandleDisconnect() {
			await Application.Current.Dispatcher.InvokeAsync(() => {
				foreach (var notification in _notifications) {
					if (notification.SystemNotificationTag == null) continue;
					App.SystemNotifier.DismissNotification(notification.SystemNotificationTag);
				}
				_notifications.Clear();
			});
		}

		public async Task HandleJson(RemoteDevice.Connection connection, dynamic json) {
			var device = connection.RemoteDevice;
			string action = json.action;
			switch (action) {
				case "posted": {
					string key = json.key;
					long timestamp = json.timestamp;
					string appName = json.appName;
					string title = json.title;
					string message = json.message;
					string base64Icon = json.icon;
					JArray actions = json.actions;
					var pngIcon = base64Icon != null ? Convert.FromBase64String(base64Icon) : null;
					var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
					dateTime = dateTime.AddMilliseconds(timestamp);
					await Application.Current.Dispatcher.InvokeAsync(() => {
						BitmapImage icon = null;
						if (pngIcon != null) {
							using (var stream = new MemoryStream(pngIcon)) {
								icon = new BitmapImage();
								icon.BeginInit();
								icon.StreamSource = stream;
								icon.CacheOption = BitmapCacheOption.OnLoad;
								icon.EndInit();
							}
						}

						var item = new NotificationItem(key, appName, title, message, icon, dateTime) {
							Actions = actions.Select(act => {
								dynamic a = act;
								int index = a.index;
								string aTitle = a.title;
								bool isTextInput = a.isTextInput;
								return new NotificationItem.Action(index, aTitle, isTextInput);
							}).ToArray()
						};
						int i;
						for (i = 0; i < _notifications.Count; i++) {
							if (_notifications[i].Key != item.Key) continue;
							_notifications[i] = item;
							break;
						}
						if (i == _notifications.Count) {
							_notifications.Add(item);
						}

						var systemNotification = new SystemNotification {
							AppName = item.AppName,
							Title = item.Title,
							Text = item.Message,
							IconData = pngIcon,
							Timestamp = item.Timestamp,
							Actions = item.Actions.Select(act => new SystemNotification.Action {
								Index = act.Index,
								Title = act.Title,
								IsTextInput = act.IsTextInput
							}).ToArray()
						};
						systemNotification.Activated += sender => {
							if (Settings.Default.DismissNotificationsByClick) {
								Task.Run(async () => {
									await connection.SendJson(new JObject {
										["type"] = "notification",
										["key"] = key
									});
								});
							} else {
								App.ShowDeviceWindow(device);
							}
						};
						systemNotification.ActionActivated += (sender, index, text) => {
							Task.Run(async () => {
								await connection.SendJson(new JObject {
									["type"] = "notification", 
									["key"] = key,
									["actionIndex"] = index, 
									["actionText"] = text
								});
							});
						};
						App.SystemNotifier.ShowNotification(systemNotification);
						item.SystemNotificationTag = systemNotification.Tag;
					});
					break;
				}
				case "removed": {
					string key = json.key;
					await Application.Current.Dispatcher.InvokeAsync(() => {
						for (var i = 0; i < _notifications.Count; i++) {
							var notification = _notifications[i];
							if (notification.Key != key) continue;
							_notifications.RemoveAt(i);
							if (notification.SystemNotificationTag != null) {
								App.SystemNotifier.DismissNotification(notification.SystemNotificationTag);
							}
							break;
						}
					});
					break;
				}
			}
		}
	}

	public class NotificationItem {
		public string Key { get; }
		public string AppName { get; }
		public string Title { get; }
		public string Message { get; }
		public BitmapImage Icon { get; }
		public DateTime Timestamp { get; }
		public Action[] Actions;
		internal string SystemNotificationTag;
		
		public NotificationItem(string key, string appName, string title, string message, BitmapImage icon, DateTime timestamp) {
			Key = key;
			AppName = appName;
			Title = title;
			Message = message;
			Icon = icon;
			Timestamp = timestamp;
		}

		public class Action {
			public int Index { get; }
			public string Title { get; }
			public bool IsTextInput { get; }

			public Action(int index, string title, bool isTextInput) {
				Index = index;
				Title = title;
				IsTextInput = isTextInput;
			}
		}
	}
}
