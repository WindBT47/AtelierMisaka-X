using AtelierMisaka.Commands;
using AtelierMisaka.Models;
using CefSharp;
using CefSharp.Wpf;
using System.Net;
using System.Threading.Tasks;

namespace AtelierMisaka
{
    public class GlobalCommand
    {
        private static void OpenSettingDrawer()
        {
            GlobalData.VM_MA.IsShowDocument = false;
            GlobalData.VM_MA.SyncFantiaRunModeDraft();
            GlobalData.VM_MA.SyncXRunModeDraft();
            GlobalData.VM_MA.PopPage = GlobalData.Pop_Setting;
            GlobalData.VM_MA.LZindex = 3;
        }

        private static void CloseOverlay()
        {
            GlobalData.VM_MA.PopPage = null;
            GlobalData.VM_MA.IsShowDocument = false;
            GlobalData.VM_MA.LZindex = 0;
        }

        public static CommonCommand OpBrowserCommand = new CommonCommand(() =>
        {
            ((ChromiumWebBrowser)GlobalData.VM_MA.PatreonCefBrowser).Load(GlobalData.VM_MA.MainPage);
            GlobalData.VM_MA.ShowLogin = true;
        });

        public static CommonCommand CloseBrowserCommand = new CommonCommand(async () =>
        {
            var cwb = (ChromiumWebBrowser)GlobalData.VM_MA.PatreonCefBrowser;
            cwb.Load("about:blank");
            await Task.Delay(300);
            GlobalData.VM_MA.ShowLogin = false;
        });

        public static ParamCommand<BackType> BackCommand = new ParamCommand<BackType>((flag) =>
        {
            if (flag == BackType.Main)
            {
                OpenSettingDrawer();
            }
            else
            {
                CloseOverlay();
            }
        });

        public static CommonCommand OpenSettingCommand = new CommonCommand(() =>
        {
            OpenSettingDrawer();
        });

        public static CommonCommand ToggleSettingCommand = new CommonCommand(() =>
        {
            if (GlobalData.VM_MA.PopPage == GlobalData.Pop_Setting)
            {
                CloseOverlay();
            }
            else
            {
                OpenSettingDrawer();
            }
        });

        public static ParamCommand<BaseItem> ShowDocumentCommand = new ParamCommand<BaseItem>((bi) =>
        {
            GlobalData.VM_MA.SelectedDocument = bi;
            GlobalData.VM_MA.IsShowDocument = true;
            GlobalData.Pop_Document.LoadData(bi);
            GlobalData.VM_MA.PopPage = GlobalData.Pop_Document;
            GlobalData.VM_MA.LZindex = 3;
        });

        public static ParamCommand<BaseItem> GetCoverCommand = new ParamCommand<BaseItem>((bi) =>
        {
            GlobalData.VM_MA.SelectedDocument = bi;
            if (string.IsNullOrEmpty(bi.CoverPicThumb))
            {
                return;
            }
            bi.NeedLoadCover = false;
            GlobalData.CaptureUtil.GetCover(bi);
        });

        public static CommonCommand ExitCommand = new CommonCommand(async () =>
        {
            GlobalData.VM_MA.Messages = GlobalLanguage.Msg_ExitApp;
            while (GlobalData.VM_MA.ShowCheck)
            {
                await Task.Delay(200);
            }
            if (GlobalData.CheckResult == false)
            {
                return;
            }
            System.Windows.Application.Current.Shutdown();
        });

        public static CommonCommand ShowDLCommand = new CommonCommand(() =>
        {
            GlobalData.VM_MA.MainSection = MainSection.Downloads;
            CloseOverlay();
        });

        public static CommonCommand OpenLinkCommand = new CommonCommand(() =>
        {
            System.Diagnostics.Process.Start(GlobalData.VM_MA.SelectedDocument.Link);
        });

        public static ParamCommand<string> OpenBrowserCommand = new ParamCommand<string>((link) =>
        {
            System.Diagnostics.Process.Start(link);
        });

        public static CommonCommand CheckVersionCommand = new CommonCommand(() =>
        {
            if (GlobalData.VM_MA.LatestVersion.EndsWith("ck"))
            {
                GlobalData.VM_MA.LatestVersion = "Checking";
                Task.Run(() =>
                {
                    HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(GlobalData.VM_MA.VersionLink);
                    webRequest.AllowAutoRedirect = false;

                    try
                    {
                        var webResponse = webRequest.GetResponse();
                        string realurl = webResponse.Headers["Location"];
                        webResponse.Close();
                        if (!string.IsNullOrEmpty(realurl))
                        {
                            var tem = realurl.Split('/');
                            var tep = tem[tem.Length - 1];
                            if (tep.StartsWith("V"))
                            {
                                GlobalData.VM_MA.LatestVersion = tep;
                                GlobalData.VM_MA.VersionLink = realurl;
                                return;
                            }
                        }
                        GlobalData.VM_MA.LatestVersion = "Error";
                    }
                    catch
                    {
                        GlobalData.VM_MA.LatestVersion = "Error";
                    }
                    finally
                    {
                        webRequest.Abort();
                    }
                });
            }
            else
            {
                System.Diagnostics.Process.Start(GlobalData.VM_MA.VersionLink);
            }
        },
        () => { return !GlobalData.VM_MA.LatestVersion.Equals("Checking"); });

        public static CommonCommand LikePostCommand = new CommonCommand(async () =>
        {
            if (GlobalData.VM_MA.IsLiked_Document)
                return;

            var ret = await GlobalData.CaptureUtil.LikePost(GlobalData.VM_MA.SelectedDocument.ID, GlobalData.VM_MA.Artist.Cid);
            if (ret.Error == ErrorType.NoError)
            {
                GlobalData.VM_MA.IsLiked_Document = true;
            }
            else
            {
                if (GlobalData.VM_MA.IsLiked_Document)
                    return;

                GlobalData.VM_MA.Messages = ret.Msgs;
            }
        });
    }
}
