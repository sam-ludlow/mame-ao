using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

using System.Data.SQLite;
using Newtonsoft.Json;

namespace Spludlow.MameAO
{
	internal class CoreMame : ICore
	{
		string ICore.Name { get => "mame"; }
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
			string mameLatestJson = Tools.FetchTextCached("https://api.github.com/repos/mamedev/mame/releases/latest");

			if (mameLatestJson == null)
				throw new ApplicationException("Unanle to get core lastest");

			mameLatestJson = Tools.PrettyJSON(mameLatestJson);

			dynamic mameLatest = JsonConvert.DeserializeObject<dynamic>(mameLatestJson);

			if (_Version == null)
				_Version = ((string)mameLatest.tag_name).Substring(4);

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);
			Directory.CreateDirectory(_CoreDirectory);

			if (File.Exists(Path.Combine(_CoreDirectory, "mame.exe")) == true)
				return 0;
			
			string binariesUrl = "https://github.com/mamedev/mame/releases/download/mame@VERSION@/mame@VERSION@b_64bit.exe";
			binariesUrl = binariesUrl.Replace("@VERSION@", _Version);

			string binariesFilename = Path.Combine(_CoreDirectory, Path.GetFileName(binariesUrl));

			Console.Write($"Downloading {binariesUrl} {binariesFilename} ...");
			Tools.Download(binariesUrl, binariesFilename);
			Console.WriteLine("...done");

			Console.Write($"Extracting {binariesFilename} {_CoreDirectory} ...");
			Mame.RunSelfExtract(binariesFilename);
			Console.WriteLine("...done");

			return 1;
		}

		void ICore.Xml()
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			Cores.ExtractXML(Path.Combine(_CoreDirectory, "mame.exe"));
		}
		private static string LatestLocalVersion(string directory)
		{
			List<string> versions = new List<string>();

			foreach (string versionDirectory in Directory.GetDirectories(directory))
			{
				string version = Path.GetFileName(versionDirectory);

				string exeFilename = Path.Combine(versionDirectory, "mame.exe");

				if (File.Exists(exeFilename) == true)
					versions.Add(version);
			}

			if (versions.Count == 0)
				throw new ApplicationException($"No MAME versions found in '{directory}'.");

			versions.Sort();

			return versions[versions.Count - 1];
		}

		void ICore.SQLite()
		{
			if (_Version == null)
				_Version = LatestLocalVersion(_RootDirectory);

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			Tools.ConsoleHeading(1, new string[] { "SQLite MAME", _Version, _CoreDirectory });

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
			Cores.AllSHA1(hashSet, _ConnectionStringMachine, new string[] { "rom", "disk" });
			Cores.AllSHA1(hashSet, _ConnectionStringSoftware, new string[] { "rom", "disk" });
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

		DataRow[] ICore.GetMachineDisks(DataRow machine) => Cores.GetMachineDisks(_ConnectionStringMachine, machine);

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

		DataRow[] ICore.GetSoftwareDisks(DataRow software) => Cores.GetSoftwareDisks(_ConnectionStringSoftware, software);

		DataRow[] ICore.GetMachineFeatures(DataRow machine) => Cores.GetMachineFeatures(_ConnectionStringMachine, machine);

		string ICore.GetRequiredMedia(string machine_name, string softwarelist_name, string software_name) =>
			Cores.GetRequiredMedia(_ConnectionStringMachine, _ConnectionStringSoftware, _SoftwareListDescriptions, machine_name, softwarelist_name, software_name);

		DataTable ICore.QueryMachines(DataQueryProfile profile, int offset, int limit, string search) =>
			Cores.QueryMachines(_ConnectionStringMachine, profile, offset, limit, search);

		DataTable ICore.QuerySoftware(string softwarelist_name, int offset, int limit, string search, string favorites_machine) =>
			Cores.QuerySoftware(_ConnectionStringSoftware, softwarelist_name, offset, limit, search, favorites_machine);

	}
}
