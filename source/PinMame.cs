using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Spludlow.MameAO
{
	internal class PinMame
	{
		public string _RootDirectory = null;
		public string _CoreDirectory = null;

		public string _Version = null;

		public void Initialize(string directory, string version)
		{
			//	TODO: validate version
			_RootDirectory = directory;
			Directory.CreateDirectory(_RootDirectory);

			if (version != "0")
				_Version = version;
		}

		public static string LatestLocalVersion(string directory)
		{
			List<Version> versions = new List<Version>();

			foreach (string versionDirectory in Directory.GetDirectories(directory))
			{
				string name = Path.GetFileName(versionDirectory);

				if (!File.Exists(Path.Combine(versionDirectory, "PinMAME.exe")))
					continue;

				if (!Version.TryParse(name, out Version version))
					throw new ApplicationException($"Invalid version: {name}");

				versions.Add(version);
			}

			if (versions.Count == 0)
				throw new ApplicationException($"No versions found in '{directory}'.");

			string latestVersion = versions.Max().ToString();

			Console.WriteLine($"LatestLocalVersion\t{directory}\t{latestVersion}");

			return latestVersion;
		}

		public int Get()
		{
			int result = 0;

			dynamic release = JsonConvert.DeserializeObject<dynamic>(Tools.FetchTextCached("https://api.github.com/repos/vpinball/pinmame/releases/latest") ?? throw new ApplicationException("Unable to get pinmame release"));
			_Version = (string)release.tag_name;

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			Console.WriteLine($"{_CoreDirectory}");
			Directory.CreateDirectory(_CoreDirectory);

			foreach (string assetStartsWith in new string[] { "PinMAME-sc-", "VPinMAME-sc-" })
			{
				string exeFilename = Path.Combine(_CoreDirectory, assetStartsWith[0] == 'P' ? "PinMAME.exe" : "Setup64.exe");

				var releaseAssets = ((JArray)release.assets).Where(token => ((string)token["name"]).StartsWith(assetStartsWith) && ((string)token["name"]).EndsWith("-win-x64.zip")).ToArray();
				if (releaseAssets.Length != 1)
					throw new ApplicationException($"Did not find single PinMAME asset in release: {releaseAssets.Length}");

				string binariesUrl = (string)releaseAssets[0]["browser_download_url"];
				string binariesFilename = Path.Combine(_CoreDirectory, Path.GetFileName(binariesUrl));

				if (File.Exists(exeFilename) == false)
				{
					Console.Write($"Downloading {binariesUrl} {binariesFilename} ...");
					Tools.Download(binariesUrl, binariesFilename);
					Console.WriteLine("...done");

					foreach (string filename in Directory.GetFiles(_CoreDirectory, "*.txt"))
						File.Delete(filename);

					Console.Write($"Extracting {binariesFilename} {_CoreDirectory} ...");
					ZipFile.ExtractToDirectory(binariesFilename, _CoreDirectory);
					Console.WriteLine("...done");

					result = 1;
				}
			}

			return result;
		}

		public void Xml()
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			string exeFilename = Path.Combine(_CoreDirectory, "PinMAME.exe");
			string xmlFilename = Path.Combine(_CoreDirectory, "_pinmame.xml");

			if (File.Exists(xmlFilename) == false)
			{
				Console.Write("Extract PinMAME XML ...");
				Mame.ExtractXML(exeFilename, xmlFilename, "-listxml");
				Console.WriteLine("...done");
			}
		}
	}
}
