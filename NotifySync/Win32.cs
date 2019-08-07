using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NotifySync {
	public static class Win32 {
		public const int WM_CHANGECBCHAIN = 0x30D;
		public const int WM_DRAWCLIPBOARD = 0x308;

		[DllImport("user32.dll")]
		public static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

		[DllImport("User32")]
		public static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);
	}
}
