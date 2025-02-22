using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Spludlow.MameAO
{
	public class Export
	{
		public static readonly DateTime MagicModifiedDate = new DateTime(1996, 12, 24, 23, 32, 0, DateTimeKind.Utc);

		public static string NameHashManifestText(Dictionary<string, string> nameHashes)
		{
			StringBuilder manifest = new StringBuilder();
			foreach (string name in nameHashes.Keys.OrderBy(i => i))
			{
				manifest.Append(name);
				manifest.Append("\t");
				manifest.Append(nameHashes[name]);
				manifest.Append("\n");
			}
			return manifest.ToString();
		}

		public static void CreateArchive(Dictionary<string, string> nameHashes, HashStore hashStore, string tempDirectory, string targetFilename)
		{
			HashSet<string> directories = new HashSet<string>();

			for (int pass = 0; pass < 2; ++pass)
			{
				foreach (string name in nameHashes.Keys)
				{
					List<string> pathParts = new List<string>(name.Split('/'));
					pathParts.Insert(0, tempDirectory);
					string tempFilename = Path.Combine(pathParts.ToArray());

					if (pass == 0)
					{
						string directory = Path.GetDirectoryName(tempFilename);
						if (directories.Add(directory) == true)
							Directory.CreateDirectory(directory);
					}
					else
					{
						File.Copy(hashStore.Filename(nameHashes[name]), tempFilename);
					}
				}
			}

			foreach (string tempFilename in Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories))
				File.SetLastWriteTimeUtc(tempFilename, MagicModifiedDate);

			ZipFile.CreateFromDirectory(tempDirectory, targetFilename);
		}

		public static void MachineRoms(string targetDirectory)
		{
			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name, description FROM machine ORDER BY machine.name");
			DataTable romTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");

			string manifestFilename = Path.Combine(targetDirectory, "_mame-ao-manifest-machine-rom.txt");

			DataTable manifestTable = Tools.MakeDataTable("Export Machine ROM",
				"FileName	Length	LastWriteTime	SHA1",
				"String*	Int64	DateTime		String");

			if (File.Exists(manifestFilename) == true)
				manifestTable = Tools.TextTableReadFile(manifestFilename);

			HashSet<DataRow> badManifestRows = new HashSet<DataRow>();

			Console.Write("Check manifest...");
			foreach (DataRow manifestRow in manifestTable.Rows)
			{
				string targetFilename = Path.Combine(targetDirectory, (string)manifestRow["FileName"]);

				if (File.Exists(targetFilename) == false)
				{
					badManifestRows.Add(manifestRow);
					Console.Write("O");
				}
				else
				{
					FileInfo info = new FileInfo(targetFilename);

					if (info.Length != (long)manifestRow["Length"] ||
						info.LastWriteTime.AddTicks(-(info.LastWriteTime.Ticks % TimeSpan.TicksPerSecond)) != (DateTime)manifestRow["LastWriteTime"])
					{
						badManifestRows.Add(manifestRow);
						File.Delete(targetFilename);
						Console.Write("X");
					}
					else
					{
						Console.Write(".");
					}
				}
			}
			Console.WriteLine("...done");

			foreach (var row in badManifestRows)
				row.Delete();
			manifestTable.AcceptChanges();

			try
			{
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

						string targetFilename = Path.Combine(targetDirectory, machine_name + ".zip");
						string targetName = targetFilename.Substring(targetDirectory.Length + 1);

						string zipManifest = NameHashManifestText(nameHashes);
						string zipManifestSHA1 = Tools.SHA1HexText(zipManifest, Encoding.ASCII);

						DataRow existingManifestRow = manifestTable.Rows.Find(targetName);

						if (existingManifestRow != null && (string)existingManifestRow["SHA1"] != zipManifestSHA1)
						{
							File.Delete(targetFilename);
							existingManifestRow.Delete();
							manifestTable.AcceptChanges();
							existingManifestRow = null;
							Console.WriteLine($"Zip Replace SHA1: {targetFilename}");
						}

						if (existingManifestRow != null)
							continue;

						if (File.Exists(targetFilename) == true)
						{
							File.Delete(targetFilename);
							Console.WriteLine($"Zip Replace Unknown: {targetFilename}");
						}

						string tempDirectory = Path.Combine(tempDir.Path, machine_name);
						Directory.CreateDirectory(tempDirectory);
						CreateArchive(nameHashes, Globals.RomHashStore, tempDirectory, targetFilename);
						Directory.Delete(tempDirectory, true);

						FileInfo info = new FileInfo(targetFilename);

						manifestTable.Rows.Add(targetName, info.Length, info.LastWriteTime, zipManifestSHA1);

						Console.WriteLine(targetFilename);
					}
				}
			}
			finally
			{
				File.WriteAllText(manifestFilename, Tools.TextTable(manifestTable), Encoding.UTF8);
			}

			Globals.Reports.SaveHtmlReport(manifestTable, "Export Machine ROM");
		}

		public static void MachineRomsOLD(string targetDirectory)
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

					string archiveDirectory = Path.Combine(tempDir.Path, machine_name);
					Directory.CreateDirectory(archiveDirectory);

					foreach (string name in nameHashes.Keys)
					{
						string storeFilename = Globals.RomHashStore.Filename(nameHashes[name]);
						string romFilename = Path.Combine(archiveDirectory, name);
						File.Copy(storeFilename, romFilename);
					}

					System.IO.Compression.ZipFile.CreateFromDirectory(archiveDirectory, archiveFilename);

					Directory.Delete(archiveDirectory, true);

					Console.WriteLine(archiveFilename);
				}
			}

			Globals.Reports.SaveHtmlReport(reportTable, "Export Machine ROM");
		}

		public static void MachineDisks(string targetDirectory)
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

		public static void SoftwareRoms(string targetDirectory)
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

						string archiveDirectory = Path.Combine(tempDir.Path, software_name);
						Directory.CreateDirectory(archiveDirectory);

						foreach (string name in nameHashes.Keys)
						{
							string storeFilename = Globals.RomHashStore.Filename(nameHashes[name]);
							string romFilename = Path.Combine(archiveDirectory, name);
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

		public static void SoftwareDisks(string targetDirectory)
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
