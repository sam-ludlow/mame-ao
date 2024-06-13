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

			DataRow machine = Globals.Database.GetMachine(machineName) ?? throw new ApplicationException($"Machine not found: {machineName}");

			//Globals.PlaceReport = Reports.PlaceReportTemplate("Place Assets OLD HEADING");

			int missingCount = 0;

			missingCount += PlaceMachineRoms(machineName);
			missingCount += PlaceMachineDisks(machineName);

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
								missingCount += PlaceSoftwareRoms(softwarelist, findSoftware);
								missingCount += PlaceSoftwareDisks(softwarelist, findSoftware);

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

			//if (Globals.Settings.Options["PlaceReport"] == "Yes")
			//	Globals.Reports.SaveHtmlReport(Globals.PlaceReport, $"Place Assets {machineName} {softwareName}".Trim());

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
			Console.WriteLine($"isbios:         {Tools.DataRowValue(machine, "isbios")}");
			Console.WriteLine($"isdevice:       {Tools.DataRowValue(machine, "isdevice")}");
			Console.WriteLine($"ismechanical:   {Tools.DataRowValue(machine, "ismechanical")}");
			Console.WriteLine($"runnable:       {Tools.DataRowValue(machine, "runnable")}");

			foreach (DataRow feature in features)
				Console.WriteLine($"Feature issue: {Tools.DataRowValue(feature, "type")} {Tools.DataRowValue(feature, "status")} {Tools.DataRowValue(feature, "overall")}");

			Console.WriteLine();
		}



		private static int PlaceMachineRoms(string mainMachineName)
		{
			int missingCount = 0;

			ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.MachineRom][0];

			foreach (string machineName in FindAllMachines(mainMachineName))
			{
				DataRow machineRow = Globals.Database.GetMachine(machineName) ?? throw new ApplicationException($"Machine not found: ${machineName}");

				DataRow[] assetRows = Globals.Database.GetMachineRoms(machineRow);

				if (AssetsRequired(Globals.RomHashStore, assetRows) == true)
				{
					ArchiveOrgFile file = item.GetFile(machineName);
					if (file != null)
						DownloadImportFiles(item.DownloadLink(file), file.size);
				}

				string targetDirectory = Path.Combine(Globals.MameDirectory, "roms", machineName);

				missingCount += PlaceAssetFiles(assetRows, Globals.RomHashStore, targetDirectory, null);
			}

			return missingCount;
		}

		private static int PlaceMachineDisks(string machineName)
		{
			ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.MachineDisk][0];

			DataRow machineRow = Globals.Database.GetMachine(machineName);

			DataRow[] assetRows = Globals.Database.GetMachineDisks(machineRow);

			if (AssetsRequired(Globals.DiskHashStore, assetRows) == true)
			{
				foreach (DataRow row in assetRows)
				{
					if ((bool)row["_required"] == false)
						continue;

					string name = (string)row["name"];
					string sha1 = (string)row["sha1"];

					ArchiveOrgFile file = MachineDiskAvailableSourceFile(machineRow, row, item);

					if (file != null)
						DownloadImportDisk(item, file, sha1);
				}
			}

			string targetDirectory = Path.Combine(Globals.MameDirectory, "roms", machineName);

			return PlaceAssetFiles(assetRows, Globals.DiskHashStore, targetDirectory, ".chd");
		}

		private static int PlaceSoftwareRoms(DataRow softwareList, DataRow software)
		{
			ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.SoftwareRom][0];

			string softwareListName = (string)softwareList["name"];
			string softwareName = (string)software["name"];

			ArchiveOrgFile file = item.GetFile(softwareListName);

			if (file == null)
				return 0;

			DataRow[] assetRows = Globals.Database.GetSoftwareRoms(software);

			if (AssetsRequired(Globals.RomHashStore, assetRows) == true)
			{
				string requiredSoftwareName = softwareName;
				string parentSoftwareName = Tools.DataRowValue(software, "cloneof");
				if (parentSoftwareName != null)
					requiredSoftwareName = parentSoftwareName;

				string listEnc = Uri.EscapeDataString(softwareListName);
				string softEnc = Uri.EscapeDataString(requiredSoftwareName);

				string url = item.DownloadLink(file) + "/@LIST@%2f@SOFTWARE@.zip";
				url = url.Replace("@LIST@", listEnc);
				url = url.Replace("@SOFTWARE@", softEnc);

				Dictionary<string, long> softwareSizes = item.GetZipContentsSizes(file, softwareListName.Length + 1, 4);

				if (softwareSizes.ContainsKey(requiredSoftwareName) == true)
					DownloadImportFiles(url, softwareSizes[requiredSoftwareName]);
			}

			string targetDirectory = Path.Combine(Globals.MameDirectory, "roms", softwareListName, softwareName);

			return PlaceAssetFiles(assetRows, Globals.RomHashStore, targetDirectory, null);
		}

		private static int PlaceSoftwareDisks(DataRow softwareList, DataRow software)
		{
			string softwareListName = (string)softwareList["name"];
			string softwareName = (string)software["name"];

			ArchiveOrgItem[] items = ArchiveOrgItem.GetItems(ItemType.SoftwareDisk, softwareListName);

			DataRow[] assetRows = Globals.Database.GetSoftwareDisks(software);

			if (AssetsRequired(Globals.DiskHashStore, assetRows) == true)
			{
				List<string> downloadSoftwareNames = new List<string>(new string[] { softwareName });

				string parentSoftwareName = Tools.DataRowValue(software, "cloneof");
				if (parentSoftwareName != null)
					downloadSoftwareNames.Add(parentSoftwareName);

				foreach (DataRow row in assetRows)
				{
					string name = (string)row["name"];
					string sha1 = (string)row["sha1"];

					foreach (ArchiveOrgItem item in items)
					{
						foreach (string downloadSoftwareName in downloadSoftwareNames)
						{
							string key = $"{softwareListName}/{downloadSoftwareName}/{name}";

							if (item.Tag != null && item.Tag != "*")
								key = $"{downloadSoftwareName}/{name}";

							ArchiveOrgFile file = item.GetFile(key);

							if (file != null)
								DownloadImportDisk(item, file, sha1);
						}
					}
				}
			}

			string targetDirectory = Path.Combine(Globals.MameDirectory, "roms", softwareListName, softwareName);

			return PlaceAssetFiles(assetRows, Globals.DiskHashStore, targetDirectory, ".chd");
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

		private static bool AssetsRequired(HashStore hashStore, DataRow[] assetRows)
		{
			if (assetRows.Length > 0)
				assetRows[0].Table.Columns.Add("_required", typeof(bool));

			bool downloadRequired = false;

			foreach (DataRow row in assetRows)
			{
				string sha1 = (string)row["sha1"];
				string name = (string)row["name"];

				bool required = !hashStore.Exists(sha1);

				row["_required"] = required;

				if (required == true)
					downloadRequired = true;

				Console.WriteLine($"{sha1}\t{required}\t{name}");
			}

			return downloadRequired;
		}

		public static void DownloadImportFiles(string url, long expectedSize)
		{
			using (TempDirectory tempDir = new TempDirectory())
			{
				string archiveFilename = Path.Combine(tempDir.Path, "archive.zip");
				string extractDirectory = Path.Combine(tempDir.Path, "OUT");
				Directory.CreateDirectory(extractDirectory);

				Console.Write($"Downloading size:{Tools.DataSize(expectedSize)} url:{url} ...");
				DateTime startTime = DateTime.Now;
				long size = Tools.Download(url, archiveFilename, Globals.DownloadDotSize, 30);
				TimeSpan took = DateTime.Now - startTime;
				Console.WriteLine("...done");

				decimal kbPerSecond = (size / (decimal)took.TotalSeconds) / 1024.0M;
				Console.WriteLine($"Download rate: {Math.Round(took.TotalSeconds, 3)}s = {Math.Round(kbPerSecond, 3)} KiB/s");
				if (size != expectedSize)
					Console.WriteLine($"!!! Unexpected downloaded file size expect:{expectedSize} actual:{size}");

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

					Console.WriteLine($"{sha1}\t{imported}\t{required}\t{subPathName}");
				}
			}
		}

		private static void DownloadImportDisk(ArchiveOrgItem item, ArchiveOrgFile file, string expectedSha1)
		{
			if (Globals.BadSources.AlreadyDownloaded(file) == true)
			{
				Console.WriteLine($"!!! Already Downloaded before and it didn't work (bad in source) chd-sha1:{expectedSha1} source-sha1: {file.sha1}");
				return;
			}

			string downloadTempDirectory = Path.Combine(Globals.RootDirectory, "_TEMP");
			string tempFilename = Path.Combine(downloadTempDirectory, DateTime.Now.ToString("s").Replace(":", "-") + "_" + Tools.ValidFileName(file.name) + ".chd");

			lock (Globals.AO._TaskInfo)
			{
				Globals.AO._TaskInfo.BytesTotal = file.size;
			}

			string url = item.DownloadLink(file);
			Console.Write($"Downloading {file.name} size:{Tools.DataSize(file.size)} url:{url} ...");
			DateTime startTime = DateTime.Now;
			long size = Tools.Download(url, tempFilename, Globals.DownloadDotSize, 3 * 60, Globals.AO._TaskInfo);
			TimeSpan took = DateTime.Now - startTime;
			Console.WriteLine("...done");

			decimal mbPerSecond = (size / (decimal)took.TotalSeconds) / (1024.0M * 1024.0M);
			Console.WriteLine($"Download rate: {Math.Round(took.TotalSeconds, 3)}s = {Math.Round(mbPerSecond, 3)} MiB/s");

			if (file.size != size)
				Console.WriteLine($"!!! Unexpected downloaded file size expect:{file.size} actual:{size}");

			string sha1 = Globals.DiskHashStore.Hash(tempFilename);

			if (sha1 != expectedSha1)
			{
				Console.WriteLine($"!!! Unexpected downloaded CHD SHA1. It's wrong in the source and will not work. expect:{expectedSha1} actual:{sha1}");
				Globals.BadSources.ReportSourceFile(file, expectedSha1, sha1);
			}

			bool required = Globals.Database._AllSHA1s.Contains(sha1);
			bool imported = false;

			if (required == true)
				imported = Globals.DiskHashStore.Add(tempFilename, true, sha1);

			Console.WriteLine($"{sha1}\t{imported}\t{required}\t{file.name}");
		}

		public static int PlaceAssetFiles(DataRow[] assetRows, HashStore hashStore, string targetDirectory, string filenameAppend)
		{
			int missingCount = 0;

			List<string[]> targetStoreFilenames = new List<string[]>();

			foreach (DataRow row in assetRows)
			{
				string name = (string)row["name"];
				string sha1 = (string)row["sha1"];

				string targetFilename = Path.Combine(targetDirectory, name);
				if (filenameAppend != null)
					targetFilename += filenameAppend;

				bool fileExists = File.Exists(targetFilename);
				bool storeExists = hashStore.Exists(sha1);
				bool place = fileExists == false && storeExists == true;

				if (storeExists == false)
					++missingCount;

				if (place == true)
					targetStoreFilenames.Add(new string[] { targetFilename, hashStore.Filename(sha1) });

				Console.WriteLine($"{sha1}\t{place}\t{storeExists}\t{name}");
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
			FindAllMachines(machineName, requiredMachines);
			return requiredMachines;
		}
		private static void FindAllMachines(string machineName, HashSet<string> requiredMachines)
		{
			if (requiredMachines.Contains(machineName) == true)
				return;

			DataRow machineRow = Globals.Database.GetMachine(machineName);

			if (machineRow == null)
				throw new ApplicationException("FindAllMachines machine not found: " + machineName);

			if ((long)machineRow["ao_rom_count"] > 0)
				requiredMachines.Add(machineName);

			string romof = machineRow.IsNull("romof") ? null : (string)machineRow["romof"];

			if (romof != null)
				FindAllMachines(romof, requiredMachines);

			foreach (DataRow row in Globals.Database.GetMachineDeviceRefs(machineName))
				FindAllMachines((string)row["name"], requiredMachines);
		}

	}
}
