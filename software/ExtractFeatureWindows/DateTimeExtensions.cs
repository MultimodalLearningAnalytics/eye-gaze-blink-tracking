using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtractFeatureWindows
{
    static class DateTimeExtensions
    {
        public static bool IsBetween(this DateTime instant, DateTime from, DateTime to)
        {
            return (instant > from && instant < to);
        }
    }
}
