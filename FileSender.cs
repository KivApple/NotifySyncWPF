using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NotifySync {
	public class FileSender {
		private const int SendBufferSize = 20480;
		
		private readonly RemoteDevice _device;
		private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
		private bool _cancel;
		
		public FileSender(RemoteDevice device) {
			_device = device;
		}

		public void CancelSending() {
			_cancel = true;
		}
		
		public void SendFiles(params string[] paths) {
			Task.Run(async () => {
				await _semaphore.WaitAsync();
				try {
					var connection = _device.CurrentConnection;
					_cancel = false;
					if (connection != null) {
						foreach (var path in paths) {
							if (!await DoSendFile(connection, path)) {
								break;
							}
						}
					}
				} catch (Exception e) {
					App.ShowException(e);
				} finally {
					_semaphore.Release();
				}
			});
		}

		private async Task<bool> DoSendFile(RemoteDevice.Connection connection, string path) {
			try {
				using (var fileStream = new FileStream(path, FileMode.Open)) {
					var fileName = Path.GetFileName(path);
					await connection.SendJson(new JObject {
						["type"] = "file",
						["name"] = fileName,
						["size"] = fileStream.Length
					});
					var notification = new SystemNotification {
						Title = fileName,
						Text = string.Format(Properties.Resources.SendingFileTo, _device.Name)
					};
					await App.RunOnUiThreadAsync(() => { App.SystemNotifier.ShowNotification(notification); });
					try {
						var buffer = new byte[SendBufferSize];
						int count;
						do {
							count = await fileStream.ReadAsync(buffer, 0, buffer.Length);
							if (_cancel) {
								throw new CancellationException();
							}
							var base64String = Convert.ToBase64String(buffer, 0, count);
							await connection.SendJson(new JObject {
								["type"] = "file",
								["chunk"] = base64String
							});
						} while (count == buffer.Length);

						await connection.SendJson(new JObject {
							["type"] = "file",
							["status"] = "complete"
						});
					} catch (Exception) {
						try {
							await connection.SendJson(new JObject {
								["type"] = "file",
								["status"] = "cancel"
							});
						}
						catch (Exception) {
							// Ignore
						}

						throw;
					} finally {
						await App.RunOnUiThreadAsync(() => {
							App.SystemNotifier.DismissNotification(notification.Tag);
						});
					}

					await App.RunOnUiThreadAsync(() => {
						App.SystemNotifier.ShowNotification(new SystemNotification {
							Title = fileName,
							Text = string.Format(Properties.Resources.FileSentTo, _device.Name)
						});
					});
				}
			} catch (IOException e) {
				await App.RunOnUiThreadAsync(() => {
					App.SystemNotifier.ShowNotification(new SystemNotification {
						Title = _device.Name,
						Text = string.Format(Properties.Resources.UnableToSendFile, e.Message)
					});
				});
				return false;
			} catch (CancellationException) {
				return false;
			}
			return true;
		}

		private class CancellationException : Exception {
		}
	}
}
