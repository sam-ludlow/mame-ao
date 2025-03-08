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

		public static string ZipManifest(Dictionary<string, string> nameHashes)
		{
			StringBuilder manifest = new StringBuilder();
			foreach (string name in nameHashes.Keys.OrderBy(i => i))
			{
				manifest.Append(name.Replace('\\', '/'));
				manifest.Append("\t");
				manifest.Append(nameHashes[name]);
				manifest.Append("\n");
			}
			return manifest.ToString();
		}

		public static string ZipManifestInspect(string zipFilename)
		{
			Dictionary<string, string> nameHashes = new Dictionary<string, string>();

			using (TempDirectory tempDir = new TempDirectory())
			{
				ZipFile.ExtractToDirectory(zipFilename, tempDir.Path);

				Tools.ClearAttributes(tempDir.Path);

				foreach (string filename in Directory.GetFiles(tempDir.Path, "*", SearchOption.AllDirectories))
				{
					string name = filename.Substring(tempDir.Path.Length + 1);
					string sha1 = Tools.SHA1HexFile(filename);

					nameHashes.Add(name, sha1);
				}
			}

			return ZipManifest(nameHashes);
		}

		public static void ZipManifestTest(string targetFilename, string zipManifestSHA1)
		{
			string newZipManifest = ZipManifestInspect(targetFilename);
			string newZipManifestSHA1 = Tools.SHA1HexText(newZipManifest, Encoding.ASCII);
			File.WriteAllText(targetFilename + ".txt", newZipManifest, Encoding.ASCII);
			if (zipManifestSHA1 != newZipManifestSHA1)
				throw new ApplicationException($"Bad SHA1: {targetFilename}");
		}

		public static void ZipCreate(Dictionary<string, string> nameHashes, HashStore hashStore, string tempDirectory, string targetFilename)
		{
			tempDirectory = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(targetFilename));
			Directory.CreateDirectory(tempDirectory);

			HashSet<string> directories = new HashSet<string>();

			for (int pass = 0; pass < 2; ++pass)
			{
				foreach (string name in nameHashes.Keys)
				{
					List<string> pathParts = new List<string>(name.Split('\\'));
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

			Directory.Delete(tempDirectory, true);
		}

		public static DataTable DirectoryManifest(string manifestFilename, string title)
		{
			DataTable manifestTable = Tools.MakeDataTable(title,
				"FileName	Length	LastWriteTime	SHA1",
				"String*	Int64	DateTime		String");

			string targetDirectory = Path.GetDirectoryName(manifestFilename);

			if (File.Exists(manifestFilename) == true)
				manifestTable = Tools.TextTableReadFile(manifestFilename);

			HashSet<DataRow> badManifestRows = new HashSet<DataRow>();

			Console.Write($"Check manifest {title} ...");
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

			return manifestTable;
		}

		public static void MachineRoms(string targetDirectory)
		{
			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnectionString, "SELECT machine_id, name, description FROM machine ORDER BY machine.name");
			DataTable romTable = Database.ExecuteFill(Globals.Database._MachineConnectionString, "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");

			string manifestFilename = Path.Combine(targetDirectory, "_mame-ao-manifest-machine-rom.txt");
			string title = "Export Machine ROM";

			Tools.ConsoleHeading(1, new string[] { title, targetDirectory });

			DataTable manifestTable = DirectoryManifest(manifestFilename, title);

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

						string zipManifest = ZipManifest(nameHashes);
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

						ZipCreate(nameHashes, Globals.RomHashStore, tempDir.Path, targetFilename);

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

			Globals.Reports.SaveHtmlReport(manifestTable, title);
		}

		public static void MachineDisks(string targetDirectory)
		{
			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnectionString, "SELECT machine_id, name, description FROM machine ORDER BY machine.name");
			DataTable diskTable = Database.ExecuteFill(Globals.Database._MachineConnectionString, "SELECT machine_id, sha1, name, merge FROM disk WHERE sha1 IS NOT NULL");

			string manifestFilename = Path.Combine(targetDirectory, "_mame-ao-manifest-machine-disk.txt");
			string title = "Export Machine DISK";

			Tools.ConsoleHeading(1, new string[] { title, targetDirectory });

			DataTable manifestTable = DirectoryManifest(manifestFilename, title);

			try
			{
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

							if (row.IsNull("merge") == false)
								continue;

							string targetFilename = Path.Combine(targetDirectory, machine_name, name + ".chd");
							string targetName = targetFilename.Substring(targetDirectory.Length + 1);

							Directory.CreateDirectory(Path.GetDirectoryName(targetFilename));

							DataRow existingManifestRow = manifestTable.Rows.Find(targetName);

							if (existingManifestRow != null && (string)existingManifestRow["SHA1"] != sha1)
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

							File.Copy(Globals.DiskHashStore.Filename(sha1), targetFilename);

							FileInfo info = new FileInfo(targetFilename);

							manifestTable.Rows.Add(targetName, info.Length, info.LastWriteTime, sha1);

							Console.WriteLine(targetFilename);
						}
					}
				}
			}
			finally
			{
				File.WriteAllText(manifestFilename, Tools.TextTable(manifestTable), Encoding.UTF8);
			}

			Globals.Reports.SaveHtmlReport(manifestTable, title);
		}

		public static void SoftwareRoms(string targetDirectory)
		{
			DataTable softwarelistTable = Database.ExecuteFill(Globals.Database._SoftwareConnectionString, "SELECT softwarelist.softwarelist_id, softwarelist.name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");
			DataTable softwareTable = Database.ExecuteFill(Globals.Database._SoftwareConnectionString, "SELECT software.software_id, software.softwarelist_id, software.name, software.description FROM software ORDER BY software.name");
			DataTable romTable = Database.ExecuteFill(Globals.Database._SoftwareConnectionString, "SELECT part.software_id, rom.name, rom.sha1 FROM (part INNER JOIN dataarea ON part.part_id = dataarea.part_id) INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id WHERE (rom.sha1 IS NOT NULL)");

			string manifestFilename = Path.Combine(targetDirectory, "_mame-ao-manifest-software-rom.txt");
			string title = "Export Software ROM";

			Tools.ConsoleHeading(1, new string[] { title, targetDirectory });

			DataTable manifestTable = DirectoryManifest(manifestFilename, title);

			try
			{
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

							string targetFilename = Path.Combine(targetDirectory, softwarelist_name, software_name + ".zip");
							string targetName = targetFilename.Substring(targetDirectory.Length + 1);

							Directory.CreateDirectory(Path.GetDirectoryName(targetFilename));

							string zipManifest = ZipManifest(nameHashes);
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

							ZipCreate(nameHashes, Globals.RomHashStore, tempDir.Path, targetFilename);

							FileInfo info = new FileInfo(targetFilename);

							manifestTable.Rows.Add(targetName, info.Length, info.LastWriteTime, zipManifestSHA1);

							Console.WriteLine(targetFilename);
						}
					}
				}
			}
			finally
			{
				File.WriteAllText(manifestFilename, Tools.TextTable(manifestTable), Encoding.UTF8);
			}

			Globals.Reports.SaveHtmlReport(manifestTable, title);
		}

		public static void SoftwareDisks(string targetDirectory)
		{
			DataTable softwarelistTable = Database.ExecuteFill(Globals.Database._SoftwareConnectionString, "SELECT softwarelist.softwarelist_id, softwarelist.name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");
			DataTable softwareTable = Database.ExecuteFill(Globals.Database._SoftwareConnectionString, "SELECT software.software_id, software.softwarelist_id, software.name, software.description FROM software ORDER BY software.name");
			DataTable diskTable = Database.ExecuteFill(Globals.Database._SoftwareConnectionString, "SELECT part.software_id, disk.name, disk.sha1 FROM (part INNER JOIN diskarea ON part.part_id = diskarea.part_id) INNER JOIN disk ON diskarea.diskarea_id = disk.diskarea_id WHERE (disk.sha1 IS NOT NULL)");

			string manifestFilename = Path.Combine(targetDirectory, "_mame-ao-manifest-software-disk.txt");
			string title = "Export Software DISK";

			Tools.ConsoleHeading(1, new string[] { title, targetDirectory });

			DataTable manifestTable = DirectoryManifest(manifestFilename, title);

			try
			{
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

								string targetFilename = Path.Combine(targetDirectory, softwarelist_name, software_name, name + ".chd");
								string targetName = targetFilename.Substring(targetDirectory.Length + 1);

								Directory.CreateDirectory(Path.GetDirectoryName(targetFilename));

								DataRow existingManifestRow = manifestTable.Rows.Find(targetName);

								if (existingManifestRow != null && (string)existingManifestRow["SHA1"] != sha1)
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

								File.Copy(Globals.DiskHashStore.Filename(sha1), targetFilename);

								FileInfo info = new FileInfo(targetFilename);

								manifestTable.Rows.Add(targetName, info.Length, info.LastWriteTime, sha1);

								Console.WriteLine(targetFilename);
							}
						}
					}
				}
			}
			finally
			{
				File.WriteAllText(manifestFilename, Tools.TextTable(manifestTable), Encoding.UTF8);
			}

			Globals.Reports.SaveHtmlReport(manifestTable, title);
		}

	}
}
