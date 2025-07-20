using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

		public static TimeSpan RestartLimit = TimeSpan.FromMinutes(5);

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
				DeleteBitTorrentDirectory(Globals.BitTorrentDirectory);
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
						DeleteBitTorrentDirectory(Globals.BitTorrentDirectory);
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

				Start();
			}

			Globals.BitTorrentAvailable = true;
		}

		public static void DeleteBitTorrentDirectory(string directory)
		{
			if (Directory.Exists(directory) == false)
				return;

			List<string> keepFilenames = new List<string>(new string[] { "_config.txt" });

			foreach (string filename in Directory.GetFiles(directory))
			{
				if (keepFilenames.Contains(Path.GetFileName(filename)) == true)
					continue;

				File.Delete(filename);
			}

			foreach (string subDirectory in Directory.GetDirectories(directory))
				Directory.Delete(subDirectory, true);
		}

		public static void Restart()
		{
			Stop();
			Start();

			Console.Write("Waiting for DOME-BT to be ready ...");

			bool ready = false;
			do {

				Thread.Sleep(5000);

				dynamic info = JsonConvert.DeserializeObject<dynamic>(Tools.Query($"{ClientUrl}/api/info"));

				Console.Write(".");

				if (info.ready_minutes != null)
					ready = true;

			} while (ready == false);

			Console.WriteLine("...done");
		}

		public static void Start()
		{
			string exeFilename = Path.Combine(Globals.BitTorrentDirectory, "dome-bt.exe");

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

		public static void Stop()
		{
			dynamic info = DomeInfo();

			if (info != null)
			{
				Console.WriteLine("Asking DOME-BT to stop");
				Tools.Query($"{ClientUrl}/api/stop");

				int pid = (int)info.pid;

				Console.Write("Waiting for DOME-BT to stop ...");
				using (Process startingProcess = Process.GetProcessById(pid))
				{
					startingProcess.WaitForExit();
				}
				Console.WriteLine("...done");
			}
		}

		public static Dictionary<ItemType, string> TorrentHashes()
		{
			Dictionary<ItemType, string> result = new Dictionary<ItemType, string>();

			dynamic info = JsonConvert.DeserializeObject<dynamic>(Tools.Query($"{ClientUrl}/api/info"));

			foreach (dynamic mangent in info.magnets)
			{
				ItemType type = (ItemType) Enum.Parse(typeof(ItemType), (string)mangent.type);
				result.Add(type, (string)mangent.hash);
			}

			return result;
		}

		public static JArray Files(string hash)
		{
			return JsonConvert.DeserializeObject<dynamic>(Tools.Query($"{ClientUrl}/api/files?hash={hash}"));
		}

		public static BitTorrentFile MachineRom(string machine)
		{
			return Download($"{ClientUrl}/api/file?machine={machine}");
		}
		public static BitTorrentFile MachineRom(string core, string machine)
		{
			return Download($"{ClientUrl}/api/file?core={core}&machine={machine}");
		}

		public static BitTorrentFile MachineDisk(string machine, string disk)
		{
			return Download($"{ClientUrl}/api/file?machine={machine}&disk={HttpUtility.UrlEncode(disk)}");
		}

		public static BitTorrentFile SoftwareRom(string list, string software)
		{
			return Download($"{ClientUrl}/api/file?list={list}&software={software}");
		}

		public static BitTorrentFile SoftwareRom(string core, string list, string software)
		{
			return Download($"{ClientUrl}/api/file?core={core}&list={list}&software={software}");
		}

		public static BitTorrentFile SoftwareDisk(string list, string software, string disk)
		{
			return Download($"{ClientUrl}/api/file?list={list}&software={software}&disk={HttpUtility.UrlEncode(disk)}");
		}

		public static BitTorrentFile Download(string apiUrl)
		{
			long expectedSize = -1;

			float percent_complete_previous = -1.0f;

			DateTime changeTime = DateTime.Now;

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

				if (expectedSize == -1)
				{
					expectedSize = (long)fileInfo.length;
					lock (Globals.WorkerTaskInfo)
						Globals.WorkerTaskInfo.BytesTotal = expectedSize;
				}

				float percent_complete = (float)fileInfo.percent_complete;

				lock (Globals.WorkerTaskInfo)
					Globals.WorkerTaskInfo.BytesCurrent = (long)(expectedSize / 100.0 * (long)percent_complete);

				TimeSpan waitSpan = DateTime.Now - changeTime;

				Console.WriteLine($"Torrent:\t{DateTime.Now}\t{(long)fileInfo.length}\t{percent_complete}\t{Math.Round(waitSpan.TotalSeconds, 0)}/{RestartLimit.TotalSeconds}\t{apiUrl}");

				if (percent_complete == 100.0f)
					return new BitTorrentFile((string)fileInfo.filename, (long)fileInfo.length);

				if (percent_complete == percent_complete_previous)
				{
					if (waitSpan > RestartLimit)
					{
						Tools.ConsoleHeading(2, new string[] {
							"DOME-BT is not downloading. Restarting it.",
							"",
							"Sometimes there aren't enough Seeders connected, a restart may help."
						});
						Restart();
						changeTime = DateTime.Now;
					}
				}
				else
				{
					changeTime = DateTime.Now;
				}
				percent_complete_previous = percent_complete;

				Thread.Sleep(5000);
			}
		}
	}
}
