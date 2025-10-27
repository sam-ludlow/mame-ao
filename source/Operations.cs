using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml.Linq;

namespace Spludlow.MameAO
{
	public class Operations
	{
		public static int ProcessOperation(Dictionary<string, string> parameters)
		{
			int exitCode;
			ICore core;

			DateTime timeStart = DateTime.Now;

			switch (parameters["operation"])
			{
				//
				//	MAME
				//
				case "mame-get":
					core = new CoreMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					exitCode = core.Get();
					break;

				case "mame-xml":
					core = new CoreMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Xml();
					exitCode = 0;
					break;

				case "mame-json":
					core = new CoreMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Json();
					exitCode = 0;
					break;

				case "mame-sqlite":
					core = new CoreMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.SQLite();
					exitCode = 0;
					break;

				case "mame-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = MameMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "mame-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = OperationsPayload.MameMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				//
				// HBMAME
				//
				case "hbmame-get":
					core = new CoreHbMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					exitCode = core.Get();
					break;

				case "hbmame-xml":
					core = new CoreHbMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Xml();
					exitCode = 0;
					break;

				case "hbmame-json":
					core = new CoreHbMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Json();
					exitCode = 0;
					break;

				case "hbmame-sqlite":
					core = new CoreHbMame();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.SQLite();
					exitCode = 0;
					break;

				case "hbmame-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = HbMameMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "hbmame-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = OperationsPayload.HbMameMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				//
				// FBNeo
				//
				case "fbneo-get":
					core = new CoreFbNeo();
					core.Initialize(parameters["directory"], parameters["version"]);
					exitCode = core.Get();
					break;

				case "fbneo-xml":
					core = new CoreFbNeo();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Xml();
					exitCode = 0;
					break;

				case "fbneo-json":
					core = new CoreFbNeo();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Json();
					exitCode = 0;
					break;

				case "fbneo-sqlite":
					core = new CoreFbNeo();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.SQLite();
					exitCode = 0;
					break;

				case "fbneo-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = FBNeoMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "fbneo-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = OperationsPayload.FBNeoMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				//
				// TOSEC
				//
				case "tosec-get":
					core = new CoreTosec();
					core.Initialize(parameters["directory"], parameters["version"]);
					exitCode = core.Get();
					break;

				case "tosec-xml":
					core = new CoreTosec();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Xml();
					exitCode = 0;
					break;

				case "tosec-json":
					core = new CoreTosec();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.Json();
					exitCode = 0;
					break;

				case "tosec-sqlite":
					core = new CoreTosec();
					core.Initialize(parameters["directory"], parameters["version"]);
					core.SQLite();
					exitCode = 0;
					break;

				case "tosec-mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = TosecMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				case "tosec-mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					exitCode = OperationsPayload.TosecMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
					break;

				default:
					throw new ApplicationException($"Unknown Operation {parameters["operation"]}");
			}

			TimeSpan timeTook = DateTime.Now - timeStart;

			Console.WriteLine($"Operation '{parameters["operation"]}' took: {Math.Round(timeTook.TotalSeconds, 0)} seconds");

			return exitCode;
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

		//
		// MS SQL
		//
		public static int MameMSSQL(string directory, string version, string serverConnectionString, string databaseNames)
		{
			if (version == "0")
				version = CoreMame.LatestLocalVersion(directory);

			directory = Path.Combine(directory, version);

			string[] xmlFilenames = new string[] {
				Path.Combine(directory, "_machine.xml"),
				Path.Combine(directory, "_software.xml"),
			};

			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });

			if (databaseNamesEach.Length != 2)
				throw new ApplicationException("database names must be 2 parts comma delimited");

			for (int index = 0; index < 2; ++index)
			{
				string sourceXmlFilename = xmlFilenames[index];
				string targetDatabaseName = databaseNamesEach[index].Trim();

				XElement document = XElement.Load(sourceXmlFilename);
				DataSet dataSet = new DataSet();
				ReadXML.ImportXMLWork(document, dataSet, null, null);

				Database.DataSet2MSSQL(dataSet, serverConnectionString, targetDatabaseName);

				Database.MakeForeignKeys(serverConnectionString, targetDatabaseName);
			}

			return 0;
		}

		public static int HbMameMSSQL(string directory, string version, string serverConnectionString, string databaseNames)
		{
			if (version == "0")
				version = CoreHbMame.LatestLocalVersion(directory);

			directory = Path.Combine(directory, version);

			string[] xmlFilenames = new string[] {
				Path.Combine(directory, "_machine.xml"),
				Path.Combine(directory, "_software.xml"),
			};

			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });

			if (databaseNamesEach.Length != 2)
				throw new ApplicationException("database names must be 2 parts comma delimited");

			for (int index = 0; index < 2; ++index)
			{
				string sourceXmlFilename = xmlFilenames[index];
				string targetDatabaseName = databaseNamesEach[index].Trim();

				XElement document = XElement.Load(sourceXmlFilename);
				DataSet dataSet = new DataSet();
				ReadXML.ImportXMLWork(document, dataSet, null, null);

				Database.DataSet2MSSQL(dataSet, serverConnectionString, targetDatabaseName);

				Database.MakeForeignKeys(serverConnectionString, targetDatabaseName);
			}

			return 0;
		}

		public static int FBNeoMSSQL(string directory, string version, string serverConnectionString, string databaseName)
		{
			if (version == "0")
				version = CoreFbNeo.FBNeoGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			DataSet dataSet = CoreFbNeo.FBNeoDataSet(directory);

			Database.DataSet2MSSQL(dataSet, serverConnectionString, databaseName);

			Database.MakeForeignKeys(serverConnectionString, databaseName);

			return 0;
		}

		public static int TosecMSSQL(string directory, string version, string serverConnectionString, string databaseName)
		{
			if (version == "0")
				version = CoreTosec.TosecGetLatestDownloadedVersion(directory);

			directory = Path.Combine(directory, version);

			DataSet dataSet = CoreTosec.TosecDataSet(directory);

			Database.DataSet2MSSQL(dataSet, serverConnectionString, databaseName);

			Database.MakeForeignKeys(serverConnectionString, databaseName);

			return 0;
		}


	}
}

