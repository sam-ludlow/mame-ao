using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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
        public static int UpdateFromPugsy(string directory, string serverConnectionString, string[] databaseNames)
        {
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

			string sourceZipFilename = Path.Combine(directory, $"{version}_source.zip");
			string targetDirectory = Path.Combine(directory, version);
			string targetZipFilename = Path.Combine(directory, $"{version}.zip");

			if (File.Exists(sourceZipFilename) == false)
				Tools.Download(downloadLink, sourceZipFilename);

			if (Directory.Exists(targetDirectory) == false)
			{
				Directory.CreateDirectory(targetDirectory);

				using (TempDirectory tempDir = new TempDirectory())
				{
					ZipFile.ExtractToDirectory(sourceZipFilename, tempDir.Path);

					string filename = Path.Combine(tempDir.Path, "cheat.7z");
					if (File.Exists(filename) == false)
						throw new ApplicationException($"Did not find: {filename}");

					Tools.ExtractToDirectory7Zip(filename, targetDirectory);

					Tools.ClearAttributes(tempDir.Path);
					Tools.ClearAttributes(targetDirectory);
				}
			}

			SqlConnection[] connections = new SqlConnection[] {
				new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseNames[0]}';"),
				new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseNames[1]}';"),
			};

			string machineVersion = null;
			if (Database.TableExists(connections[0], "cheat_machine_version") == true)
				machineVersion = (string)Database.ExecuteScalar(connections[0], "SELECT [version] FROM [cheat_machine_version] WHERE [key_1] = 1");

			string softwareVersion = null;
			if (Database.TableExists(connections[1], "cheat_software_version") == true)
				softwareVersion = (string)Database.ExecuteScalar(connections[1], "SELECT [version] FROM [cheat_software_version] WHERE [key_1] = 1");

			DataSet[] dataSets = null;
			
			if (machineVersion != version)
			{
				if (dataSets == null)
					dataSets = MameCheatsData(targetDirectory, version);

				foreach (DataTable table in dataSets[0].Tables)
					Database.ExecuteNonQuery(connections[0], $"DROP TABLE IF EXISTS [{table.TableName}];");

				Database.DataSet2MSSQLTables(dataSets[0], connections[0]);

				Database.ExecuteNonQuery(connections[0], @"
					ALTER TABLE [cheat_machine_item]
					ADD CONSTRAINT [FK_cheat_machine_item_cheat_machine]
					FOREIGN KEY ([machine_name])
					REFERENCES [cheat_machine] ([machine_name]);

					CREATE INDEX IX_cheat_machine_item_machine_name
					ON [cheat_machine_item] ([machine_name]);
				");
			}

			if (softwareVersion != version)
			{
				if (dataSets == null)
					dataSets = MameCheatsData(targetDirectory, version);

				foreach (DataTable table in dataSets[1].Tables)
					Database.ExecuteNonQuery(connections[1], $"DROP TABLE IF EXISTS [{table.TableName}];");

				Database.DataSet2MSSQLTables(dataSets[1], connections[1]);

				Database.ExecuteNonQuery(connections[1], @"
					ALTER TABLE [cheat_software_item]
					ADD CONSTRAINT [FK_cheat_software_item_cheat_software]
					FOREIGN KEY ([softwarelist_name], [software_name])
					REFERENCES [cheat_software] ([softwarelist_name], [software_name]);

					CREATE INDEX IX_cheat_software_item_softwarelist_name_software_name
					ON [cheat_software_item] ([softwarelist_name], [software_name]);
				");
			}

			if (File.Exists(targetZipFilename) == false)
			{
				ZipFile.CreateFromDirectory(targetDirectory, targetZipFilename);
				File.WriteAllText(Path.Combine(directory, "latest.txt"), version, Encoding.ASCII);
			}

			return 1;
        }

		public static DataSet[] MameCheatsData(string directory, string version)
		{
			DataSet[] dataSets = new DataSet[] { new DataSet("machine"), new DataSet("software") };

			DataTable machineVersionTable = Tools.MakeDataTable("cheat_machine_version",
				"key_1	version",
				"Int64*	String");
			machineVersionTable.Rows.Add(1L, version);
			dataSets[0].Tables.Add(machineVersionTable);

			DataTable machineItemTable = Tools.MakeDataTable("cheat_machine_item",
				"cheat_machine_item_id	machine_name	description	xml		json",
				"Int64*					String			String		String	String");
			machineItemTable.Columns[0].AutoIncrement = true;
			machineItemTable.Columns[0].AutoIncrementSeed = 1;
			dataSets[0].Tables.Add(machineItemTable);

			DataTable machineTable = Tools.MakeDataTable("cheat_machine",
				"machine_name	version	cheat_count	comments	xml		json",
				"String*		String	Int32		String		String	String");
			dataSets[0].Tables.Add(machineTable);


			DataTable softwareVersionTable = Tools.MakeDataTable("cheat_software_version",
				"key_1	version",
				"Int64*	String");
			softwareVersionTable.Rows.Add(1L, version);
			dataSets[1].Tables.Add(softwareVersionTable);

			DataTable softwareItemTable = Tools.MakeDataTable("cheat_software_item",
				"cheat_software_item_id	softwarelist_name	software_name	description	xml		json",
				"Int64*					String				String			String		String	String");
			softwareItemTable.Columns[0].AutoIncrement = true;
			softwareItemTable.Columns[0].AutoIncrementSeed = 1;
			dataSets[1].Tables.Add(softwareItemTable);

			DataTable softwareTable = Tools.MakeDataTable("cheat_software",
				"softwarelist_name	software_name	version	cheat_count	comments	xml		json",
				"String*			String*			String	Int32		String		String	String");
			dataSets[1].Tables.Add(softwareTable);

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

				string xml = document.ToString();
				string json = Tools.XML2JSON(document);

				machineTable.Rows.Add(machineName, documentVersion, cheats.Count, commentText, xml, json);

				foreach (var cheat in cheats)
				{
					xml = cheat.ToString();
					json = Tools.XML2JSON(cheat);
					machineItemTable.Rows.Add(null, machineName, cheat.Attribute("desc").Value, xml,json);
				}
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

					string xml = document.ToString();
					string json = Tools.XML2JSON(document);

					softwareTable.Rows.Add(softwareListName, softwareName, documentVersion, cheats.Count, commentText, xml, json);

					foreach (var cheat in cheats)
					{
						xml = cheat.ToString();
						json = Tools.XML2JSON(cheat);
						softwareItemTable.Rows.Add(null, softwareListName, softwareName, cheat.Attribute("desc").Value, xml, json);
					}
				}

				if (Directory.GetFiles(softwareListDirectory, "*.xml").Length == 0)
					Directory.Delete(softwareListDirectory, true);
			}
			Console.WriteLine($"Software removed: {removeCount}, keep: {keepCount}");

			return dataSets;
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
