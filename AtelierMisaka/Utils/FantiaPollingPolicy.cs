using AtelierMisaka.Models;
using System;

namespace AtelierMisaka
{
    public static class FantiaPollingPolicy
    {
        public static readonly TimeSpan OldPostGraceWindow = TimeSpan.FromDays(14);

        public static bool ShouldStopAfterSeenWindow(int consecutiveSeenCount)
        {
            return consecutiveSeenCount >= FantiaPollingCursorDic.WindowSize;
        }

        public static bool IsOldPostReordered(DateTime createDate, DateTime updateDate, DateTime cursorUpdatedAt)
        {
            if (cursorUpdatedAt == DateTime.MinValue)
            {
                return false;
            }

            var threshold = cursorUpdatedAt.Subtract(OldPostGraceWindow);
            return createDate < threshold && updateDate < threshold;
        }

        public static bool ShouldScanNextPage(bool isPollingScan, bool hasNextPage)
        {
            return hasNextPage && !isPollingScan;
        }
    }
}
