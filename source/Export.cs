using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace Spludlow.MameAO
{
	public class Export
	{
		private Database _Database;
		private HashStore _RomHashStore;
		private HashStore _DiskHashStore;
		private Reports _Reports;

		public Export(Database database, HashStore romHashStore, HashStore diskHashStore, Reports reports)
		{
			_Database = database;
			_RomHashStore = romHashStore;
			_DiskHashStore = diskHashStore;
			_Reports = reports;
		}

		public void MachineRoms(string targetDirectory)
		{
			DataTable machineTable = Database.ExecuteFill(_Database._MachineConnection, "SELECT * FROM machine ORDER BY machine.name");
			DataTable romTable = Database.ExecuteFill(_Database._MachineConnection, "SELECT * FROM rom WHERE sha1 IS NOT NULL");

			DataTable reportTable = Tools.MakeDataTable(
				"Name	Description	Status	CopyCount	ParentCount",
				"String	String		String	Int32		Int32"
			);

			using (TempDirectory tempDir = new TempDirectory())
			{
				foreach (DataRow machineRow in machineTable.Rows)
				{
					string machine_name = (string)machineRow["name"];
					string machine_description = (string)machineRow["description"];

					long machine_id = (long)machineRow["machine_id"];
					DataRow[] romRows = romTable.Select("machine_id = " + machine_id);

					Dictionary<string, string> nameHashes = new Dictionary<string, string>();

					int parentCount = 0;
					int dontHaveCount = 0;

					foreach (DataRow romRow in romRows)
					{
						string sha1 = (string)romRow["sha1"];
						string rom_name = (string)romRow["name"];

						if (romRow.IsNull("merge") == false)
						{
							++parentCount;
							continue;
						}

						if (_RomHashStore.Exists(sha1) == false)
						{
							++dontHaveCount;
							continue;
						}

						if (nameHashes.ContainsKey(rom_name) == true)
						{
							if (nameHashes[rom_name] != sha1)
								throw new ApplicationException("ROM name sha1 mismatch: " + machine_name + "," + rom_name);

							continue;
						}

						nameHashes.Add(rom_name, sha1);
					}

					if (dontHaveCount > 0 || nameHashes.Count == 0)
						continue;

					DataRow row = reportTable.Rows.Add(machine_name, machine_description, "", 0, 0);

					row["CopyCount"] = nameHashes.Count;
					row["ParentCount"] = parentCount;

					string archiveFilename = Path.Combine(targetDirectory, machine_name + ".zip");
					
					if (File.Exists(archiveFilename) == true)
					{
						row["Status"] = "Exists";
						continue;
					}

					string archiveDirectory = tempDir.Path + @"\" + machine_name;
					Directory.CreateDirectory(archiveDirectory);

					foreach (string rom_name in nameHashes.Keys)
					{
						string storeFilename = _RomHashStore.Filename(nameHashes[rom_name]);
						string romFilename = archiveDirectory + @"\" + rom_name;
						File.Copy(storeFilename, romFilename);
					}

					System.IO.Compression.ZipFile.CreateFromDirectory(archiveDirectory, archiveFilename);

					Directory.Delete(archiveDirectory, true);

					Console.WriteLine(archiveFilename);
				}
			}

			DataSet dataSet = new DataSet();
			dataSet.Tables.Add(reportTable);

			_Reports.SaveHtmlReport(dataSet, "Export Machine ROM");
		}
	}
}
