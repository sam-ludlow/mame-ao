using System;
using System.IO;
using System.Text;

namespace mame_ao.source
{
	public class BadSources
	{
		private readonly string _DataFilename;
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
			string line = $"{sourceFileInfo.sha1}\t{DateTime.Now:s}\t{sourceFileInfo.size}\t{sourceFileInfo.mtime:s}\t{extectedSHA1}\t{actualSHA1}\t{sourceFileInfo.name}{Environment.NewLine}";

			File.AppendAllText(_DataFilename, line);
		}
	}
}
