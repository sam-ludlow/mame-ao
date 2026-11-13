using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Microsoft.Win32;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
			string version = Tools.LatestVersionGitTagName(directory, "PinMAME.exe");

			if (version == null)
				throw new ApplicationException($"No versions found in '{directory}'.");

			Console.WriteLine($"Version:\t{directory}\t{version}");

			return version;
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

		public void Check()
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			Tools.ConsoleHeading(1, new string[] { "PinMAME", _CoreDirectory });

			bool ready = false;
			while (ready == false)
			{
				if (IsComInstalled() == true)
				{
					string configDirectory = ConfigDirectory();

					if (configDirectory == _CoreDirectory)
					{
						Console.WriteLine("PinMAME Install Good.");
						ready = true;
					}
					else
					{
						Console.WriteLine($"!!! PinMAME old version in use. Run installer to remove from directory then quit:\t{configDirectory}");
						RunSetup(configDirectory);
					}
				}
				else
				{
					Console.WriteLine($"!!! PinMAME not found. Run installer to install from directory then quit:\t{_CoreDirectory}");
					RunSetup(_CoreDirectory);
				}
			}
		}

		public bool IsComInstalled()
		{
			return Type.GetTypeFromProgID("VPinMAME.Controller") != null;
		}

		public string ConfigDirectory()
		{
			using (var registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Freeware\Visual PinMame\globals"))
			{
				if (registryKey == null)
					return null;
				
				var value = registryKey.GetValue("rompath");

				if (value == null)
					return null;

				return Path.GetDirectoryName((string)value);
			}
		}

		public bool RunSetup(string directory)
		{
			string exeFilename = Path.Combine(directory, "Setup64.exe");

			if (File.Exists(exeFilename) == false)
				throw new ApplicationException("Pinmame setup exe is missing");

			var startInfo = new ProcessStartInfo
			{
				FileName = exeFilename,
				WorkingDirectory = directory,
				UseShellExecute = true,
				Verb = "runas"
			};

			try
			{
				var process = Process.Start(startInfo);
				process.WaitForExit();
				return true;
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
				return false;
			}
		}
	}
}
