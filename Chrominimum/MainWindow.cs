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
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using CefSharp;
using CefSharp.WinForms;
using Chrominimum.Handlers;
using Chrominimum.Events;
using Chrominimum.Filters;

using SafeExamBrowser.Applications.Contracts;
using SafeExamBrowser.Applications.Contracts.Events;
using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.Logging;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.Settings.Browser;
using SafeExamBrowser.Browser.Contracts.Filters;
using SafeExamBrowser.Settings.Browser.Filter;
using SafeExamBrowser.Settings.Logging;
using SafeExamBrowser.Applications.Contracts.Resources.Icons;
using SafeExamBrowser.UserInterface.Contracts.Windows;
using SafeExamBrowser.UserInterface.Contracts.Windows.Events;
using SafeExamBrowser.UserInterface.Contracts.MessageBox;
using SafeExamBrowser.UserInterface.Contracts.Browser.Data;

using SebMessageBox = SafeExamBrowser.UserInterface.Contracts.MessageBox;
using ResourceHandler = Chrominimum.Handlers.ResourceHandler;
using BrowserSettings = SafeExamBrowser.Settings.Browser.BrowserSettings;

using Newtonsoft.Json.Linq;
using Request = SafeExamBrowser.Browser.Contracts.Filters.Request;

namespace Chrominimum
{
	internal partial class MainWindow : Form, IWindow
	{
		private AppSettings appSettings;
		private BrowserSettings settings;
		private ChromiumWebBrowser browser;
		private IModuleLogger logger;
		private IText text;
		private IMessageBox messageBox;

		private string startUrl;
		private bool mainInstance;

		internal event PopupRequestedEventHandler PopupRequested;
		internal event TerminationRequestedEventHandler TerminationRequested;
		public event IconChangedEventHandler IconChanged;
		public event TitleChangedEventHandler TitleChanged;
		private WindowClosingEventHandler closing;

		event WindowClosingEventHandler IWindow.Closing
		{
			add { closing += value; }
			remove { closing -= value; }
		}

		internal MainWindow(AppSettings appSettings, BrowserSettings settings, IMessageBox messageBox, int id, bool mainInstance, string startUrl, IModuleLogger logger, IText text)
		{
			this.appSettings = appSettings;
			this.messageBox = messageBox;
			this.logger = logger;
			this.startUrl = startUrl;
			this.text = text;
			this.Id = id;
			this.mainInstance = mainInstance;
			this.settings = settings;

			InitializeComponent();
		}
		internal int Id { get; }

		internal string Address
		{
			get
			{
				return browser.Address;
			}
		}

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
			var requestFilter = new RequestFilter();
			var resourceHandler = new ResourceHandler(settings, requestFilter, logger, text);
			var requestHandler = new RequestHandler(requestLogger, settings, requestFilter, resourceHandler, text);
			requestHandler.QuitUrlVisited += RequestHandler_QuitUrlVisited;

			var lifeSpanHandler = new LifeSpanHandler();
			lifeSpanHandler.PopupRequested += LifeSpanHandler_PopupRequested;

			var downloadLogger = logger.CloneFor($"{nameof(DownloadHandler)} #{Id}");
			var downloadHandler = new DownloadHandler(settings, downloadLogger);
			downloadHandler.DownloadUpdated += DownloadHandler_DownloadUpdated;

			InitializeRequestFilter(requestFilter);

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
			browser.DownloadHandler = downloadHandler;

			Controls.Add(browser);
		}

		private void AppendFilters(IRequestFilter requestFilter, dynamic filters, FilterRuleType type, FilterResult result)
		{
			if (Object.ReferenceEquals(null, filters))
			{
				return;
			}

			var factory = new RuleFactory();
			foreach (var item in filters)
			{
				// workaround: json parser is not happy about '\.' so \ is esacaped and we should restore it back here
				var re = ((string)item).Replace(@"\\", @"\");
				var rule = factory.CreateRule(type);
				rule.Initialize(new FilterRuleSettings { Expression = re, Result = result });
				requestFilter.Load(rule);
			}
		}

		private void InitializeRequestFilter(IRequestFilter requestFilter)
		{
			var factory = new RuleFactory();

			if (!String.IsNullOrEmpty(appSettings.FiltersJsonLine))
			{
				dynamic filters = JObject.Parse(appSettings.FiltersJsonLine);
				AppendFilters(requestFilter, filters["allow"], FilterRuleType.Regex, FilterResult.Allow);
				AppendFilters(requestFilter, filters["block"], FilterRuleType.Regex, FilterResult.Block);
			}

			if (requestFilter.Process(new Request { Url = startUrl }) != FilterResult.Allow)
			{
				var rule = factory.CreateRule(FilterRuleType.Simplified);
				rule.Initialize(new FilterRuleSettings { Expression = startUrl, Result = FilterResult.Allow });
				requestFilter.Load(rule);
				logger.Debug($"Automatically created filter rule to allow start URL '{startUrl}'.");
			}
		}

		private void DownloadHandler_DownloadUpdated(DownloadItemState state)
		{
			// No downloading UI requested.
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
			TitleChanged?.Invoke(e.Title);
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

		internal void Terminate()
		{
			if (browser.IsDisposed)
			{
				browser.LoadError -= Browser_LoadError;
				browser.TitleChanged -= Browser_TitleChanged;
				browser.Dispose();
			}
			TerminationRequested?.Invoke();
		}
		private void RequestHandler_QuitUrlVisited(string url)
		{
			if (settings.ConfirmQuitUrl)
			{
				var message = text.Get(TextKey.MessageBox_BrowserQuitUrlConfirmation);
				var title = text.Get(TextKey.MessageBox_BrowserQuitUrlConfirmationTitle);
				var result = messageBox.Show(message, title, MessageBoxAction.YesNo, SebMessageBox.MessageBoxIcon.Question, this);
				var terminate = result == MessageBoxResult.Yes;

				if (terminate)
				{
					logger.Info($"User confirmed termination via quit URL '{url}', forwarding request...");
					Terminate();
				}
				else
				{
					logger.Info($"User aborted termination via quit URL '{url}'.");
				}
			}
			else
			{
				logger.Info($"Automatically requesting termination due to quit URL '{url}'...");
				Terminate();
			}
		}

		public void BringToForeground()
		{
			Dispatcher.CurrentDispatcher.Invoke(() =>
			{
				if (WindowState == FormWindowState.Maximized)
				{
					WindowState = FormWindowState.Normal;
				}

				Activate();
			});
		}

	}
}
