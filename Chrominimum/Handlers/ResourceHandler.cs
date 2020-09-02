/*
 * Copyright (c) 2020 ETH Zürich, Educational Development and Technology (LET)
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CefSharp;
using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.Settings.Browser.Filter;
using SafeExamBrowser.Browser.Pages;
using BrowserSettings = SafeExamBrowser.Settings.Browser.BrowserSettings;


namespace Chrominimum.Handlers
{
	internal class ResourceHandler : CefSharp.Handler.ResourceRequestHandler
	{
		private SHA256Managed algorithm;
		private IResourceHandler contentHandler;
		private HtmlLoader htmlLoader;
		private ILogger logger;
		private IResourceHandler pageHandler;
		private IText text;

		internal ResourceHandler(BrowserSettings settings, ILogger logger, IText text)
		{
			this.algorithm = new SHA256Managed();
			this.htmlLoader = new HtmlLoader(text);
			this.logger = logger;
			this.text = text;
		}

		protected override IResourceHandler GetResourceHandler(IWebBrowser webBrowser, IBrowser browser, IFrame frame, IRequest request)
		{
			if (Block(request))
			{
				return ResourceHandlerFor(request.ResourceType);
			}

			return base.GetResourceHandler(webBrowser, browser, frame, request);
		}

		protected override CefReturnValue OnBeforeResourceLoad(IWebBrowser webBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
		{
			if (IsMailtoUrl(request.Url))
			{
				return CefReturnValue.Cancel;
			}

			return base.OnBeforeResourceLoad(webBrowser, browser, frame, request, callback);
		}

		protected override void OnResourceRedirect(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl)
		{
			base.OnResourceRedirect(chromiumWebBrowser, browser, frame, request, response, ref newUrl);
		}

		protected override bool OnResourceResponse(IWebBrowser webBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
		{
			return base.OnResourceResponse(webBrowser, browser, frame, request, response);
		}

		private bool Block(IRequest request)
		{
			var block = false;
			return block;
		}

		private bool IsMailtoUrl(string url)
		{
			return url.StartsWith(Uri.UriSchemeMailto);
		}

		private IResourceHandler ResourceHandlerFor(ResourceType resourceType)
		{
            if (contentHandler == default(IResourceHandler))
            {
                contentHandler = CefSharp.ResourceHandler.FromString(htmlLoader.LoadBlockedContent());
            }

            if (pageHandler == default(IResourceHandler))
            {
                pageHandler = CefSharp.ResourceHandler.FromString(htmlLoader.LoadBlockedPage());
            }

            switch (resourceType)
			{
				case ResourceType.MainFrame:
				case ResourceType.SubFrame:
					return pageHandler;
				default:
					return contentHandler;
			}
		}

	}
}
