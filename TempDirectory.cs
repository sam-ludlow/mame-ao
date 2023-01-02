using System;
using System.IO;

namespace Spludlow
{
	public class TempDirectory : IDisposable
	{
		private string LockFilePath;
		public string Path;

		public TempDirectory()
		{
			this.Start(null);
		}

		public TempDirectory(string rootDir)
		{
			this.Start(rootDir);
		}

		private void Start(string rootDir)
		{
			this.LockFilePath = System.IO.Path.GetTempFileName();
//			this.LockFilePath = @"\\?\" + System.IO.Path.GetTempFileName(); //	Long filename support

			this.Path = this.LockFilePath + ".dir";

			Directory.CreateDirectory(this.Path);
		}

		public void Dispose()
		{
			if (Directory.Exists(this.Path) == true)
			{
				Directory.Delete(this.Path, true);
			}

			if (this.LockFilePath != null)
				File.Delete(this.LockFilePath);
		}

		public override string ToString()
		{
			return this.Path;
		}

	}
}
