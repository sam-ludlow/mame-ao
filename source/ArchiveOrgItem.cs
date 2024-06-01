using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Spludlow.MameAO
{

	public enum ItemType
	{
		MachineRom,
		MachineDisk,
		SoftwareRom,
		SoftwareDisk,
		Support,
	};

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
		public string Key;

		public string SubDirectory;
		public string Tag;

		public Dictionary<string, ArchiveOrgFile> Files = null;

		public string UrlDetails;
		public string UrlMetadata;
		public string UrlDownload;

		public string Title;
		public DateTime ItemLastUpdated;

		public int[] TitleVersionSubString = null;

		public string Version = "";
		public string Status = "";

		private List<string> AcceptedExtentions = new List<string>(new string[] { ".zip", ".chd" });

		public ArchiveOrgItem(string key, string subDirectory, string tag, int[] titleVersionSubString)
		{
			Key = key;
			SubDirectory = subDirectory;
			Tag = tag;
			TitleVersionSubString = titleVersionSubString;

			UrlDetails = $"https://archive.org/details/{Key}";
			UrlMetadata = $"https://archive.org/metadata/{Key}";
			UrlDownload = $"https://archive.org/download/{Key}";
		}

		public ArchiveOrgFile GetFile(string key)
		{
			if (Files == null)
				Initialize();

			if (key == null)
				return null;

			if (Files.ContainsKey(key) == false)
				return null;

			return Files[key];
		}

		public static ArchiveOrgItem[] GetItems(ItemType itemType, string tag)
		{
			List<ArchiveOrgItem> results = new List<ArchiveOrgItem>();

			foreach (string tagQuery in new string[] { tag, "*" })
			{
				foreach (ArchiveOrgItem sourceItem in Globals.ArchiveOrgItems[itemType].Where(item => item.Tag == tagQuery))
					results.Add(sourceItem);
			}

			if (results.Count == 0)
				throw new ApplicationException($"Did not find any source sets: {itemType}");

			return results.ToArray();
		}

		public string DownloadLink(ArchiveOrgFile file)
		{
			return $"{UrlDownload}/{file.name}";
		}

		private void Initialize()
		{
			Files = new Dictionary<string, ArchiveOrgFile>();	//	don't retry ?

			string cacheDirectory = Path.Combine(Globals.RootDirectory, "_METADATA", "archive.org");
			Directory.CreateDirectory(cacheDirectory);

			string cacheFilename = Path.Combine(cacheDirectory, $"{Key}.json");

			string json = Tools.FetchTextCached(UrlMetadata, cacheFilename);

			if (json == null || json == "{}")
			{
				Status = "bad";
				Console.WriteLine($"WARNING archive.org item not available: {Key}");
				return;
			}

			dynamic metadata = JsonConvert.DeserializeObject<dynamic>(json);

			Title = (string)metadata.metadata.title;
			ItemLastUpdated = Tools.FromEpochDate((double)metadata.item_last_updated);

			if (TitleVersionSubString != null)
				Version = Title.Substring(TitleVersionSubString[0], TitleVersionSubString[1]);

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

			Status = "ok";
		}

		private Dictionary<string, long> GetZipContentsSizes(string listName, ArchiveOrgItem item, ArchiveOrgFile file)
		{
			//	TODO
			string cacheDirectory = Path.Combine(Globals.RootDirectory, "_METADATA", "SoftwareSizes", item.Version);

			Directory.CreateDirectory(cacheDirectory);

			string filename = Path.Combine(cacheDirectory, listName + ".htm");

			string html;
			if (File.Exists(filename) == false)
			{
				string url = item.DownloadLink(file) + "/";
				html = Tools.Query(Globals.HttpClient, url);
				File.WriteAllText(filename, html, Encoding.UTF8);
			}
			else
			{
				html = File.ReadAllText(filename, Encoding.UTF8);
			}

			Dictionary<string, long> result = new Dictionary<string, long>();

			using (StringReader reader = new StringReader(html))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();
					if (line.StartsWith("<tr><td><a href=\"//archive.org/download/") == false)
						continue;

					string[] parts = line.Split(new char[] { '<' });

					string name = null;
					string size = null;

					foreach (string part in parts)
					{
						int index = part.LastIndexOf(">");
						if (index == -1)
							continue;
						++index;

						if (part.StartsWith("a href=") == true)
						{
							name = part.Substring(index + listName.Length + 1);
							name = name.Substring(0, name.Length - 4);
						}

						if (part.StartsWith("td id=\"size\"") == true)
							size = part.Substring(index);
					}

					if (name == null || size == null)
						throw new ApplicationException($"Bad html line {listName} {line}");

					result.Add(name, Int64.Parse(size));
				}
			}

			return result;
		}

	}
}
