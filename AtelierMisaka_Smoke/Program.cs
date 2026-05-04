using AtelierMisaka;
using AtelierMisaka.Models;
using AtelierMisaka.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AtelierMisaka_Smoke
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                return RunAsync().GetAwaiter().GetResult() ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SMOKE FAIL");
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static async Task<bool> RunAsync()
        {
            var workDir = Path.Combine(Path.GetTempPath(), "AtelierMisakaSmoke", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);
            Directory.SetCurrentDirectory(workDir);
            Console.WriteLine("Smoke workdir: " + workDir);

            SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
            using (var server = new LocalHttpServer())
            {
                InitializeGlobals(workDir);

                var fantiaUrl = server.Url("fantia.bin");
                var xUrl = server.Url("x.bin");

                AddFantiaItem(fantiaUrl, workDir);
                AddXItem(xUrl, workDir);

                Assert(GlobalData.VM_DL.DownLoadItemList.Count == 2, "expected 2 queued download items");
                var fantiaDownload = GlobalData.VM_DL.DownLoadItemList.FirstOrDefault(x => x.Site == SiteType.Fantia);
                var xDownload = GlobalData.VM_DL.DownLoadItemList.FirstOrDefault(x => x.Site == SiteType.X);
                Assert(fantiaDownload != null, "missing Fantia download item");
                Assert(xDownload != null, "missing X download item");
                Assert(fantiaDownload.Cookies.StartsWith("_session_id="), "Fantia item captured wrong cookies");
                Assert(xDownload.Cookies.Contains("auth_token="), "X item captured wrong cookies");
                Assert(GlobalData.VM_DL.IsDownloading, "download queue did not auto-start");

                await WaitForDownloadsAsync(TimeSpan.FromSeconds(15));

                Assert(File.Exists(Path.Combine(fantiaDownload.SavePath, fantiaDownload.FileName)), "Fantia file was not written");
                Assert(File.Exists(Path.Combine(xDownload.SavePath, xDownload.FileName)), "X file was not written");

                var fantiaLogs = SQLiteDownloadLogHelper.Instance.GetLogs("fantia-smoke", SiteType.Fantia);
                var xLogs = SQLiteDownloadLogHelper.Instance.GetLogs("ShitpostGate", SiteType.X);
                Assert(fantiaLogs.Any(x => x.PId == "fantia-post-1" && x.Site == SiteType.Fantia), "missing Fantia download log");
                Assert(xLogs.Any(x => x.PId == "x-post-1" && x.Site == SiteType.X), "missing X download log for media author");
                Assert(xDownload.SavePath.Contains("ShitpostGate"), "X download path did not use media author");

                GlobalMethord.SetFantiaPollingPendingCursor("fantia-smoke", new[] { "fantia-post-1" });
                GlobalMethord.SetXSeenPendingCursor("Notifications:x-smoke", new[] { "x-notification-1" }, 200);
                GlobalMethord.SetXSeenPendingCursor("Timeline:x-smoke", new[] { "x-post-1" }, 100);
                Assert(GlobalMethord.CommitXSeenPendingCursor(), "X cursors did not commit");
                Assert(GlobalMethord.CommitFantiaPollingPendingCursor(), "Fantia cursor did not commit after X cursor commit");
                Assert(GlobalData.FantiaPollingCursorDic.TryGetWindow("fantia-smoke", out var fantiaCursor) && fantiaCursor.Contains("fantia-post-1"), "Fantia cursor missing after commit");
                Assert(GlobalData.XSeenCursorDic.TryGetWindow("Notifications:x-smoke", 200, out var notificationCursor) && notificationCursor.Contains("x-notification-1"), "X notification cursor missing after commit");
                Assert(GlobalData.XSeenCursorDic.TryGetWindow("Timeline:x-smoke", 100, out var timelineCursor) && timelineCursor.Contains("x-post-1"), "X timeline cursor missing after commit");

                RunFantiaPollingPolicySmoke();
                RunXTimelineParserSmoke();
                RunXRetweetQuoteParserSmoke();
                RunXNotificationFreshSmoke();
                RunXSearchQuerySmoke();
                RunXRelatedMediaEntrySmoke();
                RunXNotificationIdExtractionSmoke();
                await RunXOnlyDownloadSmokeAsync(server, Path.Combine(workDir, "x-only"));
            }

            Console.WriteLine("SMOKE PASS");
            return true;
        }

        private static void RunFantiaPollingPolicySmoke()
        {
            var cursorUpdatedAt = new DateTime(2026, 4, 29, 21, 28, 40);
            var oldPostThreshold = cursorUpdatedAt.Subtract(FantiaPollingPolicy.OldPostGraceWindow);
            Assert(FantiaPollingCursorDic.WindowSize == 15, "Fantia polling cursor window changed from 15");
            Assert(FantiaPollingCursorDic.WindowSize < 20, "Fantia polling cursor can cross one Fantia page");
            Assert(!FantiaPollingPolicy.ShouldStopAfterSeenWindow(14), "Fantia polling stopped before 15 seen posts");
            Assert(FantiaPollingPolicy.ShouldStopAfterSeenWindow(15), "Fantia polling did not stop at 15 seen posts");
            Assert(FantiaPollingPolicy.IsOldPostReordered(new DateTime(2026, 1, 10), new DateTime(2026, 1, 10), cursorUpdatedAt), "old reordered Fantia post was not filtered");
            Assert(!FantiaPollingPolicy.IsOldPostReordered(new DateTime(2026, 4, 26), new DateTime(2026, 4, 29), cursorUpdatedAt), "recently updated Fantia post was filtered");
            Assert(!FantiaPollingPolicy.IsOldPostReordered(new DateTime(2026, 1, 10), new DateTime(2026, 4, 29), cursorUpdatedAt), "old Fantia post with recent update was filtered");
            Assert(!FantiaPollingPolicy.IsOldPostReordered(oldPostThreshold, oldPostThreshold, cursorUpdatedAt), "Fantia old-post threshold should be exclusive");
            Assert(FantiaPollingPolicy.IsOldPostReordered(oldPostThreshold.AddTicks(-1), oldPostThreshold.AddTicks(-1), cursorUpdatedAt), "Fantia old-post threshold did not filter older post");
            Assert(!FantiaPollingPolicy.ShouldScanNextPage(true, true), "Fantia polling should not scan page 2");
            Assert(FantiaPollingPolicy.ShouldScanNextPage(false, true), "Fantia time range scan should keep pagination");
            Assert(!FantiaPollingPolicy.ShouldScanNextPage(false, false), "Fantia scan should not paginate without next page");
            Assert(SimulateFantiaFirstPagePollingFetchesNewHead(), "Fantia first-page polling did not fetch a new head post");

            var pids = Enumerable.Range(1, 20).Select(x => $"pid-{x}");
            GlobalMethord.SetFantiaPollingPendingCursor("fantia-window-smoke", pids);
            Assert(GlobalMethord.CommitFantiaPollingPendingCursor(), "Fantia window cursor did not commit");
            Assert(GlobalData.FantiaPollingCursorDic.TryGetWindow("fantia-window-smoke", out var window), "Fantia window cursor missing");
            Assert(window.Count == FantiaPollingCursorDic.WindowSize, "Fantia window cursor was not trimmed to window size");
            Assert(window.Contains("pid-15") && !window.Contains("pid-16"), "Fantia window cursor trim boundary is wrong");
        }

        private static bool SimulateFantiaFirstPagePollingFetchesNewHead()
        {
            var cursor = new HashSet<string>(Enumerable.Range(1, FantiaPollingCursorDic.WindowSize).Select(x => $"old-{x}"));
            var firstPage = new[] { "new-head" }.Concat(Enumerable.Range(1, 19).Select(x => $"old-{x}")).ToList();
            var fetched = new List<string>();
            var consecutiveSeen = 0;
            foreach (var pid in firstPage)
            {
                if (cursor.Contains(pid))
                {
                    consecutiveSeen++;
                    if (FantiaPollingPolicy.ShouldStopAfterSeenWindow(consecutiveSeen))
                    {
                        break;
                    }
                    continue;
                }

                consecutiveSeen = 0;
                fetched.Add(pid);
            }

            return fetched.Count == 1 && fetched[0] == "new-head";
        }

        private static void RunXTimelineParserSmoke()
        {
            var json = @"{
              ""data"": {
                ""user_results"": { ""result"": { ""rest_id"": ""907700934486003712"", ""legacy"": { ""screen_name"": ""receiver"", ""created_at"": ""Thu Sep 14 00:00:00 +0000 2017"", ""entities"": {} } } },
                ""tweet_results"": { ""result"": { ""rest_id"": ""2040000000000000001"", ""legacy"": { ""full_text"": ""media post"", ""created_at"": ""Thu Apr 30 11:00:00 +0000 2026"", ""extended_entities"": { ""media"": [ { ""type"": ""photo"", ""media_url_https"": ""https://pbs.twimg.com/media/test.jpg"" } ] } }, ""core"": { ""user_results"": { ""result"": { ""legacy"": { ""screen_name"": ""sender"" } } } } } }
              }
            }";
            var parsed = ParseXItemsForSmoke(json, false, false, false, 200);
            Assert(parsed.Item1.Count == 1, "X parser did not parse tweet media item");
            Assert(parsed.Item2.Contains("2040000000000000001"), "X parser missed tweet id in head ids");
            Assert(!parsed.Item2.Contains("907700934486003712"), "X parser treated user id as tweet id");

            var nestedMediaJson = @"{
              ""data"": {
                ""tweet_results"": { ""result"": { ""rest_id"": ""2040000000000000004"", ""legacy"": { ""full_text"": ""nested media post"", ""created_at"": ""Thu Apr 30 11:00:00 +0000 2026"", ""extended_entities"": { ""media"": [ { ""media_results"": { ""result"": { ""type"": ""photo"", ""media_url_https"": ""https://pbs.twimg.com/media/nested.jpg"" } } } ] } }, ""core"": { ""user_results"": { ""result"": { ""core"": { ""screen_name"": ""sender_core"" } } } } } }
              }
            }";
            var nestedParsed = ParseXItemsForSmoke(nestedMediaJson, false, false, false, 200);
            Assert(nestedParsed.Item1.Count == 1, "X parser did not parse nested media_results media item");
            Assert(nestedParsed.Item1[0].ContentUrls[0].Contains("nested.jpg"), "X parser nested media url is wrong");
            Assert(nestedParsed.Item1[0].AuthorScreenName == "sender_core", "X parser did not read author from core.screen_name");

            var missingTypeJson = @"{
              ""data"": {
                ""tweet_results"": { ""result"": { ""rest_id"": ""2040000000000000005"", ""legacy"": { ""full_text"": ""missing type media post"", ""created_at"": ""Thu Apr 30 11:00:00 +0000 2026"", ""extended_entities"": { ""media"": [ { ""media_url_https"": ""https://pbs.twimg.com/media/missing-type.jpg"", ""media_results"": { ""result"": { ""media_key"": ""3_2040000000000000005"" } } } ] } }, ""core"": { ""user_results"": { ""result"": { ""core"": { ""screen_name"": ""sender_core"" } } } } } }
              }
            }";
            var missingTypeParsed = ParseXItemsForSmoke(missingTypeJson, false, false, false, 200);
            Assert(missingTypeParsed.Item1.Count == 1, "X parser did not infer photo media without type");
            Assert(missingTypeParsed.Item1[0].ContentUrls[0].Contains("missing-type.jpg"), "X parser missing-type media url is wrong");
        }

        private static void RunXRetweetQuoteParserSmoke()
        {
            var originalId = "2040000000000000001";
            var retweetId = "2040000000000000002";
            var quoteId = "2040000000000000003";
            var json =
                @"{""data"":{" +
                @"""tweet_results"":{""result"":" + BuildXTweetJson(originalId, "sender", "original media", "https://pbs.twimg.com/media/original.jpg") + @"}," +
                @"""retweeted_status_result"":{""result"":" + BuildXTweetJson(retweetId, "retweet_author", "retweet media", "https://pbs.twimg.com/media/retweet.jpg") + @"}," +
                @"""quoted_status_result"":{""result"":" + BuildXTweetJson(quoteId, "quote_author", "quote media", "https://pbs.twimg.com/media/quote.jpg") + @"}" +
                @"}}";

            var originalsOnly = ParseXItemsForSmoke(json, false, false, false, 200).Item1;
            Assert(HasXItem(originalsOnly, originalId), "X parser missed original media");
            Assert(!HasXItem(originalsOnly, retweetId), "X parser included retweet media while retweet switch was off");
            Assert(!HasXItem(originalsOnly, quoteId), "X parser included quote media while quote switch was off");

            var withRetweet = ParseXItemsForSmoke(json, true, false, false, 200).Item1;
            Assert(HasXItem(withRetweet, originalId), "X parser missed original media with retweet switch on");
            Assert(HasXItem(withRetweet, retweetId), "X parser missed retweet media with retweet switch on");
            Assert(!HasXItem(withRetweet, quoteId), "X parser included quote media while quote switch was off");

            var withQuote = ParseXItemsForSmoke(json, false, true, false, 200).Item1;
            Assert(HasXItem(withQuote, originalId), "X parser missed original media with quote switch on");
            Assert(!HasXItem(withQuote, retweetId), "X parser included retweet media while retweet switch was off");
            Assert(HasXItem(withQuote, quoteId), "X parser missed quote media with quote switch on");

            var all = ParseXItemsForSmoke(json, true, true, false, 200).Item1;
            Assert(HasXItem(all, originalId), "X parser missed original media with all switches on");
            Assert(HasXItem(all, retweetId), "X parser missed retweet media with all switches on");
            Assert(HasXItem(all, quoteId), "X parser missed quote media with all switches on");

            var outerId = "2040000000000000100";
            var oldQuoteId = "2040000000000000101";
            var nestedQuoteJson =
                @"{""data"":{""tweet_results"":{""result"":{" +
                @"""rest_id"":""" + outerId + @"""," +
                @"""legacy"":{""full_text"":""new quote post"",""created_at"":""Sun May 03 16:50:00 +0000 2026""}," +
                @"""core"":{""user_results"":{""result"":{""legacy"":{""screen_name"":""ShitpostGate""}}}}," +
                @"""quotedRefResult"":{""result"":" + BuildXTweetJson(oldQuoteId, "quoted_author", "old quoted media", "https://pbs.twimg.com/media/old-quote.jpg") + @"}" +
                @"}}}}";
            var nestedQuoteItems = ParseXItemsForSmoke(nestedQuoteJson, false, true, false, 200).Item1;
            var nestedQuote = nestedQuoteItems.FirstOrDefault(x => x.ID == oldQuoteId);
            Assert(nestedQuote != null && nestedQuote.TweetType == XTweetType.Quote, "X parser missed quotedRefResult media");
            Assert(nestedQuote.TriggerAuthorScreenName == "ShitpostGate", "X parser did not keep quote trigger author");
            Assert(nestedQuote.TriggerDate.HasValue && nestedQuote.TriggerDate.Value.Year == 2026 && nestedQuote.TriggerDate.Value.Month == 5, "X parser did not keep quote trigger date");

            GlobalData.VM_MA.UseDate = true;
            GlobalData.VM_MA.Date_Start = "2026/05/03 00:00:00";
            GlobalData.VM_MA.Date_End = "2026/05/04 23:59:59";
            GlobalData.VM_MA.LastDate = DateTime.Parse(GlobalData.VM_MA.Date_Start);
            GlobalData.VM_MA.LastDate_End = DateTime.Parse(GlobalData.VM_MA.Date_End);
            var nestedQuoteInRange = ParseXItemsForSmoke(nestedQuoteJson, false, true, true, 200).Item1;
            Assert(HasXItem(nestedQuoteInRange, oldQuoteId), "X parser filtered quote media by original media date instead of trigger date");
            GlobalData.VM_MA.UseDate = false;
            GlobalData.VM_MA.Date_Start = string.Empty;
            GlobalData.VM_MA.Date_End = string.Empty;

            var mediaOnlyJson = @"{""data"":{""tweet_results"":{""result"":" + BuildXTweetJson("2040000000000000110", "ShitpostGate", "media tab original", "https://pbs.twimg.com/media/media-tab.jpg") + @"}}}";
            var userTimelineJson =
                @"{""data"":{""tweet_results"":{""result"":{" +
                @"""rest_id"":""2040000000000000111""," +
                @"""legacy"":{""full_text"":""new repost"",""created_at"":""Sun May 03 16:55:00 +0000 2026""}," +
                @"""core"":{""user_results"":{""result"":{""legacy"":{""screen_name"":""ShitpostGate""}}}}," +
                @"""retweetResult"":{""result"":" + BuildXTweetJson("2040000000000000112", "retweet_author", "retweeted media", "https://pbs.twimg.com/media/repost.jpg") + @"}" +
                @"}}}}";
            var combined = ParseXItemsForSmoke(new[] { mediaOnlyJson, userTimelineJson }, true, false, false, 200).Item1;
            Assert(HasXItem(combined, "2040000000000000112"), "X parser missed retweet media from combined media and user timeline responses");

            var rtTextId = "2040000000000000120";
            var rtTextJson = @"{""data"":{""tweet_results"":{""result"":" +
                BuildXTweetJson(rtTextId, "ShitpostGate", "RT @source_author: reposted media", "https://pbs.twimg.com/media/rt-text.jpg") +
                @"}}}";
            var rtTextDisabled = ParseXItemsForSmoke(rtTextJson, false, false, false, 200).Item1;
            Assert(!HasXItem(rtTextDisabled, rtTextId), "X parser included RT-text media while retweet switch was off");
            var rtTextEnabled = ParseXItemsForSmoke(rtTextJson, true, false, false, 200).Item1;
            var rtTextItem = rtTextEnabled.FirstOrDefault(x => x.ID == rtTextId);
            Assert(rtTextItem != null && rtTextItem.TweetType == XTweetType.Retweet, "X parser did not classify RT-text media as retweet");
            Assert(rtTextItem.TriggerAuthorScreenName == "ShitpostGate", "X parser did not keep RT-text trigger author");

            var nativeRetweetJson = @"{""data"":{""tweet_results"":{""result"":" +
                BuildXTweetJson("2040000000000000130", "source_author", "native retweet original media", "https://pbs.twimg.com/media/native-retweet.jpg") +
                @"}}}";
            var nativeRetweet = ParseXItemsForSmoke(new[] { nativeRetweetJson }, true, false, false, 200, XTweetType.Retweet).Item1.FirstOrDefault();
            Assert(nativeRetweet != null && nativeRetweet.TweetType == XTweetType.Retweet, "X parser did not force native-retweet search results to retweet");
        }

        private static void RunXNotificationFreshSmoke()
        {
            var seenId = "2040000000000000011";
            var freshId = "2040000000000000012";
            var json =
                @"{""data"":{""notifications"":[" +
                BuildXTweetJson(seenId, "sender_a", "seen media", "https://pbs.twimg.com/media/seen.jpg") + @"," +
                BuildXTweetJson(freshId, "sender_b", "fresh media", "https://pbs.twimg.com/media/fresh.jpg") +
                @"]}}";
            var parsed = ParseXItemsForSmoke(json, false, false, false, XUtils.NotificationWindowSize);
            Assert(parsed.Item2.Contains(seenId), "X notification parser missed seen tweet id");
            Assert(parsed.Item2.Contains(freshId), "X notification parser missed fresh tweet id");

            var seeded = SelectFreshXItems(parsed.Item1, new string[0], true);
            Assert(seeded.Count == 0, "X notification seed should not enqueue historical media");

            var fresh = SelectFreshXItems(parsed.Item1, new[] { seenId }, false);
            Assert(fresh.Count == 1 && fresh[0].ID == freshId, "X notification seen filter selected wrong items");
        }

        private static void RunXSearchQuerySmoke()
        {
            var parserType = typeof(GlobalData).Assembly.GetType("AtelierMisaka.XUtils");
            var build = parserType.GetMethod("BuildSearchQuery", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var start = new DateTime(2026, 1, 1);
            var end = new DateTime(2026, 2, 1);

            var mediaOnly = (string)build.Invoke(null, new object[] { "tester", start, end, false });
            Assert(mediaOnly == "from:tester since:2026-01-01 until:2026-02-01 filter:media", "X media-only search query changed");

            var related = (string)build.Invoke(null, new object[] { "tester", start, end, true });
            Assert(related == "from:tester since:2026-01-01 until:2026-02-01", "X related-media search query changed");

            var buildRetweetInclusive = parserType.GetMethod("BuildRetweetInclusiveSearchQuery", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var retweetInclusive = (string)buildRetweetInclusive.Invoke(null, new object[] { "tester", start, end });
            Assert(retweetInclusive == "from:tester since:2026-01-01 until:2026-02-01 include:nativeretweets", "X inclusive native-retweet search query changed");
        }

        private static void RunXRelatedMediaEntrySmoke()
        {
            var parserType = typeof(GlobalData).Assembly.GetType("AtelierMisaka.XUtils");
            var shouldCapture = parserType.GetMethod("ShouldCaptureUserTimelineForRelatedMedia", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            GlobalData.VM_MA.XIncludeRetweetMedia = false;
            GlobalData.VM_MA.XIncludeQuoteMedia = false;
            Assert(!(bool)shouldCapture.Invoke(null, new object[0]), "X related timeline capture should stay off by default");

            GlobalData.VM_MA.XIncludeRetweetMedia = true;
            GlobalData.VM_MA.XIncludeQuoteMedia = false;
            Assert((bool)shouldCapture.Invoke(null, new object[0]), "X retweet media should enable user timeline capture");

            GlobalData.VM_MA.XIncludeRetweetMedia = false;
            GlobalData.VM_MA.XIncludeQuoteMedia = true;
            Assert((bool)shouldCapture.Invoke(null, new object[0]), "X quote media should enable user timeline capture");
        }

        private static void RunXNotificationIdExtractionSmoke()
        {
            var parserType = typeof(GlobalData).Assembly.GetType("AtelierMisaka.XUtils");
            var extract = parserType.GetMethod("ExtractNotificationTweetIds", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var extractPostUsers = parserType.GetMethod("ExtractPostNotificationScreenNames", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var isFallbackItem = parserType.GetMethod("IsPostNotificationFallbackItem", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var json = @"{
                ""data"": {
                  ""user_results"": { ""result"": { ""__typename"": ""User"", ""rest_id"": ""907700934486003712"", ""legacy"": { ""screen_name"": ""receiver"" } } },
                  ""tweet_results"": { ""result"": { ""__typename"": ""Tweet"", ""rest_id"": ""2027017154500108419"", ""legacy"": { ""created_at"": ""Sun May 03 11:00:00 +0000 2026"" } } },
                  ""entry"": { ""content"": { ""itemContent"": { ""tweet_results"": { ""result"": { ""rest_id"": ""2027017154500108420"" } } } } },
                  ""expanded_url"": ""https://x.com/ShitpostGate/status/2027017154500108421"",
                  ""entryId"": ""tweet-2027017154500108422"",
                  ""conversation_id_str"": ""2027017154500108423""
                }
              }";
            var ids = (List<string>)extract.Invoke(null, new object[] { new[] { json }, XUtils.NotificationWindowSize });
            Assert(ids.Contains("2027017154500108419"), "X notification extraction missed typename tweet id");
            Assert(ids.Contains("2027017154500108420"), "X notification extraction missed tweet_results id");
            Assert(ids.Contains("2027017154500108421"), "X notification extraction missed status URL tweet id");
            Assert(ids.Contains("2027017154500108422"), "X notification extraction missed entryId tweet id");
            Assert(ids.Contains("2027017154500108423"), "X notification extraction missed conversation tweet id");
            Assert(!ids.Contains("907700934486003712"), "X notification extraction treated user id as tweet id");

            var aggregateJson = @"{
              ""entry"": {
                ""content"": {
                  ""rich_message"": {
                    ""text"": ""New post notifications for ShitpostGateway and sample"",
                    ""entities"": [
                      { ""ref"": { ""user_results"": { ""result"": { ""core"": { ""screen_name"": ""ShitpostGate"" }, ""legacy"": { ""screen_name"": ""ShitpostGate"" } } } } },
                      { ""ref"": { ""user_results"": { ""result"": { ""core"": { ""screen_name"": ""sample_user"" } } } } }
                    ]
                  }
                }
              }
            }";
            var postUsers = (List<string>)extractPostUsers.Invoke(null, new object[] { new[] { aggregateJson }, 10 });
            Assert(postUsers.Contains("ShitpostGate"), "X post notification extraction missed ShitpostGate");
            Assert(postUsers.Contains("sample_user"), "X post notification extraction missed aggregate post user");

            var roundStartedAt = new DateTime(2026, 5, 3, 21, 20, 0);
            var recentOriginal = BuildXSmokeItem("ShitpostGate", "2050925844147970261", XTweetType.Original, roundStartedAt.AddMinutes(-10));
            var oldOriginal = BuildXSmokeItem("ShitpostGate", "2041812926525485115", XTweetType.Original, roundStartedAt.AddDays(-30));
            var recentQuote = BuildXSmokeItem("ShitpostGate", "2050925844147970262", XTweetType.Quote, roundStartedAt.AddMinutes(-10));
            var oldQuoteWithFreshTrigger = BuildXSmokeItem("quoted_author", "2041812926525485116", XTweetType.Quote, roundStartedAt.AddDays(-30));
            oldQuoteWithFreshTrigger.TriggerAuthorScreenName = "ShitpostGate";
            oldQuoteWithFreshTrigger.TriggerDate = roundStartedAt.AddMinutes(-10);
            var wrongAuthor = BuildXSmokeItem("snexxxxxxx", "2050925844147970263", XTweetType.Original, roundStartedAt.AddMinutes(-10));
            Assert((bool)isFallbackItem.Invoke(null, new object[] { recentOriginal, "ShitpostGate", roundStartedAt }), "X post notification fallback rejected recent original media");
            Assert(!(bool)isFallbackItem.Invoke(null, new object[] { oldOriginal, "ShitpostGate", roundStartedAt }), "X post notification fallback accepted old media");
            Assert((bool)isFallbackItem.Invoke(null, new object[] { recentQuote, "ShitpostGate", roundStartedAt }), "X post notification fallback rejected recent quote media");
            Assert((bool)isFallbackItem.Invoke(null, new object[] { oldQuoteWithFreshTrigger, "ShitpostGate", roundStartedAt }), "X post notification fallback rejected old quote media with fresh trigger");
            Assert(!(bool)isFallbackItem.Invoke(null, new object[] { wrongAuthor, "ShitpostGate", roundStartedAt }), "X post notification fallback accepted wrong author");
        }

        private static Tuple<List<XItem>, List<string>> ParseXItemsForSmoke(string json, bool includeRetweets, bool includeQuotes, bool useTimeBoundary, int headWindowSize)
        {
            return ParseXItemsForSmoke(new[] { json }, includeRetweets, includeQuotes, useTimeBoundary, headWindowSize);
        }

        private static Tuple<List<XItem>, List<string>> ParseXItemsForSmoke(IEnumerable<string> jsons, bool includeRetweets, bool includeQuotes, bool useTimeBoundary, int headWindowSize)
        {
            return ParseXItemsForSmoke(jsons, includeRetweets, includeQuotes, useTimeBoundary, headWindowSize, XTweetType.Original);
        }

        private static Tuple<List<XItem>, List<string>> ParseXItemsForSmoke(IEnumerable<string> jsons, bool includeRetweets, bool includeQuotes, bool useTimeBoundary, int headWindowSize, XTweetType rootTweetType)
        {
            var parserType = typeof(GlobalData).Assembly.GetType("AtelierMisaka.XTimelineParser");
            var parse = parserType.GetMethod("ParseItems", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var args = new object[] { jsons, false, false, false, 200, null, rootTweetType };
            args[1] = includeRetweets;
            args[2] = includeQuotes;
            args[3] = useTimeBoundary;
            args[4] = headWindowSize;
            var result = parse.Invoke(null, args);
            var items = ((System.Collections.IEnumerable)result).Cast<XItem>().ToList();
            var headIds = (List<string>)args[5];
            return Tuple.Create(items, headIds);
        }

        private static bool HasXItem(IEnumerable<XItem> items, string id)
        {
            return items.Any(x => x.ID == id);
        }

        private static List<XItem> SelectFreshXItems(IEnumerable<XItem> items, IEnumerable<string> seenIds, bool seedOnly)
        {
            if (seedOnly)
            {
                return new List<XItem>();
            }

            var seen = new HashSet<string>(seenIds ?? new string[0]);
            return items.Where(x => !seen.Contains(x.ID)).ToList();
        }

        private static XItem BuildXSmokeItem(string author, string id, XTweetType tweetType, DateTime createdAt)
        {
            var item = new XItem
            {
                AuthorScreenName = author,
                TweetType = tweetType,
                Title = author + "_smoke",
                Fee = "0"
            };
            item.PID = id;
            item.SetDates(createdAt);
            item.ContentUrls.Add("https://pbs.twimg.com/media/smoke.jpg?name=orig");
            item.FileNames.Add($"{createdAt:yyyyMMdd_HHmmss}_{id}_1.jpg");
            return item;
        }

        private static string BuildXTweetJson(string id, string author, string text, string mediaUrl)
        {
            return @"{""rest_id"":""" + id + @"""," +
                @"""legacy"":{""full_text"":""" + text + @""",""created_at"":""Thu Apr 30 11:00:00 +0000 2026""," +
                @"""extended_entities"":{""media"":[{""type"":""photo"",""media_url_https"":""" + mediaUrl + @"""}]}}," +
                @"""core"":{""user_results"":{""result"":{""legacy"":{""screen_name"":""" + author + @"""}}}}}";
        }

        private static void InitializeGlobals(string workDir)
        {
            GlobalData.VM_MA = new VM_Main();
            GlobalData.VM_DL = new VM_Download
            {
                DLClients = new List<DownloadItem>(),
                CompletedList = new ObservableCollection<DownloadItem>(),
                DownLoadItemList = new ObservableCollection<DownloadItem>(),
                ThreadCount = 2
            };
            GlobalData.SyContext = SynchronizationContext.Current ?? new ImmediateSynchronizationContext();
            GlobalData.DLLogs = new DownloadLogList();
            GlobalData.LastDateDic = new CLastDateDic();
            GlobalData.FantiaPollingCursorDic = new FantiaPollingCursorDic();
            GlobalData.XSeenCursorDic = new XSeenCursorDic();
            GlobalData.LastDateDic.Add(new CLastDate("fantia-smoke", SiteType.Fantia, DateTime.Now));
            GlobalData.LastDateDic.Add(new CLastDate("x-smoke", SiteType.X, DateTime.Now));
            GlobalData.VM_MA.CheckFile = false;
            GlobalData.VM_MA.ShowLoad = false;
            GlobalData.VM_MA.SavePath = workDir;
        }

        private static void AddFantiaItem(string url, string workDir)
        {
            GlobalData.VM_MA.Site = SiteType.Fantia;
            GlobalData.VM_MA.IDName = "fantia-login";
            GlobalData.VM_MA.Cookies = "_session_id=fantia-cookie";
            GlobalData.VM_MA.SavePath = workDir;
            GlobalData.VM_MA.UseProxy = false;
            GlobalData.VM_MA.Artist = new ArtistInfo
            {
                Id = "fantia-smoke",
                Cid = "fantia-smoke",
                AName = "FantiaSmoke",
                PayLow = "0",
                PayHigh = "-1",
                PostUrl = "https://fantia.jp/fanclubs/fantia-smoke"
            };
            GlobalData.VM_DL.TempAI = "fantia-smoke";
            GlobalData.VM_DL.TempAN = "FantiaSmoke";
            GlobalData.VM_DL.TempSite = SiteType.Fantia;
            GlobalData.VM_DL.SavePath = workDir;
            GlobalData.VM_DL.IsFantia = true;
            GlobalData.DLLogs.LoadData("fantia-smoke", SiteType.Fantia);

            var item = new FantiaItem
            {
                Title = "Fantia Smoke",
                CreateDate = DateTime.Now
            };
            item.FID = "fantia-post-1";
            item.ContentUrls.Add(url);
            item.FileNames.Add("fantia.bin");
            item.Fees.Add("0");
            item.PTitles.Add("content");
            GlobalData.VM_DL.AddFantiaCommand.Execute(item);
        }

        private static void AddXItem(string url, string workDir)
        {
            GlobalData.VM_MA.Site = SiteType.X;
            GlobalData.VM_MA.IDName = "x-login";
            GlobalData.VM_MA.Cookies = "auth_token=x-cookie; ct0=x-csrf";
            GlobalData.VM_MA.SavePath = workDir;
            GlobalData.VM_MA.UseProxy = false;
            GlobalData.VM_MA.Artist = new ArtistInfo
            {
                Id = "x_smoke_login",
                Cid = "x_smoke_login",
                AName = "x_smoke_login",
                PayLow = "0",
                PayHigh = "-1",
                PostUrl = "https://x.com/x_smoke_login"
            };
            GlobalData.VM_DL.TempAI = "x_smoke_login";
            GlobalData.VM_DL.TempAN = "x_smoke_login";
            GlobalData.VM_DL.TempSite = SiteType.X;
            GlobalData.VM_DL.SavePath = workDir;
            GlobalData.VM_DL.IsFantia = false;
            GlobalData.DLLogs.LoadData("x_smoke_login", SiteType.X);

            var item = new XItem
            {
                AuthorScreenName = "ShitpostGate",
                Title = "ShitpostGate Smoke"
            };
            item.PID = "x-post-1";
            item.SetDates(DateTime.Now);
            item.ContentUrls.Add(url);
            item.FileNames.Add("x.bin");
            GlobalData.VM_DL.AddXCommand.Execute(item);
        }

        private static async Task RunXOnlyDownloadSmokeAsync(LocalHttpServer server, string workDir)
        {
            Directory.CreateDirectory(workDir);
            InitializeGlobals(workDir);
            AddXItem(server.Url("x-only.bin"), workDir);
            Assert(GlobalData.VM_DL.DownLoadItemList.Count == 1, "expected 1 queued X-only download item");

            await WaitForDownloadsAsync(TimeSpan.FromSeconds(15), 1);

            var xDownload = GlobalData.VM_DL.CompletedList.FirstOrDefault(x => x.Site == SiteType.X);
            Assert(xDownload != null, "missing completed X-only download item");
            Assert(File.Exists(Path.Combine(xDownload.SavePath, xDownload.FileName)), "X-only file was not written");
            Assert(xDownload.SavePath.Contains("ShitpostGate"), "X-only download path did not use media author");
            var xLogs = SQLiteDownloadLogHelper.Instance.GetLogs("ShitpostGate", SiteType.X);
            Assert(xLogs.Any(x => x.PId == "x-post-1" && x.Site == SiteType.X), "missing X-only download log for media author");
        }

        private static async Task WaitForDownloadsAsync(TimeSpan timeout, int expectedCount = 2)
        {
            var end = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < end)
            {
                var downloads = GlobalData.VM_DL.DownLoadItemList.Concat(GlobalData.VM_DL.CompletedList).ToList();
                if (downloads.Count >= expectedCount && downloads.All(x => x.DLStatus == DownloadStatus.Completed))
                {
                    return;
                }
                if (downloads.Any(x => x.DLStatus == DownloadStatus.Error))
                {
                    throw new InvalidOperationException("download item entered Error state");
                }
                await Task.Delay(100);
            }
            throw new TimeoutException("downloads did not complete in time");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class ImmediateSynchronizationContext : SynchronizationContext
        {
            public override void Send(SendOrPostCallback d, object state)
            {
                d(state);
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                d(state);
            }
        }

        private sealed class LocalHttpServer : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly Task _acceptTask;
            private readonly byte[] _payload = Encoding.ASCII.GetBytes("atelier-misaka-smoke");

            public LocalHttpServer()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _acceptTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
            }

            public int Port { get; }

            public string Url(string name)
            {
                return $"http://127.0.0.1:{Port}/{name}";
            }

            public void Dispose()
            {
                _cts.Cancel();
                _listener.Stop();
                try
                {
                    _acceptTask.Wait(1000);
                }
                catch { }
                _cts.Dispose();
            }

            private async Task AcceptLoopAsync(CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    TcpClient client = null;
                    try
                    {
                        client = await _listener.AcceptTcpClientAsync();
                        _ = Task.Run(() => HandleClientAsync(client));
                    }
                    catch
                    {
                        client?.Dispose();
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
            }

            private async Task HandleClientAsync(TcpClient client)
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[4096];
                    await stream.ReadAsync(buffer, 0, buffer.Length);
                    var header = Encoding.ASCII.GetBytes(
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: application/octet-stream\r\n" +
                        "Content-Length: " + _payload.Length + "\r\n" +
                        "Connection: close\r\n\r\n");
                    await stream.WriteAsync(header, 0, header.Length);
                    await stream.WriteAsync(_payload, 0, _payload.Length);
                }
            }
        }
    }
}
