using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;

namespace Spludlow.MameAO
{
	public class Place
	{
		public static void PlaceAssets(string machineName, string softwareName)
		{
			Tools.ConsoleHeading(1, "Asset Acquisition");
			Console.WriteLine();

			if (Globals.AuthCookie == null && Globals.BitTorrentAvailable == false)
			{
				Tools.ConsoleHeading(1, new string[] {
					"IMPORTANT - You must do either of the following to dowdload assets",
					"",
					"1) : Archive.org - Enter the command: .creds",
					"2) : BitTorrent  - Enter the command: .bt   ",
					""
				});

				return;
			}

			DataRow machine = Globals.Database.GetMachine(machineName) ?? throw new ApplicationException($"Machine not found: {machineName}");

			Globals.WorkerTaskReport = Reports.PlaceReportTemplate();

			int missingCount = 0;

			missingCount += PlaceMachineRoms(machineName, true);
			missingCount += PlaceMachineDisks(machineName, true);

			if (softwareName != "")
			{
				DataRow[] softwarelists = Globals.Database.GetMachineSoftwareLists(machine);
				int softwareFound = 0;

				HashSet<string> requiredSoftwareNames = new HashSet<string>(new string[] { softwareName });

				foreach (DataRow machineSoftwarelist in softwarelists)
				{
					string softwarelistName = (string)machineSoftwarelist["name"];

					DataRow softwarelist = Globals.Database.GetSoftwareList(softwarelistName);

					if (softwarelist == null)
					{
						Console.WriteLine($"!!! MAME DATA Error Machine's '{machineName}' software list '{softwarelistName}' missing.");
						continue;
					}

					foreach (DataRow findSoftware in Globals.Database.GetSoftwareListsSoftware(softwarelist))
					{
						if ((string)findSoftware["name"] == softwareName)
						{
							// Does this need to be recursive ?
							foreach (DataRow sharedFeat in Globals.Database.GetSoftwareSharedFeats(findSoftware))
							{
								if ((string)sharedFeat["name"] == "requirement")
								{
									// some don't have 2 parts
									string[] valueParts = ((string)sharedFeat["value"]).Split(new char[] { ':' });
									string value = valueParts[valueParts.Length - 1];

									requiredSoftwareNames.Add(value);
								}
							}
						}
					}
				}

				foreach (string requiredSoftwareName in requiredSoftwareNames)
				{
					foreach (DataRow machineSoftwarelist in softwarelists)
					{
						string softwarelistName = (string)machineSoftwarelist["name"];

						DataRow softwarelist = Globals.Database.GetSoftwareList(softwarelistName);

						if (softwarelist == null)
						{
							Console.WriteLine($"!!! MAME DATA Error Machine's '{machineName}' software list '{softwarelistName}' missing.");
							continue;
						}

						foreach (DataRow findSoftware in Globals.Database.GetSoftwareListsSoftware(softwarelist))
						{
							if ((string)findSoftware["name"] == requiredSoftwareName)
							{
								missingCount += PlaceSoftwareRoms(softwarelist, findSoftware, true);
								missingCount += PlaceSoftwareDisks(softwarelist, findSoftware, true);

								++softwareFound;
							}
						}
					}
				}

				if (softwareFound == 0)
					throw new ApplicationException($"Did not find software: {machineName}, {softwareName}");

				if (softwareFound > 1)
					Console.WriteLine("!!! Warning more than one software found, not sure which MAME will use. This can happern if the same name apears in different lists e.g. disk & cassette.");
			}

			Globals.Samples.PlaceAssets(machine);
			Globals.Artwork.PlaceAssets(machine);

			Cheats.Place();

			if (Globals.Settings.Options["PlaceReport"] == "Yes")
				Globals.Reports.SaveHtmlReport(Globals.WorkerTaskReport, $"Place Assets {machineName} {softwareName}".Trim());

			//
			// Info
			//
			Tools.ConsoleHeading(1, new string[] {
				"Machine Information",
				"",
				missingCount == 0 ? "Everything looks good to run MAME" : "!!! Missing ROM & Disk files. I doubt MAME will run !!!",
			});
			Console.WriteLine();

			DataRow[] features = Globals.Database.GetMachineFeatures(machine);

			Console.WriteLine($"Name:           {Tools.DataRowValue(machine, "name")}");
			Console.WriteLine($"Description:    {Tools.DataRowValue(machine, "description")}");
			Console.WriteLine($"Year:           {Tools.DataRowValue(machine, "year")}");
			Console.WriteLine($"Manufacturer:   {Tools.DataRowValue(machine, "manufacturer")}");
			Console.WriteLine($"Status:         {Tools.DataRowValue(machine, "ao_driver_status")}");

			foreach (DataRow feature in features)
				Console.WriteLine($"Feature issue:  {Tools.DataRowValue(feature, "type")} {Tools.DataRowValue(feature, "status")} {Tools.DataRowValue(feature, "overall")}");

			Console.WriteLine();
		}

		public static int PlaceMachineRoms(string mainMachineName, bool placeFiles)
		{
			int missingCount = 0;

			DataRow mainMachine = Globals.Database.GetMachine(mainMachineName) ?? throw new ApplicationException($"Machine not found: ${mainMachineName}");

			DataRow[] mainAssetRows = Globals.Database.GetMachineRoms(mainMachine);

			List<string> mainMachineNames = new List<string>(new string[] { mainMachineName });
			if (mainMachine.IsNull("cloneof") == false)
				mainMachineNames.Add((string)mainMachine["cloneof"]);

			for (int pass = 0; pass < 2; ++pass)
			{
				foreach (string machineName in FindAllMachines(mainMachineName))
				{
					string[] info = new string[] { "machine rom", mainMachineName, machineName };

					DataRow[] assetRows = mainAssetRows;
					if (mainMachineNames.Contains(machineName) == false)
						assetRows = Globals.Database.GetMachineRoms(Globals.Database.GetMachine(machineName) ?? throw new ApplicationException($"Machine not found: ${machineName}"));

					if (pass == 0)
					{
						if (AssetsRequired(Globals.RomHashStore, assetRows, info) == true)
						{
							if (Globals.BitTorrentAvailable == false)
							{
								ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.MachineRom][0];
								ArchiveOrgFile file = item.GetFile(machineName);
								if (file != null)
									DownloadImportFiles(item.DownloadLink(file), file.size, info);
							}
							else
							{
								var btFile = BitTorrent.MachineRom(machineName);
								if (btFile != null)
									DownloadImportFiles(btFile.Filename, btFile.Length, info);
							}
						}
					}
					else
					{
						if (placeFiles == true)
						{
							string targetDirectory = Path.Combine(Globals.MameDirectory, "roms", machineName);
							missingCount += PlaceAssetFiles(assetRows, Globals.RomHashStore, targetDirectory, null, info);
						}
					}
				}
			}

			return missingCount;
		}

		public static int PlaceMachineDisks(string machineName, bool placeFiles)
		{
			DataRow machineRow = Globals.Database.GetMachine(machineName);

			DataRow[] assetRows = Globals.Database.GetMachineDisks(machineRow);

			string[] info = new string[] { "machine disk", machineName, "" };

			if (AssetsRequired(Globals.DiskHashStore, assetRows, info) == true)
			{
				foreach (DataRow row in assetRows)
				{
					if ((bool)row["_required"] == false)
						continue;

					string sha1 = (string)row["sha1"];

					foreach (string[] key in MachineDiskAvailableKeys(machineRow, row))
					{
						string availableMachineName = key[0];
						string availableDiskName = key[1];

						if (Globals.BitTorrentAvailable == false)
						{
							ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.MachineDisk][0];
							ArchiveOrgFile file = item.GetFile($"{availableMachineName}/{availableDiskName}");
							if (file != null)
								DownloadImportDisk(item.DownloadLink(file), file.size, sha1, info);
						}
						else
						{
							var btFile = BitTorrent.MachineDisk(availableMachineName, availableDiskName);
							if (btFile != null)
								DownloadImportDisk(btFile.Filename, btFile.Length, sha1, info);
						}
					}
				}
			}

			string targetDirectory = Path.Combine(Globals.MameDirectory, "roms", machineName);

			if (placeFiles == true)
				return PlaceAssetFiles(assetRows, Globals.DiskHashStore, targetDirectory, ".chd", info);

			return 0;
		}

		public static string[][] MachineDiskAvailableKeys(DataRow machineRow, DataRow diskRow)
		{
			string machineName = Tools.DataRowValue(machineRow, "name");

			string diskName = Tools.DataRowValue(diskRow, "name");
			string merge = Tools.DataRowValue(diskRow, "merge");

			List<string> machineNames = new List<string>(new string[] { machineName });

			DataRow currentRow = machineRow;
			while (currentRow.IsNull("romof") == false)
			{
				string romof = (string)currentRow["romof"];
				machineNames.Add(romof);

				currentRow = Globals.Database.GetMachine(romof);
			}

			string availableDiskName = diskName;

			if (merge != null)
				availableDiskName = merge;

			List<string[]> keys = new List<string[]>();

			foreach (string availableMachineName in machineNames)
				keys.Add(new string[] { availableMachineName, availableDiskName });

			return keys.ToArray();
		}

		private static bool DownloadImportDisk(string urlOrFilename, long length, string expectedSha1, string[] info)
		{
			string tempFilename = Path.Combine(Globals.TempDirectory, DateTime.Now.ToString("s").Replace(":", "-") + "_" + expectedSha1 + ".chd");

			lock (Globals.WorkerTaskInfo)
			{
				Globals.WorkerTaskInfo.BytesTotal = length;
			}

			DateTime startTime = DateTime.Now;
			long size;

			if (urlOrFilename.StartsWith("http") == true)
			{
				Console.Write($"Downloading size:{Tools.DataSize(length)} url:{urlOrFilename} ...");
				size = Tools.Download(urlOrFilename, tempFilename, length);
				Console.WriteLine("...done");
			}
			else
			{
				File.Copy(urlOrFilename, tempFilename);

				FileInfo fileInfo = new FileInfo(tempFilename);
				size = fileInfo.Length;
			}

			TimeSpan took = DateTime.Now - startTime;
			if (took.TotalSeconds < 1)
				took = TimeSpan.FromSeconds(1);

			DateTime when = DateTime.Now;

			Globals.WorkerTaskReport.Tables["Download"].Rows.Add(when, info[0], info[1], info[2], urlOrFilename, size, (long)took.TotalSeconds);

			decimal mbPerSecond = (size / (decimal)took.TotalSeconds) / (1024.0M * 1024.0M);
			Console.WriteLine($"Download rate: {Math.Round(took.TotalSeconds, 3)}s = {Math.Round(mbPerSecond, 3)} MiB/s");

			if (length != size)
				Console.WriteLine($"!!! Unexpected downloaded file size expect:{length} actual:{size}");

			Console.Write($"CHD Verify {tempFilename} ...");
			string sha1 = Globals.DiskHashStore.Hash(tempFilename);
			Console.WriteLine("...done");

			if (sha1 != expectedSha1)
				Console.WriteLine($"!!! Unexpected downloaded CHD SHA1. It's wrong in the source and will not work. expect:{expectedSha1} actual:{sha1}");

			bool required = Globals.Database._AllSHA1s.Contains(sha1);
			bool imported = false;

			if (required == true)
				imported = Globals.DiskHashStore.Add(tempFilename, true, sha1);

			Globals.WorkerTaskReport.Tables["Import"].Rows.Add(when, info[0], info[1], info[2], sha1, required, imported, Path.GetFileName(tempFilename));

			return true;
		}

		public static int PlaceSoftwareRoms(DataRow softwareList, DataRow software, bool placeFiles)
		{
			string softwareListName = (string)softwareList["name"];
			string softwareName = (string)software["name"];

			DataRow[] assetRows = Globals.Database.GetSoftwareRoms(software);

			string[] info = new string[] { "software rom", softwareListName, softwareName };

			if (AssetsRequired(Globals.RomHashStore, assetRows, info) == true)
			{
				string requiredSoftwareName = softwareName;
				string parentSoftwareName = Tools.DataRowValue(software, "cloneof");
				if (parentSoftwareName != null)
					requiredSoftwareName = parentSoftwareName;

				if (Globals.BitTorrentAvailable == false)
				{
					ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.SoftwareRom][0];
					ArchiveOrgFile file = item.GetFile(softwareListName);
					if (file == null)
						return 0;

					string listEnc = Uri.EscapeDataString(softwareListName);
					string softEnc = Uri.EscapeDataString(requiredSoftwareName);

					string url = item.DownloadLink(file) + "/@LIST@%2f@SOFTWARE@.zip";
					url = url.Replace("@LIST@", listEnc);
					url = url.Replace("@SOFTWARE@", softEnc);

					Dictionary<string, long> softwareSizes = item.GetZipContentsSizes(file, softwareListName.Length + 1, 4);

					if (softwareSizes == null)
						throw new ApplicationException($"Can't get software sizes for Software ROM in list: {softwareListName}");

					if (softwareSizes.ContainsKey(requiredSoftwareName) == true)
						DownloadImportFiles(url, softwareSizes[requiredSoftwareName], info);
				}
				else
				{
					var btFile = BitTorrent.SoftwareRom(softwareListName, requiredSoftwareName);
					if (btFile != null)
						DownloadImportFiles(btFile.Filename, btFile.Length, info);
				}
			}

			string targetDirectory = Path.Combine(Globals.MameDirectory, "roms", softwareListName, softwareName);

			if (placeFiles == true)
				return PlaceAssetFiles(assetRows, Globals.RomHashStore, targetDirectory, null, info);

			return 0;
		}

		public static int PlaceSoftwareDisks(DataRow softwareList, DataRow software, bool placeFiles)
		{
			string softwareListName = (string)softwareList["name"];
			string softwareName = (string)software["name"];

			DataRow[] assetRows = Globals.Database.GetSoftwareDisks(software);

			string[] info = new string[] { "software disk", softwareListName, softwareName };

			if (AssetsRequired(Globals.DiskHashStore, assetRows, info) == true)
			{
				List<string> downloadSoftwareNames = new List<string>(new string[] { softwareName });

				string parentSoftwareName = Tools.DataRowValue(software, "cloneof");
				if (parentSoftwareName != null)
					downloadSoftwareNames.Add(parentSoftwareName);

				foreach (DataRow row in assetRows)
				{
					string name = (string)row["name"];
					string sha1 = (string)row["sha1"];

					foreach (string downloadSoftwareName in downloadSoftwareNames)
					{
						if (Globals.BitTorrentAvailable == false)
						{
							bool found = false;

							ArchiveOrgItem[] items = ArchiveOrgItem.GetItems(ItemType.SoftwareDisk, softwareListName);
							foreach (ArchiveOrgItem item in items)
							{
								if (found == true)
									break;

								string key = $"{softwareListName}/{downloadSoftwareName}/{name}";

								if (item.Tag != null && item.Tag != "*")
									key = $"{downloadSoftwareName}/{name}";

								ArchiveOrgFile file = item.GetFile(key);
								if (file != null)
									found = DownloadImportDisk(item.DownloadLink(file), file.size, sha1, info);
							}
						}
						else
						{
							var btFile = BitTorrent.SoftwareDisk(softwareListName, downloadSoftwareName, name);
							if (btFile != null)
								DownloadImportDisk(btFile.Filename, btFile.Length, sha1, info);
						}
					}
				}
			}

			string targetDirectory = Path.Combine(Globals.MameDirectory, "roms", softwareListName, softwareName);

			if (placeFiles == true)
				return PlaceAssetFiles(assetRows, Globals.DiskHashStore, targetDirectory, ".chd", info);

			return 0;
		}

		public static ArchiveOrgFile MachineDiskAvailableSourceFile(DataRow machineRow, DataRow diskRow, ArchiveOrgItem sourceItem)
		{
			string machineName = Tools.DataRowValue(machineRow, "name");

			string diskName = Tools.DataRowValue(diskRow, "name");
			string merge = Tools.DataRowValue(diskRow, "merge");

			List<string> machineNames = new List<string>(new string[] { machineName });

			DataRow currentRow = machineRow;
			while (currentRow.IsNull("romof") == false)
			{
				string romof = (string)currentRow["romof"];
				machineNames.Add(romof);

				currentRow = Globals.Database.GetMachine(romof);
			}

			string availableDiskName = diskName;

			if (merge != null)
				availableDiskName = merge;

			foreach (string availableMachineName in machineNames)
			{
				string key = $"{availableMachineName}/{availableDiskName}";

				ArchiveOrgFile file = sourceItem.GetFile(key);

				if (file != null)
					return file;
			}

			return null;
		}

		public static bool AssetsRequired(HashStore hashStore, DataRow[] assetRows, string[] info)
		{
			if (assetRows.Length > 0 && assetRows[0].Table.Columns.Contains("_required") == false)
				assetRows[0].Table.Columns.Add("_required", typeof(bool));

			DateTime when = DateTime.Now;

			bool downloadRequired = false;

			foreach (DataRow row in assetRows)
			{
				string sha1 = (string)row["sha1"];
				string name = (string)row["name"];

				bool required = !hashStore.Exists(sha1);

				row["_required"] = required;

				if (required == true)
					downloadRequired = true;

				Globals.WorkerTaskReport.Tables["Require"].Rows.Add(when, info[0], info[1], info[2], sha1, required, name);
			}

			return downloadRequired;
		}

		public static void DownloadImportFiles(string urlOrFilename, long expectedSize, string[] info)
		{
			using (TempDirectory tempDir = new TempDirectory())
			{
				string archiveFilename = Path.Combine(tempDir.Path, "archive.zip");
				string extractDirectory = Path.Combine(tempDir.Path, "OUT");
				Directory.CreateDirectory(extractDirectory);

				DateTime startTime = DateTime.Now;
				long size;

				if (urlOrFilename.StartsWith("http") == true)
				{
					Console.Write($"Downloading size:{Tools.DataSize(expectedSize)} url:{urlOrFilename} ...");
					size = Tools.Download(urlOrFilename, archiveFilename, expectedSize);
					Console.WriteLine("...done");
				}
				else
				{
					File.Copy(urlOrFilename, archiveFilename);

					FileInfo fileInfo = new FileInfo(archiveFilename);
					size = fileInfo.Length;
				}

				TimeSpan took = DateTime.Now - startTime;
				if (took.TotalSeconds < 1)
					took = TimeSpan.FromSeconds(1);

				decimal kbPerSecond = (size / (decimal)took.TotalSeconds) / 1024.0M;
				Console.WriteLine($"Download rate: {Math.Round(took.TotalSeconds, 3)}s = {Math.Round(kbPerSecond, 3)} KiB/s");
				if (size != expectedSize)
					Console.WriteLine($"!!! Unexpected file size expect:{expectedSize} actual:{size}");

				DateTime when = DateTime.Now;

				Globals.WorkerTaskReport.Tables["Download"].Rows.Add(when, info[0], info[1], info[2], urlOrFilename, expectedSize, (long)took.TotalSeconds);

				Console.Write($"Extracting {archiveFilename} ...");
				ZipFile.ExtractToDirectory(archiveFilename, extractDirectory);
				Console.WriteLine("...done");

				Tools.ClearAttributes(tempDir.Path);

				foreach (string filename in Directory.GetFiles(extractDirectory, "*", SearchOption.AllDirectories))
				{
					string subPathName = filename.Substring(extractDirectory.Length);
					string sha1 = Globals.RomHashStore.Hash(filename);
					bool required = Globals.Database._AllSHA1s.Contains(sha1);
					bool imported = false;

					if (required == true)
						imported = Globals.RomHashStore.Add(filename);

					Globals.WorkerTaskReport.Tables["Import"].Rows.Add(when, info[0], info[1], info[2], sha1, required, imported, subPathName);
				}
			}
		}

		public static int PlaceAssetFiles(DataRow[] assetRows, HashStore hashStore, string targetDirectory, string filenameAppend, string[] info)
		{
			int missingCount = 0;

			List<string[]> targetStoreFilenames = new List<string[]>();

			DateTime when = DateTime.Now;

			foreach (DataRow row in assetRows)
			{
				string name = (string)row["name"];
				string sha1 = (string)row["sha1"];

				string targetFilename = Path.Combine(targetDirectory, name);
				if (filenameAppend != null)
					targetFilename += filenameAppend;

				bool fileExists = File.Exists(targetFilename);
				bool have = hashStore.Exists(sha1);
				bool place = fileExists == false && have == true;

				if (have == false)
					++missingCount;

				if (place == true)
					targetStoreFilenames.Add(new string[] { targetFilename, hashStore.Filename(sha1) });

				Globals.WorkerTaskReport.Tables["Place"].Rows.Add(when, info[0], info[1], info[2], sha1, place, have, name);
			}

			PlaceFiles(targetStoreFilenames.ToArray());

			return missingCount;
		}
		private static void PlaceFiles(string[][] targetStoreFilenames)
		{
			HashSet<string> directories = new HashSet<string>();
			foreach (string[] targetStoreFilename in targetStoreFilenames)
				directories.Add(Path.GetDirectoryName(targetStoreFilename[0]));

			foreach (string directory in directories)
				Directory.CreateDirectory(directory);

			if (Globals.LinkingEnabled == true)
			{
				Tools.LinkFiles(targetStoreFilenames);
			}
			else
			{
				foreach (string[] targetStoreFilename in targetStoreFilenames)
					File.Copy(targetStoreFilename[1], targetStoreFilename[0], true);
			}
		}

		private static HashSet<string> FindAllMachines(string machineName)
		{
			HashSet<string> requiredMachines = new HashSet<string>();

			DataRow machineRow = Globals.Database.GetMachine(machineName) ?? throw new ApplicationException($"Machine not found (FindAllMachines): ${machineName}");
			if (machineRow.IsNull("cloneof") == false)
				requiredMachines.Add((string)machineRow["cloneof"]);

			FindAllMachinesWork(machineName, requiredMachines);

			return requiredMachines;
		}
		private static void FindAllMachinesWork(string machineName, HashSet<string> requiredMachines)
		{
			if (requiredMachines.Contains(machineName) == true)
				return;

			DataRow machineRow = Globals.Database.GetMachine(machineName) ?? throw new ApplicationException($"Machine not found (FindAllMachinesWork): ${machineName}");

			if ((long)machineRow["ao_rom_count"] > 0)
				requiredMachines.Add(machineName);

			string romof = machineRow.IsNull("romof") ? null : (string)machineRow["romof"];

			if (romof != null)
				FindAllMachinesWork(romof, requiredMachines);

			foreach (DataRow row in Globals.Database.GetMachineDeviceRefs(machineName))
				FindAllMachinesWork((string)row["name"], requiredMachines);
		}

	}
}
