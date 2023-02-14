using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Spludlow
{
	public class HashStore
	{
		private string _StoreDirectory;
		private HashSet<string> _HashSet;

		private HashMethod _HashMethod;

		private object _Lock = new object();

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
	}
}
