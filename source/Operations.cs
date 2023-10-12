using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace Spludlow.MameAO
{
	/// <summary>
	/// Operations - Used for MAME data processing pipelines
	/// </summary>
	public class Operations
	{

		public static int ProcessOperation(Dictionary<string, string> parameters, MameAOProcessor proc)
		{
			switch (parameters["OPERATION"])
			{
				case "GET_MAME":
					ValidateRequiredParameters(parameters, new string[] { "VERSION" });

					return GetMame(parameters["DIRECTORY"], parameters["VERSION"], proc._HttpClient);

				case "MAKE_XML":
					ValidateRequiredParameters(parameters, new string[] { "VERSION" });

					return MakeXML(parameters["DIRECTORY"], parameters["VERSION"]);

				case "MAKE_JSON":
					ValidateRequiredParameters(parameters, new string[] { "VERSION" });

					return MakeJSON(parameters["DIRECTORY"], parameters["VERSION"]);

				case "MAKE_SQLITE":
					ValidateRequiredParameters(parameters, new string[] { "VERSION" });

					return MakeSQLite(parameters["DIRECTORY"], parameters["VERSION"]);

				case "MAKE_MSSQL":
					ValidateRequiredParameters(parameters, new string[] { "VERSION", "MSSQL_SERVER", "MSSQL_TARGET_NAMES" });

					return MakeMSSQL(parameters["DIRECTORY"], parameters["VERSION"], parameters["MSSQL_SERVER"], parameters["MSSQL_TARGET_NAMES"]);

				default:
					throw new ApplicationException($"Unknown Operation {parameters["OPERATION"]}");
			}
		}

		private static void ValidateRequiredParameters(Dictionary<string, string> parameters, string[] required)
		{
			List<string> missing = new List<string>();

			foreach (string name in required)
				if (parameters.ContainsKey(name) == false)
					missing.Add(name);

			if (missing.Count > 0)
				throw new ApplicationException($"This operation requires these parameters '{String.Join(", ", missing)}'.");

		}

		public static string GetLatestDownloadedVersion(string directory)
		{
			List<string> versions = new List<string>();

			foreach (string versionDirectory in Directory.GetDirectories(directory))
			{
				string version = Path.GetFileName(versionDirectory);

				string exeFilename = Path.Combine(versionDirectory, "mame.exe");

				if (File.Exists(exeFilename) == true)
					versions.Add(version);
			}

			if (versions.Count == 0)
				throw new ApplicationException($"No MAME versions found in '{directory}'.");

			versions.Sort();

			return versions[versions.Count - 1];
		}

		//
		// MAME
		//
		public static int GetMame(string directory, string version, HttpClient httpClient)
		{
			int newVersion = 0;

			string mameLatestJson = Tools.Query(httpClient, "https://api.github.com/repos/mamedev/mame/releases/latest");
			mameLatestJson = Tools.PrettyJSON(mameLatestJson);

			dynamic mameLatest = JsonConvert.DeserializeObject<dynamic>(mameLatestJson);

			if (version == "0")
				version = ((string)mameLatest.tag_name).Substring(4);

			string versionDirectory = Path.Combine(directory, version);

			if (Directory.Exists(versionDirectory) == false)
				Directory.CreateDirectory(versionDirectory);

			string exeFilename = Path.Combine(versionDirectory, "mame.exe");

			if (File.Exists(exeFilename) == false)
			{
				newVersion = 1;

				string binariesUrl = "https://github.com/mamedev/mame/releases/download/mame@VERSION@/mame@VERSION@b_64bit.exe";
				binariesUrl = binariesUrl.Replace("@VERSION@", version);

				string binariesFilename = Path.Combine(versionDirectory, Path.GetFileName(binariesUrl));

				Tools.Download(binariesUrl, binariesFilename, 0, 15);

				Mame.RunSelfExtract(binariesFilename);
			}

			return newVersion;
		}

		//
		// XML
		//
		public static int MakeXML(string directory, string version)
		{
			if (version == "0")
				version = GetLatestDownloadedVersion(directory);

			string versionDirectory = Path.Combine(directory, version);

			string exeFilename = Path.Combine(versionDirectory, "mame.exe");

			string machineXmlFilename = Path.Combine(versionDirectory, "_machine.xml");
			string softwareXmlFilename = Path.Combine(versionDirectory, "_software.xml");

			if (File.Exists(machineXmlFilename) == false)
				Mame.ExtractXML(exeFilename, machineXmlFilename, "-listxml");

			if (File.Exists(softwareXmlFilename) == false)
				Mame.ExtractXML(exeFilename, softwareXmlFilename, "-listsoftware");

			return 0;
		}

		//
		// JSON
		//
		public static int MakeJSON(string directory, string version)
		{
			if (version == "0")
				version = GetLatestDownloadedVersion(directory);

			string versionDirectory = Path.Combine(directory, version);

			string machineXmlFilename = Path.Combine(versionDirectory, "_machine.xml");
			string softwareXmlFilename = Path.Combine(versionDirectory, "_software.xml");

			string machineJsonFilename = Path.Combine(versionDirectory, "_machine.json");
			string softwareJsonFilename = Path.Combine(versionDirectory, "_software.json");

			if (File.Exists(machineJsonFilename) == false)
				XML2JSON(machineXmlFilename, machineJsonFilename);

			if (File.Exists(softwareJsonFilename) == false)
				XML2JSON(softwareXmlFilename, softwareJsonFilename);

			return 0;
		}
		public static void XML2JSON(string inputXmlFilename, string outputJsonFilename)
		{
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(inputXmlFilename);

			JsonSerializerSettings serializerSettings = new JsonSerializerSettings();
			serializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;

			using (StreamWriter streamWriter = new StreamWriter(outputJsonFilename, false, new UTF8Encoding(false)))
			{
				CustomJsonWriter customJsonWriter = new CustomJsonWriter(streamWriter);

				JsonSerializer jsonSerializer = JsonSerializer.Create(serializerSettings);
				jsonSerializer.Serialize(customJsonWriter, xmlDocument);
			}
		}

		//
		// SQLite
		//
		public static int MakeSQLite(string directory, string version)
		{
			if (version == "0")
				version = GetLatestDownloadedVersion(directory);

			string versionDirectory = Path.Combine(directory, version);

			string machineSqlLiteFilename = Path.Combine(versionDirectory, "_machine.sqlite");
			string softwareSqlLiteFilename = Path.Combine(versionDirectory, "_software.sqlite");

			string machineXmlFilename = Path.Combine(versionDirectory, "_machine.xml");
			string softwareXmlFilename = Path.Combine(versionDirectory, "_software.xml");

			if (File.Exists(machineSqlLiteFilename) == false)
			{
				XML2SQLite(machineXmlFilename, machineSqlLiteFilename);
				GC.Collect();
			}

			if (File.Exists(softwareSqlLiteFilename) == false)
			{
				XML2SQLite(softwareXmlFilename, softwareSqlLiteFilename);
				GC.Collect();
			}

			return 0;
		}
		public static void XML2SQLite(string xmlFilename, string sqliteFilename)
		{
			XElement document = XElement.Load(xmlFilename);

			DataSet dataSet = new DataSet();

			ReadXML.ImportXMLWork(document, dataSet, null, null);

			File.WriteAllBytes(sqliteFilename, new byte[0]);

			string connectionString = $"Data Source='{sqliteFilename}';datetimeformat=CurrentCulture;";

			SQLiteConnection connection = new SQLiteConnection(connectionString);

			Database.DatabaseFromXML(document, connection, dataSet);
		}

		//
		// MS SQL
		//
		public static int MakeMSSQL(string directory, string version, string serverConnectionString, string databaseNames)
		{
			if (version == "0")
				version = GetLatestDownloadedVersion(directory);

			string versionDirectory = Path.Combine(directory, version);

			string[] xmlFilenames = new string[] {
				Path.Combine(versionDirectory, "_machine.xml"),
				Path.Combine(versionDirectory, "_software.xml"),
			};

			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });

			if (databaseNamesEach.Length != 2)
				throw new ApplicationException("database names must be 2 parts comma delimited");

			for (int index = 0; index < 2; ++index)
			{
				string sourceXmlFilename = xmlFilenames[index];
				string targetDatabaseName = databaseNamesEach[index].Trim();

				XML2MSSQL(sourceXmlFilename, serverConnectionString, targetDatabaseName);

				GC.Collect();
			}

			return 0;
		}
		public static void XML2MSSQL(string xmlFilename, string serverConnectionString, string databaseName)
		{
			SqlConnection targetConnection = new SqlConnection(serverConnectionString);

			if (Database.DatabaseExists(targetConnection, databaseName) == true)
				return;

			Database.ExecuteNonQuery(targetConnection, $"CREATE DATABASE[{databaseName}]");

			targetConnection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';");

			XElement document = XElement.Load(xmlFilename);

			DataSet dataSet = new DataSet();

			ReadXML.ImportXMLWork(document, dataSet, null, null);

			foreach (DataTable table in dataSet.Tables)
			{
				List<string> columnDefs = new List<string>();

				foreach (DataColumn column in table.Columns)
				{
					int max = 1;
					if (column.DataType.Name == "String")
					{
						foreach (DataRow row in table.Rows)
						{
							if (row.IsNull(column) == false)
								max = Math.Max(max, ((string)row[column]).Length);
						}
					}

					switch (column.DataType.Name)
					{
						case "String":
							columnDefs.Add($"[{column.ColumnName}] NVARCHAR({max})");
							break;

						case "Int64":
							columnDefs.Add($"[{column.ColumnName}] BIGINT" + (columnDefs.Count == 0 ? " NOT NULL PRIMARY KEY" : ""));
							break;

						default:
							throw new ApplicationException($"SQL Bulk Copy, Unknown datatype {column.DataType.Name}");
					}
				}

				string createText = $"CREATE TABLE [{table.TableName}]({String.Join(", ", columnDefs.ToArray())});";

				Console.WriteLine(createText);

				Database.ExecuteNonQuery(targetConnection, createText);

				using (SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(targetConnection))
				{
					sqlBulkCopy.DestinationTableName = table.TableName;

					sqlBulkCopy.BulkCopyTimeout = 15 * 60;

					targetConnection.Open();
					try
					{
						sqlBulkCopy.WriteToServer(table);
					}
					finally
					{
						targetConnection.Close();
					}
				}
			}
		}

	}

	public class CustomJsonWriter : JsonTextWriter
	{
		public CustomJsonWriter(TextWriter writer) : base(writer) { }
		public override void WritePropertyName(string name)
		{
			if (name.StartsWith("@") || name.StartsWith("#"))
				base.WritePropertyName(name.Substring(1));
			else
				base.WritePropertyName(name);
		}
	}
}
