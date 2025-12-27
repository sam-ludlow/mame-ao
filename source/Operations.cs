using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

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
			{
				switch (operation)
				{
					case "snap_import":
						ValidateRequiredParameters(parameters, new string[] { "source", "target" });
						Snap.ImportSnap(parameters["source"], parameters["target"]);
						break;

					default:
						throw new ApplicationException($"Bad operation: {operation}");
				}
			}
			else
			{
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
						core.MSSql(parameters["server"], parameters["names"].Split(',').Select(name => name.Trim()).ToArray());
						break;

					case "mssql-payload":
						ValidateRequiredParameters(parameters, new string[] { "server", "names" });
						core.MSSqlPayload(parameters["server"], parameters["names"].Split(',').Select(name => name.Trim()).ToArray());
						break;

					default:
						throw new ApplicationException($"Bad operation: {operation}");
				}
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
	}
}
