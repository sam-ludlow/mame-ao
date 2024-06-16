using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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
		private readonly GitHubRepo GitHubRepo;
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

			ArtworkData artworkData = new ArtworkData
			{
				Version = "",
				DataSet = null
			};
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

		public void PlaceAssets(DataRow machineRow)
		{
			List<string> machineNames = new List<string>(new string[] { (string)machineRow["name"] });
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

			foreach (string machineName in machineNames)
			{
				DataRow machineArtworkRow = data.DataSet.Tables["machine"].Select($"name = '{machineName}'").SingleOrDefault();

				if (machineArtworkRow == null)
					continue;

				string[] info = new string[] { "artwork", machineName, "" };

				long machine_id = (long)machineArtworkRow["machine_id"];

				DataRow[] assetRows = data.DataSet.Tables["rom"].Select($"machine_id = {machine_id} AND name IS NOT NULL AND sha1 IS NOT NULL");

				if (Place.AssetsRequired(Globals.RomHashStore, assetRows, info) == true)
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

					Dictionary<string, long> softwareSizes = item.GetZipContentsSizes(file, 0, 4);

					Place.DownloadImportFiles(url, softwareSizes[machineName], info);
				}

				string targetDirectory = Path.Combine(MameArtworkDirectory, machineName);

				Place.PlaceAssetFiles(assetRows, Globals.RomHashStore, targetDirectory, null, info);
			}
		}
	}
}
