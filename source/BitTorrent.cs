using System;
using System.Net.Http;
using System.Threading;
using System.Web;

using Newtonsoft.Json;

namespace Spludlow.MameAO
{
	public class BitTorrentFile
	{
		public string Url;
		public long Length;

		public BitTorrentFile(string url, long length)
		{
			Url = url;
			Length = length;
		}
	}

	public class BitTorrent
	{
		public static string ClientUrl = "http://localhost:12381";


		public static bool IsAvailable()
		{
			try
			{
				Tools.Query($"{ClientUrl}/api/info");
				Tools.ConsoleHeading(2, "DOME-BT (Pleasure Dome Bit Torrents) Available");
				return true;
			}
			catch (HttpRequestException)
			{
				return false;
			}
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
					return new BitTorrentFile((string)fileInfo.url, (long)fileInfo.length);

				Thread.Sleep(5000);
			}
		}
	}
}
