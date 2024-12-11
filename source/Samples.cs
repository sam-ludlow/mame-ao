using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace mame_ao.source
{
	public class Samples
	{
		public string Version = "";
		public DataSet DataSet = null;

		private readonly string MameSamplesDirectory;

		public Samples()
		{
			MameSamplesDirectory = Path.Combine(Globals.MameDirectory, "samples");
		}

		public void Initialize()
		{
			if (DataSet != null)
				return;

			DataSet = new DataSet();

			GitHubRepo repo = Globals.GitHubRepos["MAME_Dats"];

			string url = repo.UrlRaw + "/main/MAME_dat/MAME_Samples.dat";

			Tools.ConsoleHeading(2, new string[] {
				$"Samples Initialize",
				url
			});

			string xml = repo.Fetch(url);

			if (xml == null)
				return;

			XElement document = XElement.Parse(xml);
			ReadXML.ImportXMLWork(document, DataSet, null, null);

			Version = GetDataSetVersion(DataSet);

			DataSet.Tables["machine"].PrimaryKey = new DataColumn[] { DataSet.Tables["machine"].Columns["name"] };
			DataSet.Tables["rom"].PrimaryKey = new DataColumn[] { DataSet.Tables["rom"].Columns["machine_id"], DataSet.Tables["rom"].Columns["name"] };

			foreach (DataRow row in DataSet.Tables["rom"].Rows)
			{
				if (row.IsNull("sha1") == false)
					Globals.Database._AllSHA1s.Add((string)row["sha1"]);
			}

			Console.WriteLine($"Version:\t{Version}");
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
			Initialize();

			if (DataSet.Tables.Count == 0)
				return;

			if (machineRow.IsNull("sampleof") == true)
				return;

			string[] sampleNames = Globals.Database.GetMachineSamples(machineRow).Select(row => (string)row["name"]).ToArray();

			if (sampleNames.Length == 0)
				return;

			string machineName = (string)machineRow["name"];
			string machineSampleOf = (string)machineRow["sampleof"];

			DataRow sampleMachineRow = DataSet.Tables["machine"].Rows.Find(machineSampleOf);
			if (sampleMachineRow == null)
			{
				Console.WriteLine($"!!! Sample machine not found: {machineSampleOf}");
				return;
			}

			long machine_id = (long)sampleMachineRow["machine_id"];

			List<DataRow> sampleRoms = new List<DataRow>();
			foreach (string sampleName in sampleNames)
			{
				DataRow sampleRom = DataSet.Tables["rom"].Rows.Find(new object[] { machine_id, sampleName + ".wav" });

				if (sampleRom == null || sampleRom.IsNull("name") || sampleRom.IsNull("sha1"))
					continue;

				sampleRoms.Add(sampleRom);
			}

			if (sampleRoms.Count == 0)
				return;

			string[] info = new string[] { "samples", machineName, machineSampleOf };

			if (Place.AssetsRequired(Globals.RomHashStore, sampleRoms.ToArray(), info) == true)
			{
				ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.Support][0];

				string key = $"Samples/{machineSampleOf}";
				ArchiveOrgFile file = item.GetFile(key);
				if (file == null)
				{
					Console.WriteLine($"!!! Sample file not on archive.org: {key}");
					return;
				}

				string url = item.DownloadLink(file);

				Place.DownloadImportFiles(url, file.size, info);
			}

			string targetDirectory = Path.Combine(MameSamplesDirectory, machineSampleOf);

			Place.PlaceAssetFiles(sampleRoms.ToArray(), Globals.RomHashStore, targetDirectory, null, info);
		}
	}
}
