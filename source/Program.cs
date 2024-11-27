using Spectre.Console;
using System;

namespace mame_ao.source
{
    public static class Program
	{
		public static int Main(string[] args)
        {
            HellowWorld();
            foreach (string arg in args)
            {
                int index = arg.IndexOf('=');
                if (index == -1)
                    throw new ApplicationException("Bad argument, expecting KEY=VALUE, " + arg);

                Globals.Arguments.Add(arg.Substring(0, index).ToUpper(), arg.Substring(index + 1));
            }

            if (Globals.Arguments.ContainsKey("DIRECTORY") == false)
                Globals.Arguments.Add("DIRECTORY", Environment.CurrentDirectory);

            //MameAOProcessor proc = new();

            if (Globals.Arguments.ContainsKey("OPERATION") == true)
                return Operations.ProcessOperation(Globals.Arguments);

            if (Globals.Arguments.ContainsKey("UPDATE") == true)
            {
                SelfUpdate.Update(Int32.Parse(Globals.Arguments["UPDATE"]));
                return 0;
            }

            //var proc = ;
            //Application.Init();
            //Application.Run(new MameAOProcessor());
            //Application.Run(new MyView());
            //proc.RunAsync().RunSynchronously();
            return 0;
        }

        public static void HellowWorld()
        {
            //AnsiConsoleSettings settings = new AnsiConsoleSettings();
            //System.IO.TextWriter t = settings.Out.Writer;
            AnsiConsole.Markup("[underline red]Hello[/] World!");
            //return t;
        }
    }
}
