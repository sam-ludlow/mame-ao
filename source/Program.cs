using System;
using System.Collections.Generic;

namespace Spludlow.MameAO
{
	internal class Program
	{
		static int Main(string[] args)
		{
			//args = new string[] { "tosec-mssql", "directory=C:\\ao-data\\tosec", "server=Data Source='my-mssql-server';Integrated Security=True;TrustServerCertificate=True;", "names=ao-tosec-test" };
			//args = new string[] { "tosec-mssql-payload", "directory=C:\\ao-data\\tosec", "server=Data Source='my-mssql-server';Integrated Security=True;TrustServerCertificate=True;", "names=ao-tosec-test" };


			if (args.Length > 0 && args[0].Contains("=") == false)
				args[0] = $"operation={args[0]}";

			Dictionary<string, string> arguments = new Dictionary<string, string>();

			foreach (string arg in args)
			{
				int index = arg.IndexOf('=');
				if (index == -1)
					throw new ApplicationException($"Bad argument, expecting key=value: {arg}");

				arguments.Add(arg.Substring(0, index).ToLower().Trim(), arg.Substring(index + 1).Trim());
			}

			if (arguments.ContainsKey("directory") == false)
				arguments.Add("directory", Environment.CurrentDirectory);

			MameAOProcessor proc = new MameAOProcessor(arguments["directory"]);

			if (arguments.ContainsKey("operation") == true)
			{
				if (arguments.ContainsKey("version") == false)
					arguments.Add("version", "0");

				return Operations.ProcessOperation(arguments);
			}

			if (arguments.ContainsKey("update") == true)
			{
				SelfUpdate.Update(Int32.Parse(arguments["update"]));
				return 0;
			}

			proc.Run();

			return 0;
		}
	}
}
