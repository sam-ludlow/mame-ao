using System;
using System.Collections.Generic;

namespace Spludlow.MameAO
{
	internal class Program
	{
		static int Main(string[] args)
		{
			Dictionary<string, string> parameters = new Dictionary<string, string>();

			foreach (string arg in args)
			{
				int index = arg.IndexOf('=');
				if (index == -1)
					throw new ApplicationException("Bad argument, expecting KEY=VALUE, " + arg);

				parameters.Add(arg.Substring(0, index).ToUpper(), arg.Substring(index + 1));
			}

			if (parameters.ContainsKey("DIRECTORY") == false)
				parameters.Add("DIRECTORY", Environment.CurrentDirectory);

			MameAOProcessor proc = new MameAOProcessor(parameters["DIRECTORY"]);

			if (parameters.ContainsKey("OPERATION") == true)
				return Operations.ProcessOperation(parameters, proc);

			if (parameters.ContainsKey("UPDATE") == true)
			{
				proc.Update(Int32.Parse(parameters["UPDATE"]));
				return 0;
			}

			proc.Run();

			return 0;
		}
	}
}
