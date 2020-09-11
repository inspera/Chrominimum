/*
 * Copyright (c) 2020 ETH Zürich, Educational Development and Technology (LET)
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Security.Policy;

namespace Chrominimum
{
	internal class AppSettings
	{
		internal bool AllowReload { get; set; }
		internal bool AllowNavigation { get; set; }
		internal bool ShowMenu { get; set; }
		internal IList<string> StartUrls { get; set; }
		internal string QuitUrl { get; set; }
		internal string LogDir { get; set; }
		internal DateTime StartTime { get; set; }
		internal string QuitPasswordHash { get; set; }
		internal int MainWindowWidth { get; set; }
		internal string MainWindowSide { get; set; }
		internal int PopupWindowWidth { get; set; }
		internal string PopupWindowSide { get; set; }
		internal string UserAgent { get; set; }
		internal string LogFilePrefix { get; set; }
		internal string DownloadDirectory { get; set; }
		internal IList<string> AllowedUrls { get; set; }
		internal IList<string> AllowedUrlRegexps { get; set; }
		internal RectangleF MainWindowGeometry { get; set; }
		internal IList<RectangleF> NewWindowsGeometry { get; set; }

		internal const string AppName = "SEBLight";

		internal void Initialize()
		{
			var appDataLocalFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
			StartTime = DateTime.Now;
			ShowMenu = false;
			AllowNavigation = false;
			AllowReload = false;

			LogDir = Path.Combine(appDataLocalFolder, "Logs");
			LogFilePrefix = Path.Combine(LogDir, StartTime.ToString("yyyy-MM-dd\\_HH\\hmm\\mss\\s"));

			var args = Environment.GetCommandLineArgs();
			if (args.Length != 2)
			{
				throw new ArgumentException("please pass json config file location as an argument");
			}
			var configFileName = args[1];
			if (!File.Exists(configFileName))
			{
				throw new ArgumentException($"cant't find config file: {configFileName}");
			}
			dynamic config = JObject.Parse(File.ReadAllText(configFileName));

			StartUrls = new List<string>();
			foreach (var item in config.startUrls)
			{
				string startUrl = (string)item;
				if (!IsValidUrl(startUrl))
				{
					throw new ArgumentException($"startUrl is not valid: {startUrl}");
				}
				StartUrls.Add(startUrl);
			}

			QuitUrl = config.quitUrl;
			if (!IsValidUrl(QuitUrl))
			{
				throw new ArgumentException($"quitUrl is not valid: {QuitUrl}");
			}

			QuitPasswordHash = config.quitPasswordHash;
			UserAgent = config.userAgent;

			DownloadDirectory = config.downloadDir;
			if (String.IsNullOrEmpty(DownloadDirectory))
			{
				DownloadDirectory = GetTemporaryDirectory();
			}

			AllowedUrls = new List<string>();
			foreach (var item in config.allowedUrls)
			{
				AllowedUrls.Add(((string)item).Replace(@"\\", @"\"));
			}
			AllowedUrlRegexps = new List<string>();
			foreach (var item in config.allowedUrlRegexps)
			{
				AllowedUrlRegexps.Add(((string)item).Replace(@"\\", @"\"));
			}

			MainWindowGeometry = ReadWindowGeometry(config.mainWindow);
			NewWindowsGeometry = new List<RectangleF>();
			foreach (var item in config.newWindows)
			{
				RectangleF rect = ReadWindowGeometry(item);
				NewWindowsGeometry.Add(rect);
			}

		}

		public RectangleF ReadWindowGeometry(dynamic jsonObj)
		{
			RectangleF rect = new RectangleF();
			rect.X = jsonObj.left;
			rect.Y = jsonObj.top;
			rect.Width = jsonObj.width;
			rect.Height = jsonObj.height;
			return rect;
		}

		public string GetTemporaryDirectory()
		{
			string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Directory.CreateDirectory(tempDirectory);
			return tempDirectory;
		}

		private bool IsValidUrl(string value)
		{
			var valid = false;

			valid |= Regex.IsMatch(value, @".+\..+");
			valid |= Regex.IsMatch(value, @".+\..+\..+");
			valid |= Uri.IsWellFormedUriString(value, UriKind.Absolute);

			return valid;
		}
	}
}
