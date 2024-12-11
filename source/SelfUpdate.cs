using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace mame_ao.source
{
	public class SelfUpdate
	{
		public static async Task UpdateAsync(int startingPid)
		{
			string updateDirectory = Path.Combine(Globals.TempDirectory, "UPDATE");

			if (startingPid <= 0)
			{
				GitHubRepo repo = Globals.GitHubRepos["mame-ao"];

				if (repo.tag_name == Globals.AssemblyVersion)
				{
					Console.WriteLine($"MAME-AO is already up to date '{Globals.AssemblyVersion}'.");
					if (startingPid == 0)
						return;
				}

				Console.WriteLine($"Updating MAME-AO '{Globals.AssemblyVersion}' => '{repo.tag_name}'...");

				string archiveUrl = repo.Assets[repo.Assets.First().Key];
				string archiveFilename = Path.Combine(Globals.RootDirectory, $"mame-ao-{repo.tag_name}.zip");

				await Tools.DownloadAsync(archiveUrl, archiveFilename);

				if (Directory.Exists(updateDirectory) == true)
				{
					try
					{
						Directory.Delete(updateDirectory, true);
					}
					catch (UnauthorizedAccessException e)
					{
						throw new ApplicationException("Looks like an update is currently running, please kill all mame-ao processes and try again, " + e.Message, e);
					}
				}

				ZipFile.ExtractToDirectory(archiveFilename, updateDirectory);

				int pid = Process.GetCurrentProcess().Id;

				ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(updateDirectory, "mame-ao.exe"))
				{
					WorkingDirectory = Globals.RootDirectory,
					Arguments = $"UPDATE={pid} DIRECTORY=\"{Globals.RootDirectory}\"",
					UseShellExecute = true,
				};

				Console.Write("Starting update process...");
				using (Process process = new Process())
				{
					process.StartInfo = startInfo;
					process.Start();

					if (process.HasExited == true)
						throw new ApplicationException($"Update process exited imediatly after starting {process.ExitCode}.");
				}
				Console.WriteLine("...done");

				Console.WriteLine("Waiting to be killed...");
				Thread.Sleep(Timeout.Infinite);
			}
			else
			{
				try
				{
					UpdateChild(updateDirectory, startingPid);
				}
				catch (Exception e)
				{
					Tools.ReportError(e, "UPDATE FATAL ERROR", true);
				}
			}
		}

		public static void UpdateChild(string updateDirectory, int startingPid)
		{
			Console.WriteLine($"MAME-AO UPDATER {Globals.AssemblyVersion}");
			Console.WriteLine($"Target Directory: {Globals.RootDirectory}, Update From Directory {updateDirectory}.");

			Console.WriteLine("Killing starting process...");
			using (Process startingProcess = Process.GetProcessById(startingPid))
			{
				startingProcess.Kill();
				startingProcess.WaitForExit();
			}
			Console.WriteLine("...done");

			try
			{
				File.Delete(Path.Combine(Globals.RootDirectory, "mame-ao.exe"));
			}
			catch (UnauthorizedAccessException e)
			{
				throw new ApplicationException("Looks like the starting process is currently running, please kill all mame-ao processes and try again, " + e.Message, e);
			}

			foreach (string sourceFilename in Directory.GetFiles(updateDirectory))
			{
				string targetFilename = Path.Combine(Globals.RootDirectory, Path.GetFileName(sourceFilename));

				File.Copy(sourceFilename, targetFilename, true);

				Console.WriteLine(targetFilename);
			}

			ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(Globals.RootDirectory, "mame-ao.exe"))
			{
				WorkingDirectory = Globals.RootDirectory,
				UseShellExecute = true,
			};

			Console.Write("Starting updated mame-ao process...");
			using (Process process = new Process())
			{
				process.StartInfo = startInfo;
				process.Start();

				if (process.HasExited == true)
					throw new ApplicationException($"New mame-ao process exited imediatly after starting {process.ExitCode}.");
			}
			Console.WriteLine("...done");

			Console.WriteLine("Exiting this process...");
			Environment.Exit(0);
		}
	}
}
