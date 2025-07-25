using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

using System.Data.SqlClient;
using System.Data.SQLite;

namespace Spludlow.MameAO
{
	public class Database
	{
		public static DataQueryProfile[] DataQueryProfiles = new DataQueryProfile[] {
			new DataQueryProfile(){
				Key = "arcade-good",
				Text = "Arcade Good",
				Decription = "Arcade Machines - Status Good - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'good') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins > 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "arcade-imperfect",
				Text = "Arcade Imperfect",
				Decription = "Arcade Machines - Status Imperfect - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'imperfect') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins > 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "computer-console-good",
				Text = "Computers & Consoles Good",
				Decription = "Computers & Consoles with software - status good - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'good') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins = 0) AND (ao_softwarelist_count > 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "computer-console-imperfect",
				Text = "Computers & Consoles Imperfect",
				Decription = "Computers & Consoles with software - status imperfect - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'imperfect') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins = 0) AND (ao_softwarelist_count > 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "other-good",
				Text = "Other Good",
				Decription = "Other Systems without software - status good - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'good') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins = 0) AND (ao_softwarelist_count = 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "other-imperfect",
				Text = "Other Imperfect",
				Decription = "Other Systems without software - status imperfect - Parents only",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.cloneof IS NULL) AND (driver.status = 'imperfect') AND (machine.runnable = 'yes') AND (machine.isdevice = 'no') AND (ao_input_coins = 0) AND (ao_softwarelist_count = 0) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "everything",
				Text = "Everything",
				Decription = "Absolutely Everything",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.runnable = 'yes') AND (machine.isdevice = 'no') @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
			new DataQueryProfile(){
				Key = "favorites",
				Text = "Favorites",
				Decription = "Favorites",
				CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((machine.runnable = 'yes') AND (machine.isdevice = 'no') @FAVORITES @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
			},
		};

		public static DataQueryProfile GetDataQueryProfile(string key)
		{
			DataQueryProfile found = null;

			if (key.StartsWith("genre") == true)
			{
				long genre_id = Int64.Parse(key.Split(new char[] { '-' })[1]);

				found = new DataQueryProfile()
				{
					Key = key,
					Text = "genre",
					Decription = "genre",
					CommandText =
					"SELECT machine.*, driver.*, COUNT() OVER() AS ao_total " +
					"FROM machine INNER JOIN driver ON machine.machine_id = driver.machine_id " +
					"WHERE ((genre_id = @genre_id) @SEARCH) " +
					"ORDER BY machine.description COLLATE NOCASE ASC " +
					"LIMIT @LIMIT OFFSET @OFFSET",
				};

				found.CommandText = found.CommandText.Replace("@genre_id", genre_id.ToString());
			}
			else
			{
				foreach (DataQueryProfile profile in Database.DataQueryProfiles)
				{
					if (profile.Key == key)
					{
						found = profile;
						break;
					}
				}
			}

			if (found == null)
				throw new ApplicationException($"Data Profile not found {key}");

			return found;
		}

		public static void DatabaseFromXML(string name, string connectionString, DataSet dataSet)
		{
			using (SQLiteConnection connection = new SQLiteConnection(connectionString))
			{
				Console.Write($"Creating SQLite {name} ...");
				connection.Open();
				try
				{
					foreach (DataTable table in dataSet.Tables)
					{
						List<string> columnDefinitions = new List<string>();

						foreach (DataColumn column in table.Columns)
						{
							string dataType = "TEXT";
							if (column.ColumnName.EndsWith("_id") == true)
							{
								dataType = columnDefinitions.Count == 0 ? "INTEGER PRIMARY KEY" : "INTEGER";
							}
							else
							{
								if (column.DataType == typeof(int) || column.DataType == typeof(long))
									dataType = "INTEGER";
							}

							if (table.TableName == "machine" && column.ColumnName == "description")
								dataType += " COLLATE NOCASE";

							columnDefinitions.Add($"\"{column.ColumnName}\" {dataType}");
						}

						string tableDefinition = $"CREATE TABLE {table.TableName}({String.Join(",", columnDefinitions.ToArray())});";

						using (SQLiteCommand command = new SQLiteCommand(tableDefinition, connection))
						{
							command.ExecuteNonQuery();
						}
					}

					foreach (DataTable table in dataSet.Tables)
					{
						Console.Write($"{table.TableName}...");

						List<string> columnNames = new List<string>();
						List<string> parameterNames = new List<string>();
						foreach (DataColumn column in table.Columns)
						{
							columnNames.Add($"\"{column.ColumnName}\"");
							parameterNames.Add("@" + column.ColumnName);
						}

						string commandText = $"INSERT INTO {table.TableName}({String.Join(",", columnNames.ToArray())}) VALUES({String.Join(",", parameterNames.ToArray())});";

						SQLiteTransaction transaction = connection.BeginTransaction();
						try
						{
							foreach (DataRow row in table.Rows)
							{
								using (SQLiteCommand command = new SQLiteCommand(commandText, connection, transaction))
								{
									foreach (DataColumn column in table.Columns)
										command.Parameters.AddWithValue("@" + column.ColumnName, row[column]);

									command.ExecuteNonQuery();
								}
							}

							transaction.Commit();
						}
						catch
						{
							transaction.Rollback();
							throw;
						}
					}

					if (name == "mame")
					{
						foreach (string commandText in new string[] {
						"CREATE INDEX machine_name_index ON machine(name);"
						})
							using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
								command.ExecuteNonQuery();
					}

					if (name == "softwarelists")
					{

					}
				}
				finally
				{
					connection.Close();
				}
				Console.WriteLine("...done.");
			}
		}

		public static bool TableExists(SQLiteConnection connection, string tableName)
		{
			object obj = ExecuteScalar(connection, $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}'");

			if (obj == null || obj is DBNull)
				return false;

			return true;
		}
		public static string[] TableList(SQLiteConnection connection)
		{
			List<string> tableNames = new List<string>();

			DataTable table = Database.ExecuteFill(connection, "SELECT name FROM sqlite_master WHERE type = 'table'");
			foreach (DataRow row in table.Rows)
			{
				string tableName = (string)row[0];

				if (tableName.StartsWith("sqlite_") == false)
					tableNames.Add(tableName);
			}
			return tableNames.ToArray();
		}
		public static object ExecuteScalar(SQLiteConnection connection, string commandText)
		{
			connection.Open();
			try
			{
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
					return command.ExecuteScalar();
			}
			finally
			{
				connection.Close();
			}
		}

		public static int ExecuteNonQuery(string connectionString, string commandText)
		{
			using (SQLiteConnection connection = new SQLiteConnection(connectionString))
			{
				connection.Open();
				try
				{
					using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
						return command.ExecuteNonQuery();
				}
				finally
				{
					connection.Close();
				}
			}
		}
		public static int ExecuteNonQuery(SQLiteConnection connection, string commandText)
		{
			bool openClose = connection.State == ConnectionState.Closed;

			if (openClose)
				connection.Open();
			try
			{
				using (SQLiteCommand command = new SQLiteCommand(commandText, connection))
					return command.ExecuteNonQuery();
			}
			finally
			{
				if (openClose)
					connection.Close();
			}
		}
		public static DataTable ExecuteFill(string connectionString, string commandText)
		{
			var table = new DataTable();
			using (var connection = new SQLiteConnection(connectionString))
				using (var adapter = new SQLiteDataAdapter(commandText, connection))
					adapter.Fill(table);
			return table;
		}
		public static DataTable ExecuteFill(SQLiteConnection connection, string commandText)
		{
			var table = new DataTable();
			using (var adapter = new SQLiteDataAdapter(commandText, connection))
				adapter.Fill(table);
			return table;
		}
		public static DataTable ExecuteFill(SQLiteCommand command)
		{
			var table = new DataTable();
			using (var adapter = new SQLiteDataAdapter(command))
				adapter.Fill(table);
			return table;
		}

		//
		// MS SQL
		//

		public static bool DatabaseExists(SqlConnection connection, string databaseName)
		{
			object obj = ExecuteScalar(connection, $"SELECT name FROM sys.databases WHERE name = '{databaseName}'");

			if (obj == null || obj is DBNull)
				return false;

			return true;
		}

		public static object ExecuteScalar(SqlConnection connection, string commandText)
		{
			connection.Open();
			try
			{
				using (SqlCommand command = new SqlCommand(commandText, connection))
					return command.ExecuteScalar();
			}
			finally
			{
				connection.Close();
			}

		}
		public static object ExecuteScalar(SqlCommand command)
		{
			command.Connection.Open();
			try
			{
				return command.ExecuteScalar();
			}
			finally
			{
				command.Connection.Close();
			}

		}

		public static int ExecuteNonQuery(SqlConnection connection, string commandText)
		{
			connection.Open();
			try
			{
				using (SqlCommand command = new SqlCommand(commandText, connection))
				{
					command.CommandTimeout = 15 * 60;
					return command.ExecuteNonQuery();
				}
			}
			finally
			{
				connection.Close();
			}
		}
		public static DataTable ExecuteFill(SqlConnection connection, string commandText)
		{
			DataTable table = new DataTable();
			using (SqlDataAdapter adapter = new SqlDataAdapter(commandText, connection))
				adapter.Fill(table);
			return table;
		}

		public static void BulkInsert(SqlConnection connection, DataTable table)
		{
			using (SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(connection))
			{
				sqlBulkCopy.DestinationTableName = table.TableName;

				sqlBulkCopy.BulkCopyTimeout = 15 * 60;

				connection.Open();
				try
				{
					sqlBulkCopy.WriteToServer(table);
				}
				finally
				{
					connection.Close();
				}
			}
		}

		public static string[] TableList(SqlConnection connection)
		{
			List<string> result = new List<string>();

			DataTable table = ExecuteFill(connection,
				"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME");

			foreach (DataRow row in table.Rows)
				result.Add((string)row["TABLE_NAME"]);

			return result.ToArray();
		}

		public static bool TableExists(SqlConnection connection, string tableName)
		{
			using (SqlCommand command = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE (TABLE_TYPE='BASE TABLE' AND TABLE_NAME=@TABLE_NAME)", connection))
			{
				command.Parameters.AddWithValue("@TABLE_NAME", tableName);
				object obj = ExecuteScalar(command);

				if (obj == null || obj is DBNull)
					return false;

				return true;
			}
		}

		public static void ConsoleQuery(ICore core, string database, string commandText)
		{
			using (SQLiteConnection connection = new SQLiteConnection(database == "m" ? core.ConnectionStrings[0] : core.ConnectionStrings[1]))
			{
				try
				{
					if (commandText.ToUpper().StartsWith("SELECT") == true)
					{
						DataTable table = ExecuteFill(connection, commandText);

						StringBuilder text = new StringBuilder();

						foreach (DataColumn column in table.Columns)
						{
							if (column.Ordinal > 0)
								text.Append('\t');
							text.Append(column.ColumnName);
						}
						Console.WriteLine(text.ToString());

						text.Length = 0;
						foreach (DataColumn column in table.Columns)
						{
							if (column.Ordinal > 0)
								text.Append('\t');
							text.Append(new String('=', column.ColumnName.Length));
						}
						Console.WriteLine(text.ToString());

						foreach (DataRow row in table.Rows)
						{
							text.Length = 0;
							foreach (DataColumn column in table.Columns)
							{
								if (column.Ordinal > 0)
									text.Append('\t');
								if (row.IsNull(column) == false)
									text.Append(Convert.ToString(row[column]));
							}
							Console.WriteLine(text.ToString());
						}
					}
					else
					{
						ExecuteNonQuery(connection, commandText);
					}
				}
				catch (SQLiteException e)
				{
					Console.WriteLine(e.Message);
				}

				Console.WriteLine();
			}
		}

	}
}
