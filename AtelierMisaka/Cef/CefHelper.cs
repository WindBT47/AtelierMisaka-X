using CefSharp;
using CefSharp.Enums;
using CefSharp.Handler;
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

        public static bool Initialize()
        {
            if (initialized) return true;

            EnsureCefSharpCoreRuntimeLoaded();

            var cefSettings = new CefSettings()
            {
                RootCachePath = CachePath,
                LocalesDirPath = Path.Combine(cefDirectory, "locales"),
                ResourcesDirPath = cefDirectory,
                BrowserSubprocessPath = Path.Combine(cefDirectory, "CefSharp.BrowserSubprocess.exe"),
                CachePath = CachePath,
                LogFile = CefDiagnostics.CefLogFile,
            };
            cefSettings.CefCommandLineArgs.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36");
            cefSettings.CefCommandLineArgs.Add("disable-gpu", "1");
            cefSettings.CefCommandLineArgs.Add("do-not-de-elevate", "1");
            cefSettings.LogSeverity = LogSeverity.Warning;

            CefSharpSettings.WcfEnabled = true;
            CefSharpSettings.ShutdownOnExit = true;
            CefSharpSettings.SubprocessExitIfParentProcessClosed = true;
            if (!Cef.Initialize(cefSettings, performDependencyCheck: true, browserProcessHandler: new AtelierBrowserProcessHandler()))
            {
                if (IsExpectedEarlyExit(Cef.GetExitCode()))
                {
                    GlobalMethord.ErrorLog(BuildInitializationFailureMessage());
                    return false;
                }

                throw new InvalidOperationException(BuildInitializationFailureMessage());
            }

            initialized = true;
            return true;
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

        private static void EnsureCefSharpCoreRuntimeLoaded()
        {
            if (Directory.Exists(Path.Combine(asesmblyDirectory, cefArch)))
            {
                CefRuntime.LoadCefSharpCoreRuntimeAnyCpu(asesmblyDirectory);
            }
        }

        private static bool IsExpectedEarlyExit(ResultCode exitCode)
        {
            return exitCode == ResultCode.NormalExitProcessNotified
                || exitCode == ResultCode.NormalExitAutoDeElevated;
        }

        private static string BuildInitializationFailureMessage()
        {
            var subprocessPath = Path.Combine(cefDirectory, "CefSharp.BrowserSubprocess.exe");
            var runtimePath = Path.Combine(cefDirectory, "CefSharp.Core.Runtime.dll");
            var libcefPath = Path.Combine(cefDirectory, "libcef.dll");
            var localesPath = Path.Combine(cefDirectory, "locales");
            var exitCode = Cef.GetExitCode();

            return "CefSharp initialization failed."
                + Environment.NewLine + "ProcessArchitecture: " + (Environment.Is64BitProcess ? "x64" : "x86")
                + Environment.NewLine + "CefExitCode: " + (int)exitCode + " (" + exitCode + ")"
                + Environment.NewLine + "CefDirectory: " + cefDirectory
                + Environment.NewLine + "RootCachePath: " + CachePath
                + Environment.NewLine + "CachePath: " + CachePath
                + Environment.NewLine + "BrowserSubprocessPath: " + subprocessPath + " Exists=" + File.Exists(subprocessPath)
                + Environment.NewLine + "CefSharp.Core.Runtime.dll Exists=" + File.Exists(runtimePath)
                + Environment.NewLine + "libcef.dll Exists=" + File.Exists(libcefPath)
                + Environment.NewLine + "locales Exists=" + Directory.Exists(localesPath)
                + Environment.NewLine + "CefLogFile: " + CefDiagnostics.CefLogFile;
        }

        private sealed class AtelierBrowserProcessHandler : BrowserProcessHandler
        {
            protected override bool OnAlreadyRunningAppRelaunch(IReadOnlyDictionary<string, string> commandLine, string currentDirectory)
            {
                CefDiagnostics.Write("Cef", "already-running-app-relaunch currentDirectory=" + currentDirectory);
                return true;
            }
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
