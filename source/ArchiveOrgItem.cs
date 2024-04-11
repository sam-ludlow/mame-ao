using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;

namespace Spludlow.MameAO
{
	public class ArchiveOrgFile
	{
		private static readonly DateTime EpochDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		public ArchiveOrgFile(dynamic file)
		{
			name = file.name;
			size = Int64.Parse((string)file.size);
			sha1 = file.sha1;
			mtime = EpochDateTime.AddSeconds(double.Parse((string)file.mtime));
		}

		public string name { get; set; }
		public long size { get; set; }
		public string sha1 { get; set; }
		public DateTime mtime { get; set; }
	}

	public class ArchiveOrgItem
	{
		private string Key;
		private string SubDirectory;

		private Dictionary<string, ArchiveOrgFile> Files = null;

		public string UrlDetails;
		public string UrlMetadata;
		public string UrlDownload;

		public string Title;
		public DateTime ItemLastUpdated;

		private List<string> AcceptedExtentions = new List<string>(new string[] { ".zip", ".chd" });

		public ArchiveOrgItem(string key, string subDirectory)
		{
			Key = key;
			SubDirectory = subDirectory;

			UrlDetails = $"https://archive.org/details/{Key}";
			UrlMetadata = $"https://archive.org/metadata/{Key}";
			UrlDownload = $"https://archive.org/download/{Key}";
		}

		public ArchiveOrgFile GetFile(string name)
		{
			if (Files == null)
				Initialize();

			if (Files.ContainsKey(name) == false)
				return null;

			return Files[name];
		}

		private void Initialize()
		{
			Files = new Dictionary<string, ArchiveOrgFile>();

			string cacheDirectory = Path.Combine(Globals.RootDirectory, "_METADATA", "archive.org");
			Directory.CreateDirectory(cacheDirectory);

			string cacheFilename = Path.Combine(cacheDirectory, $"{Key}.json");

			string json = Tools.FetchTextCached(UrlMetadata, cacheFilename);

			if (json == null || json == "{}")
			{
				Console.WriteLine($"WARNING archive.org item not available: {Key}");
				return;
			}

			dynamic metadata = JsonConvert.DeserializeObject<dynamic>(json);

			Title = (string)metadata.metadata.title;
			ItemLastUpdated = Tools.FromEpochDate((double)metadata.item_last_updated);

			foreach (dynamic file in metadata.files)
			{
				string name = (string)file.name;
				string extention = Path.GetExtension(name);

				if ((SubDirectory == null || name.StartsWith(SubDirectory) == true) && AcceptedExtentions.Contains(extention) == true)
				{
					if (SubDirectory != null)
						name = name.Substring(SubDirectory.Length);

					name = name.Substring(0, name.Length - extention.Length);

					Files.Add(name, new ArchiveOrgFile(file));
				}
			}
		}

	}
}
