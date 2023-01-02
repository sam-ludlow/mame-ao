using Spludlow.MameAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mame_ao
{
	internal class Program
	{
		static void Main(string[] args)
		{
			if (args.Length != 1)
			{
				Console.WriteLine("usage: mame-ao.exe <mame machine name>");
				return;
			}
			MameAOProcessor proc = new MameAOProcessor();
			proc.Run(args[0]);
		}
	}
}
