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
			MameAOProcessor proc = new MameAOProcessor();
			proc.Run("mrdo");
		}
	}
}
