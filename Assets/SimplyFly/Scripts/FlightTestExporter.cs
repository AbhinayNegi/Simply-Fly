using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SimplyFly.GroundOps
{
    public static class FlightTestExporter
    {
        public static void Export(
            IReadOnlyList<FlightTelemetrySample> samples,
            FlightTestSummary summary,
            out string csvPath,
            out string jsonPath)
        {
            string folder = Path.Combine(
                Application.persistentDataPath, "FlightTestLogs");
            Directory.CreateDirectory(folder);
            string safeScenario = MakeSafeName(summary.scenarioName);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseName = $"{stamp}_{safeScenario}";
            csvPath = Path.Combine(folder, baseName + ".csv");
            jsonPath = Path.Combine(folder, baseName + "_summary.json");

            StringBuilder csv = new StringBuilder(
                Math.Max(1024, samples.Count * 600));
            csv.AppendLine(FlightTelemetrySample.CsvHeader);
            foreach (FlightTelemetrySample sample in samples)
                csv.AppendLine(sample.ToCsvRow());
            File.WriteAllText(csvPath, csv.ToString(), Encoding.UTF8);
            File.WriteAllText(
                jsonPath,
                JsonUtility.ToJson(summary, true),
                Encoding.UTF8);
        }

        private static string MakeSafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Flight_Test";
            string safe = value.Replace(' ', '_');
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safe = safe.Replace(invalid, '_');
            return safe;
        }
    }
}
