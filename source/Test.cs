using System;
using System.Data;
using System.Diagnostics;
using System.IO;

namespace Spludlow.MameAO
{
	public class Test
	{
		public static void Run(string profile, int count)
		{
			Database.DataQueryProfile dataQueryProfile = Globals.Database.GetDataQueryProfile(profile);

			DataTable table = Globals.Database.QueryMachine(dataQueryProfile.Key, 0, 0xfff, null);

			if (count == 0 || count > table.Rows.Count)
				count = table.Rows.Count;

			Random random = new Random();

			while (count-- > 0)
			{
				int index = random.Next(table.Rows.Count);

				DataRow machineRow = table.Rows[index];

				string machine_name = (string)machineRow["name"];
				long softwarelist_count = (long)machineRow["ao_softwarelist_count"];

				string software = "";

				if (softwarelist_count > 0)
				{
					DataRow[] softwareListRows = Globals.Database.GetMachineSoftwareLists(Globals.Database.GetMachine(machine_name));

					string softwareListName = (string)softwareListRows[random.Next(softwareListRows.Length)]["name"];

					DataRow softwareListRow = Globals.Database.GetSoftwareList(softwareListName);

					if (softwareListRow != null)
					{
						DataRow[] softwareRows = Globals.Database.GetSoftwareListsSoftware(softwareListRow);

						DataRow softwareRow = softwareRows[random.Next(softwareRows.Length)];

						software = (string)softwareRow["name"];
					}
					else
					{
						Console.WriteLine($"!!! Software list not found: {softwareListName}");
					}
				}

				table.Rows[index].Delete();
				table.AcceptChanges();

				string arguments = $"{machine_name} -verifyroms";   //	Can't verifyroms {software}

				Tools.ConsoleHeading(2, new string[] { $"START Test {profile} machine:{machine_name} software:{software}",
					arguments,
					$"Remaining tests {count}, Remaining rows {table.Rows.Count}" });

				Place.PlaceAssets(machine_name, software);

				int exitCode = RunMame(arguments);

				Tools.ConsoleHeading(2, new string[] { $"END Test {profile} {machine_name}", $"Exit Code {exitCode}" });
			}
		}

		public static int RunMame(string arguments)
		{
			string binFilename = Path.Combine(Globals.MameDirectory, "mame.exe");

			ProcessStartInfo startInfo = new ProcessStartInfo(binFilename)
			{
				WorkingDirectory = Path.GetDirectoryName(binFilename),
				Arguments = arguments,
				UseShellExecute = false,
			};

			using (Process process = new Process())
			{
				process.StartInfo = startInfo;

				process.Start();
				process.WaitForExit();

				return process.ExitCode;
			}
		}
	}
}
