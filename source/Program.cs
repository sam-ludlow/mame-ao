using System;
using System.Collections.Generic;

namespace Spludlow.MameAO
{
	internal class Program
	{
		static void Main(string[] args)
		{
			Dictionary<string, string> parameters = new Dictionary<string, string>();

			foreach (string arg in args)
			{
				Console.WriteLine(arg);

				int index = arg.IndexOf('=');
				if (index == -1)
					throw new ApplicationException("Bad argument, expecting KEY=VALUE, " + arg);

				parameters.Add(arg.Substring(0, index).ToUpper(), arg.Substring(index + 1));
			}

			if (parameters.ContainsKey("DIRECTORY") == false)
				parameters.Add("DIRECTORY", Environment.CurrentDirectory);

			MameAOProcessor proc = new MameAOProcessor(parameters["DIRECTORY"]);

			if (parameters.ContainsKey("OPERATION") == true)
			{
				switch (parameters["OPERATION"])
				{
					//	.\mame-ao.exe OPERATION=GET_MAME VERSION=0 DIRECTORY="C:\MAME AO Ops"
					case "GET_MAME":
						if (parameters.ContainsKey("VERSION") == false)
							throw new ApplicationException("This operation requires VERSION");

						Operations.GetMame(parameters["DIRECTORY"], parameters["VERSION"], proc._HttpClient);
						return;

					//	.\mame-ao.exe OPERATION=MAKE_MSSQL VERSION=0259 MSSQL_SERVER="Data Source='SPLCAL-MAIN';Integrated Security=True;TrustServerCertificate=True;" MSSQL_TARGET_NAMES="MameAoMachine, MameAoSoftware" DIRECTORY="C:\MAME AO Ops"
					case "MAKE_MSSQL":
						if (parameters.ContainsKey("VERSION") == false)
							throw new ApplicationException("This operation requires VERSION");
						
						if (parameters.ContainsKey("MSSQL_SERVER") == false)
							throw new ApplicationException("This operation requires MSSQL_SERVER");
						
						if (parameters.ContainsKey("MSSQL_TARGET_NAMES") == false)
							throw new ApplicationException("This operation requires MSSQL_TARGET_NAME");

						Operations.MakeMsSql(parameters["DIRECTORY"], parameters["VERSION"], parameters["MSSQL_SERVER"], parameters["MSSQL_TARGET_NAMES"]);
						return;

					default:
						throw new ApplicationException($"Unknown Operation {parameters["OPERATION"]}");
				}
			}

			if (parameters.ContainsKey("UPDATE") == true)
			{
				proc.Update(Int32.Parse(parameters["UPDATE"]));
				return;
			}

			proc.Run();
		}
	}
}
