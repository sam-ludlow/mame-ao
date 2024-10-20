using System;
using System.Collections.Generic;

namespace Spludlow.MameAO
{
	internal class Program
	{
		static int Main(string[] args)
		{
			foreach (string arg in args)
			{
				int index = arg.IndexOf('=');
				if (index == -1)
					throw new ApplicationException("Bad argument, expecting KEY=VALUE, " + arg);

				Globals.Arguments.Add(arg.Substring(0, index).ToUpper(), arg.Substring(index + 1));
			}

			if (Globals.Arguments.ContainsKey("DIRECTORY") == false)
				Globals.Arguments.Add("DIRECTORY", Environment.CurrentDirectory);

			MameAOProcessor proc = new MameAOProcessor();

			if (Globals.Arguments.ContainsKey("OPERATION") == true)
				return Operations.ProcessOperation(Globals.Arguments, proc);

			if (Globals.Arguments.ContainsKey("UPDATE") == true)
			{
				proc.Update(Int32.Parse(Globals.Arguments["UPDATE"]));
				return 0;
			}

			proc.RunAsync();

			return 0;
		}
	}
}
