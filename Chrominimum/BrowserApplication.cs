using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Text.RegularExpressions;

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

using Newtonsoft.Json.Linq;
using System.Threading;

namespace Chrominimum
{
	internal class BrowserIconResource : BitmapIconResource
	{
		public BrowserIconResource(string uri = null)
		{
			Uri = new Uri(uri ?? "pack://application:,,,/SafeExamBrowser.UserInterface.Desktop;component/Images/Inspera.ico");
		}
	}

	internal class FiltersObj
	{
		public IList<string> SameWindowUrls { get; set; }
	}

	internal class BrowserApplication : IApplication
	{
		public bool AutoStart { get; private set; }
		public IconResource Icon { get; private set; }
		public Guid Id { get; private set; }
		public string Name { get; private set; }
		public string Tooltip { get; private set; }

		public event WindowsChangedEventHandler WindowsChanged;
		public event TerminationRequestedEventHandler TerminationRequested;

		private List<BrowserApplicationInstance> instances;

		private IModuleLogger logger;
		private BrowserSettings settings;
		private AppSettings appSettings;
		private IText text;
		private IMessageBox messageBox;
		private int instanceIdCounter = default(int);

		private List<Regex> sameWindowRxs;

		internal BrowserApplication(AppSettings appSettings, IMessageBox messageBox, bool mainInstance, IModuleLogger logger, IText text)
		{
			this.appSettings = appSettings;
			this.messageBox = messageBox;
			this.logger = logger;
			this.text = text;
			this.instances = new List<BrowserApplicationInstance>();
			this.sameWindowRxs = new List<Regex>();
			Icon = new BrowserIconResource();

			this.WindowsChanged += Instance_WindowsChanged;

			foreach (var item in appSettings.AllowedUrlRegexps)
			{
				Regex rx = new Regex(item, RegexOptions.Compiled | RegexOptions.IgnoreCase);
				this.sameWindowRxs.Add(rx);
			}
		}

		public IEnumerable<IApplicationWindow> GetWindows()
		{
			return new List<IApplicationWindow>(instances);
		}

		public void Initialize()
		{
		}

		internal void CreateNewInstance(string url = null)
		{
			var id = ++instanceIdCounter;
			var isMainInstance = instances.Count == 0;
			var numWindows = instances.Count;
			var instanceLogger = new ModuleLogger(logger, nameof(MainWindow));
			var startUrl = url ?? appSettings.StartUrl;
			var instance = new BrowserApplicationInstance(appSettings, messageBox, id, isMainInstance, numWindows, startUrl, instanceLogger, text);
			instance.PopupRequested += Instance_PopupRequested;
			instance.TerminationRequested += Instance_TerminationRequested;
			instance.Terminated += Instance_Terminated;

			instance.Activate();
			instances.Add(instance);
			logger.Info($"Created browser instance {instance.Id}.");
			WindowsChanged?.Invoke();
		}

		private void Instance_TerminationRequested()
		{
			logger.Info("Attempting to shutdown as requested by the browser...");
			TerminationRequested?.Invoke();
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
				instance.TerminationRequested += Instance_TerminationRequested;
				instance.Terminated -= Instance_Terminated;
				instance.Terminate();
				logger.Info($"Terminated browser instance {instance.Id}.");
			}

			logger.Info("Terminated browser.");
		}
		private void Instance_PopupRequested(PopupRequestedEventArgs args)
		{
			logger.Info($"Received request to create new instance for '{args.Url}'...");

			Func<string, string, bool> SameUrl = (string curerntUrl, string requestedUrl) => {
				foreach (var rx in this.sameWindowRxs)
				{
					if (rx.IsMatch(curerntUrl) && rx.IsMatch(requestedUrl))
					{
						return true;
					}
				}
				return false;
			};

			foreach (var item in instances)
			{
				if (item.Address == args.Url || SameUrl(item.Address, args.Url))
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
