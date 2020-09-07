﻿/*
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
using CefSharp;
using CefSharp.WinForms;

using SafeExamBrowser.I18n;
using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.Logging;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.UserInterface.Contracts;
using SafeExamBrowser.UserInterface.Contracts.Shell;
using SafeExamBrowser.UserInterface.Desktop;
using SebMessageBox = SafeExamBrowser.UserInterface.Contracts.MessageBox;

using SafeExamBrowser.Settings.SystemComponents;
using SafeExamBrowser.SystemComponents.Audio;
using SafeExamBrowser.SystemComponents.Keyboard;
using SafeExamBrowser.SystemComponents.PowerSupply;
using SafeExamBrowser.SystemComponents.WirelessNetwork;

using Chrominimum.Events;


namespace Chrominimum
{

	internal class SEBContext : ApplicationContext
	{
		private BrowserApplication browser;
		private ILogger logger;
		private IText text;
		private AppSettings appSettings;

		private IUserInterfaceFactory uiFactory;
		private ITaskbar taskbar;
		private ITaskview taskview;
		private SebMessageBox.IMessageBox messageBox;
		private HashAlgorithm hashAlgorithm;

		internal SEBContext(AppSettings settings)
		{
			appSettings = settings;
			logger = new Logger();
			hashAlgorithm = new HashAlgorithm();

			InitializeLogging();
			InitializeText();

			uiFactory = new UserInterfaceFactory(text);
			messageBox = new MessageBoxFactory(text);

			taskbar = uiFactory.CreateTaskbar(logger);
			taskbar.QuitButtonClicked += Shell_QuitButtonClicked;
			taskbar.Show();

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

			browser = new BrowserApplication(appSettings, true, new ModuleLogger(logger, nameof(BrowserApplication)), text);
			taskbar.AddApplicationControl(uiFactory.CreateApplicationControl(browser, Location.Taskbar), true);

			taskview.Add(browser);
			browser.Initialize();
			browser.CreateNewInstance();
		}

		private void ClosingSeqence()
		{
			browser.Terminate();
			ExitThread();
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

	}

	public static class Program
	{
		[STAThread]
		public static void Main()
		{
			var appSettings = new AppSettings();
			appSettings.Initialize();

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new SEBContext(appSettings));
		}
	}
}
