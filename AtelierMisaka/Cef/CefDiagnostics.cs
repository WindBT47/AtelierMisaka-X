using CefSharp;
using CefSharp.Handler;
using CefSharp.Wpf;
using System;
using System.IO;
using System.Text;
using System.Windows;

namespace AtelierMisaka
{
    public static class CefDiagnostics
    {
        private static readonly object LogLock = new object();
        private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private const int ResponseBodyLimit = 4000;

        public static string BrowserLogFile => Path.Combine(BaseDirectory, "browser.log");
        public static string CefLogFile => Path.Combine(BaseDirectory, "cef.log");

        public static void Attach(ChromiumWebBrowser browser, string browserName)
        {
            if (browser == null)
            {
                return;
            }

            browser.ConsoleMessage += (sender, args) =>
            {
                if (args.Level >= LogSeverity.Warning)
                {
                    Write(browserName, $"console {args.Level} line={args.Line} source={Sanitize(args.Source)} message={Sanitize(args.Message)}");
                }
            };
            browser.LoadError += (sender, args) =>
            {
                Write(browserName, $"load-error frameMain={SafeIsMainFrame(args.Frame)} code={args.ErrorCode} text={Sanitize(args.ErrorText)} url={Sanitize(args.FailedUrl)}");
            };
            browser.FrameLoadEnd += (sender, args) =>
            {
                if (args.Frame != null && args.Frame.IsValid && args.Frame.IsMain)
                {
                    Write(browserName, $"frame-load-end status={args.HttpStatusCode} url={Sanitize(args.Url)}");
                }
            };
            browser.LoadingStateChanged += (sender, args) =>
            {
                Write(browserName, $"loading isLoading={args.IsLoading} canBack={args.CanGoBack} canForward={args.CanGoForward}");
            };
            browser.AddressChanged += (sender, args) =>
            {
                Write(browserName, $"address old={Sanitize(args.OldValue?.ToString())} new={Sanitize(args.NewValue?.ToString())}");
            };
            browser.TitleChanged += (sender, args) =>
            {
                Write(browserName, $"title old={Sanitize(args.OldValue?.ToString())} new={Sanitize(args.NewValue?.ToString())}");
            };
            browser.StatusMessage += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Value))
                {
                    Write(browserName, $"status {Sanitize(args.Value)}");
                }
            };

            if (browser.ResourceRequestHandlerFactory == null)
            {
                browser.ResourceRequestHandlerFactory = new DiagnosticResourceRequestHandlerFactory(browserName);
            }
        }

        public static void LogResourceComplete(string browserName, string url, UrlRequestStatus status, int statusCode, long receivedContentLength)
        {
            if (ShouldLogResource(url, status, statusCode, false))
            {
                Write(browserName, $"resource-complete status={status} http={statusCode} bytes={receivedContentLength} url={Sanitize(url)}");
            }
        }

        public static void LogResponseBody(string browserName, string url, int statusCode, byte[] body)
        {
            if (statusCode < 400 || body == null || body.Length == 0)
            {
                return;
            }

            var text = Sanitize(Encoding.UTF8.GetString(body));
            if (text.Length > ResponseBodyLimit)
            {
                text = text.Substring(0, ResponseBodyLimit) + "...[truncated]";
            }
            Write(browserName, $"resource-body http={statusCode} bytes={body.Length} url={Sanitize(url)} body={text}");
        }

        public static void Write(string source, string message)
        {
            try
            {
                lock (LogLock)
                {
                    File.AppendAllText(BrowserLogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}] {message}{Environment.NewLine}");
                }
            }
            catch { }
        }

        private static bool ShouldHandleResource(string url, bool isNavigation)
        {
            return isNavigation || IsXUrl(url);
        }

        private static bool ShouldLogResource(string url, UrlRequestStatus status, int statusCode, bool isNavigation)
        {
            return status != UrlRequestStatus.Success ||
                   statusCode >= 400 ||
                   isNavigation ||
                   IsXApiUrl(url) ||
                   IsXLoginUrl(url);
        }

        private static bool IsXUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            var lower = url.ToLowerInvariant();
            return lower.Contains("://x.com/") ||
                   lower.Contains("://twitter.com/") ||
                   lower.Contains("://api.x.com/") ||
                   lower.Contains("://api.twitter.com/");
        }

        private static bool IsXApiUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            var lower = url.ToLowerInvariant();
            return lower.Contains("/i/api/") ||
                   lower.Contains("/graphql/") ||
                   lower.Contains("/1.1/");
        }

        private static bool IsXLoginUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            var lower = url.ToLowerInvariant();
            return lower.Contains("/i/flow/login") ||
                   lower.Contains("/account/login") ||
                   lower.Contains("/login") ||
                   lower.Contains("/oauth");
        }

        private static bool SafeIsMainFrame(IFrame frame)
        {
            try
            {
                return frame != null && frame.IsValid && frame.IsMain;
            }
            catch
            {
                return false;
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private class DiagnosticResourceRequestHandlerFactory : IResourceRequestHandlerFactory
        {
            private readonly string _browserName;

            public DiagnosticResourceRequestHandlerFactory(string browserName)
            {
                _browserName = browserName;
            }

            public bool HasHandlers => true;

            public IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
            {
                try
                {
                    if (ShouldHandleResource(request.Url, isNavigation))
                    {
                        return new DiagnosticResourceRequestHandler(_browserName, request.Url, isNavigation);
                    }

                    return null;
                }
                finally
                {
                    request.Dispose();
                }
            }
        }

        private class DiagnosticResourceRequestHandler : ResourceRequestHandler
        {
            private readonly string _browserName;
            private readonly string _url;
            private readonly bool _isNavigation;
            private readonly MemoryStream _body = new MemoryStream();
            private readonly bool _captureBody;

            public DiagnosticResourceRequestHandler(string browserName, string url, bool isNavigation)
            {
                _browserName = browserName;
                _url = url;
                _isNavigation = isNavigation;
                _captureBody = IsXApiUrl(url) || IsXLoginUrl(url);
            }

            protected override IResponseFilter GetResourceResponseFilter(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
            {
                if (_captureBody)
                {
                    return new CefSharp.ResponseFilter.StreamResponseFilter(_body);
                }
                return base.GetResourceResponseFilter(chromiumWebBrowser, browser, frame, request, response);
            }

            protected override void OnResourceRedirect(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl)
            {
                if (_isNavigation || IsXLoginUrl(_url))
                {
                    Write(_browserName, $"resource-redirect http={response?.StatusCode ?? 0} from={Sanitize(_url)} to={Sanitize(newUrl)}");
                }
                base.OnResourceRedirect(chromiumWebBrowser, browser, frame, request, response, ref newUrl);
            }

            protected override void OnResourceLoadComplete(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
            {
                if (ShouldLogResource(_url, status, response?.StatusCode ?? 0, _isNavigation))
                {
                    LogResourceComplete(_browserName, _url, status, response?.StatusCode ?? 0, receivedContentLength);
                }
                LogResponseBody(_browserName, _url, response?.StatusCode ?? 0, _body.ToArray());
                base.OnResourceLoadComplete(chromiumWebBrowser, browser, frame, request, response, status, receivedContentLength);
            }
        }
    }
}
