using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

using System.Data.SQLite;

namespace Spludlow.MameAO
{
	public class CoreHbMame : ICore
	{
		string ICore.Name { get => "hbmame"; }
		string ICore.Version { get => _Version; }
		string ICore.Directory { get => _CoreDirectory; }

		string[] ICore.ConnectionStrings { get => new string[] { _ConnectionStringMachine, _ConnectionStringSoftware }; }
		Dictionary<string, string> ICore.SoftwareListDescriptions { get => _SoftwareListDescriptions; }

		private string _RootDirectory = null;
		private string _CoreDirectory = null;

		private string _Version = null;


		private string _ConnectionStringMachine = null;
		private string _ConnectionStringSoftware = null;

		private Dictionary<string, DataRow[]> _MachineDevicesRefs = null;
		private Dictionary<string, string> _SoftwareListDescriptions = null;

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
			string url = "https://hbmame.1emulation.com/";
			string html;
			
			try
			{
				html = Tools.Query(url);
			}
			catch (Exception e)
			{
				Console.WriteLine($"!!! Can not download HBMAME HTML, {e.Message}");
				return 0;
			}
			
			string downloadUrl = null;

			string find = "<a href=\"";
			int index = 0;
			while ((index = html.IndexOf(find, index)) != -1)
			{
				int endIndex = html.IndexOf("\"", index + find.Length);
				if (endIndex == -1)
					break;

				string link = html.Substring(index + find.Length, endIndex - index - find.Length);
				if (link.StartsWith("hbmame") == true && link.Contains("ui") == false && link.EndsWith(".7z") == true)
				{
					if (downloadUrl == null)
						downloadUrl = new Uri(new Uri(url), link).AbsoluteUri;
					else
						throw new ApplicationException("Found more dowload links than expected");
				}

				index = endIndex;
			}

			if (downloadUrl == null)
				throw new ApplicationException("Did not find download link");

			//	TODO:	Always use latest for now
			_Version = ParseVersion(html);

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);
			Directory.CreateDirectory(_CoreDirectory);

			if (File.Exists(Path.Combine(_CoreDirectory, "hbmame.exe")) == true)
				return 0;

			using (TempDirectory tempDir = new TempDirectory())
			{
				string archiveFilename = Path.Combine(tempDir.Path, "hbmame.7z");

				Console.Write($"Downloading {downloadUrl} {archiveFilename} ...");
				Tools.Download(downloadUrl, archiveFilename, 1);
				Console.WriteLine("...done");

				Console.Write($"Extracting {archiveFilename} {_CoreDirectory} ...");
				Tools.ExtractToDirectory7Zip(archiveFilename, _CoreDirectory);
				Console.WriteLine("...done");
			}

			return 1;
		}
		private static string ParseVersion(string html)
		{
			string version = null;

			string find = "HBMAME 0.";
			int index = 0;
			while ((index = html.IndexOf(find, index)) != -1)
			{
				int endIndex = html.IndexOf("(", index + find.Length);
				if (endIndex == -1)
					break;

				string text = html.Substring(index + find.Length, endIndex - index - find.Length);
				if (version == null)
					version = "0." + text.Trim();
				else
					throw new ApplicationException("HBMAME Found more versions than expected");

				index = endIndex;
			}

			if (version == null)
				throw new ApplicationException("HBMAME Did not find version");

			return version;
		}

		void ICore.Xml()
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			Cores.ExtractXML(Path.Combine(_CoreDirectory, "hbmame.exe"));
		}
		private static string LatestLocalVersion(string directory)
		{
			SortedDictionary<int, string> versions = new SortedDictionary<int, string>();
			string version;

			foreach (string versionDirectory in Directory.GetDirectories(directory))
			{
				version = Path.GetFileName(versionDirectory);

				if (version.StartsWith("0.") == false)
					continue;

				if (File.Exists(Path.Combine(versionDirectory, "hbmame.exe")) == false)
					continue;

				string[] parts = version.Split('.');

				if (parts.Length != 3)
					continue;

				versions.Add(Int32.Parse(parts[2]), version);
			}

			if (versions.Count == 0)
				throw new ApplicationException($"HBMAME version not found in '{directory}'.");

			version = versions[versions.Keys.Last()];

			return version;
		}

		void ICore.SQLite()
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			Tools.ConsoleHeading(1, new string[] { "SQLite HBMAME", _Version, _CoreDirectory });

			Cores.MakeSQLite(_CoreDirectory, null, null, false, null, null);
		}

		void ICore.SQLiteAo()
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			InitializeConnections();


			Cores.MakeSQLite(_CoreDirectory, ReadXML.RequiredMachineTables, ReadXML.RequiredSoftwareTables, false, Globals.AssemblyVersion, Cores.AddExtraAoData);

			//
			// AO bump check
			//
			using (SQLiteConnection connection = new SQLiteConnection(_ConnectionStringMachine))
			{
				string databaseAssemblyVersion = null;
				if (Database.TableExists(connection, "ao_info") == true)
				{
					object obj = Database.ExecuteScalar(connection, "SELECT [assembly_version] FROM [ao_info] WHERE ([ao_info_id] = 1)");

					if (obj == null || !(obj is string))
						throw new ApplicationException("MAME ao_info bad table");

					databaseAssemblyVersion = (string)obj;
				}

				if (databaseAssemblyVersion != Globals.AssemblyVersion)
				{
					Console.WriteLine("SQLite database from previous version re-creating.");
					Cores.MakeSQLite(_CoreDirectory, ReadXML.RequiredMachineTables, ReadXML.RequiredSoftwareTables, true, Globals.AssemblyVersion, Cores.AddExtraAoData);
				}
			}

			//
			// Cache machine device_ref to speed up machine dependancy resolution
			//
			_MachineDevicesRefs = new Dictionary<string, DataRow[]>();

			DataTable device_refTable = Database.ExecuteFill(_ConnectionStringMachine, "SELECT * FROM device_ref");

			foreach (DataRow row in Database.ExecuteFill(_ConnectionStringMachine, "SELECT machine_id, name FROM machine").Rows)
				_MachineDevicesRefs.Add((string)row["name"], device_refTable.Select($"machine_id = {(long)row["machine_id"]}"));

			//
			// Cache softwarelists for description
			//
			_SoftwareListDescriptions = new Dictionary<string, string>();

			foreach (DataRow row in Database.ExecuteFill(_ConnectionStringSoftware, "SELECT name, description FROM softwarelist").Rows)
				_SoftwareListDescriptions.Add((string)row["name"], (string)row["description"]);


		}

		private void InitializeConnections()
		{
			_ConnectionStringMachine = $"Data Source='{Path.Combine(_CoreDirectory, "_machine.sqlite")}';datetimeformat=CurrentCulture;";
			_ConnectionStringSoftware = $"Data Source='{Path.Combine(_CoreDirectory, "_software.sqlite")}';datetimeformat=CurrentCulture;";
		}


		void ICore.AllSHA1(HashSet<string> hashSet)
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			InitializeConnections();

			Console.Write($"Load all database SHA1 ...");
			Cores.AllSHA1(hashSet, _ConnectionStringMachine, new string[] { "rom" });
			Cores.AllSHA1(hashSet, _ConnectionStringSoftware, new string[] { "rom" });
			Console.WriteLine("...done");
		}


		void ICore.MSSql()
		{
			throw new NotImplementedException();
		}

		void ICore.MSSqlHtml()
		{
			throw new NotImplementedException();
		}

		void ICore.MSSqlPayload()
		{
			throw new NotImplementedException();
		}


		DataRow ICore.GetMachine(string machine_name) => Cores.GetMachine(_ConnectionStringMachine, machine_name);
		
		DataRow[] ICore.GetMachineRoms(DataRow machine) => Cores.GetMachineRoms(_ConnectionStringMachine, machine);

		DataRow[] ICore.GetMachineDisks(DataRow machine) => new DataRow[0];

		DataRow[] ICore.GetMachineSamples(DataRow machine) => Cores.GetMachineSamples(_ConnectionStringMachine, machine);

		DataRow[] ICore.GetMachineSoftwareLists(DataRow machine) => Cores.GetMachineSoftwareLists(_ConnectionStringMachine, machine, _SoftwareListDescriptions);

		DataRow ICore.GetSoftwareList(string softwarelist_name) => Cores.GetSoftwareList(_ConnectionStringSoftware, softwarelist_name);

		HashSet<string> ICore.GetReferencedMachines(string machine_name) => Cores.GetReferencedMachines(this, machine_name);

		DataRow[] ICore.GetMachineDeviceRefs(string machine_name) => _MachineDevicesRefs[machine_name];

		DataRow ICore.GetSoftware(DataRow softwarelist, string software_name) => Cores.GetSoftware(_ConnectionStringSoftware, softwarelist, software_name);

		DataRow ICore.GetSoftware(string softwarelist_name, string software_name) => Cores.GetSoftware(_ConnectionStringSoftware, softwarelist_name, software_name);

		DataRow[] ICore.GetSoftwareSharedFeats(DataRow software) => Cores.GetSoftwareSharedFeats(_ConnectionStringSoftware, software);

		DataRow[] ICore.GetSoftwareListsSoftware(DataRow softwarelist) => Cores.GetSoftwareListsSoftware(_ConnectionStringSoftware, softwarelist);

		DataRow[] ICore.GetSoftwareRoms(DataRow software) => Cores.GetSoftwareRoms(_ConnectionStringSoftware, software);

		DataRow[] ICore.GetSoftwareDisks(DataRow software) => new DataRow[0];

		DataRow[] ICore.GetMachineFeatures(DataRow machine) => Cores.GetMachineFeatures(_ConnectionStringMachine, machine);

		string ICore.GetRequiredMedia(string machine_name, string softwarelist_name, string software_name) =>
			Cores.GetRequiredMedia(_ConnectionStringMachine, _ConnectionStringSoftware, _SoftwareListDescriptions, machine_name, softwarelist_name, software_name);

		DataTable ICore.QueryMachines(DataQueryProfile profile, int offset, int limit, string search) =>
			Cores.QueryMachines(_ConnectionStringMachine, profile, offset, limit, search);

		DataTable ICore.QuerySoftware(string softwarelist_name, int offset, int limit, string search, string favorites_machine) =>
			Cores.QuerySoftware(_ConnectionStringSoftware, softwarelist_name, offset, limit, search, favorites_machine);

	}
}
