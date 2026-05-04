using AtelierMisaka.Models;
using CefSharp;
using CefSharp.Handler;
using CefSharp.Wpf;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AtelierMisaka
{
    public class XUtils : BaseUtils
    {
        public const int TimelineWindowSize = 100;
        public const int NotificationWindowSize = 200;
        private const int SearchMaxSlices = 36;
        private const string NotificationDebugTarget = "ShitpostGate";
        private static readonly TimeSpan PostNotificationFallbackLookback = TimeSpan.FromHours(2);

        private readonly Regex _userUrl = new Regex(@"^(?:https?://(?:www\.)?(?:x|twitter)\.com/)?@?([^/?#]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly ConcurrentQueue<XCapturedResponse> _apiData = new ConcurrentQueue<XCapturedResponse>();
        private readonly SemaphoreSlim _cefGate = new SemaphoreSlim(1, 1);
        private ChromiumWebBrowser _cwb;

        public List<string> LastScanHeadIds { get; private set; } = new List<string>();
        public string LastCursorKey { get; private set; } = string.Empty;

        public async override Task<ResultMessage> CheckCookies()
        {
            try
            {
                EnsureBrowser();
                var cookies = await _cwb.GetCookieManager().VisitUrlCookiesAsync("https://x.com", true);
                if (cookies == null || cookies.Find(x => x.Name == "auth_token") == null)
                {
                    return ResultHelper.NoError(null);
                }

                GlobalData.VM_MA.Cookies = string.Join("; ", cookies.Select(x => $"{x.Name}={x.Value}"));

                var jsons = await CaptureJsonAsync("https://x.com/home", 6000);
                if (XTimelineParser.TryExtractViewerScreenName(jsons, out string screenName) &&
                    !string.Equals(NormalizeScreenName(screenName), NormalizeScreenName(GlobalData.VM_MA.IDName), StringComparison.OrdinalIgnoreCase))
                {
                    return ResultHelper.NoError(false);
                }

                return ResultHelper.NoError(true);
            }
            catch (Exception ex)
            {
                GlobalMethord.ErrorLog(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine + "-----------------------------------------------");
                return ResultHelper.UnKnownError();
            }
        }

        public async override Task<ResultMessage> GetArtistInfo(string url)
        {
            try
            {
                var screenName = ParseScreenName(url);
                if (string.IsNullOrEmpty(screenName))
                {
                    return ResultHelper.PathError();
                }

                string userId = screenName;
                string displayName = screenName;
                var jsons = await CaptureJsonAsync($"https://x.com/{screenName}", 7000);
                XTimelineParser.TryExtractUserInfo(jsons, screenName, out userId, out displayName);

                var ai = new ArtistInfo()
                {
                    Id = userId,
                    Cid = screenName,
                    AName = GlobalMethord.RemoveLastDot(GlobalMethord.ReplacePath(displayName)),
                    PostUrl = $"https://x.com/{screenName}",
                    PayLow = GlobalData.VM_MA.Artist.PayLow,
                    PayHigh = GlobalData.VM_MA.Artist.PayHigh
                };
                return ResultHelper.NoError(ai);
            }
            catch (Exception ex)
            {
                GlobalMethord.ErrorLog(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine + "-----------------------------------------------");
                return ResultHelper.UnKnownError();
            }
        }

        public async override Task<ResultMessage> GetArtistList()
        {
            await Task.CompletedTask;
            return ResultHelper.NoError(GlobalData.VM_MA.ArtistList.ToList());
        }

        public override bool GetCover(BaseItem bi)
        {
            return false;
        }

        public async override Task<ResultMessage> GetPostIDs(string uid)
        {
            try
            {
                if (GlobalData.VM_MA.XNotificationMode)
                {
                    return await GetNotificationItems();
                }
                if (GlobalData.VM_MA.XHistorySearchMode)
                {
                    return await GetSearchItems(uid);
                }
                return await GetTimelineItems(uid);
            }
            catch (Exception ex)
            {
                GlobalMethord.ErrorLog(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine + "-----------------------------------------------");
                return ResultHelper.UnKnownError();
            }
        }

        public async override Task<ResultMessage> LikePost(string pid, string cid)
        {
            await Task.CompletedTask;
            return ResultHelper.UnKnownError(GlobalLanguage.Msg_ErrorUnSupported);
        }

        private async Task<ResultMessage> GetTimelineItems(string uid)
        {
            var screenName = string.IsNullOrEmpty(uid) ? ParseScreenName(GlobalData.VM_MA.Artist.PostUrl) : uid;
            if (string.IsNullOrEmpty(screenName))
            {
                return ResultHelper.PathError();
            }

            LastCursorKey = $"Timeline:{NormalizeScreenName(screenName)}";
            var jsons = await CaptureJsonAsync($"https://x.com/{screenName}/media", 10000);
            if (ShouldCaptureUserTimelineForRelatedMedia())
            {
                jsons.AddRange(await CaptureJsonAsync($"https://x.com/{screenName}", 10000, 2));
            }

            var items = XTimelineParser.ParseItems(jsons, GlobalData.VM_MA.XIncludeRetweetMedia, GlobalData.VM_MA.XIncludeQuoteMedia, true, TimelineWindowSize, out List<string> headIds);
            items = items.Where(x =>
                    x.TweetType != XTweetType.Original ||
                    string.Equals(NormalizeScreenName(x.AuthorScreenName), NormalizeScreenName(screenName), StringComparison.OrdinalIgnoreCase))
                .ToList();
            LastScanHeadIds = headIds;
            return ResultHelper.NoError(items);
        }

        private async Task<ResultMessage> GetSearchItems(string uid)
        {
            var screenName = string.IsNullOrEmpty(uid) ? ParseScreenName(GlobalData.VM_MA.Artist.PostUrl) : uid;
            if (string.IsNullOrEmpty(screenName) || !TryGetSearchDateRange(out DateTime start, out DateTime end))
            {
                return ResultHelper.PathError();
            }

            LastCursorKey = string.Empty;
            var jsons = new List<string>();
            var retweetJsons = new List<string>();
            var queries = new List<string>();
            var endExclusive = end.Date.AddDays(1);
            foreach (var slice in BuildSearchSlices(start.Date, endExclusive))
            {
                var includeRelatedMedia = GlobalData.VM_MA.XIncludeRetweetMedia || GlobalData.VM_MA.XIncludeQuoteMedia;
                var query = BuildSearchQuery(screenName, slice.Item1, slice.Item2, includeRelatedMedia);
                queries.Add(query);
                jsons.AddRange(await CaptureJsonAsync(BuildSearchUrl(query), 12000, 4));

                if (GlobalData.VM_MA.XIncludeRetweetMedia)
                {
                    var retweetInclusiveQuery = BuildRetweetInclusiveSearchQuery(screenName, slice.Item1, slice.Item2);
                    queries.Add(retweetInclusiveQuery);
                    jsons.AddRange(await CaptureJsonAsync(BuildSearchUrl(retweetInclusiveQuery), 12000, 4));
                }
            }

            if (GlobalData.VM_MA.XIncludeRetweetMedia)
            {
                queries.Add($"profile:{screenName}");
                jsons.AddRange(await CaptureJsonAsync($"https://x.com/{screenName}", 10000, 3));
            }

            var parsedItems = XTimelineParser.ParseItems(jsons, GlobalData.VM_MA.XIncludeRetweetMedia, GlobalData.VM_MA.XIncludeQuoteMedia, true, TimelineWindowSize, out List<string> headIds);
            var retweetItems = XTimelineParser.ParseItems(retweetJsons, true, GlobalData.VM_MA.XIncludeQuoteMedia, true, TimelineWindowSize, out List<string> retweetHeadIds, XTweetType.Retweet);
            parsedItems = parsedItems
                .Concat(retweetItems)
                .GroupBy(x => x.ID)
                .Select(x => x.First())
                .ToList();
            headIds.AddRange(retweetHeadIds);
            headIds = headIds.Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(TimelineWindowSize).ToList();
            var items = parsedItems.Where(x =>
                    x.TweetType != XTweetType.Original ||
                    string.Equals(NormalizeScreenName(x.AuthorScreenName), NormalizeScreenName(screenName), StringComparison.OrdinalIgnoreCase))
                .ToList();
            LastScanHeadIds = headIds;
            LogSearchDebug(screenName, queries, jsons, retweetJsons, headIds, parsedItems, items, retweetItems);
            return ResultHelper.NoError(items);
        }

        private async Task<ResultMessage> GetNotificationItems()
        {
            var roundStartedAt = DateTime.Now;
            var loginName = NormalizeScreenName(GlobalData.VM_MA.IDName);
            LastCursorKey = $"Notifications:{loginName}";

            var seenIds = new List<string>();
            var hasCursor = GlobalData.XSeenCursorDic != null && GlobalData.XSeenCursorDic.TryGetWindow(LastCursorKey, NotificationWindowSize, out seenIds);
            if (!hasCursor)
            {
                seenIds = new List<string>();
            }

            var seedOnly = GlobalData.ForceXNotificationSeed || !hasCursor;
            GlobalData.ForceXNotificationSeed = false;

            var captured = await CaptureResponsesAsync("https://x.com/notifications", 10000, 2, IsNotificationTimelineResponse, false);
            var notificationJsons = captured
                .Select(x => x.Body)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            var items = XTimelineParser.ParseItems(notificationJsons, GlobalData.VM_MA.XIncludeRetweetMedia, GlobalData.VM_MA.XIncludeQuoteMedia, false, NotificationWindowSize, out List<string> headIds);
            var notificationTweetIds = ExtractNotificationTweetIds(notificationJsons, NotificationWindowSize);
            var postNotificationUsers = ExtractPostNotificationScreenNames(notificationJsons, 10);
            var seen = new HashSet<string>(seenIds);
            var knownItemIds = new HashSet<string>(items.Select(x => x.ID));
            LogNotificationDebug("parsed", captured, notificationJsons, notificationTweetIds, postNotificationUsers, headIds, items, seenIds, seedOnly, null);

            if (!seedOnly)
            {
                foreach (var tweetId in notificationTweetIds.Where(x => !seen.Contains(x) && !knownItemIds.Contains(x)).Take(10))
                {
                    var detailJsons = await CaptureJsonAsync($"https://x.com/i/web/status/{tweetId}", 8000, 1, IsTweetDetailRelatedResponse);
                    var detailItems = XTimelineParser.ParseItems(detailJsons, GlobalData.VM_MA.XIncludeRetweetMedia, GlobalData.VM_MA.XIncludeQuoteMedia, false, NotificationWindowSize, out List<string> detailHeadIds);
                    LogTweetDetailDebug(tweetId, detailJsons, detailHeadIds, detailItems);
                    headIds.AddRange(detailHeadIds);
                    foreach (var detailItem in detailItems.Where(x => x.ID == tweetId || x.TweetType != XTweetType.Original))
                    {
                        if (knownItemIds.Add(detailItem.ID))
                        {
                            items.Add(detailItem);
                        }
                    }
                }
            }

            foreach (var screenName in postNotificationUsers.Where(x => !string.Equals(x, loginName, StringComparison.OrdinalIgnoreCase)).Take(6))
            {
                var timelineJsons = await CaptureJsonAsync($"https://x.com/{screenName}/media", 9000, 1);
                if (ShouldCaptureUserTimelineForRelatedMedia())
                {
                    timelineJsons.AddRange(await CaptureJsonAsync($"https://x.com/{screenName}", 9000, 2));
                }

                var timelineItems = XTimelineParser.ParseItems(timelineJsons, GlobalData.VM_MA.XIncludeRetweetMedia, GlobalData.VM_MA.XIncludeQuoteMedia, false, TimelineWindowSize, out List<string> timelineHeadIds)
                    .Where(x => IsPostNotificationFallbackItem(x, screenName, roundStartedAt))
                    .OrderByDescending(x => x.CreateDate)
                    .Take(5)
                    .ToList();
                headIds.AddRange(timelineHeadIds);
                LogPostNotificationUserDebug(screenName, timelineJsons, timelineHeadIds, timelineItems);

                if (seedOnly)
                {
                    continue;
                }

                foreach (var timelineItem in timelineItems)
                {
                    if (knownItemIds.Add(timelineItem.ID))
                    {
                        items.Add(timelineItem);
                    }
                }
            }

            LastScanHeadIds = notificationTweetIds.Concat(headIds).Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(NotificationWindowSize).ToList();

            if (seedOnly)
            {
                CefDiagnostics.Write("XNotification", $"captured={captured.Count} notificationJsons={notificationJsons.Count} notificationIds={notificationTweetIds.Count} headIds={LastScanHeadIds.Count} items={items.Count} fresh=0 seedOnly=True");
                return ResultHelper.NoError(new List<BaseItem>());
            }

            var fresh = items.Where(x => !seen.Contains(x.ID)).Cast<BaseItem>().ToList();
            LogNotificationDebug("fresh", captured, notificationJsons, notificationTweetIds, postNotificationUsers, headIds, items, seenIds, seedOnly, fresh.OfType<XItem>().ToList());
            CefDiagnostics.Write("XNotification", $"captured={captured.Count} notificationJsons={notificationJsons.Count} notificationIds={notificationTweetIds.Count} headIds={LastScanHeadIds.Count} items={items.Count} fresh={fresh.Count} seedOnly=False");
            return ResultHelper.NoError(fresh);
        }

        private static bool ShouldCaptureUserTimelineForRelatedMedia()
        {
            return GlobalData.VM_MA.XIncludeRetweetMedia || GlobalData.VM_MA.XIncludeQuoteMedia;
        }

        private async Task<List<string>> CaptureJsonAsync(string url, int waitMs, int scrollCount = 2, Func<XCapturedResponse, bool> includeResponse = null)
        {
            var responses = await CaptureResponsesAsync(url, waitMs, scrollCount, includeResponse, false);
            return responses.Select(x => x.Body).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        private async Task<List<XCapturedResponse>> CaptureResponsesAsync(string url, int waitMs, int scrollCount = 2, Func<XCapturedResponse, bool> includeResponse = null, bool keepMatchingBacklog = false)
        {
            await _cefGate.WaitAsync();
            try
            {
                EnsureBrowser();
                var result = new List<XCapturedResponse>();
                DrainQueue(keepMatchingBacklog ? result : null, includeResponse);
                if (!(_cwb.RequestHandler is XRequestHandler))
                {
                    _cwb.RequestHandler = new XRequestHandler(_apiData);
                }

                _cwb.Load(url);
                var waited = 0;
                var scrolls = 0;
                var nextScrollAt = scrollCount > 0 ? Math.Max(1000, waitMs / (scrollCount + 1)) : waitMs + 1;
                while (waited < waitMs)
                {
                    await Task.Delay(250);
                    waited += 250;
                    if (scrolls < scrollCount && waited >= nextScrollAt)
                    {
                        try
                        {
                            await _cwb.EvaluateScriptAsync("window.scrollTo(0, document.body.scrollHeight);");
                        }
                        catch { }
                        scrolls++;
                        nextScrollAt += Math.Max(1000, waitMs / (scrollCount + 1));
                    }
                }

                DrainQueue(result, includeResponse);
                return result;
            }
            finally
            {
                _cefGate.Release();
            }
        }

        private bool TryGetSearchDateRange(out DateTime start, out DateTime end)
        {
            start = GlobalData.VM_MA.LastDate;
            end = GlobalData.VM_MA.LastDate_End;
            return GlobalData.VM_MA.UseDate &&
                !string.IsNullOrWhiteSpace(GlobalData.VM_MA.Date_Start) &&
                end >= start;
        }

        private IEnumerable<Tuple<DateTime, DateTime>> BuildSearchSlices(DateTime start, DateTime endExclusive)
        {
            if (endExclusive <= start)
            {
                endExclusive = start.AddDays(1);
            }

            var totalMonths = Math.Max(1, ((endExclusive.Year - start.Year) * 12) + endExclusive.Month - start.Month + 1);
            var stepMonths = Math.Max(1, (int)Math.Ceiling(totalMonths / (double)SearchMaxSlices));
            var current = start;
            while (current < endExclusive)
            {
                var next = new DateTime(current.Year, current.Month, 1).AddMonths(stepMonths);
                if (next <= current)
                {
                    next = current.AddDays(1);
                }
                if (next > endExclusive)
                {
                    next = endExclusive;
                }
                yield return Tuple.Create(current, next);
                current = next;
            }
        }

        private string BuildSearchUrl(string query)
        {
            return $"https://x.com/search?q={Uri.EscapeDataString(query)}&src=typed_query&f=live";
        }

        private static string BuildSearchQuery(string screenName, DateTime start, DateTime endExclusive, bool includeRelatedMedia)
        {
            var query = $"from:{screenName} since:{start:yyyy-MM-dd} until:{endExclusive:yyyy-MM-dd}";
            if (!includeRelatedMedia)
            {
                query += " filter:media";
            }
            return query;
        }

        private static string BuildRetweetInclusiveSearchQuery(string screenName, DateTime start, DateTime endExclusive)
        {
            return $"from:{screenName} since:{start:yyyy-MM-dd} until:{endExclusive:yyyy-MM-dd} include:nativeretweets";
        }

        private void EnsureBrowser()
        {
            if (_cwb != null)
            {
                return;
            }
            _cwb = GlobalData.VM_MA.XCefBrowser as ChromiumWebBrowser ?? GlobalData.VM_MA.PatreonCefBrowser as ChromiumWebBrowser;
            if (_cwb == null)
            {
                throw new InvalidOperationException("X browser is not initialized.");
            }
        }

        private void DrainQueue(List<XCapturedResponse> result = null, Func<XCapturedResponse, bool> includeResponse = null)
        {
            while (_apiData.TryDequeue(out XCapturedResponse data))
            {
                if (result != null && (includeResponse == null || includeResponse(data)))
                {
                    result.Add(data);
                }
            }
        }

        private static bool IsNotificationTimelineResponse(XCapturedResponse response)
        {
            if (response == null || string.IsNullOrEmpty(response.Url))
            {
                return false;
            }
            var url = response.Url.ToLowerInvariant();
            return url.Contains("notificationstimeline");
        }

        private static bool IsTweetDetailRelatedResponse(XCapturedResponse response)
        {
            if (response == null || string.IsNullOrEmpty(response.Url))
            {
                return false;
            }
            var url = response.Url.ToLowerInvariant();
            return url.Contains("/i/api/graphql/") &&
                   !url.Contains("home") &&
                   !url.Contains("notificationstimeline");
        }

        private static List<string> ExtractNotificationTweetIds(IEnumerable<string> jsons, int headWindowSize)
        {
            var result = new List<string>();
            if (jsons == null)
            {
                return result;
            }

            foreach (var json in jsons.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                try
                {
                    CollectNotificationTweetIdsFromText(json, result);
                    CollectNotificationTweetIds(JToken.Parse(json), result, string.Empty);
                }
                catch { }
            }
            return result.Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(headWindowSize).ToList();
        }

        private static List<string> ExtractPostNotificationScreenNames(IEnumerable<string> jsons, int maxCount)
        {
            var result = new List<string>();
            if (jsons == null)
            {
                return result;
            }

            foreach (var json in jsons.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                try
                {
                    foreach (var obj in WalkJson(JToken.Parse(json)).OfType<JObject>())
                    {
                        var text = obj.SelectToken("rich_message.text")?.Value<string>() ??
                                   obj.SelectToken("message.text")?.Value<string>() ??
                                   obj.SelectToken("text")?.Value<string>() ??
                                   string.Empty;
                        if (text.IndexOf("New post notifications", StringComparison.OrdinalIgnoreCase) < 0 &&
                            text.IndexOf("New posts from", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        CollectScreenNames(obj, result);
                    }
                }
                catch { }
            }

            return result.Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(maxCount).ToList();
        }

        private static void CollectScreenNames(JToken token, List<string> result)
        {
            foreach (var item in token.SelectTokens("$..core.screen_name").Concat(token.SelectTokens("$..legacy.screen_name")))
            {
                var screenName = item?.Value<string>();
                if (!string.IsNullOrWhiteSpace(screenName))
                {
                    result.Add(screenName.Trim().TrimStart('@'));
                }
            }
        }

        private static bool IsPostNotificationFallbackItem(XItem item, string screenName, DateTime roundStartedAt)
        {
            if (item == null || item.ContentUrls == null || item.ContentUrls.Count == 0)
            {
                return false;
            }

            if (item.TweetType == XTweetType.Original &&
                !string.Equals(NormalizeScreenNameStatic(item.AuthorScreenName), NormalizeScreenNameStatic(screenName), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (item.TweetType != XTweetType.Original &&
                !string.IsNullOrEmpty(item.TriggerAuthorScreenName) &&
                item.TriggerDate.HasValue)
            {
                return string.Equals(NormalizeScreenNameStatic(item.TriggerAuthorScreenName), NormalizeScreenNameStatic(screenName), StringComparison.OrdinalIgnoreCase) &&
                       item.TriggerDate.Value >= roundStartedAt.Subtract(PostNotificationFallbackLookback);
            }

            return item.CreateDate >= roundStartedAt.Subtract(PostNotificationFallbackLookback);
        }

        private static string NormalizeScreenNameStatic(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            return value.Trim().TrimStart('@');
        }

        private static IEnumerable<JToken> WalkJson(JToken token)
        {
            if (token == null)
            {
                yield break;
            }

            yield return token;
            foreach (var child in token.Children())
            {
                foreach (var item in WalkJson(child))
                {
                    yield return item;
                }
            }
        }

        private static void LogSearchDebug(string screenName, IList<string> queries, IList<string> jsons, IList<string> retweetJsons, IList<string> headIds, IList<XItem> parsedItems, IList<XItem> filteredItems, IList<XItem> retweetItems)
        {
            try
            {
                var responseBytes = string.Join(",", (jsons ?? new List<string>()).Select(x => x?.Length ?? 0).Take(20));
                var retweetBytes = string.Join(",", (retweetJsons ?? new List<string>()).Select(x => x?.Length ?? 0).Take(20));
                CefDiagnostics.Write("XSearchDebug",
                    $"screenName={SafeLog(screenName)} includeRT={GlobalData.VM_MA?.XIncludeRetweetMedia} includeQuote={GlobalData.VM_MA?.XIncludeQuoteMedia} " +
                    $"queries={queries?.Count ?? 0} jsons={jsons?.Count ?? 0} retweetJsons={retweetJsons?.Count ?? 0} bytes=[{responseBytes}] retweetBytes=[{retweetBytes}] headIds=[{string.Join(",", (headIds ?? new List<string>()).Take(20))}] " +
                    $"parsed={parsedItems?.Count ?? 0} retweets={retweetItems?.Count ?? 0} filtered={filteredItems?.Count ?? 0} retweetItems=[{SummarizeXItems(retweetItems)}] parsedItems=[{SummarizeXItems(parsedItems)}] filteredItems=[{SummarizeXItems(filteredItems)}] mediaDebug=[{SummarizeTweetMediaDebug(jsons, string.Empty)}] retweetMediaDebug=[{SummarizeTweetMediaDebug(retweetJsons, string.Empty)}]");

                foreach (var query in (queries ?? new List<string>()).Take(8))
                {
                    CefDiagnostics.Write("XSearchDebug", $"query={SafeLog(query, 240)}");
                }
            }
            catch (Exception ex)
            {
                CefDiagnostics.Write("XSearchDebug", $"log-error {SafeLog(ex.Message)}");
            }
        }

        private static void LogNotificationDebug(string stage, IList<XCapturedResponse> captured, IList<string> notificationJsons, IList<string> notificationTweetIds, IList<string> postNotificationUsers, IList<string> headIds, IList<XItem> items, IList<string> seenIds, bool seedOnly, IList<XItem> fresh)
        {
            try
            {
                var targetHits = CountTargetHits(notificationJsons, NotificationDebugTarget);
                var containsTarget = targetHits > 0;
                var candidateIds = string.Join(",", (notificationTweetIds ?? new List<string>()).Take(20));
                var seenMatches = string.Join(",", (notificationTweetIds ?? new List<string>()).Where(x => seenIds != null && seenIds.Contains(x)).Take(20));
                var parsedItems = SummarizeXItems(items);
                var freshItems = SummarizeXItems(fresh);
                var responseBytes = string.Join(",", (captured ?? new List<XCapturedResponse>()).Select(x => x?.Body?.Length ?? 0).Take(10));

                CefDiagnostics.Write("XNotificationDebug",
                    $"stage={stage} login={SafeLog(GlobalData.VM_MA?.IDName)} seedOnly={seedOnly} includeRT={GlobalData.VM_MA?.XIncludeRetweetMedia} includeQuote={GlobalData.VM_MA?.XIncludeQuoteMedia} " +
                    $"captured={captured?.Count ?? 0} jsons={notificationJsons?.Count ?? 0} bytes=[{responseBytes}] target={NotificationDebugTarget} targetHits={targetHits} containsTarget={containsTarget} " +
                    $"candidateIds=[{candidateIds}] postUsers=[{string.Join(",", (postNotificationUsers ?? new List<string>()).Take(20))}] seenCandidateIds=[{seenMatches}] headIds=[{string.Join(",", (headIds ?? new List<string>()).Take(20))}] parsedItems=[{parsedItems}] freshItems=[{freshItems}]");

                if (containsTarget)
                {
                    foreach (var snippet in ExtractTargetSnippets(notificationJsons, NotificationDebugTarget, 3, 360))
                    {
                        CefDiagnostics.Write("XNotificationDebug", $"targetSnippet={snippet}");
                    }
                }
                else if ((notificationTweetIds == null || notificationTweetIds.Count == 0) && notificationJsons != null && notificationJsons.Count > 0)
                {
                    CefDiagnostics.Write("XNotificationDebug", $"noCandidateIds jsonShape={SummarizeJsonShape(notificationJsons)}");
                }
            }
            catch (Exception ex)
            {
                CefDiagnostics.Write("XNotificationDebug", $"log-error {SafeLog(ex.Message)}");
            }
        }

        private static void LogPostNotificationUserDebug(string screenName, IList<string> timelineJsons, IList<string> timelineHeadIds, IList<XItem> timelineItems)
        {
            try
            {
                CefDiagnostics.Write("XNotificationDebug",
                    $"postUser screenName={SafeLog(screenName)} jsons={timelineJsons?.Count ?? 0} targetHits={CountTargetHits(timelineJsons, NotificationDebugTarget)} " +
                    $"headIds=[{string.Join(",", (timelineHeadIds ?? new List<string>()).Take(20))}] items=[{SummarizeXItems(timelineItems)}] mediaDebug=[{SummarizeTweetMediaDebug(timelineJsons, screenName)}]");
            }
            catch (Exception ex)
            {
                CefDiagnostics.Write("XNotificationDebug", $"postUser-log-error screenName={SafeLog(screenName)} {SafeLog(ex.Message)}");
            }
        }

        private static void LogTweetDetailDebug(string tweetId, IList<string> detailJsons, IList<string> detailHeadIds, IList<XItem> detailItems)
        {
            try
            {
                CefDiagnostics.Write("XNotificationDebug",
                    $"detail tweetId={SafeLog(tweetId)} jsons={detailJsons?.Count ?? 0} targetHits={CountTargetHits(detailJsons, NotificationDebugTarget)} " +
                    $"headIds=[{string.Join(",", (detailHeadIds ?? new List<string>()).Take(20))}] items=[{SummarizeXItems(detailItems)}]");

                foreach (var snippet in ExtractTargetSnippets(detailJsons, NotificationDebugTarget, 2, 360))
                {
                    CefDiagnostics.Write("XNotificationDebug", $"detailTargetSnippet tweetId={SafeLog(tweetId)} snippet={snippet}");
                }
            }
            catch (Exception ex)
            {
                CefDiagnostics.Write("XNotificationDebug", $"detail-log-error tweetId={SafeLog(tweetId)} {SafeLog(ex.Message)}");
            }
        }

        private static int CountTargetHits(IEnumerable<string> jsons, string target)
        {
            if (jsons == null || string.IsNullOrEmpty(target))
            {
                return 0;
            }

            return jsons.Sum(json =>
            {
                if (string.IsNullOrEmpty(json))
                {
                    return 0;
                }
                var count = 0;
                var index = 0;
                while ((index = json.IndexOf(target, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    count++;
                    index += target.Length;
                }
                return count;
            });
        }

        private static IEnumerable<string> ExtractTargetSnippets(IEnumerable<string> jsons, string target, int maxCount, int radius)
        {
            if (jsons == null || string.IsNullOrEmpty(target))
            {
                yield break;
            }

            var emitted = 0;
            foreach (var json in jsons.Where(x => !string.IsNullOrEmpty(x)))
            {
                var index = json.IndexOf(target, StringComparison.OrdinalIgnoreCase);
                while (index >= 0 && emitted < maxCount)
                {
                    var start = Math.Max(0, index - radius);
                    var length = Math.Min(json.Length - start, radius * 2 + target.Length);
                    yield return SafeLog(json.Substring(start, length));
                    emitted++;
                    index = json.IndexOf(target, index + target.Length, StringComparison.OrdinalIgnoreCase);
                }

                if (emitted >= maxCount)
                {
                    yield break;
                }
            }
        }

        private static string SummarizeXItems(IEnumerable<XItem> items)
        {
            if (items == null)
            {
                return string.Empty;
            }

            return string.Join("; ", items.Take(10).Select(x =>
                $"{SafeLog(x.AuthorScreenName)}:{SafeLog(x.ID)}:{x.TweetType}:trigger={SafeLog(x.TriggerAuthorScreenName)}:{x.TriggerDate?.ToString("HH:mm:ss")}:media={x.ContentUrls?.Count ?? 0}:title={SafeLog(x.Title, 80)}"));
        }

        private static string SummarizeTweetMediaDebug(IEnumerable<string> jsons, string screenName)
        {
            if (jsons == null)
            {
                return string.Empty;
            }

            var result = new List<string>();
            foreach (var json in jsons.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                try
                {
                    foreach (var obj in WalkJson(JToken.Parse(json)).OfType<JObject>())
                    {
                        var legacy = obj["legacy"] as JObject;
                        if (legacy?["full_text"] == null)
                        {
                            continue;
                        }

                        var id = obj["rest_id"]?.Value<string>() ?? legacy["id_str"]?.Value<string>() ?? obj["id_str"]?.Value<string>();
                        if (string.IsNullOrEmpty(id))
                        {
                            continue;
                        }

                        var author = obj.SelectToken("core.user_results.result.legacy.screen_name")?.Value<string>() ??
                                     obj.SelectToken("core.user_results.result.core.screen_name")?.Value<string>() ??
                                     obj.SelectToken("legacy.user.screen_name")?.Value<string>() ??
                                     string.Empty;
                        if (!string.IsNullOrEmpty(screenName) &&
                            !string.IsNullOrEmpty(author) &&
                            !string.Equals(NormalizeScreenNameStatic(author), NormalizeScreenNameStatic(screenName), StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var mediaArray = legacy.SelectToken("extended_entities.media") as JArray ?? legacy.SelectToken("entities.media") as JArray;
                        var mediaCount = mediaArray?.Count ?? 0;
                        var firstMedia = mediaArray?.OfType<JObject>().FirstOrDefault();
                        var nested = firstMedia?.SelectToken("media_results.result") as JObject;
                        var firstKeys = firstMedia == null ? string.Empty : string.Join("/", firstMedia.Properties().Select(x => x.Name).Take(12));
                        var nestedKeys = nested == null ? string.Empty : string.Join("/", nested.Properties().Select(x => x.Name).Take(12));
                        var url = firstMedia?["media_url_https"]?.Value<string>() ??
                                  firstMedia?.SelectToken("media_results.result.media_url_https")?.Value<string>() ??
                                  firstMedia?.SelectToken("media_results.result.legacy.media_url_https")?.Value<string>() ??
                                  string.Empty;
                        var legacyKeys = string.Join("/", legacy.Properties().Select(x => x.Name).Take(24));
                        var anyMediaUrls = WalkJson(obj).OfType<JProperty>().Count(x => x.Name.IndexOf("media_url", StringComparison.OrdinalIgnoreCase) >= 0);
                        var anyVideoInfo = WalkJson(obj).OfType<JProperty>().Count(x => x.Name.IndexOf("video_info", StringComparison.OrdinalIgnoreCase) >= 0);
                        var anyCards = WalkJson(obj).OfType<JProperty>().Count(x => x.Name.IndexOf("card", StringComparison.OrdinalIgnoreCase) >= 0);
                        var relatedKeys = string.Join("/", obj.Properties()
                            .Select(x => x.Name)
                            .Where(x => x.IndexOf("quote", StringComparison.OrdinalIgnoreCase) >= 0 || x.IndexOf("retweet", StringComparison.OrdinalIgnoreCase) >= 0)
                            .Take(12));
                        result.Add($"{SafeLog(author)}:{SafeLog(id)}:created={SafeLog(legacy["created_at"]?.Value<string>())}:mediaCount={mediaCount}:relatedKeys={SafeLog(relatedKeys, 160)}:legacyKeys={SafeLog(legacyKeys, 220)}:firstKeys={SafeLog(firstKeys, 160)}:nestedKeys={SafeLog(nestedKeys, 160)}:mediaUrlKeys={anyMediaUrls}:videoInfoKeys={anyVideoInfo}:cardKeys={anyCards}:url={SafeLog(url, 120)}");
                        if (result.Count >= 6)
                        {
                            return string.Join("; ", result);
                        }
                    }
                }
                catch { }
            }

            return string.Join("; ", result);
        }

        private static string SummarizeJsonShape(IEnumerable<string> jsons)
        {
            var shapes = new List<string>();
            foreach (var json in jsons.Where(x => !string.IsNullOrWhiteSpace(x)).Take(3))
            {
                try
                {
                    var token = JToken.Parse(json);
                    if (token is JObject obj)
                    {
                        shapes.Add(string.Join("|", obj.Properties().Select(x => x.Name).Take(20)));
                    }
                    else
                    {
                        shapes.Add(token.Type.ToString());
                    }
                }
                catch
                {
                    shapes.Add("invalid-json");
                }
            }
            return SafeLog(string.Join(" / ", shapes), 600);
        }

        private static string SafeLog(string value, int maxLength = 1000)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var text = value.Replace("\r", "\\r").Replace("\n", "\\n");
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...[truncated]";
        }

        private static void CollectNotificationTweetIdsFromText(string text, List<string> ids)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (Match match in Regex.Matches(text, @"(?:/status/|tweet-)(\d{15,22})|(?:tweet_id|status_id|conversation_id_str)[""'\s:\\]+(\d{15,22})", RegexOptions.IgnoreCase))
            {
                var id = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (!string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }
            }
        }

        private static void CollectNotificationTweetIds(JToken token, List<string> ids, string path)
        {
            if (token == null)
            {
                return;
            }

            if (token is JObject obj)
            {
                if (TryGetNotificationTweetId(obj, path, out string id))
                {
                    ids.Add(id);
                }

                foreach (var property in obj.Properties())
                {
                    if (property.Value is JValue value)
                    {
                        CollectNotificationTweetIdsFromValue(property.Name, value, ids);
                    }
                    CollectNotificationTweetIds(property.Value, ids, $"{path}.{property.Name}");
                }
            }
            else if (token is JArray array)
            {
                foreach (var item in array)
                {
                    CollectNotificationTweetIds(item, ids, path);
                }
            }
        }

        private static bool TryGetNotificationTweetId(JObject obj, string path, out string id)
        {
            id = obj["rest_id"]?.Value<string>() ?? obj.SelectToken("legacy.id_str")?.Value<string>() ?? obj["id_str"]?.Value<string>();
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            var typeName = obj["__typename"]?.Value<string>() ?? string.Empty;
            if (typeName.IndexOf("Tweet", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var lowerPath = (path ?? string.Empty).ToLowerInvariant();
            if (lowerPath.Contains("user_results") || lowerPath.Contains("users_result") || lowerPath.Contains(".user."))
            {
                return false;
            }
            return lowerPath.Contains("tweet_results") ||
                   lowerPath.Contains("tweetresult") ||
                   lowerPath.Contains(".tweet.") ||
                   obj.SelectToken("legacy.full_text") != null;
        }

        private static void CollectNotificationTweetIdsFromValue(string propertyName, JValue value, List<string> ids)
        {
            var text = value?.Value?.ToString();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            CollectNotificationTweetIdsFromText(text, ids);
            var key = (propertyName ?? string.Empty).ToLowerInvariant();
            if ((key.Contains("tweet") || key.Contains("status") || key.Contains("conversation")) &&
                Regex.IsMatch(text, @"^\d{15,22}$"))
            {
                ids.Add(text);
            }
        }

        private string ParseScreenName(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            var match = _userUrl.Match(url.Trim());
            if (!match.Success)
            {
                return string.Empty;
            }

            var screenName = NormalizeScreenName(match.Groups[1].Value);
            if (string.IsNullOrEmpty(screenName) || screenName.Equals("home", StringComparison.OrdinalIgnoreCase) || screenName.Equals("notifications", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            return screenName;
        }

        private string NormalizeScreenName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            return value.Trim().TrimStart('@');
        }
    }

    public class XRequestHandler : RequestHandler
    {
        private readonly ConcurrentQueue<XCapturedResponse> _queue;

        public XRequestHandler(ConcurrentQueue<XCapturedResponse> queue)
        {
            _queue = queue;
        }

        protected override IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            var url = request.Url.ToLowerInvariant();
            if (url.Contains("x.com/i/api/") ||
                url.Contains("twitter.com/i/api/") ||
                url.Contains("api.x.com/graphql"))
            {
                return new XResourceRequestHandler(_queue, request.Url);
            }
            return base.GetResourceRequestHandler(chromiumWebBrowser, browser, frame, request, isNavigation, isDownload, requestInitiator, ref disableDefaultHandling);
        }
    }

    public class XResourceRequestHandler : ResourceRequestHandler
    {
        private readonly MemoryStream _memory = new MemoryStream();
        private readonly ConcurrentQueue<XCapturedResponse> _queue;
        private readonly string _url;

        public XResourceRequestHandler(ConcurrentQueue<XCapturedResponse> queue, string url)
        {
            _queue = queue;
            _url = url;
        }

        protected override IResponseFilter GetResourceResponseFilter(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        {
            return new CefSharp.ResponseFilter.StreamResponseFilter(_memory);
        }

        protected override void OnResourceLoadComplete(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
        {
            CefDiagnostics.LogResourceComplete("XBackgroundCapture", _url, status, response?.StatusCode ?? 0, receivedContentLength);
            CefDiagnostics.LogResponseBody("XBackgroundCapture", _url, response?.StatusCode ?? 0, _memory.ToArray());
            if (status == UrlRequestStatus.Success && response != null && response.StatusCode >= 200 && response.StatusCode < 300 && _memory.Length > 0)
            {
                try
                {
                    _queue.Enqueue(new XCapturedResponse(_url, Encoding.UTF8.GetString(_memory.ToArray())));
                }
                catch { }
            }
            base.OnResourceLoadComplete(chromiumWebBrowser, browser, frame, request, response, status, receivedContentLength);
        }
    }

    public class XCapturedResponse
    {
        public XCapturedResponse(string url, string body)
        {
            Url = url ?? string.Empty;
            Body = body ?? string.Empty;
        }

        public string Url { get; }
        public string Body { get; }
    }
}
