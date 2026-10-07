using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spludlow.MameAO
{
	public class HashStore
	{
		private readonly string _StoreDirectory;
		private HashSet<string> _HashSet;

		private readonly HashMethod _HashMethod;

		private readonly object _Lock = new object();

		public delegate string HashMethod(string filename);

		public HashStore(string storeDirectory, HashMethod hashMethod)
		{
			_StoreDirectory = storeDirectory;

			_HashMethod = hashMethod;

			if (Directory.Exists(_StoreDirectory) == false)
				Directory.CreateDirectory(_StoreDirectory);

			Refresh();
		}


		public int Length
		{
			get
			{
				lock (_Lock)
				{
					return _HashSet.Count;
				}
			}
		}

		public void Refresh()
		{
			lock (_Lock)
			{
				_HashSet = new HashSet<string>();

				foreach (string filename in Directory.GetFiles(_StoreDirectory, "*", SearchOption.AllDirectories))
					_HashSet.Add(Path.GetFileName(filename));
			}
		}

		public string Hash(string filename)
		{
			return _HashMethod(filename);
		}

		public bool Add(string filename)
		{
			return Add(filename, false);
		}
		public bool Add(string filename, bool move)
		{
			return Add(filename, move, null);
		}
		public bool Add(string filename, bool move, string sha1)
		{
			if (sha1 == null)
				sha1 = _HashMethod(filename);

			bool adding = false;

			lock (_Lock)
			{
				if (_HashSet.Contains(sha1) == false)
				{
					adding = true;
					_HashSet.Add(sha1);
				}
			}

			if (adding == true)
			{
				string storeFilename = StoreFilename(sha1, true);
				if (move == false)
					File.Copy(filename, storeFilename);
				else
					File.Move(filename, storeFilename);
			}

			return adding;
		}

		public bool Delete(string sha1)
		{
			bool deleting = false;

			lock (_Lock)
			{
				if (_HashSet.Contains(sha1) == true)
				{
					deleting = true;
					_HashSet.Remove(sha1);

					string storeFilename = StoreFilename(sha1, false);
					File.Delete(storeFilename);
				}
			}

			return deleting;
		}

		public bool Exists(string sha1)
		{
			lock (_Lock)
			{
				return _HashSet.Contains(sha1);
			}
		}

		public string Filename(string sha1)
		{
			if (Exists(sha1) == false)
				return null;

			return StoreFilename(sha1, false);
		}

		public string[] Hashes()
		{
			lock (_Lock)
			{
				return _HashSet.ToArray();
			}
		}

		public string[] FileNames()
		{
			string[] hashes = Hashes();
			string[] fileNames = new string[hashes.Length];

			for (int index = 0; index < hashes.Length; ++index)
				fileNames[index] = StoreFilename(hashes[index], false);

			return fileNames;
		}

		private string StoreFilename(string sha1, bool writeMode)
		{
			string filename = GetFileName(_StoreDirectory, sha1);

			if (writeMode == true)
			{
				string directory = Path.GetDirectoryName(filename);
				if (Directory.Exists(directory) == false)
					Directory.CreateDirectory(directory);
			}

			return filename;
		}

		public static string GetFileName(string storeDirectory, string sha1)
		{
			StringBuilder path = new StringBuilder();
			path.Append(storeDirectory);

			path.Append(@"\");
			path.Append(sha1.Substring(0, 2));

			path.Append(@"\");
			path.Append(sha1);

			return path.ToString();
		}
		public static void ValidateHashStore(HashStore hashStore, string type)
		{
			int progressFrequency = type == "DISK" ? 10 : 1000;

			string[] filenames = hashStore.FileNames().ToArray();

			string title = $"Validate Hash Store: {type} {filenames.Length}";

			Tools.ConsoleHeading(1, title);

			DataTable table = Tools.MakeDataTable(
				"Filename	Problem",
				"String		String"
			);

			DateTime startTime = DateTime.Now;

			long progress = 0;

			var options = new ParallelOptions
			{
				MaxDegreeOfParallelism = 4
			};
			Parallel.ForEach(filenames, options, (filename) =>
			{
				long current = Interlocked.Increment(ref progress);
				if ((current % progressFrequency) == 0 || current == 1)
					Console.WriteLine($"{Math.Round((double)progress / (double)filenames.Length * 100.0, 1)} %\t\t{Math.Round((DateTime.Now - startTime).TotalMinutes, 1)} m");

				try
				{
					string storeHash = Path.GetFileNameWithoutExtension(filename);
					string actualHash = hashStore.Hash(filename);

					if (storeHash != actualHash)
						throw new ApplicationException($"storeHash:{storeHash}, actualHash:{actualHash}");
				}
				catch (Exception ee)
				{
					Console.WriteLine($"{filename}\t{ee.Message}");

					lock (table)
						table.Rows.Add(filename, ee.Message);
				}
			});

			Console.WriteLine($"Took Minutes: {Math.Round((DateTime.Now - startTime).TotalMinutes, 1)}");

			if (table.Rows.Count > 0)
			{
				Console.WriteLine("!!! Bad files found see report.");
				Globals.Reports.SaveHtmlReport(table, title);
			}
			else
			{
				Console.WriteLine("All files are OK.");
			}
		}

	}
}
