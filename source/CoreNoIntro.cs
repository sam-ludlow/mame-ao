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
	internal class CoreNoIntro : ICore
	{
		string ICore.Name { get => "no-intro"; }
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
			//	https://datomatic.no-intro.org/index.php?page=download&s=64&op=daily
			string importDirectory = @"C:\tmp\No-Intro";

			string[] filenames = Directory.GetFiles(importDirectory, "*.zip");

			if (filenames.Length == 0)
				return 0;

			if (filenames.Length > 1)
				throw new ApplicationException("More than one zip file");

			string name = Path.GetFileNameWithoutExtension(filenames[0]);
			int index = name.LastIndexOf("(");
			name = name.Substring(index + 1);
			index = name.IndexOf(")");
			name = name.Substring(0, index);

			if (_Version == null)
				_Version = name;

			_CoreDirectory = Path.Combine(_RootDirectory, _Version);
			Directory.CreateDirectory(_CoreDirectory);

			string zipFilename = Path.Combine(_CoreDirectory, "no-intro.zip");

			if (File.Exists(zipFilename) == true)
				return 0;

			File.Move(filenames[0], zipFilename);

			return 1;
		}

		void ICore.Xml()
		{
			if (_Version == null)
				_Version = LatestDownloadedVersion();
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			using (TempDirectory tempDir = new TempDirectory())
			{
				string zipFilename = Path.Combine(_CoreDirectory, "no-intro.zip");

				Console.Write($"Extract ZIP {zipFilename} ...");
				ZipFile.ExtractToDirectory(zipFilename, tempDir.Path);
				Console.WriteLine("...done");

				Tools.ClearAttributes(tempDir.Path);

				foreach (string sourceFilename in Directory.GetFiles(tempDir.Path, "*.dat", SearchOption.AllDirectories))
				{
					string name = Path.GetFileNameWithoutExtension(sourceFilename);
					string targetFilename = Path.Combine(_CoreDirectory, name + ".dat");
					File.Move(sourceFilename, targetFilename);
				}
			}

			Dictionary<string, XmlDocument> subsetDocuments = new Dictionary<string, XmlDocument>();

			foreach (string sourceFilename in Directory.GetFiles(_CoreDirectory, "*.dat", SearchOption.AllDirectories))
			{
				XmlDocument datafileDoc = new XmlDocument();
				datafileDoc.Load(sourceFilename);

				string subset = datafileDoc.SelectSingleNode("/datafile/header/subset")?.InnerText ?? "No-Intro";

				XmlDocument subsetDoc;
				if (subsetDocuments.ContainsKey(subset) == false)
				{
					subsetDoc = new XmlDocument();
					subsetDocuments.Add(subset, subsetDoc);
					XmlElement subsetElement = subsetDoc.CreateElement("subset");
					subsetElement.SetAttribute("name", subset);
					subsetDoc.AppendChild(subsetElement);
				}
				else
				{
					subsetDoc = subsetDocuments[subset];
				}

				XmlNode sourceNode = datafileDoc.GetElementsByTagName("datafile").Cast<XmlNode>().Single();
				XmlNode targetNode = subsetDoc.ImportNode(sourceNode, true);
				subsetDoc.SelectSingleNode("/subset").AppendChild(targetNode);

				Console.WriteLine($"{subset}\t{sourceFilename}");
			}

			foreach (string subset in subsetDocuments.Keys)
			{
				XmlDocument doc = subsetDocuments[subset];

				string targetFilename = Path.Combine(_CoreDirectory, subset.ToLower().Replace(' ', '-') + ".xml");

				XmlWriterSettings settings = new XmlWriterSettings
				{
					OmitXmlDeclaration = false,
					Indent = true,
					IndentChars = "\t",
				};

				using (XmlWriter xmlWriter = XmlWriter.Create(targetFilename, settings))
				{
					doc.Save(xmlWriter);
				}

			}
		}

		void ICore.MSSql(string serverConnectionString, string[] databaseNames)
		{
			if (_Version == null)
				_Version = LatestDownloadedVersion();
			_CoreDirectory = Path.Combine(_RootDirectory, _Version);

			DataSet dataSet = NoIntroDataSet(_CoreDirectory);

			Database.DataSet2MSSQL(dataSet, serverConnectionString, databaseNames[0]);

			Database.MakeForeignKeys(serverConnectionString, databaseNames[0]);
		}

		public static DataSet NoIntroDataSet(string directory)
		{
			DataSet dataSet = new DataSet();

			foreach (string filename in Directory.GetFiles(directory, "*.dat"))
			{
				Console.WriteLine($"{filename}");

				XElement document = XElement.Load(filename);

				foreach (var element in document.Descendants("game_id"))
					element.Name = "game_code";

				foreach (var element in document.Descendants("id"))
					element.Name = "datafile_identity";

				foreach (var game in document.Descendants("game"))
				{
					var attribute = game.Attribute("id");
					if (attribute != null)
					{
						game.SetAttributeValue("game_identity", attribute.Value);
						attribute.Remove();
					}
				}

				foreach (var category in document.Descendants("category"))
					category.SetAttributeValue("name", category.Value);

				foreach (var element in document.Descendants("game_code"))
					element.SetAttributeValue("name", element.Value);

				document.Descendants("clrmamepro").Remove();

				DataSet fileDataSet = new DataSet();
				ReadXML.ImportXMLWork(document, fileDataSet, null, null);

				Tools.DataFileMoveHeader(fileDataSet);

				foreach (DataTable table in dataSet.Tables)
					foreach (DataColumn column in table.Columns)
						column.AutoIncrement = false;

				Tools.DataFileMergeDataSet(fileDataSet, dataSet);
			}

			dataSet.Tables["datafile"].Columns.Remove("xsi");
			dataSet.Tables["datafile"].Columns.Remove("schemaLocation");
			dataSet.Tables["datafile"].Columns.Remove("homepage");
			dataSet.Tables["datafile"].Columns.Remove("url");

			foreach (DataRow row in dataSet.Tables["datafile"].Rows)
			{
				string subset = row.IsNull("subset") == false ? (string)row["subset"] : "no-intro";
				row["subset"] = subset.ToLower().Replace(' ', '-');
			}

			dataSet.Tables["datafile"].AcceptChanges();

			return dataSet;
		}

		void ICore.MSSqlPayload(string serverConnectionString, string[] databaseNames)
		{
			if (_Version == null)
				_Version = LatestDownloadedVersion();

			OperationsPayload.NoIntroMSSQLPayloads(_RootDirectory, _Version, serverConnectionString, databaseNames[0]);
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
