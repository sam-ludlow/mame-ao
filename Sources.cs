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

		public class MameSourceSet
		{
			public MameSetType SetType;
			public string MetadataUrl;
			public string DownloadUrl;
			public string HtmlSizesUrl;
			public Dictionary<string, long> AvailableDownloadSizes;
		}

		private static readonly MameSourceSet[] MameSourceSets = new MameSourceSet[] {
			new MameSourceSet
			{
				SetType = MameSetType.MachineRom,
				MetadataUrl = "https://archive.org/metadata/mame-merged",
				DownloadUrl = "https://archive.org/download/mame-merged/mame-merged/@MACHINE@.zip",
				HtmlSizesUrl = null,
			},
			new MameSourceSet
			{
				SetType = MameSetType.MachineDisk,
				MetadataUrl = "https://archive.org/metadata/mame-chds-roms-extras-complete",
				DownloadUrl = "https://archive.org/download/mame-chds-roms-extras-complete/@MACHINE@/@DISK@.chd",
				HtmlSizesUrl = null,
			},
			new MameSourceSet
			{
				SetType = MameSetType.SoftwareRom,
				MetadataUrl = "https://archive.org/metadata/mame-sl",
				DownloadUrl = "https://archive.org/download/mame-sl/mame-sl/@LIST@.zip/@LIST@%2f@SOFTWARE@.zip",
				HtmlSizesUrl = "https://archive.org/download/mame-sl/mame-sl/@LIST@.zip/",
			},
			new MameSourceSet
			{
				SetType = MameSetType.SoftwareDisk,
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
	}
}
