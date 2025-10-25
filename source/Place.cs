using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Spludlow.MameAO
{
	public class Place
	{
		public static void PlaceAssets(ICore core, string machineName, string softwareName)
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

			if (Globals.BitTorrentAvailable == false && core.Name != "mame")
				throw new ApplicationException("Archive.org downloads are only supported for MAME");

			DataRow machine = core.GetMachine(machineName) ?? throw new ApplicationException($"Machine not found: {machineName}");

			Globals.WorkerTaskReport = Reports.PlaceReportTemplate();

			int missingCount = 0;

			missingCount += PlaceMachineRoms(core, machineName, true);
			missingCount += PlaceMachineDisks(core, machineName, true);

			if (softwareName != "")
			{
				List<string> requiredSoftwareNames = new List<string>(new string[] { softwareName });

				DataRow[] softwarelists = core.GetMachineSoftwareLists(machine);
				int softwareFound = 0;

				foreach (DataRow machineSoftwarelist in softwarelists)
				{
					string softwarelistName = (string)machineSoftwarelist["name"];

					DataRow softwarelistRow = core.GetSoftwareList(softwarelistName);
					if (softwarelistRow == null)
					{
						Console.WriteLine($"!!! DATA Error Machine's '{machineName}' software list '{softwarelistName}' missing.");
						continue;
					}

					DataRow softwareRow = core.GetSoftware(softwarelistRow, softwareName);
					if (softwareRow != null)
					{
						//Does this need to be recursive ?
						foreach (DataRow sharedFeat in core.GetSoftwareSharedFeats(softwareRow))
						{
							if ((string)sharedFeat["name"] == "requirement")
							{
								string[] valueParts = ((string)sharedFeat["value"]).Split(':');

								string requirementSoftware = valueParts[valueParts.Length - 1];

								if (requiredSoftwareNames.Contains(requirementSoftware) == false)
									requiredSoftwareNames.Add(requirementSoftware);
							}
						}
					}
				}

				foreach (string requiredSoftwareName in requiredSoftwareNames)
				{
					foreach (DataRow machineSoftwarelist in softwarelists)
					{
						string softwarelistName = (string)machineSoftwarelist["name"];

						DataRow softwarelist = core.GetSoftwareList(softwarelistName);

						if (softwarelist == null)
						{
							Console.WriteLine($"!!! DATA Error Machine's '{machineName}' software list '{softwarelistName}' missing.");
							continue;
						}

						foreach (DataRow findSoftware in core.GetSoftwareListsSoftware(softwarelist))
						{
							if ((string)findSoftware["name"] == requiredSoftwareName)
							{
								missingCount += PlaceSoftwareRoms(core, softwarelist, findSoftware, true);
								missingCount += PlaceSoftwareDisks(core, softwarelist, findSoftware, true);

								++softwareFound;
							}
						}
					}
				}

				if (softwareFound == 0)
					throw new ApplicationException($"Did not find software: {machineName}, {softwareName}");
			}

			Globals.Samples.PlaceAssets(core.Directory, machine);
			Globals.Artwork.PlaceAssets(core.Directory, machine);

			Cheats.Place(core.Directory);

			if (Globals.Settings.Options["PlaceReport"] == "Yes")
				Globals.Reports.SaveHtmlReport(Globals.WorkerTaskReport, $"Place Assets {machineName} {softwareName}".Trim());

			//
			// Info
			//
			Tools.ConsoleHeading(1, new string[] {
				"Machine Information",
				"",
				missingCount == 0 ? "Everything looks good" : "!!! Missing ROM & Disk files. I doubt MAME will run !!!",
			});
			Console.WriteLine();

			DataRow[] features = Globals.Core.GetMachineFeatures(machine);

			Console.WriteLine($"Name:           {Tools.DataRowValue(machine, "name")}");
			Console.WriteLine($"Description:    {Tools.DataRowValue(machine, "description")}");
			Console.WriteLine($"Year:           {Tools.DataRowValue(machine, "year")}");
			Console.WriteLine($"Manufacturer:   {Tools.DataRowValue(machine, "manufacturer")}");
			Console.WriteLine($"Status:         {Tools.DataRowValue(machine, "ao_driver_status")}");

			foreach (DataRow feature in features)
				Console.WriteLine($"Feature issue:  {Tools.DataRowValue(feature, "type")} {Tools.DataRowValue(feature, "status")} {Tools.DataRowValue(feature, "overall")}");

			Console.WriteLine();
		}

		public static int PlaceMachineRoms(ICore core, string machine_name, bool placeFiles)
		{
			var requiredMachineAssets = new Dictionary<string, DataRow[]>();
			foreach (string name in core.GetReferencedMachines(machine_name))
			{
				DataRow[] assetRows = core.GetMachineRoms(name);
				if (assetRows.Length > 0)
					requiredMachineAssets.Add(name, assetRows);
			}

			// Child has roms and parent does not, still need parent ZIP
			DataRow machine = core.GetMachine(machine_name);
			if (machine.IsNull("cloneof") == false)
			{
				string cloneof_machine_name = (string)machine["cloneof"];
				if (requiredMachineAssets.ContainsKey(cloneof_machine_name) == false && requiredMachineAssets.ContainsKey(machine_name) == true)
					requiredMachineAssets.Add(cloneof_machine_name, requiredMachineAssets[machine_name]);
			}

			Console.WriteLine($"Required Machines: {String.Join(", ", requiredMachineAssets.Select(pair => $"{pair.Key}({pair.Value.Length})"))}");

			int missingCount = 0;

			for (int pass = 0; pass < 2; ++pass)
			{
				foreach (string requiredMachineName in requiredMachineAssets.Keys)
				{
					DataRow[] assetRows = requiredMachineAssets[requiredMachineName];

					string[] info = new string[] { "machine rom", machine_name, requiredMachineName };

					if (pass == 0)
					{
						if (AssetsRequired(Globals.RomHashStore, assetRows, info) == true)
						{
							if (Globals.BitTorrentAvailable == false)
							{
								ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.MachineRom][0];
								ArchiveOrgFile file = item.GetFile(requiredMachineName);
								if (file != null)
									DownloadImportFiles(item.DownloadLink(file), file.size, info);
							}
							else
							{
								var btFile = BitTorrent.MachineRom(core.Name, requiredMachineName);
								if (btFile != null)
									DownloadImportFiles(btFile.Filename, btFile.Length, info);
							}
						}
					}
					else
					{
						if (placeFiles == true)
						{
							string targetDirectory = Path.Combine(core.Directory, "roms", requiredMachineName);
							missingCount += PlaceAssetFiles(assetRows, Globals.RomHashStore, targetDirectory, null, info);
						}
					}
				}
			}

			return missingCount;
		}

		public static int PlaceMachineDisks(ICore core, string machineName, bool placeFiles)
		{
			DataRow machineRow = core.GetMachine(machineName);

			DataRow[] assetRows = core.GetMachineDisks(machineRow);

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
							var btFile = BitTorrent.MachineDisk(core.Name, availableMachineName, availableDiskName);
							if (btFile != null)
								DownloadImportDisk(btFile.Filename, btFile.Length, sha1, info);
						}
					}
				}
			}

			string targetDirectory = Path.Combine(core.Directory, "roms", machineName);

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

				currentRow = Globals.Core.GetMachine(romof);
			}

			string availableDiskName = diskName;

			if (merge != null)
				availableDiskName = merge;

			List<string[]> keys = new List<string[]>();

			foreach (string availableMachineName in machineNames)
				keys.Add(new string[] { availableMachineName, availableDiskName });

			return keys.ToArray();
		}

		public static int PlaceSoftwareRoms(ICore core, DataRow softwareList, DataRow software, bool placeFiles)
		{
			string softwareListName = (string)softwareList["name"];
			string softwareName = (string)software["name"];

			DataRow[] assetRows = core.GetSoftwareRoms(software);

			string[] info = new string[] { "software rom", softwareListName, softwareName };

			if (AssetsRequired(Globals.RomHashStore, assetRows, info) == true)
			{
				string requiredSoftwareName = softwareName;
				string parentSoftwareName = software.Table.Columns.Contains("cloneof") == true ? Tools.DataRowValue(software, "cloneof") : null;
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
					var btFile = BitTorrent.SoftwareRom(core.Name, softwareListName, requiredSoftwareName);
					if (btFile != null)
						DownloadImportFiles(btFile.Filename, btFile.Length, info);
				}
			}

			string targetDirectory = Path.Combine(core.Directory, "roms", softwareListName, softwareName);

			if (placeFiles == true)
				return PlaceAssetFiles(assetRows, Globals.RomHashStore, targetDirectory, null, info);

			return 0;
		}

		public static int PlaceSoftwareDisks(ICore core, DataRow softwareList, DataRow software, bool placeFiles)
		{
			string softwareListName = (string)softwareList["name"];
			string softwareName = (string)software["name"];

			DataRow[] assetRows = core.GetSoftwareDisks(software);

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
							var btFile = BitTorrent.SoftwareDisk(core.Name, softwareListName, downloadSoftwareName, name);
							if (btFile != null)
								DownloadImportDisk(btFile.Filename, btFile.Length, sha1, info);
						}
					}
				}
			}

			string targetDirectory = Path.Combine(core.Directory, "roms", softwareListName, softwareName);

			if (placeFiles == true)
				return PlaceAssetFiles(assetRows, Globals.DiskHashStore, targetDirectory, ".chd", info);

			return 0;
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

			bool required = Globals.AllSHA1.Contains(sha1);
			bool imported = false;

			if (required == true)
				imported = Globals.DiskHashStore.Add(tempFilename, true, sha1);

			Globals.WorkerTaskReport.Tables["Import"].Rows.Add(when, info[0], info[1], info[2], sha1, required, imported, Path.GetFileName(tempFilename));

			return true;
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
					string subPathName = filename.Substring(extractDirectory.Length + 1);
					string sha1 = Globals.RomHashStore.Hash(filename);
					bool required = Globals.AllSHA1.Contains(sha1);
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
	}
}
