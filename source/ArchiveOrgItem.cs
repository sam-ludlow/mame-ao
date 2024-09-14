using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

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

	public class ArchiveOrgAuth
	{
		public static string CacheFilename;

		private static HttpClient HttpClient;
		private static CookieContainer CookieContainer;

		static ArchiveOrgAuth()
		{
			CacheFilename = Path.Combine(Globals.CacheDirectory, "archive.org-auth-cookie.txt");

			CookieContainer = new CookieContainer();

			var handler = new HttpClientHandler()
			{
				CookieContainer = CookieContainer
			};
			
			HttpClient = new HttpClient(handler);
			HttpClient.DefaultRequestHeaders.Add("User-Agent", $"mame-ao/{Globals.AssemblyVersion} (https://github.com/sam-ludlow/mame-ao)");
		}

		public static string GetCookie()
		{
			if (File.Exists(CacheFilename) == false || (DateTime.Now - File.GetLastWriteTime(CacheFilename) > TimeSpan.FromDays(90)))
			{
				Console.WriteLine();
				Tools.ConsoleHeading(2, new string[] {
					"Please enter your archive.org credentials.",
					"You can create an account here https://archive.org/account/signup",
					"Your username & password are not stored just the auth cookie which is kept here",
					CacheFilename,
					"If you have download problems with a status of 401/403 delete this file then re-start MAME-AO."
				});

				string username;
				string password;

				string cookie = null;

				do
				{
					Console.WriteLine();

					Console.WriteLine("Enter your Archive.org username:");
					username = Console.ReadLine();

					Console.WriteLine("Enter your Archive.org password:");
					password = Console.ReadLine();

					try
					{
						if (username != "")
							cookie = GetAuthCookie(username, password);
					}
					catch (HttpRequestException e)
					{
						if (e.Message.Contains("401"))
							Console.WriteLine("Bad credentials try again. Hit enter to skip (You won't be able to download).");
						else
							throw e;
					}

				} while (cookie == null && username != "");

				if (cookie != null)
					File.WriteAllText(CacheFilename, cookie);
			}

			if (File.Exists(CacheFilename) == true)
				return File.ReadAllText(CacheFilename);

			return null;
		}

		private static string GetAuthCookie(string username, string password)
		{
			GetInitCookie();

			List<string> cookies = new List<string>();

			using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://archive.org/account/login"))
			{
				using (var content = new MultipartFormDataContent())
				{
					content.Add(new StringContent(username), "username");
					content.Add(new StringContent(password), "password");
					content.Add(new StringContent("true"), "remember");
					content.Add(new StringContent("https://archive.org/"), "referer");
					content.Add(new StringContent("true"), "login");
					content.Add(new StringContent("true"), "submit_by_js");

					requestMessage.Content = content;

					Task<HttpResponseMessage> requestTask = HttpClient.SendAsync(requestMessage);

					requestTask.Wait();
					HttpResponseMessage responseMessage = requestTask.Result;

					responseMessage.EnsureSuccessStatusCode();

					Task<string> responseMessageTask = responseMessage.Content.ReadAsStringAsync();
					responseMessageTask.Wait();

					string responseBody = responseMessageTask.Result;

					foreach (string cookie in responseMessage.Headers.GetValues("Set-Cookie"))
					{
						string[] parts = cookie.Split(';');
						string[] pair = parts[0].Split('=');
						if (pair.Length == 2)
							cookies.Add($"{pair[0].Trim()}={pair[1].Trim()}");
					}
				}
			}

			cookies.Add("donation=x");

			return String.Join("; ", cookies.ToArray());
		}

		private static void GetInitCookie()
		{
			List<string> cookies = new List<string>();

			using (Task<HttpResponseMessage> requestTask = HttpClient.GetAsync("https://archive.org/account/login"))
			{
				requestTask.Wait();

				HttpResponseMessage responseMessage = requestTask.Result;
				responseMessage.EnsureSuccessStatusCode();
			}
		}
	}

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

		public string Status = "";

		private readonly List<string> AcceptedExtentions = new List<string>(new string[] { ".zip", ".chd" });

		public ArchiveOrgItem(string key, string subDirectory, string tag)
		{
			Key = key;
			SubDirectory = subDirectory;
			Tag = tag;

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
			Files = new Dictionary<string, ArchiveOrgFile>();

			string json = Tools.FetchTextCached(UrlMetadata);

			if (json == null || json == "{}")
			{
				Status = "bad";
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

			Status = "ok";
		}

		public Dictionary<string, long> GetZipContentsSizes(ArchiveOrgFile file, int offset, int chopEnd)
		{
			string url = DownloadLink(file) + "/";

			string html = Tools.FetchTextCached(url);

			if (html == null)
			{
				Console.WriteLine($"!!! Can not get ZIP contents: {url}");
				return null;
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
							name = part.Substring(index);

						if (part.StartsWith("td id=\"size\"") == true)
							size = part.Substring(index);
					}

					if (name == null || size == null)
						throw new ApplicationException($"Bad html line {line}");

					if (offset != 0)
						name = name.Substring(offset);

					if (chopEnd != 0)
						name = name.Substring(0, name.Length - chopEnd);

					result.Add(name, Int64.Parse(size));
				}
			}

			return result;
		}

	}
}
