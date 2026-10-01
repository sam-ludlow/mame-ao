using System;
using System.Collections.Generic;
using System.IO;

namespace Spludlow.MameAO
{
	internal class Program
	{
		static int Main(string[] args)
		{
			string lookupFilename = @"C:\ao-data\fbneo-sha1-lookup.txt";
			string sqlLiteFilename = @"C:\GIT\mame-ao\bin\Debug\fbneo\2026-10-01T13-44-45\_fbneo.sqlite";

			//	TODO Learn command - dont have to remake database

			//CoreFbNeo.UtilImportXmlHashLookup(lookupFilename, @"C:\GIT\mame-ao\bin\Debug\_TEMP\CACHE\FBNeo%201.0.0.3%20260723%20GIT7a28a7d%20debug%20ROMs%20(split).xml");

			//CoreFbNeo.UtilGetHashLookup(lookupFilename, sqlLiteFilename);

			//CoreFbNeo.UtilLearnHashLookup(lookupFilename, sqlLiteFilename, @"C:\tmp\FBNeo mame-ao export\gba\gba");

			//CoreFbNeo.UtilReportHashLookup(lookupFilename, sqlLiteFilename);

			//foreach (string directory in Directory.GetDirectories(@"C:\tmp\FBNeo 1.0.0.3 260723 GIT7a28a7d debug ROMs (split)"))
			//	CoreFbNeo.UtilLearnHashLookup(lookupFilename, sqlLiteFilename, directory);

			//return 0;

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

			Globals.AO = new MameAOProcessor(arguments["directory"]);

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

			Globals.AO.Run();

			return 0;
		}
	}
}
