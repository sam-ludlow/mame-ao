using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Spludlow.MameAO
{
    public class Cheats
    {
        public static void Place()
        {
            if (Globals.Settings.Options["Cheats"] == "No")
                return;

            string directory = Path.Combine(Globals.Core.Directory, "cheat");

            if (Directory.Exists(directory) == true)
                return;

            string version = Tools.FetchTextCached("https://mame.spludlow.co.uk/data/mame-cheats/latest.txt");

            if (version == null)
                return;

			string zipUrl = $"https://mame.spludlow.co.uk/data/mame-cheats/{version}.zip";

			string zipCacheFilename = Path.Combine(Globals.CacheDirectory, $"mame-cheats-{version}.zip");

			Tools.ConsoleHeading(2, new string[] {
				$"Pugsy's Cheats {version}",
				zipUrl
			});

			if (File.Exists(zipCacheFilename) == false)
            {
				Console.Write($"Dowbloading Cheats: {zipCacheFilename} ...");
				Tools.Download(zipUrl, zipCacheFilename);
				Console.WriteLine("...done.");
			}

            Console.Write($"Extracting Cheats: {directory} ...");
            ZipFile.ExtractToDirectory(zipCacheFilename, directory);
            Console.WriteLine("...done.");
        }
    }
}
