﻿/*
 * Copyright (c) 2020 ETH Zürich, Educational Development and Technology (LET)
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.IO;
using System.Windows.Forms;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using CommandLine;

namespace Chrominimum
{
	public class Options
	{
		[Option("allow-navigation", Default=false, Required = false)]
		public bool AllowNavigation { get; set; }

		[Option("disable-reload", Default=false, Required = false)]
		public bool DisableReload { get; set; }

		[Option("maximized", Default=true, Required = false)]
		public bool ShowMaximized { get; set; }

		[Option("logdir", Required = false)]
		public string LogDir { get; set; }

		[Option("quitPasswordHash", Required = false)]
		public string QuitPasswordHash { get; set; }

		[Option("layout", Default="L80:R50", Required = false)]
		public string Layout { get; set; }

		[Option("useragent-suffix", Required = false)]
		public string UserAgentSuffix { get; set; }

		[Option("filters", Required = false)]
		public string FiltersFileName { get; set; }

		[Option("quit-url", Required = false)]
		public string QuitUrl { get; set; }

		[Option("download-dir", Required = false)]
		public string DownloadDirectory { get; set; }

		[Value(0)]
		public string ProgramName { get; set; }

		[Value(1)]
		public string StartUrl { get; set; }
	}

	internal class AppSettings
	{
		internal bool AllowReload { get; set; }
		internal bool AllowNavigation { get; set; }
		internal bool ShowMaximized { get; set; }
		internal bool ShowMenu { get; set; }
		internal string StartUrl { get; set; }
		internal string QuitUrl { get; set; }
		internal string LogDir { get; set; }
		internal DateTime StartTime { get; set; }
		internal string QuitPasswordHash { get; set; }
		internal int MainWindowWidth { get; set; }
		internal string MainWindowSide { get; set; }
		internal int PopupWindowWidth { get; set; }
		internal string PopupWindowSide { get; set; }
		internal string UserAgentSuffix { get; set; }
		internal string LogFilePrefix { get; set; }
		internal string FiltersJsonLine { get; set; }
		internal string DownloadDirectory { get; set; }

		internal const string AppName = "SEBLight";

		internal void Initialize()
		{
			var appDataLocalFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
			StartTime = DateTime.Now;

			var result = Parser.Default.ParseArguments<Options>(Environment.GetCommandLineArgs())
				.WithParsed(options => {
					AllowNavigation = options.AllowNavigation;
					AllowReload = !options.DisableReload;
					ShowMenu = false;
					ShowMaximized = options.ShowMaximized;
					StartUrl = !String.IsNullOrEmpty(options.StartUrl) && IsValidStartUrl(options.StartUrl)
						? options.StartUrl
						: ConfigurationManager.AppSettings["StartUrl"];
					QuitUrl = options.QuitUrl;
					LogDir = !String.IsNullOrEmpty(options.LogDir)
						? options.LogDir
						: Path.Combine(appDataLocalFolder, "Logs");
					QuitPasswordHash = options.QuitPasswordHash;
					ParseLayout(options.Layout);
					UserAgentSuffix = options.UserAgentSuffix;
					LogFilePrefix = Path.Combine(LogDir, StartTime.ToString("yyyy-MM-dd\\_HH\\hmm\\mss\\s"));
					if (!String.IsNullOrEmpty(options.FiltersFileName))
					{
						if (!File.Exists(options.FiltersFileName))
						{
							throw new ArgumentException("cant't find filters file: " + options.FiltersFileName);
						}
						FiltersJsonLine = File.ReadAllText(options.FiltersFileName);
					}
					DownloadDirectory = !String.IsNullOrEmpty(options.DownloadDirectory)
						? options.DownloadDirectory
						: GetTemporaryDirectory();
				});

			var args = Environment.GetCommandLineArgs();
		}

		public string GetTemporaryDirectory()
		{
			string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Directory.CreateDirectory(tempDirectory);
			return tempDirectory;
		}

		private void ParseLayout(string rawValue)
		{
			string[] blocks = rawValue.Split(':');
			if (blocks.Length != 2)
			{
				throw new ArgumentException("wrong layout: " + rawValue);
			}
			ParseOneLayoutBlock(blocks[0], value => MainWindowSide = value, value => MainWindowWidth = value);
			ParseOneLayoutBlock(blocks[1], value => PopupWindowSide = value, value => PopupWindowWidth = value);
		}

		private void ParseOneLayoutBlock(string rawValue, Action<string> setSide, Action<int> setWidth)
		{
			if(rawValue[0] != 'L' && rawValue[0] != 'R')
			{
				throw new ArgumentException("wrong layout: " + rawValue);
			}
			setSide(rawValue[0].ToString());
			setWidth(int.Parse(rawValue.Substring(1)));
		}

		private bool IsValidStartUrl(string value)
		{
			var valid = false;

			valid |= Regex.IsMatch(value, @".+\..+");
			valid |= Regex.IsMatch(value, @".+\..+\..+");
			valid |= Uri.IsWellFormedUriString(value, UriKind.Absolute);

			return valid;
		}
	}
}
