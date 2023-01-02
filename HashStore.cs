using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
			this._StoreDirectory = storeDirectory;

			this._HashMethod = hashMethod;

			if (Directory.Exists(this._StoreDirectory) == false)
				Directory.CreateDirectory(this._StoreDirectory);

			this.Refresh();
		}


		public int Length
		{
			get
			{
				lock (this._Lock)
				{
					return this._HashSet.Count;
				}
			}
		}

		public void Refresh()
		{
			lock (this._Lock)
			{
				this._HashSet = new HashSet<string>();

				foreach (string filename in Directory.GetFiles(this._StoreDirectory, "*", SearchOption.AllDirectories))
					this._HashSet.Add(Path.GetFileName(filename));
			}
		}

		public string Hash(string filename)
		{
			return this._HashMethod(filename);
		}

		public bool Add(string filename)
		{
			return this.Add(filename, false);
		}
		public bool Add(string filename, bool move)
		{
			return Add(filename, move, null);
		}
		public bool Add(string filename, bool move, string sha1)
		{
			//	May already have
			if (sha1 == null)
				sha1 = this._HashMethod(filename);

			bool adding = false;

			lock (this._Lock)
			{
				if (this._HashSet.Contains(sha1) == false)
				{
					adding = true;
					this._HashSet.Add(sha1);
				}
			}

			if (adding == true)
			{
				string storeFilename = this.StoreFilename(sha1, true);
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

			lock (this._Lock)
			{
				if (this._HashSet.Contains(sha1) == true)
				{
					deleting = true;
					this._HashSet.Remove(sha1);

					string storeFilename = this.StoreFilename(sha1, false);
					File.Delete(storeFilename);
				}
			}

			return deleting;
		}

		public bool Exists(string sha1)
		{
			lock (this._Lock)
			{
				return this._HashSet.Contains(sha1);
			}
		}

		public string Filename(string sha1)
		{
			if (this.Exists(sha1) == false)
				return null;

			return this.StoreFilename(sha1, false);
		}

		public string[] Hashes()
		{
			lock (this._Lock)
			{
				return this._HashSet.ToArray();
			}
		}

		public string[] FileNames()
		{
			string[] hashes = this.Hashes();
			string[] fileNames = new string[hashes.Length];

			for (int index = 0; index < hashes.Length; ++index)
				fileNames[index] = this.StoreFilename(hashes[index], false);

			return fileNames;
		}

		private string StoreFilename(string sha1, bool writeMode)
		{
			//if (sha1.Length != 40)
			//	throw new ApplicationException("Bad sha1: " + sha1);

			string filename = GetFileName(this._StoreDirectory, sha1);

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
