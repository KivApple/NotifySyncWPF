using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotifySync {
	public class IPCServer {
		private const string NamedPipeName = "NotifySync";
		
		public IPCServer() {
			Task.Run(Serve);
			Task.Run(Serve);
		}

		private async Task<byte[]> ReadBytes(Stream stream, uint count) {
			var buffer = new byte[count];
			await stream.ReadAsync(buffer, 0, buffer.Length);
			return buffer;
		}

		private async Task<uint> ReadUInt(Stream stream) {
			var buffer = await ReadBytes(stream, 4);
			return BitConverter.ToUInt32(buffer, 0);
		}

		private async Task<string> ReadString(Stream stream) {
			var length = await ReadUInt(stream);
			var stringBytes = await ReadBytes(stream, length);
			return Encoding.UTF8.GetString(stringBytes);
		}

		private async Task WriteBytes(Stream stream, byte[] buffer) {
			await stream.WriteAsync(buffer, 0, buffer.Length);
		}

		private async Task WriteUInt(Stream stream, uint value) {
			var bytes = BitConverter.GetBytes(value);
			await WriteBytes(stream, bytes);
		}

		private async Task WriteString(Stream stream, string value) {
			var bytes = Encoding.UTF8.GetBytes(value);
			await WriteUInt(stream, (uint) bytes.Length);
			await WriteBytes(stream, bytes);
		}

		private async Task Serve() {
			try {
				using (var pipe = new NamedPipeServerStream(NamedPipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances)) {
					await pipe.WaitForConnectionAsync();
					Task.Run(Serve);
					var request = await ReadString(pipe);
					switch (request) {
						case "device-list": {
							var devices = App.ProtocolServer.PairedDevices
								.Where(device => device.IsConnected);
							await WriteUInt(pipe, (uint) devices.Count());
							foreach (var device in devices) {
								await WriteString(pipe, device.Id);
								await WriteString(pipe, device.Name);
							}
							break;
						}
						case "send-files": {
							var deviceId = await ReadString(pipe);
							var count = await ReadUInt(pipe);
							var fileNames = new string[count];
							for (var i = 0; i < count; i++) {
								fileNames[i] = await ReadString(pipe);
							}
							var device = App.ProtocolServer.PairedDevices.First(dev => dev.Id == deviceId);
							device.FileSender.SendFiles(fileNames);
							break;
						}
					}	
				}
			} catch (Exception e) {
				App.ShowException(e);
			}
		}
	}
}
