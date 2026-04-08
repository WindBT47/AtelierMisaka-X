using CefSharp;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AtelierMisaka
{
    public static class CefHelper
    {
        private static readonly string asesmblyDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private static readonly string cefArch = Environment.Is64BitProcess ? "x64" : "x86";
        public static readonly string cefDirectory = GetCefDirectory();
        public static readonly string dllDirectory = Path.Combine(asesmblyDirectory, "Dlls");
        private static bool initialized;

        public static string CachePath => Path.Combine(asesmblyDirectory, "ChromiumCache");

        public static void Initialize()
        {
            if (initialized) return;

            var cefSettings = new CefSettings()
            {
                LocalesDirPath = Path.Combine(cefDirectory, "locales"),
                ResourcesDirPath = cefDirectory,
                BrowserSubprocessPath = Path.Combine(cefDirectory, "CefSharp.BrowserSubprocess.exe"),
                CachePath = CachePath,
            };
            cefSettings.CefCommandLineArgs.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/112.0.0.0 Safari/537.36");
            cefSettings.LogSeverity = LogSeverity.Disable;

            CefSharpSettings.WcfEnabled = true;
            CefSharpSettings.ShutdownOnExit = true;
            CefSharpSettings.SubprocessExitIfParentProcessClosed = true;
            Cef.Initialize(cefSettings);

            initialized = true;
        }

        public static Assembly ResolveCefSharpAssembly(object sender, ResolveEventArgs args)
        {
            var assemblyName = args.Name.Split(new[] { ',' }, 2).FirstOrDefault() + ".dll";
            if (args.Name.StartsWith("CefSharp"))
            {
                var archSpecificPath = Path.Combine(cefDirectory, assemblyName);

                return File.Exists(archSpecificPath)
                    ? Assembly.LoadFile(archSpecificPath)
                    : null;
            }
            else
            {
                var archSpecificPath = Path.Combine(dllDirectory, assemblyName);

                return File.Exists(archSpecificPath)
                    ? Assembly.LoadFile(archSpecificPath)
                    : null;
            }
        }

        public static async Task<bool> SetProxy(ChromiumWebBrowser cwb, string Address)
        {
            if (string.IsNullOrEmpty(Address))
            {
                return await Cef.UIThreadTaskFactory.StartNew(delegate
                {
                    var rc = cwb.GetBrowser().GetHost().RequestContext;
                    var v = new Dictionary<string, object>();
                    v["mode"] = "direct";
                    v["server"] = Address;
                    return rc.SetPreference("proxy", v, out string error);
                });
            }
            if (!GlobalRegex.Regex_Proxy.IsMatch(Address))
            {
                return false;
            }
            return await Cef.UIThreadTaskFactory.StartNew(delegate
            {
                var rc = cwb.GetBrowser().GetHost().RequestContext;
                var v = new Dictionary<string, object>();
                v["mode"] = "fixed_servers";
                v["server"] = Address;
                return rc.SetPreference("proxy", v, out string error);
            });
        }

        private static string GetCefDirectory()
        {
            var appLocalArchDir = Path.Combine(asesmblyDirectory, cefArch);
            if (Directory.Exists(appLocalArchDir))
            {
                return appLocalArchDir;
            }

            var cefChromeArchDir = Path.Combine(asesmblyDirectory, "CefChrome", cefArch);
            if (Directory.Exists(cefChromeArchDir))
            {
                return cefChromeArchDir;
            }

            return Path.Combine(asesmblyDirectory, "CefChrome");
        }
    }
}
