using System;
using System.IO;

namespace Spludlow.MameAO
{
	public class HbMame
	{
		public string DirectoryRoot;
		public string DirectoryExe;
		public string FilenameExe;
		public string Version = null;

		public HbMame(string directory)
		{
			DirectoryRoot = directory;
			Directory.CreateDirectory(directory);
		}

		public void Initialize()
		{
			bool newVersion = Operations.GetHbMame(DirectoryRoot, "0") == 1;

			string version = Operations.HbMameGetLatestDownloadedVersion(DirectoryRoot);

			DirectoryExe = Path.Combine(DirectoryRoot, version);
			FilenameExe = Path.Combine(DirectoryExe, "hbmame.exe");

			if (File.Exists(FilenameExe) == false)
				throw new ApplicationException($"EXE not found: {FilenameExe}");

			Operations.MakeHbMameXML(DirectoryRoot, version);

			Operations.MakeHbMameSQLite(DirectoryRoot, version);

			Console.WriteLine($"newVersion\t{newVersion}");
			Console.WriteLine($"version\t{version}");

			Version = version;
		}

	}
}
