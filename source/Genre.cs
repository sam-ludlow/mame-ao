using System;
using System.Collections.Generic;
using System.Net.Http;
using System.IO;
using System.IO.Compression;
using System.Data;

namespace Spludlow.MameAO
{
	public class Genre
	{
		private readonly HttpClient HttpClient;
		private readonly string RootDirectory;
		private readonly Database Database;

		public DataSet Data = null;

		public Genre(HttpClient httpClient, string rootDirectory, Database database)
		{
			HttpClient = httpClient;

			RootDirectory = Path.Combine(rootDirectory, "_METADATA", "Genre");

			Database = database;

			Directory.CreateDirectory(RootDirectory);
		}


		public void Initialize()
		{
			try
			{
				RefreshRomote();
			}
			catch (Exception e)
			{
				Console.WriteLine($"!!! Warning unable to get genres from web, {e.Message}");
			}

			string version = GetLatestLocal();

			if (version == null)
			{
				Console.WriteLine("!!! Genres not available remote or local.");
				return;
			}

			Console.Write($"Genre version using: {version}, loading...");

			string filename = Path.Combine(RootDirectory, version, "catver.ini");

			//
			// Parse .ini
			//

			List<string> groups = new List<string>();
			List<string> genres = new List<string>();

			DataTable machineGroupGenreTable;

			try
			{
				machineGroupGenreTable = ParseIni(filename, groups, genres);
			}
			catch (Exception e)
			{
				Console.WriteLine($"!!! Error parsing genres file, {e.Message}");
				return;
			}

			groups.Sort();
			genres.Sort();


			string[] statuses = new string[] { "good", "imperfect", "preliminary" };

			Dictionary<string, string> machineStatus = GetMachineDriverStatuses();

			Data = new DataSet();

			//
			// Groups
			//

			DataTable groupTable = Tools.MakeDataTable(
				"group_id	group_name",
				"Int64		String"
			);
			groupTable.TableName = "groups";
			groupTable.PrimaryKey = new DataColumn[] { groupTable.Columns["group_id"] };
			groupTable.Columns["group_id"].AutoIncrement = true;
			groupTable.Columns["group_id"].AutoIncrementSeed = 1;
			foreach (string status in statuses)
				groupTable.Columns.Add(status, typeof(int));

			foreach (string group in groups)
			{
				DataRow groupRow = groupTable.Rows.Add(null, group, 0, 0, 0);

				foreach (DataRow machineRow in machineGroupGenreTable.Select($"group = '{group.Replace("'", "''")}'"))
				{
					string machine = (string)machineRow["machine"];
					if (machineStatus.ContainsKey(machine) == false)
						continue;
					
					string status = machineStatus[machine];
					groupRow[status] = (int)groupRow[status] + 1;
				}
			}

			Data.Tables.Add(groupTable);

			//
			// Genres
			//

			DataTable genreTable = Tools.MakeDataTable(
				"genre_id	group_id	genre_name",
				"Int64		Int64		String"
			);
			genreTable.TableName = "genres";
			genreTable.PrimaryKey = new DataColumn[] { genreTable.Columns["genre_id"] };
			genreTable.Columns["genre_id"].AutoIncrement = true;
			genreTable.Columns["genre_id"].AutoIncrementSeed = 1;
			foreach (string status in statuses)
				genreTable.Columns.Add(status, typeof(int));

			foreach (string genre in genres)
			{
				string group = genre.Split(new char[] { '/' })[0].Trim();

				long group_id = (long)groupTable.Select($"group_name = '{group.Replace("'", "''")}'")[0]["group_id"];

				DataRow genreRow = genreTable.Rows.Add(null, group_id, genre, 0, 0, 0);

				foreach (DataRow machineRow in machineGroupGenreTable.Select($"genre = '{genre.Replace("'", "''")}'"))
				{
					string machine = (string)machineRow["machine"];
					if (machineStatus.ContainsKey(machine) == false)
						continue;

					string status = machineStatus[machine];
					genreRow[status] = (int)genreRow[status] + 1;
				}
			}

			Data.Tables.Add(genreTable);


			Console.WriteLine("...done");
		}

		public DataTable ParseIni(string filename, List<string> groups, List<string> genres)
		{
			DataTable table = Tools.MakeDataTable(
				"machine	group	genre",
				"String		String	String"
			);

			bool inData = false;

			foreach (string rawLine in File.ReadAllLines(filename))
			{
				string line = rawLine.Trim();
				if (line.Length == 0)
					continue;

				if (line == "[Category]")
				{
					inData = true;
					continue;
				}

				if (line == "[VerAdded]")
					break;

				if (inData == false)
					continue;

				string[] parts;

				parts = line.Split(new char[] { '=' });

				if (parts.Length != 2)
					throw new ApplicationException("Not 2 parts on line");

				string machine = parts[0];
				string genre = parts[1];

				if (genres.Contains(genre) == false)
					genres.Add(genre);

				parts = parts[1].Split(new char[] { '/' });
				string group = parts[0].Trim();

				if (groups.Contains(group) == false)
					groups.Add(group);

				table.Rows.Add(machine, group, genre);
			}

			return table;
		}

		public string GetLatestZipLink()
		{
			string url = "https://www.progettosnaps.net/catver/";

			string html = Tools.Query(HttpClient, url);

			int index = html.IndexOf("href=\"/download/?tipo=catver");

			if (index == -1)
				throw new ApplicationException("Did not find download link");

			html = html.Substring(index + 6);

			index = html.IndexOf("\"");

			if (index == -1)
				throw new ApplicationException("Did not find closing quote");

			html = html.Substring(0, index);

			return new Uri(new Uri(url), html).AbsoluteUri;
		}

		public void RefreshRomote()
		{
			string zipUrl = GetLatestZipLink();

			int index = zipUrl.LastIndexOf("_");

			if (index == -1)
				throw new ApplicationException("Did not find last underscore");

			string version = zipUrl.Substring(index + 1);

			index = version.LastIndexOf(".");

			if (index == -1)
				throw new ApplicationException("Did not find last dot");

			version = version.Substring(0, index);

			string zipFilename = Path.Combine(RootDirectory, version + ".zip");

			if (File.Exists(zipFilename) == false)
				Tools.Download(zipUrl, zipFilename, 0, 3);

			string versionDirectory = Path.Combine(RootDirectory, version);

			if (Directory.Exists(versionDirectory) == false)
			{
				Directory.CreateDirectory(versionDirectory);
				ZipFile.ExtractToDirectory(zipFilename, versionDirectory);

				Console.WriteLine("progettosnaps.net/catver new version");
			}

			string filename = Path.Combine(versionDirectory, "catver.ini");

			if (File.Exists(filename) == false)
				throw new ApplicationException("ProgettoSnaps catver.ini not found: " + filename);

		}

		public string GetLatestLocal()
		{
			List<string> versions = new List<string>();

			foreach (string directory in Directory.GetDirectories(RootDirectory))
			{
				if (File.Exists(Path.Combine(directory, "catver.ini")) == true)
					versions.Add(Path.GetFileName(directory));
			}

			versions.Sort();

			if (versions.Count == 0)
				return null;

			return versions[versions.Count - 1];
		}

		private Dictionary<string, string> GetMachineDriverStatuses()
		{
			DataTable table = Database.ExecuteFill(Database._MachineConnection,
				"SELECT machine.name, driver.status FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id");

			Dictionary<string, string> result = new Dictionary<string, string>();

			foreach (DataRow row in table.Rows)
				result.Add((string)row["name"], (string)row["status"]);

			return result;
		}

	}
}
