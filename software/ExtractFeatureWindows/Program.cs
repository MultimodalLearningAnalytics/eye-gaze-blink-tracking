namespace ExtractFeatureWindows
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Psi;

    class Program
    {
        private const string EXPERIMENT = "experiment2";
        private const string PSI_STORES_PATH = "/some/path/todo";
        private const string GAZEBLINK_STORE_NAME = "GazeBlinkData";
        private const int EXP2_NUMBER_OF_STORES = 12;
        private const string EXP3_DISTRACTION_STORE_NAME = "Distraction";

        public static int windowSizeSeconds;

        static void Main(string[] args)
        {
            if (EXPERIMENT == "experiment2")
            {
                Program.windowSizeSeconds = 10;
                Experiment2Extraction();
                Program.windowSizeSeconds = 20;
                Experiment2Extraction();
                Program.windowSizeSeconds = 30;
                Experiment2Extraction();
            }
            else if (EXPERIMENT == "experiment3")
            {
                Program.windowSizeSeconds = 10;
                Experiment3Extraction();
                Program.windowSizeSeconds = 20;
                Experiment3Extraction();
                Program.windowSizeSeconds = 30;
                Experiment3Extraction();
            }
            else if (EXPERIMENT == "fullstore")
            {
                Program.windowSizeSeconds = 20;
                FullStoreExtraction();
            }
            else if (EXPERIMENT == "exp3-attentive-inattentive")
            {
                Program.windowSizeSeconds = 20;
                ExtractExperiment3AttentiveInattentive();
            }
        }

        static private void Experiment2Extraction()
        {
            List<string> attentiveCsv = new List<string>();

            for (int i = 0; i < EXP2_NUMBER_OF_STORES; i++)
            {
                using (var p = Pipeline.Create())
                {
                    var gazeBlinkData = PsiStore.Open(p, GAZEBLINK_STORE_NAME, Path.Combine(PSI_STORES_PATH, $"{GAZEBLINK_STORE_NAME}.{i.ToString("D4")}"));
                    var gazeXStream = gazeBlinkData.OpenStream<float>("GazeAngleX");
                    var gazeYStream = gazeBlinkData.OpenStream<float>("GazeAngleY");
                    var blinkStream = gazeBlinkData.OpenStream<int>("Blink");

                    var gazeXValues = new List<(float gazeX, DateTime orgTime)>();
                    var gazeYValues = new List<(float gazeY, DateTime orgTime)>();
                    var blinkValues = new List<(int blink, DateTime orgTime)>();
                    var overallGazeValues = new List<(float gazeX, float gazeY, DateTime orgTime)>();

                    DateTime? windowStart = null;

                    gazeXStream.Join(gazeYStream).Join(blinkStream).Do((tuple, e) => {
                        if (!windowStart.HasValue) // Initialize windowStart
                            windowStart = e.OriginatingTime;

                        if (e.OriginatingTime > windowStart.Value.AddSeconds(windowSizeSeconds))
                        {
                            Console.WriteLine($"Calculating features for {windowStart} to {e.OriginatingTime}");
                            string gazeFeatures = GazeFeatures.CalculateGazeFeatures(gazeXValues, gazeYValues, overallGazeValues);
                            string blinkFeatures = BlinkFeatures.CalculateBlinkFeatures(blinkValues);
                            attentiveCsv.Add(gazeFeatures + "," + blinkFeatures);

                            gazeXValues = new List<(float gazeX, DateTime orgTime)>();
                            gazeYValues = new List<(float gazeY, DateTime orgTime)>();
                            blinkValues = new List<(int blink, DateTime orgTime)>();
                            overallGazeValues = new List<(float gazeX, float gazeY, DateTime orgTime)>();
                            windowStart = e.OriginatingTime;
                        }
                        else
                        {
                            gazeXValues.Add((tuple.Item1, e.OriginatingTime));
                            gazeYValues.Add((tuple.Item2, e.OriginatingTime));
                            blinkValues.Add((tuple.Item3, e.OriginatingTime));
                            overallGazeValues.Add((tuple.Item1, tuple.Item2, e.OriginatingTime));
                        }
                    });

                    p.Run(ReplayDescriptor.ReplayAll);
                }
            }

            string csvHeader = GazeFeatures.CsvHeader + "," + BlinkFeatures.CsvHeader + ",class";
            File.WriteAllLines(PSI_STORES_PATH + $"/{windowSizeSeconds}s-AttentiveFeatures.csv", attentiveCsv
                .Select(s => s + ",attentive")
                .Prepend(csvHeader)
                .ToList());
        }

        static private void Experiment3Extraction()
        {
            var distractionTimes = new List<DateTime>();
            var inattentiveTimes = new List<DateTime>();

            using (var p = Pipeline.Create())
            {
                var store = PsiStore.Open(p, EXP3_DISTRACTION_STORE_NAME, PSI_STORES_PATH);

                var distractionStream = store.OpenStream<string>("Distraction");
                var inattentiveStream = store.OpenStream<string>("Inattentive");

                distractionStream.Do((d, e) => distractionTimes.Add(e.OriginatingTime));
                inattentiveStream.Do((i, e) => inattentiveTimes.Add(e.OriginatingTime));

                p.Run(ReplayDescriptor.ReplayAll);
            }

            var distractionCsv = distractionTimes.Select(time => ExtractFeaturesWindow(time, windowSizeSeconds));
            var inattentiveCsv = inattentiveTimes.Select(time => ExtractFeaturesWindow(time, windowSizeSeconds));
            var distractedInattentiveCsv = distractionCsv.Concat(inattentiveCsv);

            string csvHeader = GazeFeatures.CsvHeader + "," + BlinkFeatures.CsvHeader + ",class";
            File.WriteAllLines(PSI_STORES_PATH + $"/{windowSizeSeconds}s-DistractedFeatures.csv", distractionCsv
                .Select(s => s + ",distracted")
                .Prepend(csvHeader)
                .ToList());
            File.WriteAllLines(PSI_STORES_PATH + $"/{windowSizeSeconds}s-InattentiveFeatures.csv", inattentiveCsv
                .Select(s => s + ",inattentive")
                .Prepend(csvHeader)
                .ToList());
            File.WriteAllLines(PSI_STORES_PATH + $"/{windowSizeSeconds}s-DistractedInattentiveFeatures.csv", distractedInattentiveCsv
                .Select(s => s + ",inattentive")
                .Prepend(csvHeader)
                .ToList());
        }

        static private void FullStoreExtraction()
        {
            List<string> fullCsv = new List<string>();
            using (var p = Pipeline.Create())
            {
                var gazeBlinkData = PsiStore.Open(p, GAZEBLINK_STORE_NAME, PSI_STORES_PATH);
                var gazeXStream = gazeBlinkData.OpenStream<float>("GazeAngleX");
                var gazeYStream = gazeBlinkData.OpenStream<float>("GazeAngleY");
                var blinkStream = gazeBlinkData.OpenStream<int>("Blink");

                var gazeXValues = new List<(float gazeX, DateTime orgTime)>();
                var gazeYValues = new List<(float gazeY, DateTime orgTime)>();
                var blinkValues = new List<(int blink, DateTime orgTime)>();
                var overallGazeValues = new List<(float gazeX, float gazeY, DateTime orgTime)>();

                DateTime? storeStart = null;

                gazeXStream.Join(gazeYStream).Join(blinkStream).Do((tuple, e) => {
                    if (!storeStart.HasValue) // Initialize windowStart
                        storeStart = e.OriginatingTime;

                    gazeXValues.Add((tuple.Item1, e.OriginatingTime));
                    gazeYValues.Add((tuple.Item2, e.OriginatingTime));
                    blinkValues.Add((tuple.Item3, e.OriginatingTime));
                    overallGazeValues.Add((tuple.Item1, tuple.Item2, e.OriginatingTime));

                    gazeXValues = gazeXValues.EvictOlderThan(e.OriginatingTime, windowSizeSeconds);
                    gazeYValues = gazeYValues.EvictOlderThan(e.OriginatingTime, windowSizeSeconds);
                    blinkValues = blinkValues.EvictOlderThan(e.OriginatingTime, windowSizeSeconds);
                    overallGazeValues = overallGazeValues.EvictOlderThan(e.OriginatingTime, windowSizeSeconds);

                    if (e.OriginatingTime < storeStart.Value.AddSeconds(windowSizeSeconds)) // Not collected enough data
                        return;

                    Console.WriteLine($"Calculating features for {e.OriginatingTime.ToUniversalTime().AddSeconds(-windowSizeSeconds)} to {e.OriginatingTime}");
                    string gazeFeatures = GazeFeatures.CalculateGazeFeatures(gazeXValues, gazeYValues, overallGazeValues);
                    string blinkFeatures = BlinkFeatures.CalculateBlinkFeatures(blinkValues);
                    fullCsv.Add(e.OriginatingTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) + "," + gazeFeatures + "," + blinkFeatures);
                });

                p.Run(ReplayDescriptor.ReplayAll);
            }

            string csvHeader = "timestamp," + GazeFeatures.CsvHeader + "," + BlinkFeatures.CsvHeader + ",class";
            File.WriteAllLines(PSI_STORES_PATH + $"/{windowSizeSeconds}s-FullStoreFeatures.csv", fullCsv
                .Select(s => s + ",?")
                .Prepend(csvHeader)
                .ToList());
        }

        static private void ExtractExperiment3AttentiveInattentive()
        {
            List<DateTime> distractionTimes = new();
            List<DateTime> inattentiveTimes = new();

            using (var p = Pipeline.Create())
            {
                var distractionStore = PsiStore.Open(p, EXP3_DISTRACTION_STORE_NAME, PSI_STORES_PATH);

                var distractionStream = distractionStore.OpenStream<string>("Distraction");
                var inattentiveStream = distractionStore.OpenStream<string>("Inattentive");

                distractionStream.Do((d, e) => distractionTimes.Add(e.OriginatingTime));
                inattentiveStream.Do((i, e) => inattentiveTimes.Add(e.OriginatingTime));

                p.Run(ReplayDescriptor.ReplayAll);
            }

            // ===== Extract attentive windows ===== //
            List<(DateTime from, DateTime to)> forbiddenAttentiveWindows = new();
            distractionTimes.ForEach(d => forbiddenAttentiveWindows.Add((d.AddSeconds(-windowSizeSeconds), d)));
            inattentiveTimes.ForEach(d => forbiddenAttentiveWindows.Add((d.AddSeconds(-windowSizeSeconds), d)));
            
            List<string> attentiveTestCsv = new();
            List<string> attentiveValidationCsv = new();
            List<string> attentiveTrainingCsv = new();

            DateTime? storeStart = null;

            using (var p = Pipeline.Create())
            {
                var gazeBlinkData = PsiStore.Open(p, GAZEBLINK_STORE_NAME, PSI_STORES_PATH);
                var gazeXStream = gazeBlinkData.OpenStream<float>("GazeAngleX");
                var gazeYStream = gazeBlinkData.OpenStream<float>("GazeAngleY");
                var blinkStream = gazeBlinkData.OpenStream<int>("Blink");

                var gazeXValues = new List<(float gazeX, DateTime orgTime)>();
                var gazeYValues = new List<(float gazeY, DateTime orgTime)>();
                var blinkValues = new List<(int blink, DateTime orgTime)>();
                var overallGazeValues = new List<(float gazeX, float gazeY, DateTime orgTime)>();

                DateTime? windowStart = null;
                gazeXStream.Join(gazeYStream).Join(blinkStream).Do((tuple, e) => {
                    if (!storeStart.HasValue) // Initialize storeStart
                        storeStart = e.OriginatingTime;
                    if (!windowStart.HasValue) // Initialize windowStart
                        windowStart = e.OriginatingTime;

                    if (e.OriginatingTime > windowStart.Value.AddSeconds(windowSizeSeconds)) // reached window length
                    {
                        if (forbiddenAttentiveWindows.All(f => !e.OriginatingTime.IsBetween(f.from, f.to) && !windowStart.Value.IsBetween(f.from, f.to)))
                        {
                            // Not a forbidden window!
                            Console.WriteLine($"Calculating features for {e.OriginatingTime.AddSeconds(-windowSizeSeconds)} to {e.OriginatingTime}");
                            string gazeFeatures = GazeFeatures.CalculateGazeFeatures(gazeXValues, gazeYValues, overallGazeValues);
                            string blinkFeatures = BlinkFeatures.CalculateBlinkFeatures(blinkValues);
                            if (windowStart.Value < storeStart.Value.AddMinutes(10))
                                attentiveTestCsv.Add(gazeFeatures + "," + blinkFeatures);
                            else if (windowStart.Value < storeStart.Value.AddMinutes(20))
                                attentiveValidationCsv.Add(gazeFeatures + "," + blinkFeatures);
                            else
                                attentiveTrainingCsv.Add(gazeFeatures + "," + blinkFeatures);
                        }
                        else
                        {
                            // Forbidden window!
                            Console.WriteLine($"Forbidden window for attentive features from {windowStart} to {e.OriginatingTime}");
                        }

                        gazeXValues = new();
                        gazeYValues = new();
                        blinkValues = new();
                        overallGazeValues = new();

                        windowStart = e.OriginatingTime;
                    }
                    else
                    {
                        gazeXValues.Add((tuple.Item1, e.OriginatingTime));
                        gazeYValues.Add((tuple.Item2, e.OriginatingTime));
                        blinkValues.Add((tuple.Item3, e.OriginatingTime));
                        overallGazeValues.Add((tuple.Item1, tuple.Item2, e.OriginatingTime));
                    }
                });

                p.Run(ReplayDescriptor.ReplayAll);
            }

            string csvHeader = GazeFeatures.CsvHeader + "," + BlinkFeatures.CsvHeader + ",class";

            File.WriteAllLines(PSI_STORES_PATH + $"/{windowSizeSeconds}s-exp3-Attentive-test.csv", attentiveTestCsv
                .Select(s => s + ",attentive")
                .Prepend(csvHeader)
                .ToList()
            );
            File.WriteAllLines(PSI_STORES_PATH + $"/{windowSizeSeconds}s-exp3-Attentive-validation.csv", attentiveValidationCsv
                .Select(s => s + ",attentive")
                .Prepend(csvHeader)
                .ToList()
            );
            File.WriteAllLines(PSI_STORES_PATH + $"/{windowSizeSeconds}s-exp3-Attentive-training.csv", attentiveTrainingCsv
                .Select(s => s + ",attentive")
                .Prepend(csvHeader)
                .ToList()
            );

            // ===== Extract inattentive windows ===== //

            var combinedTimes = distractionTimes.Concat(inattentiveTimes);
            var inattentiveTestCsv = combinedTimes.Where(t => t < storeStart.Value.AddMinutes(10)).Select(t => ExtractFeaturesWindow(t, windowSizeSeconds));
            var inattentiveValidationCsv = combinedTimes.Where(t => t > storeStart.Value.AddMinutes(10) && t < storeStart.Value.AddMinutes(20)).Select(t => ExtractFeaturesWindow(t, windowSizeSeconds));
            var inattentiveTrainingCsv = combinedTimes.Where(t => t > storeStart.Value.AddMinutes(20)).Select(t => ExtractFeaturesWindow(t, windowSizeSeconds));

            File.WriteAllLines(PSI_STORES_PATH + $"/{windowSizeSeconds}s-exp3-Inattentive-test.csv", inattentiveTestCsv
                .Select(s => s + ",inattentive")
                .Prepend(csvHeader)
                .ToList()
            );
            File.WriteAllLines(PSI_STORES_PATH + $"/{windowSizeSeconds}s-exp3-Inattentive-validation.csv", inattentiveValidationCsv
                .Select(s => s + ",inattentive")
                .Prepend(csvHeader)
                .ToList()
            );
            File.WriteAllLines(PSI_STORES_PATH + $"/{windowSizeSeconds}s-exp3-Inattentive-training.csv", inattentiveTrainingCsv
                .Select(s => s + ",inattentive")
                .Prepend(csvHeader)
                .ToList()
            );
        }

        static private string ExtractFeaturesWindow(DateTime time, int windowSize)
        {
            using (var p = Pipeline.Create())
            {
                var gazeBlinkData = PsiStore.Open(p, GAZEBLINK_STORE_NAME, PSI_STORES_PATH);
                var gazeXStream = gazeBlinkData.OpenStream<float>("GazeAngleX");
                var gazeYStream = gazeBlinkData.OpenStream<float>("GazeAngleY");
                var blinkStream = gazeBlinkData.OpenStream<int>("Blink");

                var gazeXValues = new List<(float gazeX, DateTime orgTime)>();
                var gazeYValues = new List<(float gazeY, DateTime orgTime)>();
                var blinkValues = new List<(int blink, DateTime orgTime)>();
                var overallGazeValues = new List<(float gazeX, float gazeY, DateTime orgTime)>();

                gazeXStream.Do((x, e) => gazeXValues.Add((x, e.OriginatingTime)));
                gazeYStream.Do((y, e) => gazeYValues.Add((y, e.OriginatingTime)));
                blinkStream.Do((b, e) => blinkValues.Add((b, e.OriginatingTime)));
                gazeXStream.Join(gazeYStream).Do((tuple, e) => overallGazeValues.Add((tuple.Item1, tuple.Item2, e.OriginatingTime)));

                DateTime startTime = time.Subtract(TimeSpan.FromSeconds(windowSize));
                DateTime stopTime = time;
                Console.WriteLine($"Calculating features for {startTime} to {stopTime}");
                p.Run(startTime, stopTime, false);

                string gazeFeatures = GazeFeatures.CalculateGazeFeatures(gazeXValues, gazeYValues, overallGazeValues);
                string blinkFeatures = BlinkFeatures.CalculateBlinkFeatures(blinkValues);
                return gazeFeatures + "," + blinkFeatures;
            }
        }
    }
}
