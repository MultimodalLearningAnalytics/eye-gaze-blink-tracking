using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtractFeatureWindows
{
    public static class LinqStatitisticsExtensions
    {
        // Adapted from https://stackoverflow.com/a/2253903/7387250
        public static float StdDev(this IEnumerable<float> values)
        {
            float ret = 0;
            int count = values.Count();
            if (count  > 1)
            {
                float avg = values.Average();

                // Sum of (value-avg)^2
                float sum = (float) values.Sum(d => Math.Pow(d - avg, 2));

                // Square root of sum of squared error divided by n
                ret = (float) Math.Sqrt(sum / count);
            }
            return ret;
        }

        // Adapted from https://stackoverflow.com/a/10738416/7387250
        public static float Median(this IEnumerable<float> source)
        {
            int count = source.Count();
            if(count == 0)
                return 0.0f;

            source = source.OrderBy(n => n);

            int midpoint = count / 2;
            if(count % 2 == 0)
                return (source.ElementAt(midpoint - 1) + source.ElementAt(midpoint)) / 2.0f;
            else
                return source.ElementAt(midpoint);
        }
    }
}
