using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace Spludlow.MameAO
{
	public class Sources
	{
		public enum MameSetType
		{
			MachineRom,
			MachineDisk,
			SoftwareRom,
			SoftwareDisk,
		};

		private static readonly DateTime _EpochDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		public class SourceFileInfo
		{
			public SourceFileInfo(dynamic file)
			{
				name = file.name;
				size = Int64.Parse((string)file.size);
				sha1 = file.sha1;
				mtime = _EpochDateTime.AddSeconds(double.Parse((string)file.mtime));
			}

			public string name { get; set; }
			public long size { get; set; }
			public string sha1 { get; set; }
			public DateTime mtime { get; set; }
			public string url { get; set; }
		}

		public class MameSourceSet
		{
			public MameSetType SetType;
			public string ListName;
			public string DetailsUrl;
			public string MetadataUrl;
			public string DownloadUrl;
			public string HtmlSizesUrl;
			public Dictionary<string, SourceFileInfo> AvailableDownloadFileInfos = new Dictionary<string, SourceFileInfo>();

			public string Title;
			public string Version;
			public string Status;
		}

		private List<MameSourceSet> MameSourceSets = new List<MameSourceSet>(new MameSourceSet[] {
			
			//
			// Machine ROM
			//
			new MameSourceSet
			{
				SetType = MameSetType.MachineRom,
				DetailsUrl = "https://archive.org/details/mame-merged",
				MetadataUrl = "https://archive.org/metadata/mame-merged",
				DownloadUrl = "https://archive.org/download/mame-merged/mame-merged/@MACHINE@.zip",
				HtmlSizesUrl = null,
			},

			//
			// Machine DISK
			//
			new MameSourceSet
			{
				//	This item name is misleading, check the item's title on archive.org. It is a very good and kept up to date, 
				SetType = MameSetType.MachineDisk,
				DetailsUrl = "https://archive.org/details/MAME_0.225_CHDs_merged",
				MetadataUrl = "https://archive.org/metadata/MAME_0.225_CHDs_merged",
				DownloadUrl = "https://archive.org/download/MAME_0.225_CHDs_merged/@MACHINE@/@DISK@.chd",
				HtmlSizesUrl = null,
			},
			//new MameSourceSet	TODO: Add fallback support for multiple machine disk
			//{
			//	//	This item is not prefered for machine disk but will do if all else fails.
			//	SetType = MameSetType.MachineDisk,
			//	DetailsUrl = "https://archive.org/details/mame-chds-roms-extras-complete",
			//	MetadataUrl = "https://archive.org/metadata/mame-chds-roms-extras-complete",
			//	DownloadUrl = "https://archive.org/download/mame-chds-roms-extras-complete/@MACHINE@/@DISK@.chd",
			//	HtmlSizesUrl = null,
			//},

			//
			// Software ROM
			//
			new MameSourceSet
			{
				SetType = MameSetType.SoftwareRom,
				DetailsUrl = "https://archive.org/details/mame-sl",
				MetadataUrl = "https://archive.org/metadata/mame-sl",
				DownloadUrl = "https://archive.org/download/mame-sl/mame-sl/@LIST@.zip/@LIST@%2f@SOFTWARE@.zip",
				HtmlSizesUrl = "https://archive.org/download/mame-sl/mame-sl/@LIST@.zip/",
			},
		});

		public static string TuffyTDogSoftwareItems = @"
3do_m2
abc1600_hdd
abc800_hdd
amiga_hdd
amiga_workbench
archimedes_hdd
bbc_hdd
cd32
cdi
cdtv
dc
fmtowns_cd
gtfore
hp9k3xx_cdrom
hp9k3xx_hdd
hyperscan
ibm5150_hdd
ibm5170_cdrom
ibm5170_hdd
interpro
jazz
kpython2
mac_cdrom
mac_hdd
megacd
megacdj
mtx_hdd
neocd
next_cdrom
next_hdd
nuon
pc1512_hdd
pc1640_hdd
pc8801_cdrom
pc98_cd
pcecd
pcfx
pet_hdd
pico
pippin
psx
saturn
segacd
sgi_mips
sgi_mips_hdd
snes_vkun
softbox
v1050_hdd
vis
vsmile_cd
		";

		private string MetaDataDirectory;

		public Sources()
		{
			MetaDataDirectory = Path.Combine(Globals.RootDirectory, "_METADATA");

			using (StringReader reader = new StringReader(TuffyTDogSoftwareItems))
			{
				string softwareList;
				while ((softwareList = reader.ReadLine()) != null)
				{
					softwareList = softwareList.Trim();
					if (softwareList.Length == 0)
						continue;

					MameSourceSets.Add(new MameSourceSet
					{
						SetType = MameSetType.SoftwareDisk,
						ListName = softwareList,
						DetailsUrl = $"https://archive.org/details/mame-sl-chd-{softwareList}",
						MetadataUrl = $"https://archive.org/metadata/mame-sl-chd-{softwareList}",
						DownloadUrl = $"https://archive.org/download/mame-sl-chd-{softwareList}/@SOFTWARE@/@DISK@.chd",
						HtmlSizesUrl = null,
					});
				}
			}

			MameSourceSets.Add(new MameSourceSet
			{
				SetType = MameSetType.SoftwareDisk,
				ListName = "*",
				DetailsUrl = "https://archive.org/details//mame-software-list-chds-2",
				MetadataUrl = "https://archive.org/metadata/mame-software-list-chds-2",
				DownloadUrl = "https://archive.org/download/mame-software-list-chds-2/@LIST@/@SOFTWARE@/@DISK@.chd",
				HtmlSizesUrl = null,
			});
		}


		public void LoadSourceSet(MameSourceSet sourceSet, bool softwareDisk)
		{
			sourceSet.Version = "";
			sourceSet.Status = "";

			try
			{
				switch (sourceSet.SetType)
				{
					case MameSetType.MachineRom:
						sourceSet.AvailableDownloadFileInfos = AvailableFilesInMetadata(sourceSet, "mame-merged/");
						sourceSet.Version = sourceSet.Title.Substring(5, 5);
						break;

					case MameSetType.MachineDisk:
						sourceSet.AvailableDownloadFileInfos = AvailableFilesInMetadata(sourceSet, null);
						sourceSet.Version = sourceSet.Title.Substring(5, 5);
						break;

					case MameSetType.SoftwareRom:
						sourceSet.AvailableDownloadFileInfos = AvailableFilesInMetadata(sourceSet, "mame-sl/");
						sourceSet.Version = sourceSet.Title.Substring(8, 5);
						break;

					case MameSetType.SoftwareDisk:
						if (softwareDisk == true)
						{
							sourceSet.AvailableDownloadFileInfos = AvailableFilesInMetadata(sourceSet, null);
							break;
						}
						else
						{
							Console.WriteLine($"Ready to load: {sourceSet.MetadataUrl}");
							return;
						}
				}

				if (sourceSet.Version != "")
				{
					sourceSet.Version = sourceSet.Version.Replace(".", "").Trim();
					Console.WriteLine($"Version:\t{sourceSet.Version}");
				}

				sourceSet.Status = "ok";
			}
			catch (Exception e)
			{
				Tools.ReportError(e, $"Error in source, you will have problems downloading the \"{sourceSet.SetType}\" types, problem item: {sourceSet.MetadataUrl}.", false);

				sourceSet.Status = "error";
			}
		}

		private dynamic GetArchiveOrgMetaData(string name, string metadataUrl, string metadataCacheFilename)
		{
			if (File.Exists(metadataCacheFilename) == false || (DateTime.Now - File.GetLastWriteTime(metadataCacheFilename) > TimeSpan.FromHours(3)))
			{
				Console.Write($"Downloading {name} metadata JSON {metadataUrl} ...");
				File.WriteAllText(metadataCacheFilename, Tools.PrettyJSON(Tools.Query(Globals.HttpClient, metadataUrl)), Encoding.UTF8);
				Console.WriteLine("...done.");
			}

			Console.Write($"Loading {name} metadata JSON {metadataCacheFilename} ...");
			dynamic metadata = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(metadataCacheFilename, Encoding.UTF8));
			Console.WriteLine("...done.");

			return metadata;
		}

		private Dictionary<string, SourceFileInfo> AvailableFilesInMetadata(MameSourceSet sourceSet, string find)
		{
			string metadataFilename = Path.Combine(MetaDataDirectory, $"{sourceSet.SetType}_{Path.GetFileName(sourceSet.MetadataUrl)}.json");

			dynamic metadata = GetArchiveOrgMetaData(sourceSet.SetType.ToString(), sourceSet.MetadataUrl, metadataFilename);

			sourceSet.Title = metadata.metadata.title;

			var result = new Dictionary<string, SourceFileInfo>();

			foreach (dynamic file in metadata.files)
			{
				string name = (string)file.name;

				if (find != null)
				{
					if (name.StartsWith(find) == true && name.EndsWith(".zip") == true)
					{
						name = name.Substring(find.Length);
						name = name.Substring(0, name.Length - 4);

						result.Add(name, new SourceFileInfo(file));
					}
				}
				else
				{
					if (name.EndsWith(".chd") == true)
					{
						name = name.Substring(0, name.Length - 4);

						result.Add(name, new SourceFileInfo(file));
					}
				}
			}

			return result;
		}

		public MameSourceSet[] GetSourceSets(MameSetType type)
		{
			MameSourceSet[] results =
				(from sourceSet in MameSourceSets
				 where sourceSet.SetType == type
				 select sourceSet).ToArray();

			if (results.Length == 0)
				throw new ApplicationException($"Did not find any source sets: {type}");

			return results;
		}

		public MameSourceSet[] GetSourceSets(MameSetType type, string listName)
		{
			List<MameSourceSet> results = new List<MameSourceSet>();

			foreach (string listNameQuery in new string[] { listName, "*" })
			{
				foreach (MameSourceSet sourceSet in from sourceSet in MameSourceSets
					where sourceSet.SetType == type && sourceSet.ListName == listNameQuery select sourceSet)
				{
					results.Add(sourceSet);
				}
			}

			if (results.Count == 0)
				throw new ApplicationException($"Did not find any source sets: {type}");

			foreach (MameSourceSet sourceSet in results)
			{
				if (sourceSet.Status == "")
					LoadSourceSet(sourceSet, true);

			}

			return results.ToArray();
		}

		public MameSourceSet[] GetSourceSets()
		{
			return MameSourceSets.ToArray();
		}
	}
}
