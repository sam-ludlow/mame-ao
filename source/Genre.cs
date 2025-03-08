using System;
using System.Collections.Generic;
using System.IO;
using System.Data;

using System.Data.SQLite;
using System.Data.SqlClient;

namespace Spludlow.MameAO
{
	public class Genre
	{
		public string Version = "";
		public string SHA1 = "";
		public DataSet Data = null;

		public Genre()
		{
		}

		public void Initialize()
		{
			GitHubRepo repo = Globals.GitHubRepos["MAME_SupportFiles"];

			string url = repo.UrlRaw + "/main/catver.ini/catver.ini";

			Tools.ConsoleHeading(2, new string[] {
				$"Machine Genres",
				url
			});

			string iniData = repo.Fetch(url);

			if (iniData == null)
			{
				Console.WriteLine("!!! Can not get genres .ini file.");
				return;
			}

			Version = ParseVersion(iniData);
			SHA1 = Tools.SHA1HexText(iniData);

			//
			// Parse .ini
			//

			List<string> groups = new List<string>();
			List<string> genres = new List<string>();

			DataTable machineGroupGenreTable;

			try
			{
				using (StringReader reader = new StringReader(iniData))
				{
					machineGroupGenreTable = ParseIni(reader, groups, genres);
				}
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

		public string ParseVersion(string data)
		{
			string find = "catver.ini";

			int index = data.IndexOf(find);

			if (index == -1)
				return "";

			data = data.Substring(index + find.Length);

			index = data.IndexOf("/");

			if (index == -1)
				return "";

			return data.Substring(0, index).Trim();
		}

		public DataTable ParseIni(StringReader reader, List<string> groups, List<string> genres)
		{
			DataTable table = Tools.MakeDataTable(
				"machine	group	genre",
				"String*	String	String"
			);

			bool inData = false;

			string line;
			while ((line = reader.ReadLine()) != null)
			{
				line = line.Trim();
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

		private Dictionary<string, string> GetMachineDriverStatuses()
		{
			DataTable table = Database.ExecuteFill(Globals.Database._MachineConnectionString,
				"SELECT machine.name, driver.status FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id");

			Dictionary<string, string> result = new Dictionary<string, string>();

			foreach (DataRow row in table.Rows)
				result.Add((string)row["name"], (string)row["status"]);

			return result;
		}

		public void SetMachines(Dictionary<string, long[]> machineGenreIds)
		{
			DataTable infoTable = Database.ExecuteFill(Globals.Database._MachineConnectionString, "SELECT * FROM ao_info");

			if (infoTable.Columns.Contains("genre_version") == false)
			{
				Database.ExecuteNonQuery(Globals.Database._MachineConnectionString, "ALTER TABLE ao_info ADD COLUMN genre_version TEXT");
				Database.ExecuteNonQuery(Globals.Database._MachineConnectionString, "UPDATE ao_info SET genre_version = '' WHERE ao_info_id = 1");
			}

			infoTable = Database.ExecuteFill(Globals.Database._MachineConnectionString, "SELECT * FROM ao_info");

			string databaseVersion = (string)infoTable.Rows[0]["genre_version"];

			if (SHA1 == databaseVersion)
				return;

			Console.Write("Update Machines database with genre IDs ...");

			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnectionString, "SELECT * FROM machine WHERE machine_id = 0");

			if (machineTable.Columns.Contains("genre_id") == false)
				Database.ExecuteNonQuery(Globals.Database._MachineConnectionString, "ALTER TABLE machine ADD COLUMN genre_id INTEGER");

			Database.ExecuteNonQuery(Globals.Database._MachineConnectionString, "UPDATE machine SET genre_id = 0");

			machineTable = Database.ExecuteFill(Globals.Database._MachineConnectionString, "SELECT machine_id, name FROM machine");

			using (SQLiteConnection connection = new SQLiteConnection(Globals.Database._MachineConnectionString))
			{
				using (SQLiteCommand command = new SQLiteCommand("UPDATE machine SET genre_id = @genre_id WHERE machine_id = @machine_id", connection))
				{
					command.Parameters.Add("@genre_id", DbType.Int64);
					command.Parameters.Add("@machine_id", DbType.Int64);

					connection.Open();

					SQLiteTransaction transaction = connection.BeginTransaction();

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
						connection.Close();
					}
				}
			}

			Database.ExecuteNonQuery(Globals.Database._MachineConnectionString, $"UPDATE ao_info SET genre_version = '{SHA1}' WHERE ao_info_id = 1");

			Console.WriteLine("...done");

		}

	}
}
