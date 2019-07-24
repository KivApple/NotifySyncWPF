using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Interop;
using NotifySync.Properties;

namespace NotifySync {
	public class FileReceiver {
		private RemoteDevice _remoteDevice;
		private string _currentFilePath;
		private string _currentFileName;
		private long _currentFileSize;
		private long _receivedBytes;
		private FileStream _currentFileStream;
		private SystemNotification _currentNofication;

		public FileReceiver(RemoteDevice remoteDevice) {
			_remoteDevice = remoteDevice;
		}
		
		public async Task HandleJson(dynamic json) {
			if (json.name != null) {
				string fileName = json.name;
				long fileSize = json.size;
				await BeginReceive(fileName, fileSize);
			} else if (json.chunk != null) {
				string chunk = json.chunk;
				await ReceiveChunk(Convert.FromBase64String(chunk));
			} else if (json.status != null) {
				string status = json.status;
				if (status == "complete") {
					await FinishReceive();
				} else {
					await CancelReceive();
				}
			}
		}

		public async Task HandleDisconnect() {
			await CancelReceive();
		}
		
		private async Task BeginReceive(string fileName, long fileSize) {
			var safeFileName = string.Concat(fileName.Where(
				c => c != '/' && c != '\\' && c != ':' && c != '*' && c != '?' && c != '\'' && c != '\"' && c != '>' && 
				     c != '<' && c != '|' && c != '+'
			));
			while (safeFileName.Length > 0) {
				var lastChar = safeFileName[safeFileName.Length - 1];
				if (lastChar == ' ' || lastChar == '.') {
					safeFileName = safeFileName.Substring(0, safeFileName.Length - 1);
				} else {
					break;
				}
			}
			if (safeFileName.Length == 0) {
				safeFileName = "NoName.bin";
			}
			var destinationDirectory = KnownFolders.GetPath(KnownFolder.Downloads);
			if (File.Exists(destinationDirectory + "\\" + safeFileName)) {
				var counter = 1;
				string newFileName;
				do {
					counter++;
					var dot = safeFileName.LastIndexOf('.');
					var name = dot >= 0 ? safeFileName.Substring(0, dot) : safeFileName;
					var ext = dot >= 0 ? safeFileName.Substring(dot) : "";
					newFileName = name + " (" + counter + ")" + ext;
				} while (File.Exists(destinationDirectory + "\\" + newFileName));
				safeFileName = newFileName;
			}
			_currentFilePath = destinationDirectory + "\\" + safeFileName;
			_currentFileStream = new FileStream(_currentFilePath, FileMode.Create);
			_currentFileName = fileName;
			_currentFileSize = fileSize;
			_receivedBytes = 0;
			await NotifyFileBegin();
		}

		private async Task ReceiveChunk(byte[] chunk) {
			if (_currentFileStream == null) return;
			await _currentFileStream.WriteAsync(chunk, 0, chunk.Length);
			_receivedBytes += chunk.Length;
		}

		private async Task FinishReceive() {
			if (_currentFileStream == null) return;
			_currentFileStream.Close();
			_currentFileStream = null;
			await NotifyFileFinished();
			_currentFilePath = null;
			_currentFileName = null;
			_currentFileSize = 0;
			_receivedBytes = 0;
			
		}

		private async Task CancelReceive() {
			if (_currentFileStream == null) return;
			_currentFileStream.Close();
			_currentFileStream = null;
			File.Delete(_currentFilePath);
			await NotifyFileCancelled();
			_currentFilePath = null;
			_currentFileName = null;
			_currentFileSize = 0;
			_receivedBytes = 0;
		}
		
		private async Task NotifyFileBegin() {
			await App.RunOnUiThreadAsync(() => {
				_currentNofication = new SystemNotification {
					Title = _currentFileName,
					Text = string.Format(Resources.ReceivingFileFrom, _remoteDevice.Name)
				};
				App.SystemNotifier.ShowNotification(_currentNofication);
			});
		}

		private async Task NotifyFileFinished() {
			await App.RunOnUiThreadAsync(() => {
				var filePath = _currentFilePath;
				
				App.SystemNotifier.DismissNotification(_currentNofication.Tag);
				_currentNofication = null;
				byte[] fileIconData = null;
				var fileIcon = Icon.ExtractAssociatedIcon(_currentFilePath);
				if (fileIcon != null) {
					var fileIconBitmap = fileIcon.ToBitmap();
					using (var memoryStream = new MemoryStream()) {
						fileIconBitmap.Save(memoryStream, ImageFormat.Png);
						fileIconData = memoryStream.ToArray();
					}
				}
				var notification = new SystemNotification {
					Title = _currentFileName,
					Text = string.Format(Resources.FileReceivedFrom, _remoteDevice.Name),
					IconData = fileIconData,
					Actions = new [] {
						new SystemNotification.Action {
							Index = 1,
							Title = Resources.Open
						},
						new SystemNotification.Action {
							Index = 2,
							Title = Resources.ShowInFolder
						}
					}
				};
				notification.ActionActivated += (_, index, text) => {
					switch (index) {
						case 1:
							Process.Start(filePath);
							break;
						case 2:
							Process.Start("explorer.exe", "/select, \"" + filePath + "\"");
							break;
					}
				};
				App.SystemNotifier.ShowNotification(notification);
			});
		}

		private async Task NotifyFileCancelled() {
			await App.RunOnUiThreadAsync(() => {
				App.SystemNotifier.DismissNotification(_currentNofication.Tag);
				_currentNofication = null;
			});
		}
	}
}
