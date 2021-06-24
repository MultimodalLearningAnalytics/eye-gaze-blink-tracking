using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtractFeatureWindows
{
    public static class ListExtensions
    {
        public static List<(T, DateTime)> EvictOlderThan<T>(this List<(T, DateTime)> list, DateTime now, int olderThanSeconds)
        {
            return list.Where(t => t.Item2 > now.AddSeconds(-olderThanSeconds)).ToList();
        }

        public static List<(T, S, DateTime)> EvictOlderThan<T, S>(this List<(T, S, DateTime)> list, DateTime now, int olderThanSeconds)
        {
            return list.Where(t => t.Item3 > now.AddSeconds(-olderThanSeconds)).ToList();
        }
    }
}
