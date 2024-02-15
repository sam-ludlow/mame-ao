using System;
using System.Collections.Generic;
using System.Linq;

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
		}

		private static readonly MameSourceSet[] MameSourceSets = new MameSourceSet[] {
			
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

			//
			// Software DISK
			//
			new MameSourceSet
			{
				SetType = MameSetType.SoftwareDisk,
				ListName = "cdi",
				DetailsUrl = "https://archive.org/details/mame-sl-chd-cdi",
				MetadataUrl = "https://archive.org/metadata/mame-sl-chd-cdi",
				DownloadUrl = "https://archive.org/download/mame-sl-chd-cdi/@SOFTWARE@/@DISK@.chd",
				HtmlSizesUrl = null,
			},

			new MameSourceSet
			{
				SetType = MameSetType.SoftwareDisk,
				ListName = "neocd",
				DetailsUrl = "https://archive.org/details/mame-sl-chd-neocd",
				MetadataUrl = "https://archive.org/metadata/mame-sl-chd-neocd",
				DownloadUrl = "https://archive.org/download/mame-sl-chd-neocd/@SOFTWARE@/@DISK@.chd",
				HtmlSizesUrl = null,
			},

			new MameSourceSet
			{
				SetType = MameSetType.SoftwareDisk,
				ListName = "pcecd",
				DetailsUrl = "https://archive.org/details/mame-sl-chd-pcecd",
				MetadataUrl = "https://archive.org/metadata/mame-sl-chd-pcecd",
				DownloadUrl = "https://archive.org/download/mame-sl-chd-pcecd/@SOFTWARE@/@DISK@.chd",
				HtmlSizesUrl = null,
			},

			new MameSourceSet
			{
				SetType = MameSetType.SoftwareDisk,
				ListName = "*",
				DetailsUrl = "https://archive.org/details//mame-software-list-chds-2",
				MetadataUrl = "https://archive.org/metadata/mame-software-list-chds-2",
				DownloadUrl = "https://archive.org/download/mame-software-list-chds-2/@LIST@/@SOFTWARE@/@DISK@.chd",
				HtmlSizesUrl = null,
			},
		};

		public static MameSourceSet[] GetSourceSets(MameSetType type)
		{
			MameSourceSet[] results =
				(from sourceSet in MameSourceSets
				 where sourceSet.SetType == type
				 select sourceSet).ToArray();

			if (results.Length == 0)
				throw new ApplicationException($"Did not find any source sets: {type}");

			return results;
		}

		public static MameSourceSet[] GetSourceSets(MameSetType type, string listName)
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

			return results.ToArray();
		}

		public static MameSourceSet[] GetSourceSets()
		{
			return MameSourceSets;
		}
	}
}
