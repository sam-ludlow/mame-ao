using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Spludlow.MameAO
{
	public class Artwork
	{
		public string Version = "";
		public DataSet DataSet = null;

		private readonly string MameArtworkDirectory;

		public Artwork()
		{
			MameArtworkDirectory = Path.Combine(Globals.MameDirectory, "artwork");
		}

		public void Initialize()
		{
			GitHubRepo repo = Globals.GitHubRepos["MAME_Dats"];

			string url = repo.UrlRaw + "/main/pS_Resources/pS_Artwork_Official.dat";

			Tools.ConsoleHeading(2, new string[] {
				$"Machine Artwork",
				url
			});

			string xml = repo.Fetch(url);

			if (xml == null)
				return;

			Version = ParseXML(xml);

			DataSet.Tables["machine"].PrimaryKey = new DataColumn[] { DataSet.Tables["machine"].Columns["name"] };
			DataSet.Tables["rom"].PrimaryKey = new DataColumn[] { DataSet.Tables["rom"].Columns["machine_id"], DataSet.Tables["rom"].Columns["name"] };

			foreach (DataRow row in DataSet.Tables["rom"].Rows)
			{
				if (row.IsNull("sha1") == false)
					Globals.Database._AllSHA1s.Add((string)row["sha1"]);
			}

			Console.WriteLine($"Version:\t{Version}");
		}

		private string ParseXML(string xml)
		{
			XElement document = XElement.Parse(xml);
			DataSet = new DataSet();
			ReadXML.ImportXMLWork(document, DataSet, null, null);

			return GetDataSetVersion(DataSet);
		}

		public string GetDataSetVersion(DataSet dataSet)
		{
			if (dataSet.Tables.Contains("header") == false)
				throw new ApplicationException("No header table");

			DataTable table = dataSet.Tables["header"];

			if (table.Rows.Count != 1)
				throw new ApplicationException("Not one header row");

			return (string)table.Rows[0]["version"];
		}

		public void Place(DataRow machineRow)
		{
			if (DataSet == null)
				return;

			//DataRow driverRow = Globals.Database.GetMachineDriver(machineRow);

			//if (driverRow == null || (string)driverRow["requiresartwork"] != "yes")
			//	return;

			string machineName = (string)machineRow["name"];

			//	TODO: child needs parents

			Tools.ConsoleHeading(2, new string[] {
				$"Machine Artwork: {machineName}",
			});

			DataRow machineArtworkRow = DataSet.Tables["machine"].Select($"name = '{machineName}'").SingleOrDefault();

			if (machineArtworkRow == null)
			{
				Console.WriteLine($"Did not find Machine Artwork: {machineName}");
				return;
			}

			long machine_id = (long)machineArtworkRow["machine_id"];

			List<DataRow> artworkRows = new List<DataRow>();

			foreach (DataRow artworkRow in DataSet.Tables["rom"].Select($"machine_id = {machine_id}"))
			{
				if (artworkRow.IsNull("name") == true || artworkRow.IsNull("sha1") == true)
					continue;

				artworkRows.Add(artworkRow);

				Console.WriteLine($"Artwork Required:\t{artworkRow["sha1"]}\t{artworkRow["name"]}");
			}

			if (artworkRows.Count == 0)
			{
				Console.WriteLine($"Did not find Machine Artwork Item Rows: {machineName}");
				return;
			}

			//
			// Import if required
			//

			bool inStore = true;
			foreach (DataRow artworkRow in artworkRows)
			{
				string sha1 = (string)artworkRow["sha1"];
				if (Globals.RomHashStore.Exists(sha1) == false)
					inStore = false;
			}

			if (inStore == false)
			{
				ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.Support][0];

				string key = "Artworks/Artworks";
				ArchiveOrgFile file = item.GetFile(key);
				if (file == null)
				{
					Console.WriteLine($"!!! Artwork file not on archive.org: {key}");
					return;
				}

				string url = $"{item.DownloadLink(file)}/{machineName}.zip";

				using (TempDirectory tempDir = new TempDirectory())
				{
					string zipFilename = Path.Combine(tempDir.Path, machineName + ".zip");
					Tools.Download(url, zipFilename, 0, 10);

					ZipFile.ExtractToDirectory(zipFilename, tempDir.Path);
					Tools.ClearAttributes(tempDir.Path);

					foreach (string filename in Directory.GetFiles(tempDir.Path))
					{
						string fileSha1 = Globals.RomHashStore.Hash(filename);
						bool required = Globals.Database._AllSHA1s.Contains(fileSha1);
						bool imported = false;

						if (required == true)
							imported = Globals.RomHashStore.Add(filename, false, fileSha1);

						Console.WriteLine($"Artwork Imported:\t{fileSha1}\t{required}\t{imported}");
					}
				}
			}

			//
			// Place
			//

			string targetDirectory = Path.Combine(MameArtworkDirectory, machineName);
			Directory.CreateDirectory(targetDirectory);

			List<string[]> targetStoreFilenames = new List<string[]>();

			foreach (DataRow row in artworkRows)
			{
				string name = (string)row["name"];
				string sha1 = (string)row["sha1"];

				string targetFilename = Path.Combine(targetDirectory, name);

				bool fileExists = File.Exists(targetFilename);
				bool storeExists = Globals.RomHashStore.Exists(sha1);

				if (fileExists == false && storeExists == true)
				{
					targetStoreFilenames.Add(new string[] { targetFilename, Globals.RomHashStore.Filename(sha1) });
				}

				Console.WriteLine($"Place Artwork:\t{sha1}\t{fileExists}\t{storeExists}\t{targetFilename}");
			}


			if (Globals.LinkingEnabled == true)
			{
				Tools.LinkFiles(targetStoreFilenames.ToArray());
			}
			else
			{
				foreach (string[] wavStoreFilename in targetStoreFilenames)
					File.Copy(wavStoreFilename[1], wavStoreFilename[0], true);
			}

		}

	}
}
