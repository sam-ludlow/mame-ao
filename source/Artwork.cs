using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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

	}
}
