namespace ExtractFeatureWindows
{
    using System;
    using System.Linq;
    using System.Collections.Generic;

    public class GazeFeatures
    {
        public const string CsvHeader = "GazeDistanceCovered," +
            "HorSaccadeCount,HorSaccadeDistanceMean,HorSaccadeDistanceSD,HorSaccadeDistanceMedian,HorSaccadeDistanceMin,HorSaccadeDistanceMax,HorSaccadeDistanceRange," +
            "HorSaccadeTimeBetweenMean,HorSaccadeTimeBetweenSD,HorSaccadeTimeBetweenMedian,HorSaccadeTimeBetweenMin,HorSaccadeTimeBetweenMax,HorSaccadeTimeBetweenRange," +
            "VerSaccadeCount,VerSaccadeDistanceMean,VerSaccadeDistanceSD,VerSaccadeDistanceMedian,VerSaccadeDistanceMin,VerSaccadeDistanceMax,VerSaccadeDistanceRange," +
            "VerSaccadeTimeBetweenMean,VerSaccadeTimeBetweenSD,VerSaccadeTimeBetweenMedian,VerSaccadeTimeBetweenMin,VerSaccadeTimeBetweenMax,VerSaccadeTimeBetweenRange," +
            "OverallSaccadeCount,OverallSaccadeDistanceMean,OverallSaccadeDistanceSD,OverallSaccadeDistanceMedian,OverallSaccadeDistanceMin,OverallSaccadeDistanceMax,OverallSaccadeDistanceRange," +
            "OverallSaccadeTimeBetweenMean,OverallSaccadeTimeBetweenSD,OverallSaccadeTimeBetweenMedian,OverallSaccadeTimeBetweenMin,OverallSaccadeTimeBetweenMax,OverallSaccadeTimeBetweenRange";

        public static string CalculateGazeFeatures(List<(float gazeX, DateTime orgTime)> gazeXValues, List<(float gazeY, DateTime orgTime)> gazeYValues, List<(float gazeX, float gazeY, DateTime orgTime)> overallGazeValues)
        {
            // TODO GazeDistanceCovered is kinda dependent on amount of frames, do we want that?
            float GazeDistanceCovered = 0.0f;
            for (int i = 1; i < overallGazeValues.Count(); i++)
            {
                var previousValue = overallGazeValues[i - 1];
                var currentValue = overallGazeValues[i];
                float distance = (float) Math.Sqrt(Math.Pow(currentValue.gazeX - previousValue.gazeX, 2) + Math.Pow(currentValue.gazeY - previousValue.gazeY, 2));
                GazeDistanceCovered += distance;
            }

            // Extract saccade features for horizontal, vertical and overall saccades
            List<Saccade> horSaccades = ExtractSaccades(gazeXValues, 0.05f);
            string horSaccadeFeatures = GetSaccadeStatistics(horSaccades);
            List<Saccade> verSaccades = ExtractSaccades(gazeYValues, 0.10f);
            string verSaccadeFeatures = GetSaccadeStatistics(verSaccades);
            List<Saccade> overallSacades = ExtractOverallSaccades(overallGazeValues, 0.10f); // TODO find good threshold
            string overallSaccadeFeatures = GetSaccadeStatistics(overallSacades);

            return GazeDistanceCovered + "," + horSaccadeFeatures + "," + verSaccadeFeatures + "," + overallSaccadeFeatures;
        }

        private static List<Saccade> ExtractSaccades(List<(float gaze, DateTime orgTime)> gazeValues, float distanceThreshold)
        {
            if (!gazeValues.Any())
                return new List<Saccade>();

            // Walk through all gaze data points
            List<Saccade> saccades = new List<Saccade>();
            DateTime timeLastSaccade = gazeValues[0].orgTime;
            int i = 0;
            while (i < gazeValues.Count())
            {
                var value = gazeValues[i];
                int index500msLater = i;
                while (index500msLater < gazeValues.Count())
                {
                    if (gazeValues[index500msLater].orgTime > value.orgTime.AddMilliseconds(500))
                        break;
                    else
                        index500msLater++;
                }
                if (index500msLater >= gazeValues.Count())
                    break;
                float distance = Math.Abs(value.gaze - gazeValues[index500msLater].gaze);
                if (distance > distanceThreshold)
                {
                    saccades.Add(new Saccade(){
                        Distance = distance,
                        TimeSincePreviousSaccade = (float) value.orgTime.Subtract(timeLastSaccade).TotalSeconds,
                    });
                    timeLastSaccade = value.orgTime;
                    i = index500msLater + 1;
                }
                else
                {
                    i++;
                }
            }
            return saccades;
        }

        private static List<Saccade> ExtractOverallSaccades(List<(float gazeX, float gazeY, DateTime orgTime)> gazeValues, float distanceThreshold)
        {
            if (!gazeValues.Any())
                return new List<Saccade>();

            // Walk through all gaze data points
            List<Saccade> saccades = new List<Saccade>();
            DateTime timeLastSaccade = gazeValues[0].orgTime;
            int i = 0;
            while (i < gazeValues.Count())
            {
                var value = gazeValues[i];
                int index500msLater = i;
                while (index500msLater < gazeValues.Count())
                {
                    if (gazeValues[index500msLater].orgTime > value.orgTime.AddMilliseconds(500))
                        break;
                    else
                        index500msLater++;
                }
                if (index500msLater >= gazeValues.Count())
                    break;
                float distance = (float) Math.Sqrt(Math.Pow(value.gazeX - gazeValues[index500msLater].gazeX, 2) + Math.Pow(value.gazeY - gazeValues[index500msLater].gazeY, 2));
                if (distance > distanceThreshold)
                {
                    saccades.Add(new Saccade(){
                        Distance = distance,
                        TimeSincePreviousSaccade = (float) value.orgTime.Subtract(timeLastSaccade).TotalSeconds,
                    });
                    timeLastSaccade = value.orgTime;
                    i = index500msLater + 1;
                }
                else
                {
                    i++;
                }
            }
            return saccades;
        }

        private static string GetSaccadeStatistics(List<Saccade> saccades)
        {
            int SaccadeCount = 0;
            float SaccadeDistanceMean = 0;
            float SaccadeDistanceSD = 0;
            float SaccadeDistanceMedian = 0;
            float SaccadeDistanceMin = 0;
            float SaccadeDistanceMax = 0;
            float SaccadeDistanceRange = 0;
            float SaccadeTimeBetweenMean = Program.windowSizeSeconds;
            float SaccadeTimeBetweenSD = 0;
            float SaccadeTimeBetweenMedian = Program.windowSizeSeconds;
            float SaccadeTimeBetweenMin = Program.windowSizeSeconds;
            float SaccadeTimeBetweenMax = Program.windowSizeSeconds;
            float SaccadeTimeBetweenRange = 0;

            if (saccades.Any())
            {
                SaccadeCount = saccades.Count();
                SaccadeDistanceMean = saccades.Select(b => b.Distance).Average();
                SaccadeDistanceSD = saccades.Select(b => b.Distance).StdDev();
                SaccadeDistanceMedian = saccades.Select(b => b.Distance).Median();
                SaccadeDistanceMin = saccades.Select(b => b.Distance).Min();
                SaccadeDistanceMax = saccades.Select(b => b.Distance).Max();
                SaccadeDistanceRange = SaccadeDistanceMax - SaccadeDistanceMin;
                SaccadeTimeBetweenMean = saccades.Select(b => b.TimeSincePreviousSaccade).Average();
                SaccadeTimeBetweenSD = saccades.Select(b => b.TimeSincePreviousSaccade).StdDev();
                SaccadeTimeBetweenMedian = saccades.Select(b => b.TimeSincePreviousSaccade).Median();
                SaccadeTimeBetweenMin = saccades.Select(b => b.TimeSincePreviousSaccade).Min();
                SaccadeTimeBetweenMax = saccades.Select(b => b.TimeSincePreviousSaccade).Max();
                SaccadeTimeBetweenRange = SaccadeTimeBetweenMax - SaccadeTimeBetweenMin;
            }

            List<string> csvValues = new List<string> {
                SaccadeCount.ToString(),
                SaccadeDistanceMean.ToString(),
                SaccadeDistanceSD.ToString(),
                SaccadeDistanceMedian.ToString(),
                SaccadeDistanceMin.ToString(),
                SaccadeDistanceMax.ToString(),
                SaccadeDistanceRange.ToString(),
                SaccadeTimeBetweenMean.ToString(),
                SaccadeTimeBetweenSD.ToString(),
                SaccadeTimeBetweenMedian.ToString(),
                SaccadeTimeBetweenMin.ToString(),
                SaccadeTimeBetweenMax.ToString(),
                SaccadeTimeBetweenRange.ToString(),
            };
            return string.Join(',', csvValues);
        }

        private class Saccade
        {
            public float Distance { get; set; }
            public float TimeSincePreviousSaccade { get; set; }
        }
    }
}
