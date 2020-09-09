/*
 * Copyright (c) 2020 ETH Zürich, Educational Development and Technology (LET)
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace SafeExamBrowser.UserInterface.Shared.Utilities
{
	public static class Clipboard
	{
		private static class Native
		{

			[DllImport("user32.dll", SetLastError = true)]
			internal static extern bool CloseClipboard();

			[DllImport("user32.dll", SetLastError = true)]
			internal static extern bool EmptyClipboard();

			[DllImport("user32.dll", SetLastError = true)]
			internal static extern bool OpenClipboard(IntPtr hWndNewOwner);
		}

		public static void EmptyClipboard()
		{
			var success = true;

			success &= Native.OpenClipboard(IntPtr.Zero);
			success &= Native.EmptyClipboard();
			success &= Native.CloseClipboard();

			if (!success)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}
	}
}
