using System;

namespace Spludlow.MameAO
{
	internal class Program
	{
		static void Main(string[] args)
		{
			string directory = Environment.CurrentDirectory;
			if (args.Length > 0)
				directory = args[0];

			MameAOProcessor proc = new MameAOProcessor(directory);

			proc.Start();

			proc.Run();
		}
	}
}
