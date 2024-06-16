using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Spludlow.MameAO
{
	public class Mame
	{
		public static void RunSelfExtract(string filename)
		{
			string directory = Path.GetDirectoryName(filename);

			ProcessStartInfo startInfo = new ProcessStartInfo(filename)
			{
				WorkingDirectory = directory,
				Arguments = "-y",
			};

			using (Process process = new Process())
			{
				process.StartInfo = startInfo;

				process.Start();
				process.WaitForExit();

				if (process.ExitCode != 0)
					throw new ApplicationException("Bad exit code");
			}
		}

		public static void ExtractXML(string binFilename, string outputFilename, string arguments)
		{
			string directory = Path.GetDirectoryName(binFilename);

			using (StreamWriter writer = new StreamWriter(outputFilename, false, Encoding.UTF8))
			{
				ProcessStartInfo startInfo = new ProcessStartInfo(binFilename)
				{
					WorkingDirectory = directory,
					Arguments = arguments,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					StandardOutputEncoding = Encoding.UTF8,
				};

				using (Process process = new Process())
				{
					process.StartInfo = startInfo;

					process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
					{
						if (e.Data != null)
							writer.WriteLine(e.Data);
					});

					process.Start();
					process.BeginOutputReadLine();
					process.WaitForExit();

					if (process.ExitCode != 0)
						throw new ApplicationException("ExtractXML Bad exit code");
				}
			}
		}
		public static void RunMame(string binFilename, string arguments)
		{
			Tools.ConsoleHeading(1, new string[] {
				"Starting MAME",
				binFilename,
				arguments,
			});
			Console.WriteLine();

			string directory = Path.GetDirectoryName(binFilename);

			ProcessStartInfo startInfo = new ProcessStartInfo(binFilename)
			{
				WorkingDirectory = directory,
				Arguments = arguments,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				StandardOutputEncoding = Encoding.UTF8,
			};

			using (Process process = new Process())
			{
				process.StartInfo = startInfo;

				process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
				{
					if (e.Data != null)
						Console.WriteLine($"MAME output:{e.Data}");
				});

				process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
				{
					if (e.Data != null)
						Console.WriteLine($"MAME error:{e.Data}");
				});

				process.Start();
				process.BeginOutputReadLine();
				process.WaitForExit();

				Console.WriteLine();
				if (process.ExitCode == 0)
					Console.WriteLine("MAME Shell Exit OK.");
				else
					Console.WriteLine($"MAME Shell Exit BAD: {process.ExitCode}");
			}

			Console.WriteLine();
		}

		public static string LatestLocal()
		{
			List<string> versions = new List<string>();

			foreach (string versionDirectory in Directory.GetDirectories(Globals.RootDirectory))
			{
				string version = Path.GetFileName(versionDirectory);
				string exeFilename = Path.Combine(versionDirectory, "mame.exe");

				if (File.Exists(exeFilename) == true)
					versions.Add(version);
			}

			if (versions.Count == 0)
				return null;

			versions.Sort();

			return versions[versions.Count - 1];
		}

		public static DataTable ListSavedState(string rootDirectory, Database database)
		{
			DataTable table = new DataTable();
			table.Columns.Add("version", typeof(string));
			table.Columns.Add("binary_time", typeof(DateTime));
			table.Columns.Add("sta_name", typeof(string));
			table.Columns.Add("sta_time", typeof(DateTime));
			table.Columns.Add("sta_machine", typeof(string));
			table.Columns.Add("sta_description", typeof(string));


			foreach (string mameDirectory in Directory.GetDirectories(rootDirectory))
			{
				string version = Path.GetFileName(mameDirectory);

				if (version.StartsWith("_") == true)
					continue;

				string mameBin = Path.Combine(mameDirectory, "mame.exe");

				if (File.Exists(mameBin) == false)
					continue;

				DateTime lastWriteTime = File.GetLastWriteTime(mameBin);

				string staDirectory = Path.Combine(mameDirectory, "sta");

				bool found = false;

				if (Directory.Exists(staDirectory) == true)
				{
                    foreach (string staMachineDirectory in Directory.GetDirectories(staDirectory))
					{
						string staMachine = Path.GetFileName(staMachineDirectory);

						foreach (string staFilename in Directory.GetFiles(staMachineDirectory, "*.sta"))
						{
							found = true;

							string staName = Path.GetFileNameWithoutExtension(staFilename).ToUpper();

							DateTime staLastWriteTime = File.GetLastWriteTime(staFilename);

							string description = "";
							DataRow machineRow = database.GetMachine(staMachine);
							if (machineRow != null)
								description = (string)machineRow["description"];

							table.Rows.Add(version, lastWriteTime, staName, staLastWriteTime, staMachine, description);
						}
                    }
                }

				if (found == false)
					table.Rows.Add(version, lastWriteTime);
			}

			DataView view = new DataView(table)
			{
				Sort = "version, sta_time"
			};

			DataTable sortTable = table.Clone();
			foreach (DataRowView rowView in view)
				sortTable.ImportRow(rowView.Row);

			return sortTable;

		}

		public static string WhatsNew(string rootDirectory)
		{
			string name = "whatsnew.txt";

			List<string> versions = new List<string>();

			foreach (string mameDirectory in Directory.GetDirectories(rootDirectory))
			{
				string version = Path.GetFileName(mameDirectory);

				if (version.StartsWith("_") == true)
					continue;

				string txtFilename = Path.Combine(mameDirectory, name);

				if (File.Exists(txtFilename) == true)
					versions.Add(version);
			}

			versions.Sort();

			if (versions.Count == 0)
				throw new ApplicationException("Can't find any whatsnew.txt files.");

			string filename = Path.Combine(rootDirectory, versions[versions.Count - 1], name);

			return File.ReadAllText(filename, Encoding.UTF8);
		}

		public static void CollectSnaps(string rootDirectory, string targetDirectory, Reports reports)
		{
			DataTable table = Tools.MakeDataTable(
				"Version	Machine	LastWriteTime	SourceFilename	TargetFilename",
				"String		String	DateTime		String			String");

			foreach (string versionDirectory in Directory.GetDirectories(rootDirectory))
			{
				string version = Path.GetFileName(versionDirectory);

				if (version.StartsWith("_") == true)
					continue;

				string mameBin = Path.Combine(versionDirectory, "mame.exe");

				if (File.Exists(mameBin) == false)
					continue;

				string snapDirectory = Path.Combine(versionDirectory, "snap");

				if (Directory.Exists(snapDirectory) == false)
					continue;

				foreach (string machineDirectory in Directory.GetDirectories(snapDirectory))
				{
					string machineName = Path.GetFileName(machineDirectory);

					foreach (string snapFilename in Directory.GetFiles(machineDirectory, "*.png"))
					{
						DateTime lastWriteTime = File.GetLastWriteTime(snapFilename);

						string stamp = lastWriteTime.ToString("s").Replace(":", "-");

						string name = Path.GetFileNameWithoutExtension(snapFilename);

						string targetFilename = Path.Combine(targetDirectory, $"{machineName}.{version}.{stamp}.{name}.png");

						File.Move(snapFilename, targetFilename);

						table.Rows.Add(version, machineName, lastWriteTime, snapFilename, targetFilename);
					}
				}
			}

			if (table.Rows.Count > 0)
				reports.SaveHtmlReport(table, $"Collect Snaps ({table.Rows.Count})");

			Console.WriteLine($"Collected {table.Rows.Count} Snaps.");
		}


	}
}


