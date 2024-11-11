using System;

namespace mame_ao.source
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
				return Operations.ProcessOperation(Globals.Arguments);

			if (Globals.Arguments.ContainsKey("UPDATE") == true)
			{
				SelfUpdate.Update(Int32.Parse(Globals.Arguments["UPDATE"]));
				return 0;
			}

			proc.Run();

			return 0;
		}
	}
}
