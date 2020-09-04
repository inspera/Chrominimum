using System;
using System.Linq;
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
	internal class BrowserIconResource : BitmapIconResource
	{
		public BrowserIconResource(string uri = null)
		{
			Uri = new Uri(uri ?? "pack://application:,,,/SafeExamBrowser.UserInterface.Desktop;component/Images/Inspera.ico");
		}
	}

	internal class BrowserApplication : IApplication
	{
		public bool AutoStart { get; private set; }
		public IconResource Icon { get; private set; }
		public Guid Id { get; private set; }
		public string Name { get; private set; }
		public string Tooltip { get; private set; }

		public event WindowsChangedEventHandler WindowsChanged;

		private List<BrowserApplicationInstance> instances;

		private IModuleLogger logger;
		private BrowserSettings settings;
		private AppSettings appSettings;
		private IText text;
		private int instanceIdCounter = default(int);

		internal BrowserApplication(AppSettings appSettings, bool mainInstance, IModuleLogger logger, IText text)
		{
			this.appSettings = appSettings;
			this.logger = logger;
			this.text = text;
			this.instances = new List<BrowserApplicationInstance>();
			Icon = new BrowserIconResource();

			this.WindowsChanged += Instance_WindowsChanged;
		}

		public IEnumerable<IApplicationWindow> GetWindows()
		{
			return new List<IApplicationWindow>(instances);
		}
		public void Initialize()
		{
			logger.Info("Starting initialization...");

			var cefSettings = GenerateCefSettings();
			var success = Cef.Initialize(cefSettings, true, default(IApp));

			if (!success)
			{
				logger.Error("Failed to initialize browser!");
				throw new Exception("Failed to initialize browser!");
			}

			logger.Info("Initialized browser.");
		}

		private CefSettings GenerateCefSettings()
		{
			var warning = logger.LogLevel == LogLevel.Warning;
			var error = logger.LogLevel == LogLevel.Error;
			var cefSettings = new CefSettings();

			cefSettings.CefCommandLineArgs.Add("enable-media-stream");

			cefSettings.LogFile = $"{appSettings.LogFilePrefix}_Browser.log";
			cefSettings.LogSeverity = error ? LogSeverity.Error : (warning ? LogSeverity.Warning : LogSeverity.Info);
			cefSettings.UserAgent = GenerateUserAgent();

			logger.Debug($"UserAgent: {cefSettings.UserAgent}");
			logger.Debug($"Cache Path: {cefSettings.CachePath}");
			logger.Debug($"Engine Version: Chromium {Cef.ChromiumVersion}, CEF {Cef.CefVersion}, CefSharp {Cef.CefSharpVersion}");
			logger.Debug($"Log File: {cefSettings.LogFile}");
			logger.Debug($"Log Severity: {cefSettings.LogSeverity}.");

			return cefSettings;
		}

		internal void CreateNewInstance(string url = null)
		{
			var id = ++instanceIdCounter;
			var isMainInstance = instances.Count == 0;
			var instanceLogger = new ModuleLogger(logger, nameof(MainWindow));
			var startUrl = url ?? appSettings.StartUrl;
			var instance = new BrowserApplicationInstance(appSettings, id, isMainInstance, startUrl, instanceLogger, text);
			instance.PopupRequested += Instance_PopupRequested;
			instance.Terminated += Instance_Terminated;

			instance.Activate();
			instances.Add(instance);
			logger.Info($"Created browser instance {instance.Id}.");
			WindowsChanged?.Invoke();
		}

		private string GenerateUserAgent()
		{
			var osVersion = $"{Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}; Win64; x64";
			var userAgent = $"Mozilla/5.0 (Windows NT {osVersion}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{Cef.ChromiumVersion} Safari/537.36";

			if (!string.IsNullOrWhiteSpace(appSettings.UserAgentSuffix))
			{
				userAgent = $"{userAgent} {appSettings.UserAgentSuffix}";
			}

			return userAgent;
		}

		public void Start()
		{
			CreateNewInstance();
		}

		public void Terminate()
		{
			logger.Info("Initiating termination...");

			foreach (var instance in instances)
			{
				instance.Terminated -= Instance_Terminated;
				instance.Terminate();
				logger.Info($"Terminated browser instance {instance.Id}.");
			}

			Cef.Shutdown();
			logger.Info("Terminated browser.");
		}
		private void Instance_PopupRequested(PopupRequestedEventArgs args)
		{
			logger.Info($"Received request to create new instance for '{args.Url}'...");

			foreach(var item in instances)
			{
				if (item.Address == args.Url)
				{
					logger.Info($"Address is already openned in the instance '{item.Id}'. Activating it...");
					item.Activate();
					return;
				}
			}
			CreateNewInstance(args.Url);
		}

		private void Instance_Terminated(int id)
		{
			instances.Remove(instances.First(i => i.Id == id));
			WindowsChanged?.Invoke();
		}

		private void Instance_WindowsChanged()
		{
			logger.Info($"Instance_WindowsChanged");
		}
	}

}
