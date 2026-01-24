using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Spludlow.MameAO
{
    public class Cheats
    {
		//	TODO: Use Spludlow data web
        public static void Place(string coreDirectory)
        {
            if (Globals.Settings.Options["Cheats"] == "No")
                return;

            string directory = Path.Combine(coreDirectory, "cheat");

            if (Directory.Exists(directory) == true)
                return;

            string version = Tools.FetchTextCached("https://mame.spludlow.co.uk/data/mame-cheats/latest.txt");

            if (version == null)
                return;

			string zipUrl = $"https://mame.spludlow.co.uk/data/mame-cheats/{version}.zip";

			string zipCacheFilename = Path.Combine(Globals.CacheDirectory, $"mame-cheats-{version}.zip");

			Tools.ConsoleHeading(2, new string[] {
				$"Pugsy's Cheats {version}",
				zipUrl
			});

			if (File.Exists(zipCacheFilename) == false)
            {
				Console.Write($"Dowbloading Cheats: {zipCacheFilename} ...");
				Tools.Download(zipUrl, zipCacheFilename);
				Console.WriteLine("...done.");
			}

            Console.Write($"Extracting Cheats: {directory} ...");
            ZipFile.ExtractToDirectory(zipCacheFilename, directory);
            Console.WriteLine("...done.");
        }

        /// <summary>
        /// Re packing to remove 7-Zip dependancy for mame-ao
        /// There are a lot of plaeholder files with nothing in they are stripped
        /// </summary>
        public static int UpdateFromPugsy(string directory)
        {
            string sourceDirectory = Path.Combine(directory, "source");
            directory = Path.Combine(directory, "download");

            Directory.CreateDirectory(sourceDirectory);
			Directory.CreateDirectory(directory);

			string url = "https://www.mamecheat.co.uk/mame_downloads.htm";

			string html = Tools.FetchTextCached(url);
			if (html == null)
				throw new ApplicationException($"Cant download: {url}");

            Uri uri = new Uri(url);

            SortedDictionary<string, string> versionUrls = new SortedDictionary<string, string>();

            using (StringReader reader = new StringReader(html))
            {
				string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0)
                        continue;

                    if (line.Contains("XML Cheat Collection for MAME") == false)
                        continue;

                    int index;
                    string find;

                    find = "<A HREF=\"";
                    index = line.IndexOf(find);
                    if (index == -1)
                        continue;
                    line = line.Substring(index + find.Length);

                    find = ".zip\">";
					index = line.IndexOf(find);
					if (index == -1)
						continue;
					line = line.Substring(0, index + 4);

                    line = new Uri(uri, line).AbsoluteUri;

                    index = line.LastIndexOf("/");
					if (index == -1)
						continue;
                    string ver = line.Substring(index + 1);
					ver = ver.Substring(5);
					ver = ver.Substring(0, ver.Length - 4);

                    if (ver.Length != 4)
                        continue;

                    versionUrls.Add(ver, line);
				}
			}

            if (versionUrls.Count == 0)
                throw new ApplicationException("no version urls found");

            string version = versionUrls.Keys.Last();
            string downloadLink = versionUrls[version];

            Console.WriteLine($"{version}\t{downloadLink}");

			string sourceZipFilename = Path.Combine(sourceDirectory, $"{version}.zip");
			string targetZipFilename = Path.Combine(directory, $"{version}.zip");

			//if (File.Exists(sourceZipFilename) == true)
			//	return 0;

			if (File.Exists(sourceZipFilename) == false)
                Tools.Download(downloadLink, sourceZipFilename);

            DataSet dataSet;

			using (TempDirectory tempDir = new TempDirectory())
			{
				ZipFile.ExtractToDirectory(sourceZipFilename, tempDir.Path);

				string filename = Path.Combine(tempDir.Path, "cheat.7z");
				if (File.Exists(filename) == false)
					throw new ApplicationException($"Did not find: {filename}");

				string extractDirectory = Path.Combine(tempDir.Path, "cheats");
				Directory.CreateDirectory(extractDirectory);

                Tools.ExtractToDirectory7Zip(filename, extractDirectory);

				Tools.ClearAttributes(tempDir.Path);

				dataSet = MameCheatsData(extractDirectory);

				ZipFile.CreateFromDirectory(extractDirectory, targetZipFilename);
			}

			File.WriteAllText(Path.Combine(directory, "latest.txt"), version, Encoding.ASCII);

			//	TODO:	do somthing with dataSet

			return 1;
        }

		public static DataSet MameCheatsData(string directory)
		{
			DataTable machineTable = Tools.MakeDataTable("Machine",
				"FileId		FileVersion	Machine	CheatCount	Comments",
				"Int64*		String		String	Int32		String");

			DataTable softwareTable = Tools.MakeDataTable("Software",
				"FileId		FileVersion	SoftwareList	Software	CheatCount	Comments",
				"Int64*		String		String			String		Int32		String");

			DataTable cheatTable = Tools.MakeDataTable("Cheat",
				"CheatId	FileId	Description	Xml",
				"Int64*		Int64	String		String");

			DataSet dataSet = new DataSet();
			foreach (DataTable table in new DataTable[] { machineTable, softwareTable, cheatTable })
			{
				table.Columns[0].AutoIncrement = true;
				table.Columns[0].AutoIncrementSeed = 1;
				dataSet.Tables.Add(table);
			}

			int removeCount;
			int keepCount;

			//
			//	Machines
			//
			removeCount = 0;
			keepCount = 0;
			foreach (string xmlFilename in Directory.GetFiles(directory, "*.xml"))
			{
				XElement document = XElement.Load(xmlFilename);

				string documentVersion = document.Attributes("version").Single().Value;

				List<XElement>[] cheatsComments = CheatsAndCommentsFromDocument(document);
				List<XElement> cheats = cheatsComments[0];
				List<XElement> comments = cheatsComments[1];

				if (cheats.Count == 0)
				{
					++removeCount;
					File.Delete(xmlFilename);
					continue;
				}
				++keepCount;

				string machineName = Path.GetFileNameWithoutExtension(xmlFilename);

				string commentText = comments.Count == 0 ? "" : String.Join(", ", comments.Select(comment => comment.Attribute("desc").Value));

				DataRow fileRow = machineTable.Rows.Add(null, documentVersion, machineName, cheats.Count, commentText);

				foreach (var cheat in cheats)
					cheatTable.Rows.Add(null, (long)fileRow[0], cheat.Attribute("desc").Value, cheat.ToString());
			}
			Console.WriteLine($"Machines removed: {removeCount}, keep: {keepCount}");

			//
			//	Software
			//
			removeCount = 0;
			keepCount = 0;
			foreach (string softwareListDirectory in Directory.GetDirectories(directory))
			{
				string softwareListName = Path.GetFileName(softwareListDirectory);

				foreach (string xmlFilename in Directory.GetFiles(softwareListDirectory, "*.xml"))
				{
					XElement document = XElement.Load(xmlFilename);

					string documentVersion = document.Attributes("version").Single().Value;

					List<XElement>[] cheatsComments = CheatsAndCommentsFromDocument(document);
					List<XElement> cheats = cheatsComments[0];
					List<XElement> comments = cheatsComments[1];

					if (cheats.Count == 0)
					{
						++removeCount;
						File.Delete(xmlFilename);
						continue;
					}
					++keepCount;

					string softwareName = Path.GetFileNameWithoutExtension(xmlFilename);

					string commentText = comments.Count == 0 ? "" : String.Join(", ", comments.Select(comment => comment.Attribute("desc").Value));

					DataRow fileRow = softwareTable.Rows.Add(null, documentVersion, softwareListName, softwareName, cheats.Count, commentText);

					foreach (var cheat in cheats)
						cheatTable.Rows.Add(null, (long)fileRow[0], cheat.Attribute("desc").Value, cheat.ToString());
				}

				if (Directory.GetFiles(softwareListDirectory, "*.xml").Length == 0)
					Directory.Delete(softwareListDirectory, true);
			}
			Console.WriteLine($"Software removed: {removeCount}, keep: {keepCount}");

			return dataSet;
		}

		public static List<XElement>[] CheatsAndCommentsFromDocument(XElement document)
		{
			List<XElement> cheats = new List<XElement>();
			List<XElement> comments = new List<XElement>();

			foreach (var element in document.Elements())
			{
				switch (element.Name.LocalName)
				{
					case "cheat":
						if (element.HasElements == false)
						{
							XAttribute desc = element.Attribute("desc") ?? throw new ApplicationException("No desc");

							if (element.Attributes().Count() != 1)
								throw new ApplicationException("Not just desc");

							if (desc.Value.Trim().Length > 0)
								comments.Add(element);
						}
						else
						{
							cheats.Add(element);
						}
						break;

					default:
						throw new ApplicationException($"Unknown element: {element.Name}");
				}
			}

			return new List<XElement>[] { cheats, comments };
		}

	}

}
