﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace TimeZoneConverter.DataBuilder
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var tempDir = Downloader.GetTempDir();

            try
            {
                var cldrPath = Path.Combine(tempDir, "cldr");
                var tzdbPath = Path.Combine(tempDir, "tzdb");
                var railsPath = Path.Combine(tempDir, "rails");

                // Download Data
                if (!Directory.Exists(tempDir))
                {
                    var t1 = Downloader.DownloadCldrAsync(cldrPath);
                    var t2 = Downloader.DownloadTzdbAsync(tzdbPath);
                    var t3 = Downloader.DownloadRailsTzMappingAsync(railsPath);
                    Task.WaitAll(t1, t2, t3);
                }

                // Extract links from TZDB
                var links = DataExtractor.LoadTzdbLinks(tzdbPath);

                // Fixup UTC equivalencies.  Prefer Etc/UTC.
                links.Add("Etc/GMT", "Etc/UTC");
                foreach (var tzdbLink in links.ToList())
                {
                    if (tzdbLink.Value == "Etc/GMT")
                        links[tzdbLink.Key] = "Etc/UTC";
                }

                // Extract mappings and aliases from CLDR
                var mapping = DataExtractor.LoadMapping(cldrPath, links);
                var aliases = DataExtractor.LoadAliases(cldrPath, links);

                // Extract Rails mappings and aliases from Rails data
                var railsMapping = DataExtractor.LoadRailsMapping(railsPath);

                // Apply override mappings for zones not yet in the CLDR trunk we pulled in

                // Asia/Qyzylorda => Qyzylorda Standard Time
                mapping.Remove("West Asia Standard Time,KZ,Asia/Oral Asia/Aqtau Asia/Aqtobe Asia/Atyrau Asia/Qyzylorda");
                mapping.Add("West Asia Standard Time,KZ,Asia/Oral Asia/Aqtau Asia/Aqtobe Asia/Atyrau");
                mapping.Add("Qyzylorda Standard Time,001,Asia/Qyzylorda");
                mapping.Add("Qyzylorda Standard Time,KZ,Asia/Qyzylorda");

                // Europe/Volgograd => Volgograd Standard Time
                mapping.Remove("Russian Standard Time,RU,Europe/Moscow Europe/Kirov Europe/Volgograd");
                mapping.Add("Russian Standard Time,RU,Europe/Moscow Europe/Kirov");
                mapping.Add("Volgograd Standard Time,001,Europe/Volgograd");
                mapping.Add("Volgograd Standard Time,RU,Europe/Volgograd");

                // America/Metlakatla => Alaskan Standard Time
                mapping.Remove("Alaskan Standard Time,US,America/Anchorage America/Juneau America/Nome America/Sitka America/Yakutat");
                mapping.Add("Alaskan Standard Time,US,America/Anchorage America/Juneau America/Metlakatla America/Nome America/Sitka America/Yakutat");
                mapping.Remove("Pacific Standard Time,US,America/Los_Angeles America/Metlakatla");
                mapping.Add("Pacific Standard Time,US,America/Los_Angeles");

                // Add mappings for ISO country codes that aren't used in CLDR
                mapping.Add("Romance Standard Time,EA,Africa/Ceuta");
                mapping.Add("GMT Standard Time,IC,Atlantic/Canary");
                mapping.Add("Greenwich Standard Time,AC,Atlantic/St_Helena");
                mapping.Add("Greenwich Standard Time,TA,Atlantic/St_Helena");
                mapping.Add("Central Europe Standard Time,XK,Europe/Belgrade");
                mapping.Add("Central Asia Standard Time,DG,Indian/Chagos");

                // Add a few aliases for IANA abbreviated zones not tracked by CLDR
                aliases.Add("Europe/Paris,CET");
                aliases.Add("Europe/Bucharest,EET");
                aliases.Add("Europe/Berlin,MET");
                aliases.Add("Atlantic/Canary,WET");

                mapping.Sort(StringComparer.Ordinal);
                aliases.Sort(StringComparer.Ordinal);

                // Support mapping deprecated Windows zones, but after sorting so they are not used as primary results
                mapping.Add("Kamchatka Standard Time,001,Asia/Kamchatka");
                mapping.Add("Mid-Atlantic Standard Time,001,Etc/GMT+2");

                // Write to source files in the main library
                var projectPath = Path.GetFullPath(".");
                while (!File.Exists(Path.Combine(projectPath, "TimeZoneConverter.sln")))
                    projectPath = Path.GetFullPath(Path.Combine(projectPath, ".."));
                var dataPath = Path.Combine(projectPath, "src", "TimeZoneConverter", "Data");
                WriteAllLinesToCompressedFile(Path.Combine(dataPath, "Mapping.csv.gz"), mapping);
                WriteAllLinesToCompressedFile(Path.Combine(dataPath, "Aliases.csv.gz"), aliases);
                WriteAllLinesToCompressedFile(Path.Combine(dataPath, "RailsMapping.csv.gz"), railsMapping);
            }
            finally
            {
                // Cleanup Data
                Directory.Delete(tempDir, true);
            }
        }

        private static void WriteAllLinesToCompressedFile(string path, IEnumerable<string> lines)
        {
            using (var stream = File.Create(path))
            using (var compressedStream = new GZipStream(stream, CompressionLevel.Optimal))
            using (var writer = new StreamWriter(compressedStream))
            {
                foreach (var line in lines)
                    writer.WriteLine(line);
            }
        }
    }
}
