using AtelierMisaka.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AtelierMisaka
{
    internal static class XTimelineParser
    {
        internal static List<XItem> ParseItems(IEnumerable<string> jsons, bool includeRetweets, bool includeQuotes, bool useTimeBoundary, int headWindowSize, out List<string> headIds, XTweetType rootTweetType = XTweetType.Original)
        {
            var tweets = new Dictionary<string, XParsedTweet>();
            var ids = new List<string>();
            foreach (var json in jsons.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                try
                {
                    var token = JToken.Parse(json);
                    ParseToken(token, includeRetweets, includeQuotes, rootTweetType, null, tweets, ids);
                }
                catch { }
            }

            headIds = ids.Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(headWindowSize).ToList();

            return tweets.Values
                .Where(x => x.Media.Count > 0)
                .Where(x => !useTimeBoundary || !GlobalMethord.OverTime(GetTimeBoundaryDate(x)))
                .OrderByDescending(x => x.CreatedAt)
                .Select(ToItem)
                .ToList();
        }

        private static DateTime GetTimeBoundaryDate(XParsedTweet tweet)
        {
            if (tweet != null &&
                (tweet.TweetType == XTweetType.Retweet || tweet.TweetType == XTweetType.Quote) &&
                tweet.TriggerDate.HasValue)
            {
                return tweet.TriggerDate.Value;
            }

            return tweet?.CreatedAt ?? DateTime.Now;
        }

        internal static bool TryExtractViewerScreenName(IEnumerable<string> jsons, out string screenName)
        {
            foreach (var json in jsons.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                try
                {
                    var token = JToken.Parse(json);
                    if (FindViewerScreenName(token, out screenName))
                    {
                        return true;
                    }
                }
                catch { }
            }
            screenName = string.Empty;
            return false;
        }

        internal static bool TryExtractUserInfo(IEnumerable<string> jsons, string screenName, out string userId, out string displayName)
        {
            userId = screenName;
            displayName = screenName;
            foreach (var json in jsons.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                try
                {
                    var token = JToken.Parse(json);
                    foreach (var obj in Walk(token).OfType<JObject>())
                    {
                        var legacy = obj["legacy"] as JObject;
                        var foundScreenName = legacy?["screen_name"]?.Value<string>();
                        if (!string.Equals(foundScreenName, screenName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        var id = obj["rest_id"]?.Value<string>() ?? legacy?["id_str"]?.Value<string>();
                        var name = legacy?["name"]?.Value<string>();
                        if (!string.IsNullOrEmpty(id))
                        {
                            userId = id;
                        }
                        if (!string.IsNullOrEmpty(name))
                        {
                            displayName = name;
                        }
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        private static void ParseToken(JToken token, bool includeRetweets, bool includeQuotes, XTweetType tweetType, XTweetContext context, Dictionary<string, XParsedTweet> tweets, List<string> headIds)
        {
            if (token == null)
            {
                return;
            }

            if (token is JObject obj)
            {
                XParsedTweet currentTweet = null;
                if (TryParseTweet(obj, tweetType, out XParsedTweet tweet))
                {
                    currentTweet = tweet;
                    if (context != null)
                    {
                        tweet.TriggerAuthorScreenName = context.AuthorScreenName;
                        tweet.TriggerDate = context.CreatedAt;
                    }
                    else if (tweet.TweetType == XTweetType.Retweet)
                    {
                        tweet.TriggerAuthorScreenName = tweet.AuthorScreenName;
                        tweet.TriggerDate = tweet.CreatedAt;
                    }
                    AddHeadId(headIds, tweet.Id);
                    if (ShouldIncludeTweet(tweet, includeRetweets, includeQuotes) && tweet.Media.Count > 0 && !tweets.ContainsKey(tweet.Id))
                    {
                        tweets[tweet.Id] = tweet;
                    }
                }

                foreach (var property in obj.Properties())
                {
                    var name = property.Name.ToLowerInvariant();
                    if (IsRetweetProperty(name))
                    {
                        CollectTweetIds(property.Value, headIds);
                        if (includeRetweets)
                        {
                            ParseToken(property.Value, includeRetweets, includeQuotes, XTweetType.Retweet, BuildContext(currentTweet), tweets, headIds);
                        }
                        continue;
                    }

                    if (IsQuoteProperty(name))
                    {
                        CollectTweetIds(property.Value, headIds);
                        if (includeQuotes)
                        {
                            ParseToken(property.Value, includeRetweets, includeQuotes, XTweetType.Quote, BuildContext(currentTweet), tweets, headIds);
                        }
                        continue;
                    }

                    ParseToken(property.Value, includeRetweets, includeQuotes, tweetType, context, tweets, headIds);
                }
            }
            else if (token is JArray array)
            {
                foreach (var item in array)
                {
                    ParseToken(item, includeRetweets, includeQuotes, tweetType, context, tweets, headIds);
                }
            }
        }

        private static bool ShouldIncludeTweet(XParsedTweet tweet, bool includeRetweets, bool includeQuotes)
        {
            if (tweet == null)
            {
                return false;
            }

            if (tweet.TweetType == XTweetType.Retweet)
            {
                return includeRetweets;
            }

            if (tweet.TweetType == XTweetType.Quote)
            {
                return includeQuotes;
            }

            return true;
        }

        private static XTweetContext BuildContext(XParsedTweet tweet)
        {
            if (tweet == null)
            {
                return null;
            }

            return new XTweetContext
            {
                AuthorScreenName = tweet.AuthorScreenName,
                CreatedAt = tweet.CreatedAt
            };
        }

        private static void CollectTweetIds(JToken token, List<string> headIds)
        {
            if (token == null)
            {
                return;
            }

            foreach (var obj in Walk(token).OfType<JObject>())
            {
                if (TryGetTweetId(obj, out string id))
                {
                    AddHeadId(headIds, id);
                }
            }
        }

        private static bool TryParseTweet(JObject obj, XTweetType tweetType, out XParsedTweet tweet)
        {
            tweet = null;
            if (!TryGetTweetId(obj, out string id))
            {
                return false;
            }

            var legacy = obj["legacy"] as JObject;
            if (legacy == null)
            {
                return false;
            }

            if (!IsTweetLegacy(legacy))
            {
                return false;
            }

            var media = ExtractMedia(obj, legacy);
            var author = ExtractAuthor(obj);
            var createdAt = ParseXDate(legacy["created_at"]?.Value<string>());
            var text = legacy["full_text"]?.Value<string>() ?? string.Empty;
            var effectiveTweetType = tweetType == XTweetType.Original && IsRetweetText(text) ? XTweetType.Retweet : tweetType;

            tweet = new XParsedTweet
            {
                Id = id,
                AuthorScreenName = string.IsNullOrEmpty(author) ? GlobalData.VM_MA.Artist.Cid : author,
                CreatedAt = createdAt,
                Text = text,
                TweetType = effectiveTweetType,
                Media = media
            };
            return true;
        }

        private static bool IsRetweetText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith("RT @", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetTweetId(JObject obj, out string id)
        {
            var legacy = obj["legacy"] as JObject;
            id = obj["rest_id"]?.Value<string>() ?? legacy?["id_str"]?.Value<string>() ?? obj["id_str"]?.Value<string>();
            return !string.IsNullOrEmpty(id) && IsTweetLegacy(legacy);
        }

        private static bool IsTweetLegacy(JObject legacy)
        {
            return legacy?["full_text"] != null;
        }

        private static string ExtractAuthor(JObject obj)
        {
            var token = obj.SelectToken("core.user_results.result.core.screen_name")
                ?? obj.SelectToken("core.user_results.result.core.screenName")
                ?? obj.SelectToken("core.user_results.result.legacy.screen_name")
                ?? obj.SelectToken("core.user_results.result.legacy.screenName")
                ?? obj.SelectToken("legacy.user.screen_name");
            return token?.Value<string>() ?? string.Empty;
        }

        private static List<XMediaInfo> ExtractMedia(JObject tweet, JObject legacy)
        {
            var result = new List<XMediaInfo>();
            var mediaArray = legacy.SelectToken("extended_entities.media") as JArray ?? legacy.SelectToken("entities.media") as JArray;
            if (mediaArray == null)
            {
                return result;
            }

            var seen = new HashSet<string>();
            foreach (var item in mediaArray.OfType<JObject>())
            {
                var mediaObject = NormalizeMediaObject(item);
                var type = GetMediaType(item, mediaObject);
                XMediaInfo media = null;
                if (type == "photo")
                {
                    media = BuildPhotoMedia(mediaObject) ?? BuildPhotoMedia(item);
                }
                else if (type == "video" || type == "animated_gif")
                {
                    media = BuildVideoMedia(mediaObject) ?? BuildVideoMedia(item);
                }

                if (media == null || string.IsNullOrEmpty(media.Url))
                {
                    continue;
                }

                var key = media.Url.Split('?').FirstOrDefault();
                if (seen.Add(key))
                {
                    result.Add(media);
                }
            }
            return result;
        }

        private static JObject NormalizeMediaObject(JObject item)
        {
            return item.SelectToken("media_results.result.legacy") as JObject ??
                   item.SelectToken("media_results.result") as JObject ??
                   item;
        }

        private static string GetMediaType(JObject item, JObject mediaObject)
        {
            var type = mediaObject["type"]?.Value<string>() ?? item["type"]?.Value<string>();
            if (!string.IsNullOrEmpty(type))
            {
                return type;
            }

            if (mediaObject.SelectToken("video_info.variants") != null || item.SelectToken("video_info.variants") != null)
            {
                return "video";
            }

            var url = mediaObject["media_url_https"]?.Value<string>() ??
                      mediaObject["media_url"]?.Value<string>() ??
                      item["media_url_https"]?.Value<string>() ??
                      item["media_url"]?.Value<string>();
            return string.IsNullOrEmpty(url) ? string.Empty : "photo";
        }

        private static XMediaInfo BuildPhotoMedia(JObject item)
        {
            var url = item["media_url_https"]?.Value<string>() ?? item["media_url"]?.Value<string>();
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            var extension = GetExtensionFromUrl(url, "jpg");
            if (!url.Contains("?"))
            {
                url += "?name=orig";
            }
            return new XMediaInfo { Url = url, Extension = extension };
        }

        private static XMediaInfo BuildVideoMedia(JObject item)
        {
            var variants = item.SelectToken("video_info.variants") as JArray;
            if (variants == null)
            {
                return null;
            }

            JObject selected = null;
            var selectedBitrate = -1;
            foreach (var variant in variants.OfType<JObject>())
            {
                if (variant["content_type"]?.Value<string>() != "video/mp4")
                {
                    continue;
                }

                var bitrate = variant["bitrate"]?.Value<int?>() ?? 0;
                if (selected == null || bitrate > selectedBitrate)
                {
                    selected = variant;
                    selectedBitrate = bitrate;
                }
            }

            var url = selected?["url"]?.Value<string>();
            return string.IsNullOrEmpty(url) ? null : new XMediaInfo { Url = url, Extension = "mp4" };
        }

        private static string GetExtensionFromUrl(string url, string fallback)
        {
            try
            {
                var clean = url.Split('?').FirstOrDefault();
                var ext = clean.Split('/').Last().Split('.').Last();
                if (!string.IsNullOrEmpty(ext) && ext.Length <= 5)
                {
                    return ext;
                }
            }
            catch { }
            return fallback;
        }

        private static XItem ToItem(XParsedTweet tweet)
        {
            var titleText = string.IsNullOrWhiteSpace(tweet.Text) ? "media" : tweet.Text.Replace(Environment.NewLine, " ");
            if (titleText.Length > 40)
            {
                titleText = titleText.Substring(0, 40);
            }
            titleText = GlobalMethord.RemoveAllDot(GlobalMethord.ReplacePath(titleText));
            if (string.IsNullOrEmpty(titleText))
            {
                titleText = "media";
            }

            var item = new XItem
            {
                AuthorScreenName = tweet.AuthorScreenName,
                TriggerAuthorScreenName = tweet.TriggerAuthorScreenName,
                TriggerDate = tweet.TriggerDate,
                TweetType = tweet.TweetType,
                Title = $"{tweet.AuthorScreenName}_{titleText}",
                Fee = "0"
            };
            item.PID = tweet.Id;
            item.SetDates(tweet.CreatedAt);
            item.Comments.Add($"https://x.com/{tweet.AuthorScreenName}/status/{tweet.Id}");
            item.Comments.Add(tweet.Text);

            for (int i = 0; i < tweet.Media.Count; i++)
            {
                item.ContentUrls.Add(tweet.Media[i].Url);
                item.FileNames.Add($"{tweet.CreatedAt:yyyyMMdd_HHmmss}_{tweet.Id}_{i + 1}.{tweet.Media[i].Extension}");
            }
            return item;
        }

        private static DateTime ParseXDate(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                if (DateTime.TryParseExact(text, "ddd MMM dd HH:mm:ss K yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime date))
                {
                    return date.ToLocalTime();
                }
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date))
                {
                    return date.ToLocalTime();
                }
            }
            return DateTime.Now;
        }

        private static bool FindViewerScreenName(JToken token, out string screenName)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (property.Name.IndexOf("viewer", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (FindScreenName(property.Value, out screenName))
                        {
                            return true;
                        }
                    }
                    if (FindViewerScreenName(property.Value, out screenName))
                    {
                        return true;
                    }
                }
            }
            else if (token is JArray array)
            {
                foreach (var item in array)
                {
                    if (FindViewerScreenName(item, out screenName))
                    {
                        return true;
                    }
                }
            }
            screenName = string.Empty;
            return false;
        }

        private static bool FindScreenName(JToken token, out string screenName)
        {
            foreach (var obj in Walk(token).OfType<JObject>())
            {
                var value = obj["screen_name"]?.Value<string>() ?? obj.SelectToken("legacy.screen_name")?.Value<string>();
                if (!string.IsNullOrEmpty(value))
                {
                    screenName = value;
                    return true;
                }
            }
            screenName = string.Empty;
            return false;
        }

        private static bool IsRetweetProperty(string name)
        {
            return name.Contains("retweeted_status") ||
                   name.Contains("retweet_result") ||
                   name.Contains("retweetresult") ||
                   name.Contains("retweetedstatus") ||
                   name.Contains("retweeted_result") ||
                   name.Contains("retweetedresult");
        }

        private static bool IsQuoteProperty(string name)
        {
            return name.Contains("quoted_status") ||
                   name.Contains("quoted_result") ||
                   name.Contains("quotedresult") ||
                   name.Contains("quotedstatus") ||
                   name.Contains("quoted_ref") ||
                   name.Contains("quotedref") ||
                   name.Contains("quoted_tweet") ||
                   name.Contains("quotedtweet");
        }

        private static void AddHeadId(List<string> ids, string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                ids.Add(id);
            }
        }

        private static IEnumerable<JToken> Walk(JToken token)
        {
            if (token == null)
            {
                yield break;
            }

            yield return token;
            foreach (var child in token.Children())
            {
                foreach (var item in Walk(child))
                {
                    yield return item;
                }
            }
        }

        private class XParsedTweet
        {
            public string Id { get; set; }
            public string AuthorScreenName { get; set; }
            public string TriggerAuthorScreenName { get; set; }
            public DateTime? TriggerDate { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Text { get; set; }
            public XTweetType TweetType { get; set; }
            public List<XMediaInfo> Media { get; set; }
        }

        private class XTweetContext
        {
            public string AuthorScreenName { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
