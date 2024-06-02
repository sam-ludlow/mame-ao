using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Spludlow.MameAO
{
	public class ArtworkData
	{
		public string Version = "";
		public DataSet DataSet = null;
	}
	public enum ArtworkTypes
	{
		Artworks,
		ArtworksAlt,
		ArtworksWideScreen,
	}

	public class Artwork
	{
		public Dictionary<ArtworkTypes, string> ArchivePaths = new Dictionary<ArtworkTypes, string>()
		{
			{ ArtworkTypes.Artworks,			"/main/pS_Resources/pS_Artwork_Official.dat" },
			{ ArtworkTypes.ArtworksAlt,         "/main/pS_Resources/pS_Artwork_Unofficial_Alternate.dat" },
			{ ArtworkTypes.ArtworksWideScreen,  "/main/pS_Resources/pS_Artwork_WideScreen.dat" },
		};

		public Dictionary<ArtworkTypes, ArtworkData> ArtworkDatas = new Dictionary<ArtworkTypes, ArtworkData>();

		private readonly string MameArtworkDirectory;
		private GitHubRepo GitHubRepo;
		public Artwork()
		{
			MameArtworkDirectory = Path.Combine(Globals.MameDirectory, "artwork");
			GitHubRepo = Globals.GitHubRepos["MAME_Dats"];
		}

		public void Initialize(ArtworkTypes artworkType)
		{
			if (ArtworkDatas.ContainsKey(artworkType) == true)
				return;

			string url = $"{GitHubRepo.UrlRaw}{ArchivePaths[artworkType]}";

			Tools.ConsoleHeading(2, new string[] {
				$"Artwork Initialize: {artworkType}",
				url
			});

			ArtworkData artworkData = new ArtworkData();
			artworkData.Version = "";
			artworkData.DataSet = null;
			ArtworkDatas.Add(artworkType, artworkData);

			string xml = GitHubRepo.Fetch(url);

			if (xml == null)
				return;

			artworkData.DataSet = ParseXML(xml);
			artworkData.Version = GetDataSetVersion(artworkData.DataSet);

			artworkData.DataSet.Tables["machine"].PrimaryKey = new DataColumn[] { artworkData.DataSet.Tables["machine"].Columns["name"] };
			artworkData.DataSet.Tables["rom"].PrimaryKey = new DataColumn[] { artworkData.DataSet.Tables["rom"].Columns["machine_id"], artworkData.DataSet.Tables["rom"].Columns["name"] };

			foreach (DataRow row in artworkData.DataSet.Tables["rom"].Rows)
			{
				if (row.IsNull("sha1") == false)
					Globals.Database._AllSHA1s.Add((string)row["sha1"]);
			}

			Console.WriteLine($"Version:\t{artworkData.Version}");
		}

		private DataSet ParseXML(string xml)
		{
			DataSet dataSet = new DataSet();
			XElement document = XElement.Parse(xml);
			ReadXML.ImportXMLWork(document, dataSet, null, null);
			return dataSet;
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
			List<string> machineNames = new List<string>();
			machineNames.Add((string)machineRow["name"]);
			if (machineRow.IsNull("cloneof") == false)
				machineNames.Add((string)machineRow["cloneof"]);

			if (Globals.Settings.Options["Artwork"] == "No")
			{
				foreach (string machineName in machineNames)
				{
					string directory = Path.Combine(MameArtworkDirectory, machineName);
					if (Directory.Exists(directory) == true)
						Directory.Delete(directory, true);
				}
				return;
			}

			ArtworkTypes artworkType = (ArtworkTypes) Enum.Parse(typeof(ArtworkTypes), Globals.Settings.Options["Artwork"]);

			Initialize(artworkType);

			ArtworkData data = ArtworkDatas[artworkType];

			if (data.DataSet == null)
				return;

			Tools.ConsoleHeading(2, new string[] {
				$"Machine Artwork: {String.Join(", ", machineNames)}",
			});

			DataSet report = Reports.PlaceReportTemplate($"machines:{String.Join(", ", machineNames)}");

			//
			// Require
			//

			foreach (string machineName in machineNames)
			{
				DataRow machineArtworkRow = data.DataSet.Tables["machine"].Select($"name = '{machineName}'").SingleOrDefault();

				if (machineArtworkRow == null)
					continue;

				long machine_id = (long)machineArtworkRow["machine_id"];

				bool downloadRequired = false;

				List<DataRow> artworkRows = new List<DataRow>();
				foreach (DataRow artworkRow in data.DataSet.Tables["rom"].Select($"machine_id = {machine_id}"))
				{
					if (artworkRow.IsNull("name") || artworkRow.IsNull("sha1"))
						continue;

					artworkRow["name"] = Path.GetFileName((string)artworkRow["name"]);

					artworkRows.Add(artworkRow);

					string sha1 = (string)artworkRow["sha1"];
					string name = (string)artworkRow["name"];
					bool required = !Globals.RomHashStore.Exists(sha1);

					if (required == true)
						downloadRequired = true;

					report.Tables["Require"].Rows.Add(sha1, required, name);
					Console.WriteLine($"{sha1}\t{required}\t{name}");
				}

				if (artworkRows.Count == 0)
					continue;

				if (downloadRequired == true)
				{
					ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.Support][0];

					string key = $"{artworkType}/{artworkType}";
					ArchiveOrgFile file = item.GetFile(key);
					if (file == null)
					{
						Console.WriteLine($"!!! Artwork file not on archive.org: {key}");
						continue;
					}

					string url = $"{item.DownloadLink(file)}/{machineName}.zip";

					using (TempDirectory tempDir = new TempDirectory())
					{
						string zipFilename = Path.Combine(tempDir.Path, machineName + ".zip");

						report.Tables["Download"].Rows.Add(url);

						Console.Write($"Downloading {url}...");
						Tools.Download(url, zipFilename, Globals.DownloadDotSize, 10);
						Console.WriteLine("...done");

						ZipFile.ExtractToDirectory(zipFilename, tempDir.Path);
						Tools.ClearAttributes(tempDir.Path);

						foreach (string filename in Directory.GetFiles(tempDir.Path, "*", SearchOption.AllDirectories))
						{
							string subPathName = filename.Substring(tempDir.Path.Length);
							string sha1 = Globals.RomHashStore.Hash(filename);
							bool required = Globals.Database._AllSHA1s.Contains(sha1);
							bool imported = false;

							if (required == true)
								imported = Globals.RomHashStore.Add(filename, false, sha1);

							report.Tables["Import"].Rows.Add(sha1, imported, required, subPathName);
							Console.WriteLine($"{sha1}\t{imported}\t{required}\t{subPathName}");
						}
					}
				}

				//
				// Place
				//

				string targetDirectory = Path.Combine(MameArtworkDirectory, machineName);

				List<string[]> targetStoreFilenames = new List<string[]>();

				foreach (DataRow row in artworkRows)
				{
					string name = (string)row["name"];
					string sha1 = (string)row["sha1"];

					string targetFilename = Path.Combine(targetDirectory, name);

					bool fileExists = File.Exists(targetFilename);
					bool storeExists = Globals.RomHashStore.Exists(sha1);
					bool place = fileExists == false && storeExists == true;

					if (place == true)
						targetStoreFilenames.Add(new string[] { targetFilename, Globals.RomHashStore.Filename(sha1) });

					report.Tables["Place"].Rows.Add(sha1, place, storeExists, name);
					Console.WriteLine($"{sha1}\t{place}\t{storeExists}\t{name}");
				}

				Tools.PlaceFiles(targetStoreFilenames.ToArray());

				if (Globals.Settings.Options["PlaceReport"] == "Yes")
					Globals.Reports.SaveHtmlReport(report, "Place - Machine Artwork - " + report.Tables["Info"].Rows[0]["heading"]);
			}
		}
	}
}
