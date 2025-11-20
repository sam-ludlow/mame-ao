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
			int exitCode = 0;

			DateTime timeStart = DateTime.Now;

			string operation = parameters["operation"];

			int index = operation.IndexOf("-");
			if (index == -1)
				throw new ApplicationException("Bad operation, usage: 'core-operation'.");

			string coreName = operation.Substring(0, index);
			operation = operation.Substring(index + 1);

			ICore core;
			switch (coreName)
			{
				case "mame":
					core = new CoreMame();
					break;

				case "hbmame":
					core = new CoreHbMame();
					break;

				case "fbneo":
					core = new CoreFbNeo();
					break;

				case "tosec":
					core = new CoreTosec();
					break;

				default:
					throw new ApplicationException($"Bad core: {coreName}");
			}

			core.Initialize(parameters["directory"], parameters["version"]);

			switch (operation)
			{
				case "get":
					exitCode = core.Get();
					break;

				case "xml":
					core.Xml();
					break;

				case "json":
					core.Json();
					break;

				case "sqlite":
					core.SQLite();
					break;

				case "msaccess":
					core.MsAccess();
					break;

				case "zips":
					core.Zips();
					break;

				case "mssql":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					switch (coreName)
					{
						case "mame":
							MameMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
							break;

						case "hbmame":
							HbMameMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
							break;

						case "fbneo":
							FBNeoMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
							break;

						case "tosec":
							TosecMSSQL(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
							break;
					}
					break;

				case "mssql-payload":
					ValidateRequiredParameters(parameters, new string[] { "server", "names" });
					switch (coreName)
					{
						case "mame":
							OperationsPayload.MameMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
							break;

						case "hbmame":
							OperationsPayload.HbMameMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
							break;

						case "fbneo":
							OperationsPayload.FBNeoMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
							break;

						case "tosec":
							OperationsPayload.TosecMSSQLPayloads(parameters["directory"], parameters["version"], parameters["server"], parameters["names"]);
							break;
					}
					break;

				default:
					throw new ApplicationException($"Bad operation: {operation}");
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

