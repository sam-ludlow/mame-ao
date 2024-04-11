using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace Spludlow.MameAO
{
	public class Export
	{
		public Export()
		{
		}

		public void MachineRoms(string targetDirectory)
		{
			Tools.ConsoleHeading(1, new string[] { "Export Machine ROM", targetDirectory });

			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name, description FROM machine ORDER BY machine.name");
			DataTable romTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");

			DataTable reportTable = Tools.MakeDataTable(
				"Name	Description	Status	CopyCount	ParentCount",
				"String	String		String	Int32		Int32"
			);

			using (TempDirectory tempDir = new TempDirectory())
			{
				foreach (DataRow machineRow in machineTable.Rows)
				{
					long machine_id = (long)machineRow["machine_id"];
					string machine_name = (string)machineRow["name"];
					string machine_description = (string)machineRow["description"];

					Dictionary<string, string> nameHashes = new Dictionary<string, string>();

					int parentCount = 0;
					int dontHaveCount = 0;

					foreach (DataRow row in romTable.Select("machine_id = " + machine_id))
					{
						string sha1 = (string)row["sha1"];
						string name = (string)row["name"];

						if (row.IsNull("merge") == false)
						{
							++parentCount;
							continue;
						}

						if (Globals.RomHashStore.Exists(sha1) == false)
						{
							++dontHaveCount;
							continue;
						}

						if (nameHashes.ContainsKey(name) == true)
						{
							if (nameHashes[name] != sha1)
								throw new ApplicationException($"Machine ROM name sha1 mismatch: {machine_name} {name}");

							continue;
						}

						nameHashes.Add(name, sha1);
					}

					if (dontHaveCount > 0 || nameHashes.Count == 0)
						continue;

					DataRow reportRow = reportTable.Rows.Add(machine_name, machine_description, "", 0, 0);

					reportRow["CopyCount"] = nameHashes.Count;
					reportRow["ParentCount"] = parentCount;

					string archiveFilename = Path.Combine(targetDirectory, machine_name + ".zip");
					
					if (File.Exists(archiveFilename) == true)
					{
						reportRow["Status"] = "Exists";
						continue;
					}

					string archiveDirectory = tempDir.Path + @"\" + machine_name;
					Directory.CreateDirectory(archiveDirectory);

					foreach (string name in nameHashes.Keys)
					{
						string storeFilename = Globals.RomHashStore.Filename(nameHashes[name]);
						string romFilename = archiveDirectory + @"\" + name;
						File.Copy(storeFilename, romFilename);
					}

					System.IO.Compression.ZipFile.CreateFromDirectory(archiveDirectory, archiveFilename);

					Directory.Delete(archiveDirectory, true);

					Console.WriteLine(archiveFilename);
				}
			}

			Globals.Reports.SaveHtmlReport(reportTable, "Export Machine ROM");
		}

		public void MachineDisks(string targetDirectory)
		{
			Tools.ConsoleHeading(1, new string[] { "Export Machine DISK", targetDirectory });

			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name, description FROM machine ORDER BY machine.name");
			DataTable diskTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM disk WHERE sha1 IS NOT NULL");

			DataTable reportTable = Tools.MakeDataTable(
				"Name	Description	DiskName	Status",
				"String	String		String		String"
			);

			using (TempDirectory tempDir = new TempDirectory())
			{
				foreach (DataRow machineRow in machineTable.Rows)
				{
					long machine_id = (long)machineRow["machine_id"];
					string machine_name = (string)machineRow["name"];
					string machine_description = (string)machineRow["description"];

					foreach (DataRow row in diskTable.Select("machine_id = " + machine_id))
					{
						string sha1 = (string)row["sha1"];
						string name = (string)row["name"];

						if (Globals.DiskHashStore.Exists(sha1) == false)
							continue;

						DataRow reportRow = reportTable.Rows.Add(machine_name, machine_description, name, "");

						if (row.IsNull("merge") == false)
						{
							reportRow["Status"] = "In Parent";
							continue;
						}

						string filename = Path.Combine(targetDirectory, machine_name, name + ".chd");

						if (File.Exists(filename) == true)
						{
							reportRow["Status"] = "Exists";
							continue;
						}

						string directory = Path.GetDirectoryName(filename);
						if (Directory.Exists(directory) == false)
							Directory.CreateDirectory(directory);

						File.Copy(Globals.DiskHashStore.Filename(sha1), filename);

						Console.WriteLine(filename);
					}
				}
			}

			Globals.Reports.SaveHtmlReport(reportTable, "Export Machine DISK");
		}

		public void SoftwareRoms(string targetDirectory)
		{
			Tools.ConsoleHeading(1, new string[] { "Export Software ROM", targetDirectory });

			DataTable softwarelistTable = Database.ExecuteFill(Globals.Database._SoftwareConnection, "SELECT softwarelist.softwarelist_id, softwarelist.name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");
			DataTable softwareTable = Database.ExecuteFill(Globals.Database._SoftwareConnection, "SELECT software.software_id, software.softwarelist_id, software.name, software.description FROM software ORDER BY software.name");
			DataTable romTable = Database.ExecuteFill(Globals.Database._SoftwareConnection, "SELECT part.software_id, rom.name, rom.sha1 FROM (part INNER JOIN dataarea ON part.part_id = dataarea.part_id) INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id WHERE (rom.sha1 IS NOT NULL)");

			DataTable reportTable = Tools.MakeDataTable(
				"List	Software	Description	Status	CopyCount",
				"String	String		String		String	Int32"
			);

			using (TempDirectory tempDir = new TempDirectory())
			{
				foreach (DataRow softwarelistRow in softwarelistTable.Rows)
				{
					long softwarelist_id = (long)softwarelistRow["softwarelist_id"];
					string softwarelist_name = (string)softwarelistRow["name"];

					foreach (DataRow softwareRow in softwareTable.Select("softwarelist_id = " + softwarelist_id))
					{
						long software_id = (long)softwareRow["software_id"];
						string software_name = (string)softwareRow["name"];
						string software_description = (string)softwareRow["description"];

						Dictionary<string, string> nameHashes = new Dictionary<string, string>();

						int dontHaveCount = 0;

						foreach (DataRow row in romTable.Select("software_id = " + software_id))
						{
							string name = (string)row["name"];
							string sha1 = (string)row["sha1"];

							if (Globals.RomHashStore.Exists(sha1) == false)
							{
								++dontHaveCount;
								continue;
							}

							if (nameHashes.ContainsKey(name) == true)
							{
								if (nameHashes[name] != sha1)
									throw new ApplicationException($"Software ROM name sha1 mismatch: {softwarelist_name} {software_name} {name}");

								continue;
							}

							nameHashes.Add(name, sha1);
						}

						if (dontHaveCount > 0 || nameHashes.Count == 0)
							continue;

						DataRow reportRow = reportTable.Rows.Add(softwarelist_name, software_name, software_description, "", 0);

						reportRow["CopyCount"] = nameHashes.Count;

						string archiveFilename = Path.Combine(targetDirectory, softwarelist_name, software_name + ".zip");

						if (File.Exists(archiveFilename) == true)
						{
							reportRow["Status"] = "Exists";
							continue;
						}

						string directory = Path.GetDirectoryName(archiveFilename);
						if (Directory.Exists(directory) == false)
							Directory.CreateDirectory(directory);

						string archiveDirectory = tempDir.Path + @"\" + software_name;
						Directory.CreateDirectory(archiveDirectory);

						foreach (string name in nameHashes.Keys)
						{
							string storeFilename = Globals.RomHashStore.Filename(nameHashes[name]);
							string romFilename = archiveDirectory + @"\" + name;
							File.Copy(storeFilename, romFilename);
						}

						System.IO.Compression.ZipFile.CreateFromDirectory(archiveDirectory, archiveFilename);

						Directory.Delete(archiveDirectory, true);

						Console.WriteLine(archiveFilename);
					}
				}
			}

			Globals.Reports.SaveHtmlReport(reportTable, "Export Software ROM");
		}

		public void SoftwareDisks(string targetDirectory)
		{
			Tools.ConsoleHeading(1, new string[] { "Export Software DISK", targetDirectory });

			DataTable softwarelistTable = Database.ExecuteFill(Globals.Database._SoftwareConnection, "SELECT softwarelist.softwarelist_id, softwarelist.name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");
			DataTable softwareTable = Database.ExecuteFill(Globals.Database._SoftwareConnection, "SELECT software.software_id, software.softwarelist_id, software.name, software.description FROM software ORDER BY software.name");
			DataTable diskTable = Database.ExecuteFill(Globals.Database._SoftwareConnection, "SELECT part.software_id, disk.name, disk.sha1 FROM (part INNER JOIN diskarea ON part.part_id = diskarea.part_id) INNER JOIN disk ON diskarea.diskarea_id = disk.diskarea_id WHERE (disk.sha1 IS NOT NULL)");

			DataTable reportTable = Tools.MakeDataTable(
				"List	Software	Description	DiskName	Status",
				"String	String		String		String		String"
			);

			using (TempDirectory tempDir = new TempDirectory())
			{
				foreach (DataRow softwarelistRow in softwarelistTable.Rows)
				{
					long softwarelist_id = (long)softwarelistRow["softwarelist_id"];
					string softwarelist_name = (string)softwarelistRow["name"];

					foreach (DataRow softwareRow in softwareTable.Select("softwarelist_id = " + softwarelist_id))
					{
						long software_id = (long)softwareRow["software_id"];
						string software_name = (string)softwareRow["name"];
						string software_description = (string)softwareRow["description"];

						foreach (DataRow diskRow in diskTable.Select("software_id = " + software_id))
						{
							string name = (string)diskRow["name"];
							string sha1 = (string)diskRow["sha1"];

							if (Globals.DiskHashStore.Exists(sha1) == false)
								continue;

							DataRow reportRow = reportTable.Rows.Add(softwarelist_name, software_name, software_description, name, "");

							string filename = Path.Combine(targetDirectory, softwarelist_name, software_name, name + ".chd");

							if (File.Exists(filename) == true)
							{
								reportRow["Status"] = "Exists";
								continue;
							}

							string directory = Path.GetDirectoryName(filename);
							if (Directory.Exists(directory) == false)
								Directory.CreateDirectory(directory);

							File.Copy(Globals.DiskHashStore.Filename(sha1), filename);

							Console.WriteLine(filename);
						}
					}
				}
			}
			Globals.Reports.SaveHtmlReport(reportTable, "Export Software DISK");
		}



	}
}
