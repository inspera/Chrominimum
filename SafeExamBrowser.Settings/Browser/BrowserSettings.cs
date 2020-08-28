﻿/*
 * Copyright (c) 2020 ETH Zürich, Educational Development and Technology (LET)
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;

namespace SafeExamBrowser.Settings.Browser
{
	/// <summary>
	/// Defines all settings for the integrated browser application.
	/// </summary>
	[Serializable]
	public class BrowserSettings
	{
		/// <summary>
		/// The settings to be used for additional browser windows.
		/// </summary>
		public WindowSettings AdditionalWindow { get; set; }

		/// <summary>
		/// Determines whether the user will be allowed to download configuration files.
		/// </summary>
		public bool AllowConfigurationDownloads { get; set; }

		/// <summary>
		/// Determines whether the user will be allowed to select a custom location when downloading a file (excluding configuration files).
		/// </summary>
		public bool AllowCustomDownloadLocation { get; set; }

		/// <summary>
		/// Determines whether the user will be allowed to download files (excluding configuration files).
		/// </summary>
		public bool AllowDownloads { get; set; }

		/// <summary>
		/// Determines whether the user will be allowed to zoom webpages.
		/// </summary>
		public bool AllowPageZoom { get; set; }

		/// <summary>
		/// Determines whether the internal PDF reader of the browser application is enabled. If not, documents will be downloaded by default.
		/// </summary>
		public bool AllowPdfReader { get; set; }

		/// <summary>
		/// Determines whether the toolbar of the internal PDF reader (which allows to e.g. download or print a document) will be enabled.
		/// </summary>
		public bool AllowPdfReaderToolbar { get; set; }

		/// <summary>
		/// Determines whether spell checking is enabled for input fields.
		/// </summary>
		public bool AllowSpellChecking { get; set; }

		/// <summary>
		/// Determines whether the user will be allowed to upload files.
		/// </summary>
		public bool AllowUploads { get; set; }

		/// <summary>
		/// The configuration key used for integrity checks with server applications (see also <see cref="SendConfigurationKey"/>).
		/// </summary>
		public string ConfigurationKey { get; set; }

		/// <summary>
		/// Determines whether the user needs to confirm the termination of SEB by <see cref="QuitUrl"/>.
		/// </summary>
		public bool ConfirmQuitUrl { get; set; }

		/// <summary>
		/// The custom user agent to optionally be used for all requests.
		/// </summary>
		public string CustomUserAgent { get; set; }

		/// <summary>
		/// Determines whether the entire browser cache is deleted when terminating the application. IMPORTANT: If <see cref="DeleteCookiesOnShutdown"/>
		/// is set to <c>false</c>, the cache will not be deleted in order to keep the cookies for the next session.
		/// </summary>
		public bool DeleteCacheOnShutdown { get; set; }

		/// <summary>
		/// Determines whether all cookies are deleted when terminating the browser application. IMPORTANT: The browser cache will not be deleted
		/// if set to <c>false</c>, even if <see cref="DeleteCacheOnShutdown"/> is set to <c>true</c>!
		/// </summary>
		public bool DeleteCookiesOnShutdown { get; set; }

		/// <summary>
		/// Determines whether all cookies are deleted when starting the browser application.
		/// </summary>
		public bool DeleteCookiesOnStartup { get; set; }

		/// <summary>
		/// Defines a custom directory for file downloads. If not defined, all downloads will be saved in the current user's download directory.
		/// </summary>
		public string DownloadDirectory { get; set; }

		/// <summary>
		/// Determines whether the user is allowed to use the integrated browser application.
		/// </summary>
		public bool EnableBrowser { get; set; }

		/// <summary>
		/// The salt value for the calculation of the exam key which is used for integrity checks with server applications (see also <see cref="SendExamKey"/>).
		/// </summary>
		public byte[] ExamKeySalt { get; set; }

		/// <summary>
		/// The settings to be used for the browser request filter.
		/// </summary>
		public FilterSettings Filter { get; set; }

		/// <summary>
		/// The settings to be used for the main browser window.
		/// </summary>
		public WindowSettings MainWindow { get; set; }

		/// <summary>
		/// Determines how attempts to open a popup are handled.
		/// </summary>
		public PopupPolicy PopupPolicy { get; set; }

		/// <summary>
		/// Determines the proxy settings to be used by the browser.
		/// </summary>
		public ProxySettings Proxy { get; set; }

		/// <summary>
		/// An URL which will initiate the termination of SEB if visited by the user.
		/// </summary>
		public string QuitUrl { get; set; }

		/// <summary>
		/// Determines whether the configuration key header is sent with every HTTP request (see also <see cref="ConfigurationKey"/>).
		/// </summary>
		public bool SendConfigurationKey { get; set; }

		/// <summary>
		/// Determines whether the exam key header is sent with every HTTP request (see also <see cref="ExamKeySalt"/>).
		/// </summary>
		public bool SendExamKey { get; set; }

		/// <summary>
		/// The URL with which the main browser window will be loaded.
		/// </summary>
		public string StartUrl { get; set; }

		/// <summary>
		/// Determines whether a custom user agent will be used for all requests, see <see cref="CustomUserAgent"/>.
		/// </summary>
		public bool UseCustomUserAgent { get; set; }

		/// <summary>
		/// A custom suffix to be appended to the user agent.
		/// </summary>
		public string UserAgentSuffix { get; set; }

		public BrowserSettings()
		{
			AdditionalWindow = new WindowSettings();
			Filter = new FilterSettings();
			MainWindow = new WindowSettings();
			Proxy = new ProxySettings();
		}
	}
}