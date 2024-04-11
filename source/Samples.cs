using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Xml.Linq;

namespace Spludlow.MameAO
{
	public class Samples
	{
		public string Version = "";
		public DataSet DataSet = null;

		public readonly string SourceUrl = "https://raw.githubusercontent.com/AntoPISA/MAME_Dats/main/MAME_dat/MAME_Samples.dat";

		private readonly string CacheDirectory;
		private readonly string SamplesDirectory;

		public Samples()
		{
			CacheDirectory = Path.Combine(Globals.RootDirectory, "_METADATA", "samples");
			SamplesDirectory = Path.Combine(Globals.MameDirectory, "samples");

			Directory.CreateDirectory(CacheDirectory);
		}

		public void Initialize()
		{
			Tools.ConsoleHeading(2, new string[] {
				$"Machine Samples",
				SourceUrl
			});

			Version = GetLocalVersion();

			string cacheFilename = Path.Combine(CacheDirectory, Version + ".xml");

			string xml = null;

			if (Version == null || (DateTime.Now - File.GetLastWriteTime(cacheFilename) > TimeSpan.FromHours(3)))
			{
				xml = Tools.Query(Globals.HttpClient, SourceUrl);
				Version = ParseXML(xml);
				cacheFilename = Path.Combine(CacheDirectory, Version + ".xml");
				File.WriteAllText(cacheFilename, xml, Encoding.UTF8);

			}

			if (xml == null)
			{
				xml = File.ReadAllText(cacheFilename, Encoding.UTF8);
				Version = ParseXML(xml);
			}

			DataSet.Tables["machine"].PrimaryKey = new DataColumn[] { DataSet.Tables["machine"].Columns["name"] };

			DataSet.Tables["rom"].PrimaryKey = new DataColumn[] { DataSet.Tables["rom"].Columns["machine_id"], DataSet.Tables["rom"].Columns["name"] };

			Console.WriteLine($"Version:\t{Version}");
		}

		public string GetLocalVersion()
		{
			List<string> versions = new List<string>(Directory.GetFiles(CacheDirectory, "*.xml"));

			versions.Sort();

			if (versions.Count == 0)
				return null;

			return Path.GetFileNameWithoutExtension(versions[versions.Count - 1]);
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

		public void PlaceSamples(DataRow machineRow)
		{
			if (DataSet == null)
				return;

			DataRow[] sampleRows = Globals.Database.GetMachineSamples(machineRow);

			if (sampleRows.Length == 0)
				return;

			Tools.ConsoleHeading(2, new string[] {
				$"Machine Samples ({sampleRows.Length})",
			});

			string machineName = (string)machineRow["name"];

			DataRow machineSampleRow = DataSet.Tables["machine"].Rows.Find(machineName);

			if (machineSampleRow == null)
			{
				Console.WriteLine($"!!! Sample machine not found: {machineName}");
				return;
			}

			long machine_id = (long)machineSampleRow["machine_id"];

			List<DataRow> machineSampleRows = new List<DataRow>();

			foreach (string sampleName in sampleRows.Select(row => (string)row["name"]))
			{
				DataRow sampleRow = DataSet.Tables["rom"].Rows.Find(new object[] { machine_id, sampleName + ".wav" });

				if (sampleRow == null)
				{
					Console.WriteLine($"!!! Sample not found: {machineName}\t{sampleName}");
					continue;
				}

				if (sampleRow.IsNull("name") || sampleRow.IsNull("sha1"))
					continue;

				machineSampleRows.Add(sampleRow);
			}

			// need bad sources !!! dont keep downloading if not up to date in archive.org


			// !!! use source for this

			//
			// Check Store
			//
			bool download = false;
			foreach (DataRow row in machineSampleRows)
			{
				string sha1 = (string)row["sha1"];

				if (Globals.RomHashStore.Exists(sha1) == false)
					download = true;
			}

			//
			// downlaod here
			//

			if (download == true)
			{
				string url = $"https://archive.org/download/mame-support/Support/Samples/{machineName}.zip";

				using (TempDirectory tempDir = new TempDirectory())
				{
					string zipFilename = Path.Combine(tempDir.Path, machineName + ".zip");
					Tools.Download(url, zipFilename, 0, 10);

					ZipFile.ExtractToDirectory(zipFilename, tempDir.Path);
					Tools.ClearAttributes(tempDir.Path);

					foreach (string wavFilename in Directory.GetFiles(tempDir.Path, "*.wav"))
						Globals.RomHashStore.Add(wavFilename);

					Tools.ClearAttributes(tempDir.Path);
				}
			}

			//
			// Place
			//

			List<string[]> wavStoreFilenames = new List<string[]>();

			foreach (DataRow row in machineSampleRows)
			{
				string name = (string)row["name"];
				string sha1 = (string)row["sha1"];

				string wavFilename = Path.Combine(SamplesDirectory, machineName, name);

				bool fileExists = File.Exists(wavFilename);
				bool storeExists = Globals.RomHashStore.Exists(sha1);

				if (fileExists == false && storeExists == true)
				{
					wavStoreFilenames.Add(new string[] { wavFilename, Globals.RomHashStore.Filename(sha1) });
				}

				Console.WriteLine($"Place Sample:\t{fileExists}\t{storeExists}\t{sha1}\t{wavFilename}");
			}

			if (Globals.LinkingEnabled == true)
			{
				Tools.LinkFiles(wavStoreFilenames.ToArray());
			}
			else
			{
				foreach (string[] wavStoreFilename in wavStoreFilenames)
					File.Copy(wavStoreFilename[1], wavStoreFilename[0], true);
			}




		}
	}
}
