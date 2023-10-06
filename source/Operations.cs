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

namespace Spludlow.MameAO
{
	/// <summary>
	/// Operations - Used for MAME data processing pipelines
	/// </summary>
	public class Operations
	{

		// only mame should accpet "0" ?
		// others should get last version directory !!!

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
		public static bool GetMame(string directory, string version, HttpClient httpClient)
		{
			bool newVersion = false;

			string mameLatestJson = Tools.Query(httpClient, "https://api.github.com/repos/mamedev/mame/releases/latest");
			mameLatestJson = Tools.PrettyJSON(mameLatestJson);

			dynamic mameLatest = JsonConvert.DeserializeObject<dynamic>(mameLatestJson);

			if (version == "0")
				version = ((string)mameLatest.tag_name).Substring(4);

			string versionDirectory = Path.Combine(directory, version);

			if (Directory.Exists(versionDirectory) == false)
				Directory.CreateDirectory(versionDirectory);

			//
			// MAME
			//
			string exeFilename = Path.Combine(versionDirectory, "mame.exe");

			if (File.Exists(exeFilename) == false)
			{
				newVersion = true;

				string binariesUrl = "https://github.com/mamedev/mame/releases/download/mame@VERSION@/mame@VERSION@b_64bit.exe";
				binariesUrl = binariesUrl.Replace("@VERSION@", version);

				string binariesFilename = Path.Combine(versionDirectory, Path.GetFileName(binariesUrl));

				Tools.Download(binariesUrl, binariesFilename, 0, 15);

				Mame.RunSelfExtract(binariesFilename);
			}

			//
			// XML
			//
			string machineXmlFilename = Path.Combine(versionDirectory, "_machine.xml");
			string softwareXmlFilename = Path.Combine(versionDirectory, "_software.xml");

			if (File.Exists(machineXmlFilename) == false)
				Mame.ExtractXML(exeFilename, machineXmlFilename, "-listxml");

			if (File.Exists(softwareXmlFilename) == false)
				Mame.ExtractXML(exeFilename, softwareXmlFilename, "-listsoftware");


			//
			// SQLite
			//
			string machineSqlLiteFilename = Path.Combine(versionDirectory, "_machine.sqlite");
			string softwareSqlLiteFilename = Path.Combine(versionDirectory, "_software.sqlite");

			if (File.Exists(machineSqlLiteFilename) == false)
			{
				GenerateSqlLite(machineXmlFilename, machineSqlLiteFilename);
				GC.Collect();
			}

			if (File.Exists(softwareSqlLiteFilename) == false)
			{
				GenerateSqlLite(softwareXmlFilename, softwareSqlLiteFilename);
				GC.Collect();
			}

			return newVersion;
		}

		public static void GenerateSqlLite(string xmlFilename, string sqliteFilename)
		{
			XElement document = XElement.Load(xmlFilename);

			DataSet dataSet = new DataSet();

			ReadXML.ImportXMLWork(document, dataSet, null, null);

			File.WriteAllBytes(sqliteFilename, new byte[0]);

			string connectionString = $"Data Source='{sqliteFilename}';datetimeformat=CurrentCulture;";

			SQLiteConnection connection = new SQLiteConnection(connectionString);

			Database.DatabaseFromXML(document, connection, dataSet);
		}

		public static void MakeJSON(string directory, string version)
		{
			string versionDirectory = Path.Combine(directory, version);

			if (Directory.Exists(versionDirectory) == false)
				throw new ApplicationException($"The version Directory does not exist '{versionDirectory}'.");

			string machineXmlFilename = Path.Combine(versionDirectory, "_machine.xml");
			string softwareXmlFilename = Path.Combine(versionDirectory, "_software.xml");

			string machineJsonFilename = Path.Combine(versionDirectory, "_machine.json");
			string softwareJsonFilename = Path.Combine(versionDirectory, "_software.json");

			if (File.Exists(machineJsonFilename) == false)
				XML2JSON(machineXmlFilename, machineJsonFilename);

			if (File.Exists(softwareJsonFilename) == false)
				XML2JSON(softwareXmlFilename, softwareJsonFilename);
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

		public static void MakeMsSql(string directory, string version, string serverConnectionString, string databaseNames)
		{
			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });
			string[] databaseTypes = new string[] { "machine", "software" };

			if (databaseNamesEach.Length != 2)
				throw new ApplicationException("database names must be 2 parts");

			for (int index = 0; index < 2; ++index)
			{
				string targetDatabaseName = databaseNamesEach[index].Trim();
				string sourceDatabaseFilename = Path.Combine(directory, version, $"_{databaseTypes[index]}.sqlite");

				SQLite2MSSQL(sourceDatabaseFilename, serverConnectionString, targetDatabaseName);
			}
		}

		public static void SQLite2MSSQL(string sqliteFilename, string serverConnectionString, string databaseName)
		{
			SqlConnection targetConnection = new SqlConnection(serverConnectionString);

			if (Database.DatabaseExists(targetConnection, databaseName) == true)
				return;

			Database.ExecuteNonQuery(targetConnection, $"CREATE DATABASE[{databaseName}]");

			targetConnection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';");

			string sourceConnectionString = $"Data Source='{sqliteFilename}';datetimeformat=CurrentCulture;";

			SQLiteConnection sourceConnection = new SQLiteConnection(sourceConnectionString);

			foreach (string tableName in Database.TableList(sourceConnection))
			{
				DataTable table = Database.ExecuteFill(sourceConnection, $"SELECT * FROM [{tableName}]");

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

				string createText = $"CREATE TABLE [{tableName}]({String.Join(", ", columnDefs.ToArray())});";

				Console.WriteLine(createText);

				Database.ExecuteNonQuery(targetConnection, createText);

				using (SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(targetConnection))
				{
					sqlBulkCopy.DestinationTableName = tableName;

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
