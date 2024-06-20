using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Compression;
using System.IO;

namespace Spludlow.MameAO
{
	public class Import
	{
		public static void ImportDirectory(string importDirectory)
		{
			if (Directory.Exists(importDirectory) == false)
				throw new ApplicationException($"Import directory does not exist: {importDirectory}");

			Tools.ConsoleHeading(1, new string[] {
				"Import from Directory",
				importDirectory,
			});

			DataTable reportTable = Tools.MakeDataTable(
				"Filename	Type	SHA1	Action",
				"String		String	String	String"
			);

			ImportDirectory(importDirectory, Globals.Database._AllSHA1s, reportTable);

			Globals.Reports.SaveHtmlReport(reportTable, "Import Directory");
		}
		public static void ImportDirectory(string importDirectory, HashSet<string> allSHA1s, DataTable reportTable)
		{
			foreach (string filename in Directory.GetFiles(importDirectory, "*", SearchOption.AllDirectories))
			{
				Console.WriteLine(filename);

				string name = filename.Substring(importDirectory.Length + 1);

				string extention = Path.GetExtension(filename).ToLower();

				string sha1;
				string status;

				switch (extention)
				{
					case ".zip":
						sha1 = "";
						status = "";

						using (TempDirectory tempDir = new TempDirectory())
						{
							ZipFile.ExtractToDirectory(filename, tempDir.Path);

							Tools.ClearAttributes(tempDir.Path);

							reportTable.Rows.Add(name, "ARCHIVE", sha1, status);

							ImportDirectory(tempDir.Path, allSHA1s, reportTable);
						}
						break;

					case ".chd":
						sha1 = Globals.DiskHashStore.Hash(filename);
						if (allSHA1s.Contains(sha1) == true)
							status = Globals.DiskHashStore.Add(filename, false, sha1) ? "" : "Have";
						else
							status = "Unknown";

						reportTable.Rows.Add(name, "DISK", sha1, status);
						break;

					default:
						sha1 = Globals.RomHashStore.Hash(filename);
						if (allSHA1s.Contains(sha1) == true)
							status = Globals.RomHashStore.Add(filename, false, sha1) ? "" : "Have";
						else
							status = "Unknown";

						reportTable.Rows.Add(name, "ROM", sha1, status);
						break;
				}
			}
		}
	}
}
