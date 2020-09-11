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
using SafeExamBrowser.UserInterface.Contracts.MessageBox;

using ResourceHandler = Chrominimum.Handlers.ResourceHandler;
using BrowserSettings = SafeExamBrowser.Settings.Browser.BrowserSettings;

namespace Chrominimum
{
	class BrowserApplicationInstance : IApplicationWindow
	{
		private AppSettings appSettings;
		private BrowserSettings settings;
		private bool isMainInstance;
		private IModuleLogger logger;
		private string startUrl;
		private IText text;
		private IMessageBox messageBox;
		private MainWindow window;

		internal event PopupRequestedEventHandler PopupRequested;
		internal event InstanceTerminatedEventHandler Terminated;
		internal event TerminationRequestedEventHandler TerminationRequested;

		public event IconChangedEventHandler IconChanged;
		public event TitleChangedEventHandler TitleChanged;

		internal int Id { get; }
		public IntPtr Handle { get; private set; }
		public IconResource Icon { get; private set; }
		public string Title { get; private set; }

		public BrowserApplicationInstance(
			AppSettings appSettings,
			IMessageBox messageBox,
			int id,
			bool isMainInstance,
			int numWindows,
			string startUrl,
			IModuleLogger logger,
			IText text)
		{
			this.appSettings = appSettings;
			this.messageBox = messageBox;
			this.Id = id;
			this.isMainInstance = isMainInstance;
			this.logger = logger;
			this.text = text;
			this.startUrl = startUrl;
			this.settings = new BrowserSettings();
			settings.QuitUrl = appSettings.QuitUrl;
			settings.DownloadDirectory = appSettings.DownloadDirectory;
			settings.AllowDownloads = true;
			settings.MainWindow.AllowReloading = appSettings.AllowReload;
			settings.AdditionalWindow.AllowReloading = appSettings.AllowReload;

			var instanceLogger = new ModuleLogger(logger, nameof(MainWindow));
			window = new MainWindow(appSettings, settings, messageBox, id, isMainInstance, numWindows, startUrl, instanceLogger, text);
			window.TerminationRequested += () => TerminationRequested?.Invoke();
			window.IconChanged += (i) => IconChanged?.Invoke(i);

			Handle = window.Handle;
			Icon = new BrowserIconResource();

			window.Closing += Control_Closing;
			window.TitleChanged += Control_TitleChanged;
			window.PopupRequested += Control_PopupRequested;
		}

		public string Address
		{
			get
			{
				return window.Address;
			}
		}

		public void Activate()
		{
			window.Show();
			window.BringToFront();
		}

		internal void Terminate()
		{
			logger.Info($"Instance has terminated.");
			window.Close();
			Terminated?.Invoke(Id);
		}
		private void Control_TitleChanged(string title)
		{
			Title = title;
			window.Text = title;
			TitleChanged?.Invoke(Title);
		}

		private void Control_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			window.Closing -= Control_Closing;
			Terminate();
		}

		private void Control_PopupRequested(PopupRequestedEventArgs args)
		{
			PopupRequested?.Invoke(args);
		}
	}
}
