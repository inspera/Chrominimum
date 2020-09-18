/*
 * Copyright (c) 2020 ETH Zürich, Educational Development and Technology (LET)
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using CefSharp;
using CefSharp.WinForms;

using SafeExamBrowser.I18n;
using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.Logging;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.UserInterface.Contracts;
using SafeExamBrowser.UserInterface.Contracts.Shell;
using SafeExamBrowser.UserInterface.Desktop;
using SafeExamBrowser.Settings.Logging;

using SebMessageBox = SafeExamBrowser.UserInterface.Contracts.MessageBox;
using SebUIUtilities = SafeExamBrowser.UserInterface.Shared.Utilities;

using SafeExamBrowser.Settings.SystemComponents;
using SafeExamBrowser.SystemComponents.Audio;
using SafeExamBrowser.SystemComponents.Keyboard;
using SafeExamBrowser.SystemComponents.PowerSupply;
using SafeExamBrowser.SystemComponents.WirelessNetwork;

using Chrominimum.Events;
using System.Windows.Threading;

namespace Chrominimum
{

	internal class SEBContext : ApplicationContext
	{
		private BrowserApplication browser;
		private WorkingAreaHandler workingAreaHandler;
		private ILogger logger;
		private IText text;
		private AppSettings appSettings;

		private IUserInterfaceFactory uiFactory;
		private ITaskbar taskbar;
		private ITaskview taskview;
		private SebMessageBox.IMessageBox messageBox;
		private HashAlgorithm hashAlgorithm;

		private readonly Dispatcher _dispatcher;

		internal SEBContext(AppSettings settings)
		{
			appSettings = settings;
			logger = new Logger();
			hashAlgorithm = new HashAlgorithm();

			InitializeLogging();
			InitializeText();

			_dispatcher = Dispatcher.CurrentDispatcher;

			uiFactory = new UserInterfaceFactory(text);
			messageBox = new MessageBoxFactory(text);

			taskbar = uiFactory.CreateTaskbar(logger);
			taskbar.QuitButtonClicked += Shell_QuitButtonClicked;
			taskbar.Show();

			workingAreaHandler = new WorkingAreaHandler(new ModuleLogger(logger, nameof(WorkingAreaHandler)));
			workingAreaHandler.InitializeWorkingArea(taskbar.GetAbsoluteHeight());

			taskview = uiFactory.CreateTaskview();

			var audioSettings = new AudioSettings();
			var audio = new Audio(audioSettings, new ModuleLogger(logger, nameof(Audio)));
			audio.Initialize();
			taskbar.AddSystemControl(uiFactory.CreateAudioControl(audio, Location.Taskbar));

			var keyboard = new Keyboard(new ModuleLogger(logger, nameof(Keyboard)));
			keyboard.Initialize();
			taskbar.AddSystemControl(uiFactory.CreateKeyboardLayoutControl(keyboard, Location.Taskbar));

			var powerSupply = new PowerSupply(new ModuleLogger(logger, nameof(PowerSupply)));
			powerSupply.Initialize();
			taskbar.AddSystemControl(uiFactory.CreatePowerSupplyControl(powerSupply, Location.Taskbar));

			var wirelessAdapter = new WirelessAdapter(new ModuleLogger(logger, nameof(WirelessAdapter)));
			wirelessAdapter.Initialize();
			taskbar.AddSystemControl(uiFactory.CreateWirelessNetworkControl(wirelessAdapter, Location.Taskbar));

			browser = new BrowserApplication(appSettings, messageBox, true, new ModuleLogger(logger, nameof(BrowserApplication)), text);
			taskbar.AddApplicationControl(uiFactory.CreateApplicationControl(browser, Location.Taskbar), true);
			browser.TerminationRequested += () =>
			{
				Browser_TerminationRequested();
			};

			taskview.Add(browser);
			InitializeCef();
			foreach(string startUrl in appSettings.StartUrls)
			{
				browser.CreateNewInstance(startUrl);
			}
		}

		private void InitializeCef()
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


		private void ClosingSeqence()
		{
			workingAreaHandler.ResetWorkingArea();
			browser.Terminate();
			ExitThread();
		}

		private void Browser_TerminationRequested()
		{
			if (!_dispatcher.CheckAccess())
			{
				_dispatcher.Invoke(() =>
				{
					ClosingSeqence();
				});
			} else {
				ClosingSeqence();
			}
		}

		private void Shell_QuitButtonClicked(System.ComponentModel.CancelEventArgs args)
		{
			args.Cancel = !TryInitiateShutdown();
		}

		private bool TryInitiateShutdown()
		{
			var hasQuitPassword = !string.IsNullOrEmpty(appSettings.QuitPasswordHash);
			var requestShutdown = false;

			if (hasQuitPassword)
			{
				requestShutdown = TryValidateQuitPassword();
			}
			else
			{
				requestShutdown = TryConfirmShutdown();
			}

			if (requestShutdown)
			{
				ClosingSeqence();
			}

			return false;
		}

		private bool TryConfirmShutdown()
		{
			var result = messageBox.Show(TextKey.MessageBox_Quit, TextKey.MessageBox_QuitTitle,
					SebMessageBox.MessageBoxAction.YesNo, SebMessageBox.MessageBoxIcon.Question);
			var quit = result == SebMessageBox.MessageBoxResult.Yes;

			if (quit)
			{
				logger.Info("The user chose to terminate the application.");
			}

			return quit;
		}

		private bool TryValidateQuitPassword()
		{
			var dialog = uiFactory.CreatePasswordDialog(TextKey.PasswordDialog_QuitPasswordRequired, TextKey.PasswordDialog_QuitPasswordRequiredTitle);
			var result = dialog.Show();

			if (result.Success)
			{
				var passwordHash = hashAlgorithm.GenerateHashFor(result.Password);
				var isCorrect = appSettings.QuitPasswordHash.Equals(passwordHash, StringComparison.OrdinalIgnoreCase);

				if (isCorrect)
				{
					logger.Info("The user entered the correct quit password, the application will now terminate.");
				}
				else
				{
					logger.Info("The user entered the wrong quit password.");
					messageBox.Show(TextKey.MessageBox_InvalidQuitPassword,  TextKey.MessageBox_InvalidQuitPasswordTitle, icon: SebMessageBox.MessageBoxIcon.Warning);
				}

				return isCorrect;
			}

			return false;
		}

		private void InitializeLogging()
		{
			var runtimeLog = $"{appSettings.LogFilePrefix}_Runtime.log";
			var logFileWriter = new LogFileWriter(new DefaultLogFormatter(), runtimeLog);

			logFileWriter.Initialize();
			logger.Subscribe(logFileWriter);
		}

		private void InitializeText()
		{
			text = new Text(new ModuleLogger(logger, nameof(Text)));
			text.Initialize();
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

		private string GenerateUserAgent()
		{
			var osVersion = $"{Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}; Win64; x64";
			var userAgent = $"Mozilla/5.0 (Windows NT {osVersion}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{Cef.ChromiumVersion} Safari/537.36";
			if (!string.IsNullOrWhiteSpace(appSettings.UserAgent))
			{
				userAgent = $"{userAgent} {appSettings.UserAgent}";
			}
			return userAgent;
		}

	}

	public static class Program
	{
		[STAThread]
		public static void Main()
		{
			var appSettings = new AppSettings();
			appSettings.Initialize();

			SebUIUtilities.Clipboard.EmptyClipboard();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new SEBContext(appSettings));
			Cef.Shutdown();
			SebUIUtilities.Clipboard.EmptyClipboard();
		}
	}
}
