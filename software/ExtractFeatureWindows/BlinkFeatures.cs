namespace ExtractFeatureWindows
{
    using System;
    using System.Linq;
    using System.Collections.Generic;

    public class BlinkFeatures
    {
        public const string CsvHeader = "BlinkRatio,BlinkCount," +
            "BlinkDurationMean,BlinkDurationSD,BlinkDurationMedian,BlinkDurationMin,BlinkDurationMax,BlinkDurationRange," +
            "BlinkTimeBetweenMean,BlinkTimeBetweenSD,BlinkTimeBetweenMedian,BlinkTimeBetweenMin,BlinkTimeBetweenMax,BlinkTimeBetweenRange";

        public static string CalculateBlinkFeatures(List<(int blink, DateTime orgTime)> blinkValues)
        {
            // TODO BlinkRatio now simply counts frames, do we want that?
            float BlinkRatio = (float) blinkValues.Count(tuple => tuple.blink == 1) / (float) blinkValues.Count(tuple => tuple.blink == 0);
            
            // Extract blink events from data
            List<Blink> blinks = ExtractBlinks(blinkValues);
            // DEBUG blinks.ForEach(blink => Console.WriteLine($"Blink duration: {blink.Duration} time between: {blink.TimeSincePreviousBlink}"));
            
            int BlinkCount = 0;
            float BlinkDurationMean = 0;
            float BlinkDurationSD = 0;
            float BlinkDurationMedian = 0;
            float BlinkDurationMin = 0;
            float BlinkDurationMax = 0;
            float BlinkDurationRange = 0;
            float BlinkTimeBetweenMean = Program.windowSizeSeconds;
            float BlinkTimeBetweenSD = 0;
            float BlinkTimeBetweenMedian = Program.windowSizeSeconds;
            float BlinkTimeBetweenMin = Program.windowSizeSeconds;
            float BlinkTimeBetweenMax = Program.windowSizeSeconds;
            float BlinkTimeBetweenRange = 0;

            if (blinks.Any())
            {
                BlinkCount = blinks.Count();
                BlinkDurationMean = blinks.Select(b => b.Duration).Average();
                BlinkDurationSD = blinks.Select(b => b.Duration).StdDev();
                BlinkDurationMedian = blinks.Select(b => b.Duration).Median();
                BlinkDurationMin = blinks.Select(b => b.Duration).Min();
                BlinkDurationMax = blinks.Select(b => b.Duration).Max();
                BlinkDurationRange = BlinkDurationMax - BlinkDurationMin;
                BlinkTimeBetweenMean = blinks.Select(b => b.TimeSincePreviousBlink).Average();
                BlinkTimeBetweenSD = blinks.Select(b => b.TimeSincePreviousBlink).StdDev();
                BlinkTimeBetweenMedian = blinks.Select(b => b.TimeSincePreviousBlink).Median();
                BlinkTimeBetweenMin = blinks.Select(b => b.TimeSincePreviousBlink).Min();
                BlinkTimeBetweenMax = blinks.Select(b => b.TimeSincePreviousBlink).Max();
                BlinkTimeBetweenRange = BlinkTimeBetweenMax - BlinkTimeBetweenMin;
            }
            
            List<string> csvValues = new List<string> {
                BlinkRatio.ToString(),
                BlinkCount.ToString(),
                BlinkDurationMean.ToString(),
                BlinkDurationSD.ToString(),
                BlinkDurationMedian.ToString(),
                BlinkDurationMin.ToString(),
                BlinkDurationMax.ToString(),
                BlinkDurationRange.ToString(),
                BlinkTimeBetweenMean.ToString(),
                BlinkTimeBetweenSD.ToString(),
                BlinkTimeBetweenMedian.ToString(),
                BlinkTimeBetweenMin.ToString(),
                BlinkTimeBetweenMax.ToString(),
                BlinkTimeBetweenRange.ToString(),
            };
            return string.Join(',', csvValues);
        }

        private static List<Blink> ExtractBlinks(List<(int blink, DateTime orgTime)> blinkValues)
        {
            if (!blinkValues.Any())
                return new List<Blink>();

            // Walk through all blink data points
            // We 'start' a blink when we encounter a 1 with a 1 300 ms later
            // We 'finish' and store a blink when we encounter a 0 with a 0 300 ms later
            List<Blink> blinks = new List<Blink>();
            DateTime timeLastBlink = blinkValues[0].orgTime;
            DateTime? blinkStarted = null;
            int i = 0;
            while (i < blinkValues.Count())
            {
                var value = blinkValues[i];
                // DEBUG Console.WriteLine($"i: {i}, org time: {value.orgTime.Millisecond}, value blink: {value.blink}");
                if (value.blink == 1 && !blinkStarted.HasValue)
                {
                    // Find data point 300ms (or more) in future and see if it is still 1
                    int index300msLater = i;
                    while (index300msLater < blinkValues.Count())
                    {
                        if (blinkValues[index300msLater].orgTime > value.orgTime.AddMilliseconds(300))
                            break;
                        else
                            index300msLater++;
                    }
                    if (index300msLater < blinkValues.Count() && blinkValues[index300msLater].blink == 1)
                    {
                        // DEBUG Console.WriteLine($"i: {i} blink started");
                        blinkStarted = value.orgTime;
                        i = index300msLater;
                    }
                }
                else if (value.blink == 0 && blinkStarted.HasValue)
                {
                    // Find data point 300ms (or more) in future and see if it still 0
                    int index300msLater = i;
                    while (index300msLater < blinkValues.Count())
                    {
                        if (blinkValues[index300msLater].orgTime > value.orgTime.AddMilliseconds(300))
                            break;
                        else
                            index300msLater++;
                    }
                    if (index300msLater >= blinkValues.Count() || blinkValues[index300msLater].blink == 0)
                    {
                        // DEBUG Console.WriteLine($"i: {i} blink stopped");
                        blinks.Add(new Blink(){
                            Duration = (float) value.orgTime.Subtract(blinkStarted.Value).TotalSeconds,
                            TimeSincePreviousBlink = (float) blinkStarted.Value.Subtract(timeLastBlink).TotalSeconds,
                        });
                        timeLastBlink = value.orgTime;
                        blinkStarted = null;
                        i = index300msLater;
                    }
                }
                i++;
            }

            // Add last blink if blink is unfinished
            if (blinkStarted.HasValue)
            {
                var value = blinkValues.Last();
                blinks.Add(new Blink(){
                    Duration = (float) value.orgTime.Subtract(blinkStarted.Value).TotalSeconds,
                    TimeSincePreviousBlink = (float) blinkStarted.Value.Subtract(timeLastBlink).TotalSeconds,
                });
            }

            return blinks;
        }

        private class Blink
        {
            public float Duration { get; set; }
            public float TimeSincePreviousBlink { get; set; }
        }
    }
}
