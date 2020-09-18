using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using SafeExamBrowser.WindowsApi.Constants;
using System.ComponentModel;
using SafeExamBrowser.Logging;
using SafeExamBrowser.Logging.Contracts;

namespace Chrominimum
{
	/// <summary>
	/// Defines rectangular bounds, e.g. used for display-related operations.
	/// </summary>
	public interface IBounds
	{
		int Left { get; }
		int Top { get; }
		int Right { get; }
		int Bottom { get; }
	}

	internal class Bounds : IBounds
	{
		public int Left { get; set; }
		public int Top { get; set; }
		public int Right { get; set; }
		public int Bottom { get; set; }
	}

	/// <remarks>
	/// See https://msdn.microsoft.com/en-us/library/windows/desktop/dd162897(v=vs.85).aspx.
	/// </remarks>
	[StructLayout(LayoutKind.Sequential)]
	internal struct RECT
	{
		internal int Left;
		internal int Top;
		internal int Right;
		internal int Bottom;

		internal IBounds ToBounds()
		{
			return new Bounds
			{
				Left = Left,
				Top = Top,
				Right = Right,
				Bottom = Bottom
			};
		}
	}

	/// <summary>
	/// Provides access to the native Windows API exposed by <c>user32.dll</c>.
	/// </summary>
	internal static class User32
	{
		[DllImport("user32.dll", SetLastError = true)]
		internal static extern bool SystemParametersInfo(SPI uiAction, uint uiParam, ref RECT pvParam, SPIF fWinIni);

	}

	class WorkingAreaHandler
	{
		private IBounds originalWorkingArea;
		private ILogger logger;
		public static string GetIdentifierForPrimaryDisplay()
		{
			var display = Screen.PrimaryScreen.DeviceName?.Replace(@"\\.\", string.Empty);
			return $"{display} ({Screen.PrimaryScreen.Bounds.Width}x{Screen.PrimaryScreen.Bounds.Height})";
		}

		internal WorkingAreaHandler(IModuleLogger logger)
		{
			this.logger = logger;
		}

		public static IBounds GetWorkingArea()
		{
			var workingArea = new RECT();
			var success = User32.SystemParametersInfo(SPI.GETWORKAREA, 0, ref workingArea, SPIF.NONE);

			if (!success)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}

			return workingArea.ToBounds();
		}

		internal void InitializeWorkingArea(int taskbarHeight)
		{
			var identifier = WorkingAreaHandler.GetIdentifierForPrimaryDisplay();

			if (originalWorkingArea == null)
			{
				originalWorkingArea = GetWorkingArea();
				LogWorkingArea($"Saved original working area for {identifier}", originalWorkingArea);
			}

			var area = new Bounds
			{
				Left = 0,
				Top = 0,
				Right = Screen.PrimaryScreen.Bounds.Width,
				Bottom = Screen.PrimaryScreen.Bounds.Height - taskbarHeight
			};

			LogWorkingArea($"Trying to set new working area for {identifier}", area);
			SetWorkingArea(area);
			LogWorkingArea($"Working area of {identifier} is now set to", GetWorkingArea());
		}

		internal void ResetWorkingArea()
		{
			var identifier = GetIdentifierForPrimaryDisplay();

			if (originalWorkingArea != null)
			{
				SetWorkingArea(originalWorkingArea);
				LogWorkingArea($"Restored original working area for {identifier}", originalWorkingArea);
			}
			else
			{
				logger.Warn($"Could not restore original working area for {identifier}!");
			}
		}

		public void SetWorkingArea(IBounds bounds)
		{
			var workingArea = new RECT { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom };
			var success = User32.SystemParametersInfo(SPI.SETWORKAREA, 0, ref workingArea, SPIF.NONE);

			if (!success)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}


		private void LogWorkingArea(string message, IBounds area)
		{
			logger.Info($"{message}: Left = {area.Left}, Top = {area.Top}, Right = {area.Right}, Bottom = {area.Bottom}.");
		}
	}

}
