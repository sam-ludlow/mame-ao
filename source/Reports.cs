using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using System.Net;

namespace Spludlow.MameAO
{
	public class Reports
	{
		public class ReportGroup
		{
			public string Key;
			public string Text;
			public string Decription;
		}

		public class ReportType
		{
			public string Key;
			public string Group;
			public string Code;
			public string Text;
			public string Decription;
		}

		public static ReportGroup[] ReportGroups = new ReportGroup[] {
			new ReportGroup(){
				Key = "source-exists",
				Text = "Source Exists",
				Decription = "Check that files exist in the archive.org source metadata. Tests are NOT performed to check if ZIPs, ROMs, or CHDs are valid.",
			},
			new ReportGroup(){
				Key = "available",
				Text = "Available Machines & Software",
				Decription = "Report on Machines & Software that are available, already downloaded.",
			},
			new ReportGroup(){
				Key = "integrity",
				Text = "MAME Data Integrity",
				Decription = "Report on various aspects of MAME Data Integrity.",
			},
		};

		public static ReportType[] ReportTypes = new ReportType[] {
			new ReportType(){
				Key = "machine-rom",
				Group = "source-exists",
				Code = "SEMR",
				Text = "Machine Rom",
				Decription = "Check that the file exists for all parent machines with ROMs.",
			},
			new ReportType(){
				Key = "machine-disk",
				Group = "source-exists",
				Code = "SEMD",
				Text = "Machine Disk",
				Decription = "Check that all machine disks exist, including child machines.",
			},
			new ReportType(){
				Key = "software-rom",
				Group = "source-exists",
				Code = "SESR",
				Text = "Software Rom",
				Decription = "Check that all software lists exist.",
			},
			new ReportType(){
				Key = "software-disk",
				Group = "source-exists",
				Code = "SESD",
				Text = "Software Disk",
				Decription = "Check that all software disks exist, multiple sources are checked if avaialable.",
			},
			new ReportType(){
				Key = "machine-samples",
				Group = "source-exists",
				Code = "SEMS",
				Text = "Machine Samples",
				Decription = "Check that all machine samples exist.",
			},
			new ReportType(){
				Key = "machine-artwork",
				Group = "source-exists",
				Code = "SEMA",
				Text = "Machine Artwork",
				Decription = "Check that all machine artwork exists.",
			},

			new ReportType(){
				Key = "machine",
				Group = "available",
				Code = "AVM",
				Text = "Machines",
				Decription = "List Machines that are available to run.",
			},
			new ReportType(){
				Key = "software",
				Group = "available",
				Code = "AVS",
				Text = "Software",
				Decription = "List Software that is available to run.",
			},

			new ReportType(){
				Key = "machine-softwarelists-exist",
				Group = "integrity",
				Code = "IMSLM",
				Text = "Machine Lists Missing",
				Decription = "Machines Software Lists Missing.",
			},
			new ReportType(){
				Key = "softwarelists-without-machines",
				Group = "integrity",
				Code = "ISLWM",
				Text = "Software Lists no Machines",
				Decription = "Software Lists without Machines.",
			},

		};

		private string _OutputDirectory;

		public Reports()
		{
			_OutputDirectory = Path.Combine(Globals.RootDirectory, "_REPORTS");
			Directory.CreateDirectory(_OutputDirectory);
		}

		public string[] ReportTypeText()
		{
			List<string> results = new List<string>();

			foreach (ReportGroup reportGroup in ReportGroups)
			{
				foreach (ReportType reportType in ReportTypes)
				{
					if (reportType.Group != reportGroup.Key)
						continue;

					results.Add($"    {reportType.Code} : {reportType.Decription}");
				}

				results.Add("");
			}

			return results.ToArray();
		}

		private static readonly string _HtmlReportStyle =
			"body {" +
			" font-family: sans-serif;" +
			" font-size: small;" +
			" background-color: #c6eafb;" +
			"}" +
			"hr {" +
			" color: #00ADEF;" +
			" background-color: #00ADEF;" +
			" height: 6px;" +
			" border: none;" +
			" padding-left: 0px;" +
			"}" +
			"table {" +
			" border-collapse: collapse;" +
			"}" +
			"th, td {" +
			" padding: 2px;" +
			" text-align: left;" +
			"}" +
			"table, th, td {" +
			" border: 1px solid black;" +
			"}" +
			"th {" +
			" background-color: #00ADEF;" +
			" color: white;" +
			"}" +
			"tr:nth-child(even) {" +
			" background-color: #b6daeb;" +
			"}";

		public void SaveHtmlReport(DataView[] views, string[] headings, string title)
		{
			DataSet dataSet = new DataSet();

			for (int index = 0; index < views.Length; ++index)
			{
				DataView view = views[index];
				string heading = headings[index];

				DataTable table = view.Table.Clone();
				table.TableName = heading;

				foreach (DataRowView rowView in view)
					table.ImportRow(rowView.Row);

				dataSet.Tables.Add(table);
			}

			SaveHtmlReport(dataSet, title);
		}

		public void SaveHtmlReport(DataTable table, string title)
		{
			DataSet dataSet = new DataSet();
			dataSet.Tables.Add(table);
			SaveHtmlReport(dataSet, title);
		}

		public void SaveHtmlReport(DataSet dataSet, string title)
		{
			string name = DateTime.Now.ToString("s").Replace(":", "-") + "_" + Tools.ValidFileName(title);

			title = "MAME-AO - " + title;

			StringBuilder html = new StringBuilder();

			html.AppendLine("<!DOCTYPE html>");
			html.AppendLine("<html lang=\"en\">");
			html.AppendLine("<head>");
			html.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"/>");
			html.AppendLine("<title>" + title + "</title>");
			html.AppendLine("<style type=\"text/css\">" + _HtmlReportStyle + "</style>");
			html.AppendLine("</head>");
			html.AppendLine("<h1>" + title + "</h1>");

			for (int index = 0; index < dataSet.Tables.Count; ++index)
			{
				DataTable table = dataSet.Tables[index];

				html.AppendLine("<hr />");
				if (table.TableName.StartsWith("Table") == false)
					html.AppendLine("<h2>" + table.TableName + "</h2>");
				html.AppendLine(MakeHtmlTable(table, "width:100%;"));
			}

			html.AppendLine("<hr />");
			html.AppendLine("<p style=\"width:100%;\">" + name + "<span style=\"float:right\"><a href=\"https://github.com/sam-ludlow/mame-ao\">Spludlow MAME-AO</a></span></p>");
			html.AppendLine("</html>");

			string filename = Path.Combine(_OutputDirectory, name + ".htm");

			File.WriteAllText(filename, html.ToString(), Encoding.UTF8);

			Console.WriteLine();
			Console.WriteLine($"HTML Report saved \"{title}\" : {filename}");
			Console.WriteLine();

			Process.Start($"http://localhost:12380/api/report?name={Path.GetFileNameWithoutExtension(filename)}");
		}

		public static string MakeHtmlTable(DataTable table, string tableStyle)
		{
			return MakeHtmlTable(table, table.Rows.OfType<DataRow>(), tableStyle);
		}

		public static string MakeHtmlTable(DataTable table, IEnumerable<DataRow> rows, string tableStyle)
		{
			StringBuilder html = new StringBuilder();

			html.Append("<table");
			if (tableStyle != null)
			{
				html.Append(" style=\"");
				html.Append(tableStyle);
				html.Append("\"");
			}
			html.AppendLine(">");

			html.Append("<tr>");
			foreach (DataColumn column in table.Columns)
			{
				if (column.ColumnName.EndsWith("_id") == true)
					continue;

				html.Append("<th>");
				html.Append(column.ColumnName);
				html.Append("</th>");
			}
			html.AppendLine("</tr>");

			foreach (DataRow row in rows)
			{
				html.Append("<tr>");
				foreach (DataColumn column in table.Columns)
				{
					if (column.ColumnName.EndsWith("_id") == true)
						continue;

					html.Append("<td>");
					if (row.IsNull(column) == false)
					{
						string value = Convert.ToString(row[column]);

						if (value.StartsWith("<a href=") == true)
							html.Append(value);
						else
							html.Append(WebUtility.HtmlEncode(value));
					}
					html.Append("</td>");
				}
				html.AppendLine("</tr>");
			}

			html.AppendLine("</table>");

			return html.ToString();
		}

		public string GetHtml(string reportName)
		{
			string filename = Path.Combine(_OutputDirectory, reportName + ".htm");

			if (File.Exists(filename) == false)
				throw new ApplicationException($"Report does no exist: {reportName}");

			return File.ReadAllText(filename, Encoding.UTF8);
		}

		public string[] ListReports()
		{
			List<string> items = new List<string>();
			
			if (Directory.Exists(_OutputDirectory) == true)
			{
				foreach (string filename in Directory.GetFiles(_OutputDirectory))
					items.Add(Path.GetFileNameWithoutExtension(filename));

				items.Sort();
				items.Reverse();
			}

			return items.ToArray();
		}

		public bool RunReport(string reportCode)
		{
			reportCode = reportCode.ToUpper();

			MethodInfo method = this.GetType().GetMethod($"Report_{reportCode}");

			if (method == null)
				return false;

			try
			{
				Console.Write($"Running Report please wait {reportCode} ...");
				method.Invoke(this, null);
				Console.WriteLine("...done.");
			}
			catch (Exception e)
			{
				if (e is TargetInvocationException && e.InnerException != null)
					e = e.InnerException;

				Console.WriteLine("REPORT ERROR: " + e.ToString());
				throw e;
			}

			return true;
		}

		public static DataSet PlaceReportTemplate(string heading)
		{
			DataSet dataSet = new DataSet();

			dataSet.Tables.Add(Tools.MakeDataTable(
				"Info",
				"heading",
				"String"
			));

			dataSet.Tables["Info"].Rows.Add(heading);

			dataSet.Tables.Add(Tools.MakeDataTable(
				"Require",
				"sha1	require	name",
				"String	Boolean	String"
			));

			dataSet.Tables.Add(Tools.MakeDataTable(
				"Download",
				"url",
				"String"
			));

			dataSet.Tables.Add(Tools.MakeDataTable(
				"Import",
				"sha1	import	require	name",
				"String	Boolean	Boolean	String"
			));

			dataSet.Tables.Add(Tools.MakeDataTable(
				"Place",
				"sha1	place	have	name",
				"String	Boolean	Boolean	String"
			));

			return dataSet;
		}


		public void Report_SEMR()
		{
			ArchiveOrgItem sourceItem = Globals.ArchiveOrgItems[ItemType.MachineRom][0];

			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection,
				"SELECT machine_id, name, description, ao_rom_count FROM machine WHERE (ao_rom_count > 0 AND romof IS NULL) ORDER BY machine.name");

			DataTable table = Tools.MakeDataTable(
				"Status	Machine	RomCount	Filename	Size	FileSHA1	ModifiedTime",
				"String	String	Int64		String		Int64	String		DateTime");

			foreach (DataRow machineRow in machineTable.Rows)
			{
				string machineName = (string)machineRow["name"];
				long romCount = (long)machineRow["ao_rom_count"];

				DataRow row = table.Rows.Add("", machineName, romCount);

				ArchiveOrgFile file = sourceItem.GetFile(machineName);

				if (file != null)
				{
					row["Filename"] = file.name;
					row["Size"] = file.size;
					row["FileSHA1"] = file.sha1;
					row["ModifiedTime"] = file.mtime;
				}
				else
				{
					row["Status"] = "MISSING";
				}
			}

			DataSet dataSet = new DataSet();

			DataView view;
			DataTable viewTable;

			view = new DataView(table);
			view.RowFilter = "Status = 'MISSING'";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Missing in Source";
			dataSet.Tables.Add(viewTable);

			view = new DataView(table);
			view.RowFilter = "Status = ''";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Available in Source";
			dataSet.Tables.Add(viewTable);


			this.SaveHtmlReport(dataSet, "Source Exists Machine Rom");
		}

		public void Report_SEMD()
		{
			ArchiveOrgItem sourceItem = Globals.ArchiveOrgItems[ItemType.MachineDisk][0];	//	TODO: Support many

			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name, description, romof FROM machine ORDER BY machine.name");
			DataTable diskTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM disk WHERE sha1 IS NOT NULL");

			DataTable table = Tools.MakeDataTable(
				"Status	Machine	RomOf	Merge	Description	Name	SHA1	Filename	Size	FileSHA1	ModifiedTime",
				"String	String	String	String	String		String	String	String		Int64	String		DateTime");

			foreach (DataRow machineRow in machineTable.Rows)
			{
				long machine_id = (long)machineRow["machine_id"];

				string machineName = (string)machineRow["name"];
				string machineDescription = (string)machineRow["description"];
				string romof = Tools.DataRowValue(machineRow, "romof");

				foreach (DataRow diskRow in diskTable.Select("machine_id = " + machine_id))
				{
					string diskName = (string)diskRow["name"];
					string merge = Tools.DataRowValue(diskRow, "merge");
					string sha1 = (string)diskRow["sha1"];

					DataRow row = table.Rows.Add("", machineName, romof, merge, machineDescription, diskName, sha1);

					ArchiveOrgFile sourceFile = MameAOProcessor.MachineDiskAvailableSourceFile(machineRow, diskRow, sourceItem, Globals.Database);

					if (sourceFile != null)
					{
						row["Filename"] = sourceFile.name;
						row["Size"] = sourceFile.size;
						row["FileSHA1"] = sourceFile.sha1;
						row["ModifiedTime"] = sourceFile.mtime;
					}
					else
					{
						row["Status"] = "MISSING";
					}
				}
			}

			DataSet dataSet = new DataSet();

			DataView view;
			DataTable viewTable;

			view = new DataView(table);
			view.RowFilter = "Status = 'MISSING'";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Missing in Source";
			dataSet.Tables.Add(viewTable);

			view = new DataView(table);
			view.RowFilter = "Status = ''";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Available in Source";
			dataSet.Tables.Add(viewTable);

			this.SaveHtmlReport(dataSet, "Source Exists Machine Disk");
		}

		public void Report_SESR()
		{
			ArchiveOrgItem sourceItem = Globals.ArchiveOrgItems[ItemType.SoftwareRom][0];

			DataTable softwareListTable = Database.ExecuteFill(Globals.Database._SoftwareConnection,
				"SELECT softwarelist.name, softwarelist.description, Count(rom.rom_Id) AS rom_count " +
				"FROM (((softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) INNER JOIN part ON software.software_id = part.software_id) INNER JOIN dataarea ON part.part_id = dataarea.part_id) INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id " +
				"GROUP BY softwarelist.name, softwarelist.description ORDER BY softwarelist.name");
			
			DataTable table = Tools.MakeDataTable(
				"Status	Name	Description	RomCount	Filename	Size	FileSHA1	ModifiedTime",
				"String	String	String		Int64		String		Int64	String		DateTime");

			foreach (DataRow softwareListRow in softwareListTable.Rows)
			{
				string name = (string)softwareListRow["Name"];
				string description = (string)softwareListRow["Description"];
				long romCount = (long)softwareListRow["rom_count"];

				DataRow row = table.Rows.Add("", name, description, romCount);

				ArchiveOrgFile sourceFile = sourceItem.GetFile(name);

				if (sourceFile != null)
				{
					row["Filename"] = sourceFile.name;
					row["Size"] = sourceFile.size;
					row["FileSHA1"] = sourceFile.sha1;
					row["ModifiedTime"] = sourceFile.mtime;
				}
				else
				{
					row["Status"] = "MISSING";
				}
			}

			table.TableName = "All Software Lists";

			DataSet dataSet = new DataSet();
			dataSet.Tables.Add(table);

			this.SaveHtmlReport(dataSet, "Source Exists Software Rom");
		}

		public void Report_SESD()
		{
			ArchiveOrgItem[] sourceItems = Globals.ArchiveOrgItems[ItemType.SoftwareDisk];

			DataTable softwareDiskTable = Database.ExecuteFill(Globals.Database._SoftwareConnection,
				"SELECT softwarelist.name AS softwarelist_name, softwarelist.description AS softwarelist_description, software.name AS software_name, software.description AS software_description, disk.name, disk.sha1, disk.status " +
				"FROM (((softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) INNER JOIN part ON software.software_id = part.software_id) INNER JOIN diskarea ON part.part_id = diskarea.part_id) INNER JOIN disk ON diskarea.diskarea_id = disk.diskarea_id " +
				"WHERE (disk.sha1 IS NOT NULL) ORDER BY softwarelist.name, software.name, disk.name");

			DataTable table = Tools.MakeDataTable(
				"Status	Sources	SoftwareListName	SoftwareName	DiskName	SHA1	DiskStatus	Size	ModifiedTime",
				"String	String	String				String			String		String	String		Int64	DateTime");

			foreach (DataRow softwareDiskRow in softwareDiskTable.Rows)
			{
				string softwarelist_name = (string)softwareDiskRow["softwarelist_name"];
				string software_name = (string)softwareDiskRow["software_name"];
				string name = (string)softwareDiskRow["name"];
				string sha1 = (string)softwareDiskRow["sha1"];
				string status = (string)softwareDiskRow["status"];

				DataRow row = table.Rows.Add("", "", softwarelist_name, software_name, name, sha1, status);

				List<ArchiveOrgItem> foundSourceSets = new List<ArchiveOrgItem>();
				List<ArchiveOrgFile> foundFileInfos = new List<ArchiveOrgFile>();

				foreach (ArchiveOrgItem item in sourceItems)
				{
					string key = $"{softwarelist_name}/{software_name}/{name}";

					if (item.Tag != null && item.Tag != "*")
						key = $"{software_name}/{name}";

					ArchiveOrgFile file = item.GetFile(key);

					if (file != null)
					{
						foundSourceSets.Add(item);
						foundFileInfos.Add(file);
					}
				}

				if (foundSourceSets.Count > 0)
				{
					row["Sources"] = String.Join(", ", foundSourceSets.Select(set => set.Tag).ToArray());

					row["Size"] = foundFileInfos[0].size;
					row["ModifiedTime"] = foundFileInfos[0].mtime;
				}
				else
				{
					row["Status"] = "MISSING";
				}
			}

			DataSet dataSet = new DataSet();

			DataView view;
			DataTable viewTable;

			view = new DataView(table);
			view.RowFilter = "Status = 'MISSING'";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Missing in Source";
			dataSet.Tables.Add(viewTable);

			view = new DataView(table);
			view.RowFilter = "Status = ''";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Available in Source";
			dataSet.Tables.Add(viewTable);

			this.SaveHtmlReport(dataSet, "Source Exists Software Disk");
		}

		public void Report_SEMS()
		{
			Globals.Samples.Initialize();

			DataSet dataSet = new DataSet();

			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection,
				"SELECT machine.name, machine.description, machine.sampleof FROM machine WHERE (machine.sampleof IS NOT NULL)");

			Dictionary<string, List<string>> sampleMachines = new Dictionary<string, List<string>>();

			foreach (DataRow machineRow in machineTable.Rows)
			{
				string name = (string)machineRow["name"];
				string sampleof = (string)machineRow["sampleof"];

				if (sampleMachines.ContainsKey(sampleof) == false)
					sampleMachines.Add(sampleof, new List<string>());

				sampleMachines[sampleof].Add(name);
			}

			DataTable sampleMachinesTable = Tools.MakeDataTable("SampleOf Machines",
				"sampleof	machine_count	machines",
				"String		Int32			String");
			dataSet.Tables.Add(sampleMachinesTable);

			foreach (string sampleof in sampleMachines.Keys)
			{
				List<string> machines = sampleMachines[sampleof];
				machines.Sort();

				sampleMachinesTable.Rows.Add(sampleof, machines.Count, String.Join(", ", machines.ToArray()));
			}

			if (Globals.Samples.DataSet.Tables.Count > 0)
			{
				DataTable samplesTable = Tools.MakeDataTable("SampleOf Samples",
					"status		sampleof	name	size	sha1",
					"String		String		String	String	String");
				dataSet.Tables.Add(samplesTable);

				DataTable sampleMachineTable = Globals.Samples.DataSet.Tables["machine"];
				DataTable sampleRomTable = Globals.Samples.DataSet.Tables["rom"];

				ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.Support][0];

				foreach (string sampleof in sampleMachines.Keys)
				{
					DataRow sampleMachineRow = sampleMachineTable.Rows.Find(sampleof);

					if (sampleMachineRow == null)
					{
						samplesTable.Rows.Add("DATA MISSING", sampleof);
						continue;
					}

					string key = $"Samples/{sampleof}";
					ArchiveOrgFile file = item.GetFile(key);
					string status = file == null ? "ZIP MISSING" : "";

					long machine_id = (long)sampleMachineRow["machine_id"];
					foreach (DataRow sampleRomRow in sampleRomTable.Select($"machine_id = {machine_id}"))
					{
						string name = (string)sampleRomRow["name"];
						string size = (string)sampleRomRow["size"];
						string sha1 = (string)sampleRomRow["sha1"];

						samplesTable.Rows.Add(status, sampleof, name, size, sha1);
					}
				}
			}

			this.SaveHtmlReport(dataSet, "Source Exists Machine Samples");
		}

		public void Report_SEMA()
		{
			DataSet dataSet = new DataSet();

			foreach (ArtworkTypes type in Enum.GetValues(typeof(ArtworkTypes)))
			{
				Globals.Artwork.Initialize(type);

				if (Globals.Artwork.ArtworkDatas[type].DataSet != null)
				{
					foreach (DataTable sourceTable in Globals.Artwork.ArtworkDatas[type].DataSet.Tables)
					{
						DataTable table = sourceTable.Copy();
						table.TableName = $"{type}_{table.TableName}";
						dataSet.Tables.Add(table);
					}

					//string key = $"{artworkType}/{artworkType}";
					//ArchiveOrgFile file = item.GetFile(key);
					//if (file == null)
					//{
					//	Console.WriteLine($"!!! Artwork file not on archive.org: {key}");
					//	continue;
					//}
				}
			}

			this.SaveHtmlReport(dataSet, "Source Exists Machine Artwork");
		}

		public void Report_AVM()
		{
			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name, description, romof, cloneof FROM machine ORDER BY machine.name");
			DataTable romTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");
			DataTable diskTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM disk WHERE sha1 IS NOT NULL");

			DataTable table = Tools.MakeDataTable(
				"Status	Name	Description	Complete	RomCount	DiskCount	RomHave	DiskHave",
				"String	String	String		Boolean		Int64		Int64		Int64	Int64");

			foreach (DataRow machineRow in machineTable.Rows)
			{
				long machine_id = (long)machineRow["machine_id"];

				DataRow[] romRows = romTable.Select($"machine_id = {machine_id}");
				DataRow[] diskRows = diskTable.Select($"machine_id = {machine_id}");

				int romHaveCount = 0;
				foreach (DataRow romRow in romRows)
				{
					string sha1 = (string)romRow["sha1"];
					if (Globals.RomHashStore.Exists(sha1) == true)
						++romHaveCount;
				}

				int diskHaveCount = 0;
				foreach (DataRow diskRow in diskRows)
				{
					string sha1 = (string)diskRow["sha1"];
					if (Globals.DiskHashStore.Exists(sha1) == true)
						++diskHaveCount;
				}


				if (romRows.Length == 0 && diskRows.Length == 0)
					continue;

				bool complete = false;
				if (romRows.Length == romHaveCount && diskRows.Length == diskHaveCount)
					complete = true;

				table.Rows.Add("", (string)machineRow["name"], (string)machineRow["description"], complete, romRows.Length, diskRows.Length, romHaveCount, diskHaveCount);
			}

			DataSet dataSet = new DataSet();

			DataView view;
			DataTable viewTable;

			view = new DataView(table);
			view.RowFilter = "DiskCount > 0 AND Complete = 1";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "With DISK";
			dataSet.Tables.Add(viewTable);

			view = new DataView(table);
			view.RowFilter = "DiskCount = 0 AND Complete = 1";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Without DISK";
			dataSet.Tables.Add(viewTable);

			this.SaveHtmlReport(dataSet, "Avaliable Machine ROM and DISK");
		}

		public void Report_AVS()
		{
			DataTable softwarelistTable = Database.ExecuteFill(Globals.Database._SoftwareConnection,
				"SELECT softwarelist.name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");

			DataTable softwareTable = Database.ExecuteFill(Globals.Database._SoftwareConnection,
				"SELECT softwarelist.name AS softwarelist_name, software.name, software.description FROM softwarelist " +
				"INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id ORDER BY softwarelist.name, software.name");

			DataTable romTable = Database.ExecuteFill(Globals.Database._SoftwareConnection,
				"SELECT softwarelist.name AS softwarelist_name, software.name AS software_name, rom.sha1 " +
				"FROM (((softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) " +
				"INNER JOIN part ON software.software_id = part.software_id) INNER JOIN dataarea ON part.part_id = dataarea.part_id) " +
				"INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id " +
				"WHERE (rom.sha1 IS NOT NULL)");

			DataTable diskTable = Database.ExecuteFill(Globals.Database._SoftwareConnection,
				"SELECT softwarelist.name AS softwarelist_name, software.name AS software_name, disk.sha1 " +
				"FROM (((softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) " +
				"INNER JOIN part ON software.software_id = part.software_id) INNER JOIN diskarea ON part.part_Id = diskarea.part_Id) " +
				"INNER JOIN disk ON diskarea.diskarea_id = disk.diskarea_id " +
				"WHERE (disk.sha1 IS NOT NULL)");


			DataTable table = Tools.MakeDataTable(
				"Status	ListName	ListDescription	SoftwareName	SoftwareDescription	Complete	RomCount	DiskCount	RomHave	DiskHave",
				"String	String		String			String			String				Boolean		Int64		Int64		Int64	Int64");

			foreach (DataRow softwarelistRow in softwarelistTable.Rows)
			{
				string softwarelist_name = (string)softwarelistRow["name"];
				string softwarelist_description = (string)softwarelistRow["description"];

				foreach (DataRow softwareRow in softwareTable.Select($"softwarelist_name = '{softwarelist_name}'"))
				{
					string software_name = (string)softwareRow["name"];
					string software_description = (string)softwareRow["description"];

					DataRow[] romRows = romTable.Select($"softwarelist_name = '{softwarelist_name}' AND software_name = '{software_name}'");
					
					DataRow[] diskRows = diskTable.Select($"softwarelist_name = '{softwarelist_name}' AND software_name = '{software_name}'");
					
					int romHaveCount = 0;
					foreach (DataRow romRow in romRows)
					{
						string sha1 = (string)romRow["sha1"];
						if (Globals.RomHashStore.Exists(sha1) == true)
							++romHaveCount;
					}

					int diskHaveCount = 0;
					foreach (DataRow diskRow in diskRows)
					{
						string sha1 = (string)diskRow["sha1"];
						if (Globals.DiskHashStore.Exists(sha1) == true)
							++diskHaveCount;
					}


					bool complete = romRows.Length == romHaveCount && diskRows.Length == diskHaveCount;

					if (complete == true)
						table.Rows.Add("", softwarelist_name, softwarelist_description, software_name, software_description, complete, romRows.Length, diskRows.Length, romHaveCount, diskHaveCount);
				}
			}

			DataSet dataSet = new DataSet();

			DataView view;
			DataTable viewTable;

			view = new DataView(table);
			view.RowFilter = "DiskCount > 0";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "DISK Software";
			dataSet.Tables.Add(viewTable);

			view = new DataView(table);
			view.RowFilter = "DiskCount = 0";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "ROM Software";
			dataSet.Tables.Add(viewTable);

			this.SaveHtmlReport(dataSet, "Avaliable Software ROM and DISK");

		}

		public void Report_IMSLM()
		{
			DataTable machineListsTable = Database.ExecuteFill(Globals.Database._MachineConnection,
				"SELECT machine.name AS machine_name, machine.description, softwarelist.name AS softwarelist_name FROM machine INNER JOIN softwarelist ON machine.machine_id = softwarelist.machine_id ORDER BY machine.name, softwarelist.name");

			DataTable softwareListsTable = Database.ExecuteFill(Globals.Database._SoftwareConnection,
				"SELECT softwarelist.name AS softwarelist_name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");

			softwareListsTable.PrimaryKey = new DataColumn[] { softwareListsTable.Columns["softwarelist_name"] };

			DataTable table = machineListsTable.Clone();

			foreach (DataRow machineListRow in machineListsTable.Rows)
			{
				string softwarelist_name = (string)machineListRow["softwarelist_name"];

				if (softwareListsTable.Rows.Find(softwarelist_name) == null)
					table.ImportRow(machineListRow);
			}

			table.TableName = "Machines with Missing Lists";

			DataSet dataSet = new DataSet();
			dataSet.Tables.Add(table);

			this.SaveHtmlReport(dataSet, "Machines Software Lists Missing");
		}

		public void Report_ISLWM()
		{
			DataTable softwareListsTable = Database.ExecuteFill(Globals.Database._SoftwareConnection,
				"SELECT softwarelist.name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");

			DataTable machinesListsTable = Database.ExecuteFill(Globals.Database._MachineConnection,
				"SELECT softwarelist.name FROM softwarelist GROUP BY softwarelist.name ORDER BY softwarelist.name");

			machinesListsTable.PrimaryKey = new DataColumn[] { machinesListsTable.Columns["name"] };

			DataSet dataSet = new DataSet();
			DataTable table;
			
			table = softwareListsTable.Clone();

			foreach (DataRow row in softwareListsTable.Rows)
			{
				string name = (string)row["name"];

				if (machinesListsTable.Rows.Find(name) == null)
					table.ImportRow(row);
			}

			table.TableName = "MAME -listsoftware output";
			dataSet.Tables.Add(table);


			table = softwareListsTable.Clone();

			foreach (string filename in Directory.GetFiles(Path.Combine(Globals.MameDirectory, "hash"), "*.xml"))
			{
				string name = Path.GetFileNameWithoutExtension(filename);

				if (machinesListsTable.Rows.Find(name) != null)
					continue;

				XElement document = XElement.Load(filename);

				string rootElementName = document.Name.LocalName;

				if (rootElementName != "softwarelist")
					continue;

				table.Rows.Add(document.Attribute("name").Value ?? "", document.Attribute("description").Value ?? "");
			}

			table.TableName = "MAME hash directory";
			dataSet.Tables.Add(table);

			this.SaveHtmlReport(dataSet, "Software Lists without Machines");

		}

	}
}
