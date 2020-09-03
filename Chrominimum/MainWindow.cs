/*
 * Copyright (c) 2020 ETH Zürich, Educational Development and Technology (LET)
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using Chrominimum.Handlers;
using Chrominimum.Events;

using SafeExamBrowser.Applications.Contracts;
using SafeExamBrowser.Applications.Contracts.Events;
using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.Logging;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.Settings.Browser;
using SafeExamBrowser.Settings.Logging;
using SafeExamBrowser.Applications.Contracts.Resources.Icons;

using ResourceHandler = Chrominimum.Handlers.ResourceHandler;
using BrowserSettings = SafeExamBrowser.Settings.Browser.BrowserSettings;


namespace Chrominimum
{
	internal partial class MainWindow : Form
	{
		private AppSettings appSettings;
		private BrowserSettings settings;
		private ChromiumWebBrowser browser;
		private IModuleLogger logger;
		private IText text;

		private string startUrl;
		private bool mainInstance;

		internal event PopupRequestedEventHandler PopupRequested;

		internal MainWindow(AppSettings appSettings, int id, bool mainInstance, string startUrl, IModuleLogger logger, IText text)
		{
			this.appSettings = appSettings;
			this.logger = logger;
			this.startUrl = startUrl;
			this.text = text;
			this.Id = id;
			this.mainInstance = mainInstance;
			this.settings = new BrowserSettings();

			InitializeComponent();
		}
		internal int Id { get; }

		private const int CP_NOCLOSE_BUTTON = 0x200;

		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams cp = base.CreateParams;
				if (mainInstance)
				{
					cp.ClassStyle |= CP_NOCLOSE_BUTTON;
				}
				return cp;
			}
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			InitializeBrowser();
			InitializeMenu();
			InitializeWindow();
		}

		private void InitializeBrowser()
		{
			var requestLogger = logger.CloneFor($"{nameof(RequestHandler)} #{Id}");
			var resourceHandler = new ResourceHandler(settings, logger, text);
			var requestHandler = new RequestHandler(requestLogger, settings, resourceHandler, text);
			var lifeSpanHandler = new LifeSpanHandler();
			lifeSpanHandler.PopupRequested += LifeSpanHandler_PopupRequested;

			browser = new ChromiumWebBrowser(startUrl)
			{
				Dock = DockStyle.Fill
			};

			browser.DisplayHandler = new DisplayHandler(this);
			browser.KeyboardHandler = new KeyboardHandler();
			browser.LifeSpanHandler = lifeSpanHandler;
			browser.LoadError += Browser_LoadError;
			browser.MenuHandler = new ContextMenuHandler();
			browser.TitleChanged += Browser_TitleChanged;

			browser.RequestHandler = requestHandler;

			Controls.Add(browser);
		}

		private void InitializeMenu()
		{
			if (appSettings.ShowMenu)
			{
				var actions = new MenuItem("Actions");
				var help = new MenuItem("Help");

				Menu = new MainMenu();

				if (appSettings.AllowNavigation)
				{
					actions.MenuItems.Add("Navigate backwards", (o, args) => browser.GetBrowser().GoBack());
					actions.MenuItems.Add("Navigate forwards", (o, args) => browser.GetBrowser().GoForward());
				}

				if (appSettings.AllowReload)
				{
					actions.MenuItems.Add("Reload", (o, args) => browser.GetBrowser().Reload());
				}

				help.MenuItems.Add("Show version", (o, args) => ShowVersion());

				if (appSettings.AllowNavigation || appSettings.AllowReload)
				{
					Menu.MenuItems.Add(actions);
				}

				Menu.MenuItems.Add(help);
			}
		}

		private void InitializeWindow()
		{
			if (appSettings.ShowMaximized && mainInstance)
			{
				WindowState = FormWindowState.Maximized;
			}
			Height = Screen.PrimaryScreen.WorkingArea.Height;
			Width = (int)(Screen.PrimaryScreen.WorkingArea.Width * (mainInstance ? appSettings.MainWindowWidth : appSettings.PopupWindowWidth) / 100.0);

			var side = mainInstance ? appSettings.MainWindowSide : appSettings.PopupWindowSide;
			Location = new Point(Screen.PrimaryScreen.WorkingArea.Left, Screen.PrimaryScreen.WorkingArea.Top);
			if (side == "R")
            {
				Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - Width);
			}
		}

		private void ShowVersion()
		{
			new VersionWindow().ShowDialog(this);
		}

		private void Browser_TitleChanged(object sender, TitleChangedEventArgs e)
		{
			Invoke(new Action(() => Text = e.Title));
		}

		private void Browser_LoadError(object sender, LoadErrorEventArgs e)
		{
			if (e.ErrorCode != CefErrorCode.None && e.ErrorCode != CefErrorCode.Aborted)
			{
				e.Frame.LoadHtml($"<html><body>Failed to load '{e.FailedUrl}'!<br />{e.ErrorText} ({e.ErrorCode})</body></html>");
			}
		}

		private void LifeSpanHandler_PopupRequested(PopupRequestedEventArgs args)
		{
			var validCurrentUri = Uri.TryCreate(browser.Address, UriKind.Absolute, out var currentUri);
			var validNewUri = Uri.TryCreate(args.Url, UriKind.Absolute, out var newUri);
			var sameHost = validCurrentUri && validNewUri && string.Equals(currentUri.Host, newUri.Host, StringComparison.OrdinalIgnoreCase);

			switch (settings.PopupPolicy)
			{
				case PopupPolicy.Allow:
				case PopupPolicy.AllowSameHost when sameHost:
					logger.Debug($"Forwarding request to open new window for '{args.Url}'...");
					PopupRequested?.Invoke(args);
					break;
				case PopupPolicy.AllowSameWindow:
				case PopupPolicy.AllowSameHostAndWindow when sameHost:
					logger.Info($"Discarding request to open new window and loading '{args.Url}' directly...");
					browser.Load(args.Url);
					break;
				case PopupPolicy.AllowSameHost when !sameHost:
				case PopupPolicy.AllowSameHostAndWindow when !sameHost:
					logger.Info($"Blocked request to open new window for '{args.Url}' as it targets a different host.");
					break;
				default:
					logger.Info($"Blocked request to open new window for '{args.Url}'.");
					break;
			}
		}

	}
}
