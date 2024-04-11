using System;
using System.Collections.Generic;
using System.Net.Http;
using System.IO;
using System.IO.Compression;
using System.Data;
using System.Data.SQLite;

namespace Spludlow.MameAO
{
	public class Genre
	{
		const string SourceUrl = "https://www.progettosnaps.net/catver/";

		private readonly string RootDirectory;

		public string Version = "";
		public DataSet Data = null;

		public Genre()
		{
			RootDirectory = Path.Combine(Globals.RootDirectory, "_METADATA", "Genre");
			Directory.CreateDirectory(RootDirectory);
		}

		public void Initialize()
		{
			Tools.ConsoleHeading(2, new string[] {
				$"Machine Genres",
				SourceUrl
			});

			try
			{
				RefreshRomote();
			}
			catch (Exception e)
			{
				Console.WriteLine($"!!! Warning unable to get genres from web, {e.Message}");
			}

			Version = GetLatestLocal();

			if (Version == null)
			{
				Console.WriteLine("!!! Warning Genres not available remote or local.");
				return;
			}

			string filename = Path.Combine(RootDirectory, Version, "catver.ini");

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

			Console.Write("Loading Genres...");

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

			Dictionary<string, long[]> machineGenreIds = new Dictionary<string, long[]>();

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

					long genre_id = (long)genreRow["genre_id"];
					machineGenreIds.Add(machine, new long[] { group_id, genre_id });
				}
			}

			Console.WriteLine("...done");

			Data.Tables.Add(genreTable);

			SetMachines(machineGenreIds);

			Console.WriteLine($"Version:\t{Version}");
		}

		public DataTable ParseIni(string filename, List<string> groups, List<string> genres)
		{
			DataTable table = Tools.MakeDataTable(
				"machine	group	genre",
				"String*	String	String"
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

				DataRow existingRow = table.Rows.Find(machine);

				if (existingRow != null)
					Console.WriteLine($"Parse Genre Duplicate - machine: \"{machine}\" genre: \"{genre}\" mismatch: {genre != (string)existingRow["genre"]}");
				else
					table.Rows.Add(machine, group, genre);
			}

			return table;
		}

		public string GetLatestZipLink()
		{
			string html = Tools.Query(Globals.HttpClient, SourceUrl);

			int index = html.IndexOf("href=\"/download/?tipo=catver");

			if (index == -1)
				throw new ApplicationException("Did not find download link");

			html = html.Substring(index + 6);

			index = html.IndexOf("\"");

			if (index == -1)
				throw new ApplicationException("Did not find closing quote");

			html = html.Substring(0, index);

			return new Uri(new Uri(SourceUrl), html).AbsoluteUri;
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

			string versionDirectory = Path.Combine(RootDirectory, version);

			if (Directory.Exists(versionDirectory) == false)
			{
				Directory.CreateDirectory(versionDirectory);

				string zipFilename = Path.Combine(versionDirectory, version + ".zip");

				Console.WriteLine($"New Genres {zipUrl} => {zipFilename}");

				if (File.Exists(zipFilename) == false)
					Tools.Download(zipUrl, zipFilename, 0, 3);

				ZipFile.ExtractToDirectory(zipFilename, versionDirectory);
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
			DataTable table = Database.ExecuteFill(Globals.Database._MachineConnection,
				"SELECT machine.name, driver.status FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id");

			Dictionary<string, string> result = new Dictionary<string, string>();

			foreach (DataRow row in table.Rows)
				result.Add((string)row["name"], (string)row["status"]);

			return result;
		}

		public void SetMachines(Dictionary<string, long[]> machineGenreIds)
		{
			DataTable infoTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT * FROM ao_info");

			if (infoTable.Columns.Contains("genre_version") == false)
			{
				Database.ExecuteNonQuery(Globals.Database._MachineConnection, "ALTER TABLE ao_info ADD COLUMN genre_version TEXT");
				Database.ExecuteNonQuery(Globals.Database._MachineConnection, "UPDATE ao_info SET genre_version = '' WHERE ao_info_id = 1");
			}

			infoTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT * FROM ao_info");

			string databaseVersion = (string)infoTable.Rows[0]["genre_version"];

			if (Version == databaseVersion)
				return;

			Console.Write("Update Machines database with genre IDs ...");

			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT * FROM machine WHERE machine_id = 0");

			if (machineTable.Columns.Contains("genre_id") == false)
				Database.ExecuteNonQuery(Globals.Database._MachineConnection, "ALTER TABLE machine ADD COLUMN genre_id INTEGER");

			Database.ExecuteNonQuery(Globals.Database._MachineConnection, "UPDATE machine SET genre_id = 0");

			machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name FROM machine");

			using (SQLiteCommand command = new SQLiteCommand("UPDATE machine SET genre_id = @genre_id WHERE machine_id = @machine_id", Globals.Database._MachineConnection))
			{
				command.Parameters.Add("@genre_id", DbType.Int64);
				command.Parameters.Add("@machine_id", DbType.Int64);

				Globals.Database._MachineConnection.Open();

				SQLiteTransaction transaction = Globals.Database._MachineConnection.BeginTransaction();

				try
				{
					foreach (DataRow machineRow in machineTable.Rows)
					{
						long machine_id = (long)machineRow["machine_id"];
						string name = (string)machineRow["name"];

						if (machineGenreIds.ContainsKey(name) == false)
							continue;

						long genre_id = machineGenreIds[name][1];

						command.Parameters["@genre_id"].Value = genre_id;
						command.Parameters["@machine_id"].Value = machine_id;

						command.ExecuteNonQuery();
					}

					transaction.Commit();
				}
				catch
				{
					transaction.Rollback();
					throw;
				}
				finally
				{
					Globals.Database._MachineConnection.Close();
				}
			}

			Database.ExecuteNonQuery(Globals.Database._MachineConnection, $"UPDATE ao_info SET genre_version = '{Version}' WHERE ao_info_id = 1");

			Console.WriteLine("...done");

		}

	}
}
