using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spludlow.MameAO
{
	internal class CorePinballVisual : ICore
	{
		string ICore.Name => "pinball-visual";

		string ICore.Version { get => _Version; }

		string ICore.Directory { get => _CoreDirectory; }


		string[] ICore.ConnectionStrings => throw new NotImplementedException();

		Dictionary<string, string> ICore.SoftwareListDescriptions => throw new NotImplementedException();

		Dictionary<string, string[]> ICore.Filters => throw new NotImplementedException();


		private string _Version = null;
		private string _RootDirectory = null;
		private string _CoreDirectory = null;


		void ICore.Initialize(string directory, string version)
		{
			//	TODO: validate version
			_RootDirectory = directory;
			Directory.CreateDirectory(_RootDirectory);

			if (version != "0")
				_Version = version;
		}

		int ICore.Get()
		{
			string url = _Version == null ?
				"https://api.github.com/repos/vpinball/vpinball/releases/latest" :
				$"https://api.github.com/repos/vpinball/vpinball/releases/tags/mame{_Version}";

			dynamic release = JsonConvert.DeserializeObject<dynamic>(Tools.FetchTextCached(url) ?? throw new ApplicationException("Unable to get vpinball release"));

			if (_Version == null)
				_Version = (string)release.tag_name;

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);
			Directory.CreateDirectory(_CoreDirectory);

			int result = 0;

			//	Extracts to dir then install DXSETUP.exe
			//	DirectX End-User Runtimes (June 2010)
			//	https://www.microsoft.com/en-us/download/details.aspx?id=8109

			JToken[] releaseAssets;

			//
			// Visual Pinball
			//
			if (File.Exists(Path.Combine(_CoreDirectory, "VPinballX64.exe")) == false)
			{
				releaseAssets = ((JArray)release.assets)
					.Where(token => ((string)token["name"]).StartsWith("Developer.VPinballX-") && ((string)token["name"]).EndsWith("Release-win-x64.zip")).ToArray();

				if (releaseAssets.Length != 1)
					throw new ApplicationException($"Did not find single vpinball asset in release: {releaseAssets.Length} {url}");

				string binariesUrl = (string)releaseAssets[0]["browser_download_url"];

				string binariesFilename = Path.Combine(_CoreDirectory, Path.GetFileName(binariesUrl));

				Console.Write($"Downloading {binariesUrl} {binariesFilename} ...");
				Tools.Download(binariesUrl, binariesFilename);
				Console.WriteLine("...done");

				Console.Write($"Extracting {binariesFilename} {_CoreDirectory} ...");
				ZipFile.ExtractToDirectory(binariesFilename, _CoreDirectory);
				Console.WriteLine("...done");

				result = 1;
			}

			//
			// PinMAME & VPinMAME
			//
			string vPinMAMECommonDirectory = Path.Combine(_CoreDirectory, "VPinMAME");
			Directory.CreateDirectory(vPinMAMECommonDirectory);

			release = JsonConvert.DeserializeObject<dynamic>(Tools.FetchTextCached("https://api.github.com/repos/vpinball/pinmame/releases/latest") ?? throw new ApplicationException("Unable to get pinmame release"));
			string pinMameVersion = (string)release.tag_name;

			releaseAssets = ((JArray)release.assets).Where(token => ((string)token["name"]).StartsWith("PinMAME-sc-") && ((string)token["name"]).EndsWith("-win-x64.zip")).ToArray();
			if (releaseAssets.Length != 1)
				throw new ApplicationException($"Did not find single PinMAME asset in release: {releaseAssets.Length}");
			string pinMameBinariesUrl = (string)releaseAssets[0]["browser_download_url"];
			string pinMameBinariesFilename = Path.Combine(vPinMAMECommonDirectory, Path.GetFileName(pinMameBinariesUrl));
			if (File.Exists(Path.Combine(vPinMAMECommonDirectory, "PinMAME.exe")) == false)
			{
				Console.Write($"Downloading {pinMameBinariesUrl} {pinMameBinariesFilename} ...");
				Tools.Download(pinMameBinariesUrl, pinMameBinariesFilename);
				Console.WriteLine("...done");

				foreach (string filename in Directory.GetFiles(vPinMAMECommonDirectory, "*.txt"))
					File.Delete(filename);

				Console.Write($"Extracting {pinMameBinariesFilename} {vPinMAMECommonDirectory} ...");
				ZipFile.ExtractToDirectory(pinMameBinariesFilename, vPinMAMECommonDirectory);
				Console.WriteLine("...done");

				result = 1;
			}
			else
			{
				if (File.Exists(pinMameBinariesFilename) == false)
					throw new ApplicationException($"PinMAME version mismatch uninstall COM (Setup64.exe) then remove directory: {vPinMAMECommonDirectory}");
			}

			//	TODO: refactor duplication - above & below

			releaseAssets = ((JArray)release.assets).Where(token => ((string)token["name"]).StartsWith("VPinMAME-sc-") && ((string)token["name"]).EndsWith("-win-x64.zip")).ToArray();
			if (releaseAssets.Length != 1)
				throw new ApplicationException($"Did not find single VPinMAME asset in release: {releaseAssets.Length}");
			string vPinMameBinariesUrl = (string)releaseAssets[0]["browser_download_url"];
			string vPinMameBinariesFilename = Path.Combine(vPinMAMECommonDirectory, Path.GetFileName(vPinMameBinariesUrl));
			if (File.Exists(Path.Combine(vPinMAMECommonDirectory, "Setup64.exe")) == false)
			{
				Console.Write($"Downloading {vPinMameBinariesUrl} {vPinMameBinariesFilename} ...");
				Tools.Download(vPinMameBinariesUrl, vPinMameBinariesFilename);
				Console.WriteLine("...done");

				foreach (string filename in Directory.GetFiles(vPinMAMECommonDirectory, "*.txt"))
					File.Delete(filename);

				Console.Write($"Extracting {vPinMameBinariesFilename} {vPinMAMECommonDirectory} ...");
				ZipFile.ExtractToDirectory(vPinMameBinariesFilename, vPinMAMECommonDirectory);
				Console.WriteLine("...done");

				result = 1;
			}
			else
			{
				if (File.Exists(vPinMameBinariesFilename) == false)
					throw new ApplicationException($"PinMAME version mismatch uninstall COM (Setup64.exe) then remove directory: {vPinMAMECommonDirectory}");
			}

			//
			//	TODO: run COM installer or tell user to ???
			//


			// TODO - needs init (github, dirs)
			//BitTorrent.Initialize();
			//BitTorrent.EnableCore("pinball");
			//BitTorrent.EnableCore("pinmame");

			//
			// DAT
			//
			dynamic info = BitTorrent.DomeInfo();

			var torrents = ((JArray)info.torrents).Where(token => ((string)token["core"]) == "pinball" && ((string)token["type"]) == "visual").ToArray();

			if (torrents.Length != 1)
				throw new ApplicationException($"Did not find single pinball-visual torrent: {torrents.Length}");

			var datUrl = (string)torrents[0]["dat"];
			string datDirectory = Path.Combine(_CoreDirectory, "_dat_" + Uri.UnescapeDataString(Path.GetFileNameWithoutExtension(datUrl)));
			string datZipFilename = Path.Combine(datDirectory, "_dat.zip");

			foreach (string directory in Directory.GetDirectories(_CoreDirectory))
			{
				if (Path.GetFileName(directory).StartsWith("_dat_") && directory != datDirectory)
				{
					Console.WriteLine($"Remove old dat directory: {directory}");
					Directory.Delete(directory);
				}
			}

			if (Directory.Exists(datDirectory) == false || File.Exists(datZipFilename) == false)
			{
				Directory.CreateDirectory(datDirectory);

				if (File.Exists(datZipFilename) == false)
				{
					Console.Write($"Downloading {datUrl} {datZipFilename} ...");
					Tools.Download(datUrl, datZipFilename);
					Console.WriteLine("...done");
				}

				result = 1;
			}

			return result;
		}

		void ICore.Xml()
		{
			if (_Version == null)   //	TODO
				_Version = "v10.8.0-2051-28dd6c3";
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			string exeFilename = Path.Combine(_CoreDirectory, "VPinMAME", "PinMAME.exe");
			string xmlFilename = Path.Combine(_CoreDirectory, "VPinMAME", "_pinmame.xml");

			Console.Write("Extract PinMAME XML ...");
			Mame.ExtractXML(exeFilename, xmlFilename, "-listxml");
			Console.WriteLine("...done");

			string[] directories = Directory.GetDirectories(_CoreDirectory).Where(dir => Path.GetFileName(dir).StartsWith("_dat_")).ToArray();
			if (directories.Length != 1)
				throw new ApplicationException($"Did not find single _DAT_ directory: {directories.Length} {_CoreDirectory}");

			string datZipFilename = Path.Combine(directories[0], "_dat.zip");
			if (File.Exists(datZipFilename) == false)
				throw new ApplicationException($"Did not find dat zip: {datZipFilename}");

			Console.Write($"Extracting {datZipFilename} {directories[0]} ...");
			ZipFile.ExtractToDirectory(datZipFilename, directories[0]);
			Console.WriteLine("...done");
		}

		void ICore.MSSql(string serverConnectionString, string[] databaseNames)
		{
			throw new NotImplementedException();
		}

		void ICore.SQLite()
		{
			throw new NotImplementedException();
		}

		void ICore.SQLiteAo()
		{
			throw new NotImplementedException();
		}



		void ICore.AllSHA1(HashSet<string> hashSet)
		{
			throw new NotImplementedException();
		}

		DataRow ICore.GetMachine(string machine_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineDeviceRefs(string machine_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineDisks(DataRow machine)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineFeatures(DataRow machine)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineRoms(string machine_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineSamples(DataRow machine)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineSoftwareLists(DataRow machine)
		{
			throw new NotImplementedException();
		}

		HashSet<string> ICore.GetReferencedMachines(string machine_name)
		{
			throw new NotImplementedException();
		}

		string ICore.GetRequiredMedia(string machine_name, string softwarelist_name, string software_name)
		{
			throw new NotImplementedException();
		}

		DataRow ICore.GetSoftware(DataRow softwarelist, string software_name)
		{
			throw new NotImplementedException();
		}

		DataRow ICore.GetSoftware(string softwarelist_name, string software_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareDisks(DataRow software)
		{
			throw new NotImplementedException();
		}

		DataRow ICore.GetSoftwareList(string softwarelist_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareListsSoftware(DataRow softwarelist)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareRoms(DataRow software)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareSharedFeats(DataRow software)
		{
			throw new NotImplementedException();
		}



		void ICore.Json()
		{
			throw new NotImplementedException();
		}

		void ICore.MsAccess()
		{
			throw new NotImplementedException();
		}



		void ICore.MSSqlPayload(string serverConnectionString, string[] databaseNames)
		{
			throw new NotImplementedException();
		}

		DataTable ICore.QueryMachines(string profile, int offset, int limit, string search, string manufacturer, string[] status, string[] display, string[] players, string[] control, bool? mechanical, bool? clone, string order, string sort)
		{
			throw new NotImplementedException();
		}

		DataTable ICore.QuerySoftware(string softwarelist_name, int offset, int limit, string search, string publisher, string order, string sort, string favorites_machine)
		{
			throw new NotImplementedException();
		}





		void ICore.Zips()
		{
			throw new NotImplementedException();
		}
	}
}
