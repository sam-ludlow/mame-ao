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
				string[] parts = arg.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length != 2)
					throw new ApplicationException("Bad argument, expecting KEY=VALUE, " + arg);
				parameters.Add(parts[0].ToUpper(), parts[1]);
			}

			if (parameters.ContainsKey("DIRECTORY") == false)
				parameters.Add("DIRECTORY", Environment.CurrentDirectory);

			MameAOProcessor proc = new MameAOProcessor(parameters["DIRECTORY"]);

			if (parameters.ContainsKey("UPDATE") == true)
			{
				proc.Update(Int32.Parse(parameters["UPDATE"]));
				return;
			}

			proc.Run();
		}
	}
}
