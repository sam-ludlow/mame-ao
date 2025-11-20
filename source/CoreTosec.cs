using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Spludlow.MameAO
{
	internal class CoreTosec : ICore
	{
		string ICore.Name { get => "tosec"; }
		string ICore.Version { get => _Version; }
		string ICore.Directory { get => _CoreDirectory; }
		string[] ICore.ConnectionStrings { get => new string[] { _ConnectionString }; }

		Dictionary<string, string> ICore.SoftwareListDescriptions { get => null; }

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

		int ICore.Get()
		{
			string url = "https://www.tosecdev.org/downloads/category/22-datfiles";
			string html = Tools.FetchTextCached(url) ?? throw new ApplicationException("Unanle to get tosec download html");

			string find;
			int index;

			SortedDictionary<string, string> downloadPageUrls = new SortedDictionary<string, string>();

			using (StringReader reader = new StringReader(html))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();
					if (line.Length == 0)
						continue;

					find = "<div class=\"pd-subcategory\"><a href=\"";

					index = line.IndexOf(find);
					if (index != -1)
					{
						line = line.Substring(index + find.Length);

						index = line.IndexOf("\"");
						if (index != -1)
						{
							line = line.Substring(0, index);

							index = line.LastIndexOf("/");
							if (index != -1)
							{
								string key = line.Substring(index + 1);
								index = key.IndexOf("-");
								key = key.Substring(index + 1);

								downloadPageUrls.Add(key, new Uri(new Uri(url), line).AbsoluteUri);
							}
						}
					}
				}
			}

			if (downloadPageUrls.Count == 0)
				throw new ApplicationException("Did not find any TOSEC links.");

			if (_Version == null)
				_Version = downloadPageUrls.Keys.Last();

			if (downloadPageUrls.ContainsKey(_Version) == false)
				throw new ApplicationException($"Did not find TOSEC version: {_Version}");

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);
			Directory.CreateDirectory(_CoreDirectory);

			string zipFilename = Path.Combine(_CoreDirectory, "tosec.zip");

			if (File.Exists(zipFilename) == true)
				return 0;

			url = downloadPageUrls[_Version];
			html = Tools.Query(url);

			find = "<a class=\"btn btn-success\" href=\"";
			index = html.IndexOf(find);
			html = html.Substring(index + find.Length);

			index = html.IndexOf("\"");
			html = html.Substring(0, index);

			url = new Uri(new Uri(url), html).AbsoluteUri;

			Console.Write($"Downloading {url} {zipFilename} ...");
			Tools.Download(url, zipFilename, 1);
			Console.WriteLine("...done");

			return 1;
		}

		void ICore.Xml()
		{
			if (_Version == null)
				_Version = TosecGetLatestDownloadedVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			if (Directory.Exists(Path.Combine(_CoreDirectory, "CUEs")) == true)
				return;

			string zipFilename = Path.Combine(_CoreDirectory, "tosec.zip");

			Console.Write($"Extract ZIP {zipFilename} {_CoreDirectory} ...");
			ZipFile.ExtractToDirectory(zipFilename, _CoreDirectory);
			Console.WriteLine("...done");

			string[] categories = new string[] { "TOSEC", "TOSEC-ISO", "TOSEC-PIX" };

			foreach (string category in categories)
			{
				string categoryDirectory = Path.Combine(_CoreDirectory, category);

				XmlDocument categoryDocument = new XmlDocument();

				XmlElement categoryElement = categoryDocument.CreateElement("category");
				categoryElement.SetAttribute("name", category);
				categoryDocument.AppendChild(categoryElement);

				foreach (string filename in Directory.GetFiles(categoryDirectory, "*.dat"))
				{
					Console.WriteLine($"{filename}");

					XmlDocument datafileDocument = new XmlDocument();
					datafileDocument.Load(filename);

					XmlNode sourceNode = datafileDocument.GetElementsByTagName("datafile").Cast<XmlNode>().Single();
					XmlNode targetNode = categoryDocument.ImportNode(sourceNode, true);

					categoryElement.AppendChild(targetNode);
				}

				string targetFilename = Path.Combine(_CoreDirectory, $"_{category.ToLower()}.xml");

				XmlWriterSettings settings = new XmlWriterSettings
				{
					OmitXmlDeclaration = false,
					Indent = true,
					IndentChars = "\t",
				};

				using (XmlWriter xmlWriter = XmlWriter.Create(targetFilename, settings))
				{
					categoryDocument.Save(xmlWriter);
				}
			}
		}

		void ICore.Json()
		{
			if (_Version == null)
				_Version = TosecGetLatestDownloadedVersion(_RootDirectory);
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			foreach (string xmlFilename in Directory.GetFiles(_CoreDirectory, "*.xml"))
			{
				string jsonFilename = xmlFilename.Substring(0, xmlFilename.Length - 4) + ".json";

				if (File.Exists(jsonFilename) == false)
					Tools.XML2JSON(xmlFilename, jsonFilename);
			}
		}

		void ICore.SQLite()
		{
			if (_Version == null)
				_Version = TosecGetLatestDownloadedVersion(_RootDirectory);

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			string sqlLiteFilename = Path.Combine(_CoreDirectory, "_tosec.sqlite");

			if (File.Exists(sqlLiteFilename) == true)
				return;

			DataSet dataSet = TosecDataSet(_CoreDirectory);

			string connectionString = $"Data Source='{sqlLiteFilename}';datetimeformat=CurrentCulture;";

			Console.Write($"Creating SQLite database {sqlLiteFilename} ...");
			Database.DataSet2SQLite("tosec", connectionString, dataSet);
			Console.WriteLine("... done");
		}

		public static DataSet TosecDataSet(string directory)
		{
			DataSet dataSet = new DataSet();

			string[] categories = new string[] { "TOSEC", "TOSEC-ISO", "TOSEC-PIX" };

			foreach (string category in categories)
			{
				string groupDirectory = Path.Combine(directory, category);

				foreach (string filename in Directory.GetFiles(groupDirectory, "*.dat"))
				{
					string name = Path.GetFileNameWithoutExtension(filename);

					int index;

					index = name.LastIndexOf("(");
					if (index == -1)
						throw new ApplicationException("No last index of open bracket");

					string fileVersion = name.Substring(index).Trim(new char[] { '(', ')' });
					name = name.Substring(0, index).Trim();

					Console.WriteLine($"{category}\t{name}\t{fileVersion}");

					XElement document = XElement.Load(filename);
					DataSet fileDataSet = new DataSet();
					ReadXML.ImportXMLWork(document, fileDataSet, null, null);

					Tools.DataFileMoveHeader(fileDataSet);

					foreach (DataTable table in dataSet.Tables)
						foreach (DataColumn column in table.Columns)
							column.AutoIncrement = false;

					foreach (DataRow row in fileDataSet.Tables["datafile"].Rows)
					{
						if ((string)row["category"] != category)
						{
							row["category"] = category;
							Console.WriteLine($"Bad datafile category: {filename}");
                        }
					}

					Tools.DataFileMergeDataSet(fileDataSet, dataSet);
				}
			}

			return dataSet;
		}


		public static string TosecGetLatestDownloadedVersion(string directory)
		{
			List<string> versions = new List<string>();

			foreach (string versionDirectory in Directory.GetDirectories(directory))
				versions.Add(Path.GetFileName(versionDirectory));

			if (versions.Count == 0)
				throw new ApplicationException($"No TOSEC versions found in '{directory}'.");

			versions.Sort();

			return versions[versions.Count - 1];
		}


		void ICore.MsAccess()
		{
			throw new NotImplementedException();
		}
		void ICore.Zips()
		{
			throw new NotImplementedException();
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

		void ICore.MSSql()
		{
			throw new NotImplementedException();
		}

		void ICore.MSSqlHtml()
		{
			throw new NotImplementedException();
		}

		void ICore.MSSqlPayload()
		{
			throw new NotImplementedException();
		}

		DataTable ICore.QueryMachines(DataQueryProfile profile, int offset, int limit, string search)
		{
			throw new NotImplementedException();
		}

		DataTable ICore.QuerySoftware(string softwarelist_name, int offset, int limit, string search, string favorites_machine)
		{
			throw new NotImplementedException();
		}

		void ICore.SQLiteAo()
		{
			throw new NotImplementedException();
		}


	}
}
