using System;
using System.IO;
using System.Text;

namespace Spludlow.MameAO
{
	public class BadSources
	{
		private string _DataFilename;
		public BadSources()
		{
			_DataFilename = Path.Combine(Globals.RootDirectory, "_BadSources.txt");

			if (File.Exists(_DataFilename) == false)
				File.WriteAllText(_DataFilename, "", Encoding.UTF8);
		}

		public bool AlreadyDownloaded(ArchiveOrgFile sourceFileInfo)
		{
			string data = File.ReadAllText(_DataFilename, Encoding.UTF8);

			return data.IndexOf(sourceFileInfo.sha1) != -1;
		}

		public void ReportSourceFile(ArchiveOrgFile sourceFileInfo, string extectedSHA1, string actualSHA1)
		{
			string line = $"{sourceFileInfo.sha1}\t{DateTime.Now.ToString("s")}\t{sourceFileInfo.size}\t{sourceFileInfo.mtime.ToString("s")}\t{extectedSHA1}\t{actualSHA1}\t{sourceFileInfo.name}{Environment.NewLine}";

			File.AppendAllText(_DataFilename, line);
		}
	}
}
