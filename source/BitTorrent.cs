using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Web;

using Newtonsoft.Json;

namespace Spludlow.MameAO
{
	public class BitTorrentFile
	{
		public string Filename;
		public long Length;

		public BitTorrentFile(string filename, long length)
		{
			Filename = filename;
			Length = length;
		}
	}

	public class BitTorrent
	{
		public static string ClientUrl = "http://localhost:12381";

		public static dynamic DomeInfo()
		{
			try
			{
				string info = Tools.Query($"{ClientUrl}/api/info");
				return JsonConvert.DeserializeObject<dynamic>(info);
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		public static bool IsInstalled()
		{
			string exeFilename = Path.Combine(Globals.BitTorrentDirectory, "dome-bt.exe");

			return File.Exists(exeFilename);
		}

		public static void Remove()
		{
			dynamic info = DomeInfo();

			if (info != null)
			{
				int pid = (int)info.pid;

				Console.Write("Killing DOME-BT...");
				using (Process startingProcess = Process.GetProcessById(pid))
				{
					startingProcess.Kill();
					startingProcess.WaitForExit();
				}
				Console.WriteLine("...done");
			}

			Console.Write("Removing DOME-BT...");
			try
			{
				Directory.Delete(Globals.BitTorrentDirectory, true);
			}
			catch (UnauthorizedAccessException e)
			{
				throw new ApplicationException("Looks like DOME-BT is currently running, please kill all DOME-BT processes and try again, " + e.Message, e);
			}

			Directory.CreateDirectory(Globals.BitTorrentDirectory);
			Console.WriteLine("...done");

			Globals.BitTorrentAvailable = false;
		}

		public static void Initialize()
		{
			Tools.ConsoleHeading(2, "DOME-BT (Pleasuredome Bit Torrents)");

			GitHubRepo repo = Globals.GitHubRepos["dome-bt"];

			string zipName = $"dome-bt-{repo.tag_name}.zip";

			string exeFilename = Path.Combine(Globals.BitTorrentDirectory, "dome-bt.exe");

			string remoteVersion = repo.tag_name;

			string localVersion = null;
			if (File.Exists(exeFilename) == true)
			{
				Version version = AssemblyName.GetAssemblyName(exeFilename).Version;
				localVersion = $"{version.Major}.{version.Minor}";

				Console.WriteLine($"DOME-BT Installed version:	{localVersion}");
			}

			dynamic info = DomeInfo();

			if (info != null && (string)info.version == remoteVersion)
			{
				Console.WriteLine($"DOME-BT Already running {info.version}");
			}
			else
			{
				if (info != null)
				{
					int pid = (int)info.pid;

					Console.Write("Killing DOME-BT...");
					using (Process startingProcess = Process.GetProcessById(pid))
					{
						startingProcess.Kill();
						startingProcess.WaitForExit();
					}
					Console.WriteLine("...done");
				}

				if (localVersion == null || localVersion != remoteVersion)
				{
					Console.Write("Installing DOME-BT...");

					try
					{
						Directory.Delete(Globals.BitTorrentDirectory, true);
					}
					catch (UnauthorizedAccessException e)
					{
						throw new ApplicationException("Looks like DOME-BT is currently running, please kill all DOME-BT processes and try again, " + e.Message, e);
					}

					Directory.CreateDirectory(Globals.BitTorrentDirectory);

					string zipFilename = Path.Combine(Globals.BitTorrentDirectory, zipName);

					Tools.Download(repo.Assets[zipName], zipFilename);

					ZipFile.ExtractToDirectory(zipFilename, Globals.BitTorrentDirectory);
					Console.WriteLine("...done");
				}

				Console.Write("Starting DOME-BT...");
				ProcessStartInfo startInfo = new ProcessStartInfo(exeFilename)
				{
					WorkingDirectory = Globals.BitTorrentDirectory,
					UseShellExecute = true,
				};
				using (Process process = new Process())
				{
					process.StartInfo = startInfo;
					process.Start();

					if (process.HasExited == true)
						throw new ApplicationException($"DOME-BT exited imediatly after starting {process.ExitCode}.");
				}
				Console.WriteLine("...done");
			}

			Globals.BitTorrentAvailable = true;
		}

		public static BitTorrentFile MachineRom(string machine)
		{
			return Download($"{ClientUrl}/api/file?machine={machine}");
		}

		public static BitTorrentFile MachineDisk(string machine, string disk)
		{
			return Download($"{ClientUrl}/api/file?machine={machine}&disk={HttpUtility.UrlEncode(disk)}");
		}

		public static BitTorrentFile SoftwareRom(string list, string software)
		{
			return Download($"{ClientUrl}/api/file?list={list}&software={software}");
		}

		public static BitTorrentFile SoftwareDisk(string list, string software, string disk)
		{
			return Download($"{ClientUrl}/api/file?list={list}&software={software}&disk={HttpUtility.UrlEncode(disk)}");
		}

		public static BitTorrentFile Download(string apiUrl)
		{
			lock (Globals.WorkerTaskInfo)
				Globals.WorkerTaskInfo.BytesTotal = 100;

			while (true)
			{
				dynamic fileInfo;

				try
				{
					fileInfo = JsonConvert.DeserializeObject<dynamic>(Tools.Query(apiUrl));
				}
				catch (HttpRequestException e)
				{
					if (e.Message.Contains("404") == true)
						return null;
					else
						throw e;
				}

				//long expectedSize = (long)fileInfo.length;	do the maths

				float percent_complete = (float)fileInfo.percent_complete;

				lock (Globals.WorkerTaskInfo)
					Globals.WorkerTaskInfo.BytesCurrent = (long)percent_complete;

				Console.WriteLine($"Torrent:\t{DateTime.Now}\t{(long)fileInfo.length}\t{percent_complete}\t{apiUrl}");

				if (percent_complete == 100.0f)
					return new BitTorrentFile((string)fileInfo.filename, (long)fileInfo.length);

				Thread.Sleep(5000);
			}
		}
	}
}
