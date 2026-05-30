using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Spludlow.MameAO
{
	internal class CoreRedump : ICore
	{
		string ICore.Name { get => "redump"; }
		string ICore.Version { get => _Version; }
		string ICore.Directory { get => _CoreDirectory; }
		string[] ICore.ConnectionStrings { get => new string[] { _ConnectionString }; }

		Dictionary<string, string> ICore.SoftwareListDescriptions { get => null; }
		Dictionary<string, string[]> ICore.Filters { get => throw new NotImplementedException(); }

		private string _RootDirectory = null;
		private string _CoreDirectory = null;

		private string _Version = null;

		private string _ConnectionString = null;

		void ICore.Initialize(string directory, string version)
		{
			//	TODO: validate version
			_RootDirectory = directory;
			Directory.CreateDirectory(_RootDirectory);

			_Version = null;    //	Always use latest
		}

		public string LatestDownloadedVersion()
		{
			List<string> versions = new List<string>();

			foreach (string versionDirectory in Directory.GetDirectories(_RootDirectory))
				versions.Add(Path.GetFileName(versionDirectory));

			if (versions.Count == 0)
				throw new ApplicationException($"No versions found in '{_RootDirectory}'.");

			versions.Sort();

			return versions[versions.Count - 1];
		}

		int ICore.Get()
		{
			string url = "http://redump.org/downloads/";
			string html;

			try
			{
				html = Tools.FetchTextCached(url);
			}
			catch (Exception e)
			{
				Console.WriteLine($"!!! Can not download core HTML, {e.Message}");
				return 0;
			}

			if (html == null)
				throw new ApplicationException("Unable to get core html");

			if (_Version == null)
				_Version = DateTime.Now.ToString("yyyy-MM-dd");	//	TODO better plan
			
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);
			Directory.CreateDirectory(_CoreDirectory);

			using (StringReader reader = new StringReader(html))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();

					if (line.StartsWith("<table class=\"statistics\"") == false)
						continue;

					string[] rows = line.Split(new[] { "<tr>", "</tr>" }, StringSplitOptions.RemoveEmptyEntries);

					foreach (string row in rows)
					{
						string[] cells = row.Split(new[] { "<td>", "</td>", "<th>", "</th>" }, StringSplitOptions.None);

						if (cells.Length != 13)
							continue;

						string name = cells[1];
						string href = cells[5];

						if (href.StartsWith("<a href=") == false)
							continue;

						int index = href.IndexOf('"');
						href = href.Substring(index + 1);
						index = href.IndexOf('"');
						href = href.Substring(0, index);
						href = new Uri(new Uri(url), href).ToString();

						Console.WriteLine(name + "\t" + href);

						using (var response = Globals.HttpClient.GetAsync(href, HttpCompletionOption.ResponseHeadersRead).Result)
						{
							response.EnsureSuccessStatusCode();

							Console.WriteLine(response.Content.Headers.ContentDisposition.FileName);

							string targetFilename = Path.Combine(_CoreDirectory, response.Content.Headers.ContentDisposition.FileName.Trim('"'));

							if (File.Exists(targetFilename) == false)
							{
								string tempFilename = targetFilename + ".tmp";
								try
								{
									using (FileStream writeStream = new FileStream(tempFilename, FileMode.Create))
									{
										using (Stream readStream = response.Content.ReadAsStreamAsync().Result)
										{
											readStream.CopyTo(writeStream);
										}
									}
									File.Move(tempFilename, targetFilename);
								}
								finally
								{
									File.Delete(tempFilename);
								}
							}
						}
					}
				}
			}

			return 0;
		}

		void ICore.Xml()
		{
			if (_Version == null)
				_Version = LatestDownloadedVersion();
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			foreach (string zipFilename in Directory.GetFiles(_CoreDirectory, "*.zip"))
			{
				string name = Path.GetFileNameWithoutExtension(zipFilename);
				string xmlFilename = Path.Combine(_CoreDirectory, name + ".xml");

				using (TempDirectory tempDir = new TempDirectory())
				{
					Console.Write($"Extract ZIP {zipFilename} ...");
					ZipFile.ExtractToDirectory(zipFilename, tempDir.Path);
					Console.WriteLine("...done");

					Tools.ClearAttributes(tempDir.Path);

					string[] filenames = Directory.GetFiles(tempDir.Path, "*.dat");
					if (filenames.Length != 1)
						throw new ApplicationException($"Did not find one file: {zipFilename}");

					File.Delete(xmlFilename);
					File.Move(filenames[0], xmlFilename);
				}
			}

		}

		void ICore.MSSql(string serverConnectionString, string[] databaseNames)
		{
			if (_Version == null)
				_Version = LatestDownloadedVersion();
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			DataSet dataSet = RedumpDataSet(_CoreDirectory);

			Database.DataSet2MSSQL(dataSet, serverConnectionString, databaseNames[0]);

			Database.MakeForeignKeys(serverConnectionString, databaseNames[0]);
		}

		public static DataSet RedumpDataSet(string directory)
		{
			DataSet dataSet = new DataSet();

			foreach (string filename in Directory.GetFiles(directory, "*.xml"))
			{
				Console.WriteLine($"{filename}");

				XElement document = XElement.Load(filename);

				DataSet fileDataSet = new DataSet();
				ReadXML.ImportXMLWork(document, fileDataSet, null, null);

				Tools.DataFileMoveHeader(fileDataSet);

				foreach (DataTable table in dataSet.Tables)
					foreach (DataColumn column in table.Columns)
						column.AutoIncrement = false;

				Tools.DataFileMergeDataSet(fileDataSet, dataSet);
			}

			return dataSet;
		}

		void ICore.MSSqlPayload(string serverConnectionString, string[] databaseNames)
		{
			if (_Version == null)
				_Version = LatestDownloadedVersion();
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			OperationsPayload.RedumpMSSQLPayloads(_RootDirectory, _Version, serverConnectionString, databaseNames[0]);
		}







		void ICore.AllSHA1(HashSet<string> hashSet)
		{
			throw new NotImplementedException();
		}


		DataRow ICore.GetMachine(string machine_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineDeviceRefs(string machine_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineDisks(DataRow machine)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineFeatures(DataRow machine)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineRoms(string machine_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineSamples(DataRow machine)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetMachineSoftwareLists(DataRow machine)
		{
			throw new NotImplementedException();
		}

		HashSet<string> ICore.GetReferencedMachines(string machine_name)
		{
			throw new NotImplementedException();
		}

		string ICore.GetRequiredMedia(string machine_name, string softwarelist_name, string software_name)
		{
			throw new NotImplementedException();
		}

		DataRow ICore.GetSoftware(DataRow softwarelist, string software_name)
		{
			throw new NotImplementedException();
		}

		DataRow ICore.GetSoftware(string softwarelist_name, string software_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareDisks(DataRow software)
		{
			throw new NotImplementedException();
		}

		DataRow ICore.GetSoftwareList(string softwarelist_name)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareListsSoftware(DataRow softwarelist)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareRoms(DataRow software)
		{
			throw new NotImplementedException();
		}

		DataRow[] ICore.GetSoftwareSharedFeats(DataRow software)
		{
			throw new NotImplementedException();
		}



		void ICore.Json()
		{
			throw new NotImplementedException();
		}

		void ICore.MsAccess()
		{
			throw new NotImplementedException();
		}



		DataTable ICore.QueryMachines(string profile, int offset, int limit, string search, string manufacturer, string[] status, string[] display, string[] players, string[] control, bool? mechanical, bool? clone, string order, string sort)
		{
			throw new NotImplementedException();
		}

		DataTable ICore.QuerySoftware(string softwarelist_name, int offset, int limit, string search, string publisher, string order, string sort, string favorites_machine)
		{
			throw new NotImplementedException();
		}

		void ICore.SQLite()
		{
			throw new NotImplementedException();
		}

		void ICore.SQLiteAo()
		{
			throw new NotImplementedException();
		}



		void ICore.Zips()
		{
			throw new NotImplementedException();
		}
	}
}
