﻿/*
 * Copyright (c) 2020 ETH Zürich, Educational Development and Technology (LET)
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.IO;
using System.Reflection;
using SafeExamBrowser.I18n.Contracts;

namespace SafeExamBrowser.Browser.Pages
{
	internal class HtmlLoader
	{
		private IText text;
		private const string pagesPrefix = "Chrominimum.Pages";

		internal HtmlLoader(IText text)
		{
			this.text = text;
		}

		internal string LoadBlockedContent()
		{
			var assembly = Assembly.GetAssembly(typeof(HtmlLoader));
			var path = $"{pagesPrefix}.BlockedContent.html";

			using (var stream = assembly.GetManifestResourceStream(path))
			using (var reader = new StreamReader(stream))
			{
				var html = reader.ReadToEnd();

				html = html.Replace("%%MESSAGE%%", text.Get(TextKey.Browser_BlockedContentMessage));

				return html;
			}
		}

		internal string LoadBlockedPage()
		{
			var assembly = Assembly.GetAssembly(typeof(HtmlLoader));
			var path = $"{pagesPrefix}.BlockedPage.html";

			using (var stream = assembly.GetManifestResourceStream(path))
			using (var reader = new StreamReader(stream))
			{
				var html = reader.ReadToEnd();

				html = html.Replace("%%BACK_BUTTON%%", text.Get(TextKey.Browser_BlockedPageButton));
				html = html.Replace("%%MESSAGE%%", text.Get(TextKey.Browser_BlockedPageMessage));
				html = html.Replace("%%TITLE%%", text.Get(TextKey.Browser_BlockedPageTitle));

				return html;
			}
		}

		internal string LoadErrorPage()
		{
			var assembly = Assembly.GetAssembly(typeof(HtmlLoader));
			var path = $"{pagesPrefix}.LoadError.html";

			using (var stream = assembly.GetManifestResourceStream(path))
			using (var reader = new StreamReader(stream))
			{
				var html = reader.ReadToEnd();

				html = html.Replace("%%MESSAGE%%", text.Get(TextKey.Browser_LoadErrorPageMessage));
				html = html.Replace("%%RETRY_BUTTON%%", text.Get(TextKey.Browser_LoadErrorPageButton));
				html = html.Replace("%%TITLE%%", text.Get(TextKey.Browser_LoadErrorPageTitle));

				return html;
			}
		}
	}
}
