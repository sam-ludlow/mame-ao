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
		public Dictionary<ArtworkTypes, string> ZipPaths = null;
		public Dictionary<ArtworkTypes, ArtworkData> ArtworkDatas = new Dictionary<ArtworkTypes, ArtworkData>();

		public Artwork()
		{
		}

		public void Initialize(ArtworkTypes artworkType)
		{
			if (ArtworkDatas.ContainsKey(artworkType) == true)
				return;

			if (ZipPaths == null)
				ZipPaths = ParseZipPaths();

			if (ZipPaths.ContainsKey(artworkType) == false)
				return;

			string zipUrl = ZipPaths[artworkType];
			string name = Tools.ValidFileName(Path.GetFileNameWithoutExtension(zipUrl));
			string cacheFilename = Path.Combine(Globals.CacheDirectory, name + ".xml");

			Tools.ConsoleHeading(2, new string[] {
				$"Artwork Initialize: {artworkType}",
				zipUrl,
				name
			});

			if (File.Exists(cacheFilename) == false)
			{
				using (TempDirectory tempDir = new TempDirectory())
				{
					string zipFilename = Path.Combine(tempDir.Path, "archive.zip");

					Tools.Download(zipUrl, zipFilename);
					ZipFile.ExtractToDirectory(zipFilename, tempDir.Path);
					Tools.ClearAttributes(tempDir.Path);

					string tempFilename = Directory.GetFiles(tempDir.Path, "*.dat").Single();

					File.Copy(tempFilename, cacheFilename);
				}
			}

			string xml = File.ReadAllText(cacheFilename);

			ArtworkData artworkData = new ArtworkData
			{
				Version = "",
				DataSet = null
			};
			ArtworkDatas.Add(artworkType, artworkData);

			artworkData.DataSet = ParseXML(xml);
			artworkData.Version = GetDataSetVersion(artworkData.DataSet);

			artworkData.DataSet.Tables["machine"].PrimaryKey = new DataColumn[] { artworkData.DataSet.Tables["machine"].Columns["name"] };
			artworkData.DataSet.Tables["rom"].PrimaryKey = new DataColumn[] { artworkData.DataSet.Tables["rom"].Columns["machine_id"], artworkData.DataSet.Tables["rom"].Columns["name"] };

			foreach (DataRow row in artworkData.DataSet.Tables["rom"].Rows)
			{
				if (row.IsNull("sha1") == false)
					Globals.AllSHA1.Add((string)row["sha1"]);
			}
		}

		private static Dictionary<ArtworkTypes, string> ParseZipPaths()
		{
			string url = "https://www.progettosnaps.net/artworks/";

			Dictionary<ArtworkTypes, string> zipPaths = new Dictionary<ArtworkTypes, string>();

			string html = Tools.FetchTextCached(url);

			if (html == null)
				return zipPaths;

			using (StringReader reader = new StringReader(html))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();

					if (line.Length == 0 || line.StartsWith("<a href=") == false || line.Contains("pS_Artwork_") == false)
						continue;

					string fullLine = line;

					try
					{
						int index;

						index = line.IndexOf('\"');
						line = line.Substring(index + 1);
						index = line.IndexOf('\"');
						line = line.Substring(0, index);
						line = new Uri(new Uri(url), line).AbsoluteUri;
						string link = line;

						index = line.LastIndexOf('/');
						line = line.Substring(index + 12);
						index = line.IndexOf('_');
						line = line.Substring(0, index);
						string name = line;

						switch (name.ToLower())
						{
							case "official":
								zipPaths.Add(ArtworkTypes.Artworks, link);
								break;
							case "unofficial":
								zipPaths.Add(ArtworkTypes.ArtworksAlt, link);
								break;
							case "widescreen":
								zipPaths.Add(ArtworkTypes.ArtworksWideScreen, link);
								break;
							default:
								break;
						}
					}
					catch (Exception e)
					{
						Console.WriteLine($"!!! Error parsing artwork html, {fullLine}, {e.Message}");
						Console.WriteLine(e.ToString());
					}
				}
			}

			return zipPaths;
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

		public void PlaceAssets(string coreDirectory, DataRow machineRow)
		{
			List<string> machineNames = new List<string>(new string[] { (string)machineRow["name"] });
			if (machineRow.IsNull("cloneof") == false)
				machineNames.Add((string)machineRow["cloneof"]);

			if (Globals.Settings.Options["Artwork"] == "No")
			{
				foreach (string machineName in machineNames)
				{
					string directory = Path.Combine(coreDirectory, "artwork", machineName);
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
						Console.WriteLine($"!!! Artwork file not on archive.org: {item.UrlDownload}, {key}");
						continue;
					}

					Dictionary<string, long> softwareSizes = item.GetZipContentsSizes(file, 0, 4);

					if (softwareSizes.ContainsKey(machineName) == false)
					{
						Console.WriteLine($"!!! Artwork machine not in ZIP on archive.org: {machineName}, {item.UrlDownload}/{file.name}/");
						continue;
					}

					string url = $"{item.DownloadLink(file)}/{machineName}.zip";

					Place.DownloadImportFiles(url, softwareSizes[machineName], info);
				}

				string targetDirectory = Path.Combine(coreDirectory, "artwork", machineName);

				Place.PlaceAssetFiles(assetRows, Globals.RomHashStore, targetDirectory, null, info);
			}
		}
	}
}
