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
		public class ReportContext
		{
			public Database database;
			public HashStore romHashStore;
			public HashStore diskHashStore;
			public string versionDirectory;
		}
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
		public Reports(string outputDirectory)
		{
			_OutputDirectory = outputDirectory;
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
			string name = DateTime.Now.ToString("s").Replace(":", "-") + "_" + title;

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
						html.Append(WebUtility.HtmlEncode(Convert.ToString(row[column])));
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

		public bool RunReport(string reportCode, ReportContext context)
		{
			reportCode = reportCode.ToUpper();

			MethodInfo method = this.GetType().GetMethod($"Report_{reportCode}");

			if (method == null)
				return false;

			try
			{
				Console.Write($"Running Report please wait {reportCode} ...");
				method.Invoke(this, new object[] { context });
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


		public void Report_SEMR(ReportContext context)
		{
			Sources.MameSourceSet soureSet = Sources.GetSourceSets(Sources.MameSetType.MachineRom)[0];

			DataTable machineTable = Database.ExecuteFill(context.database._MachineConnection,
				"SELECT machine_id, name, description, ao_rom_count FROM machine WHERE (ao_rom_count > 0 AND romof IS NULL) ORDER BY machine.name");

			DataTable table = Tools.MakeDataTable(
				"Status	Machine	RomCount	Filename	Size	FileSHA1	ModifiedTime",
				"String	String	Int64		String		Int64	String		DateTime");

			foreach (DataRow machineRow in machineTable.Rows)
			{
				string machineName = (string)machineRow["name"];
				long romCount = (long)machineRow["ao_rom_count"];

				DataRow row = table.Rows.Add("", machineName, romCount);

				if (soureSet.AvailableDownloadFileInfos.ContainsKey(machineName) == true)
				{
					Sources.SourceFileInfo sourceFile = soureSet.AvailableDownloadFileInfos[machineName];

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

			DataSet dataSet = new DataSet();

			DataView view;
			DataTable viewTable;

			view = new DataView(table);
			view.RowFilter = $"Status = 'MISSING'";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Missing in Source";
			dataSet.Tables.Add(viewTable);

			view = new DataView(table);
			view.RowFilter = $"Status = ''";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Available in Source";
			dataSet.Tables.Add(viewTable);


			this.SaveHtmlReport(dataSet, "Source Exists Machine Rom");
		}

		public void Report_SEMD(ReportContext context)
		{
			Sources.MameSourceSet soureSet = Sources.GetSourceSets(Sources.MameSetType.MachineDisk)[0];

			DataTable machineTable = Database.ExecuteFill(context.database._MachineConnection, "SELECT machine_id, name, description, romof FROM machine ORDER BY machine.name");
			DataTable diskTable = Database.ExecuteFill(context.database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM disk WHERE sha1 IS NOT NULL");

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

					Sources.SourceFileInfo sourceFile = MameAOProcessor.MachineDiskAvailableSourceFile(machineRow, diskRow, soureSet, context.database);

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
			view.RowFilter = $"Status = 'MISSING'";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Missing in Source";
			dataSet.Tables.Add(viewTable);

			view = new DataView(table);
			view.RowFilter = $"Status = ''";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Available in Source";
			dataSet.Tables.Add(viewTable);

			this.SaveHtmlReport(dataSet, "Source Exists Machine Disk");
		}

		public void Report_SESR(ReportContext context)
		{
			Sources.MameSourceSet soureSet = Sources.GetSourceSets(Sources.MameSetType.SoftwareRom)[0];

			DataTable softwareListTable = Database.ExecuteFill(context.database._SoftwareConnection,
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

				Sources.SourceFileInfo sourceFile = soureSet.AvailableDownloadFileInfos[name];

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

		public void Report_SESD(ReportContext context)
		{
			Sources.MameSourceSet[] soureSets = Sources.GetSourceSets(Sources.MameSetType.SoftwareDisk);

			DataTable softwareDiskTable = Database.ExecuteFill(context.database._SoftwareConnection,
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

				List<Sources.MameSourceSet> foundSourceSets = new List<Sources.MameSourceSet>();
				List<Sources.SourceFileInfo> foundFileInfos = new List<Sources.SourceFileInfo>();

				foreach (Sources.MameSourceSet sourceSet in soureSets)
				{
					string key = $"{softwarelist_name}/{software_name}/{name}";

					if (sourceSet.ListName != null && sourceSet.ListName != "*")
						key = $"{software_name}/{name}";

					if (sourceSet.AvailableDownloadFileInfos.ContainsKey(key) == true)
					{
						foundSourceSets.Add(sourceSet);
						foundFileInfos.Add(sourceSet.AvailableDownloadFileInfos[key]);
					}
				}

				if (foundSourceSets.Count > 0)
				{
					row["Sources"] = String.Join(", ", foundSourceSets.Select(set => set.ListName).ToArray());

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
			view.RowFilter = $"Status = 'MISSING'";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Missing in Source";
			dataSet.Tables.Add(viewTable);

			view = new DataView(table);
			view.RowFilter = $"Status = ''";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Available in Source";
			dataSet.Tables.Add(viewTable);

			this.SaveHtmlReport(dataSet, "Source Exists Software Disk");
		}

		public void Report_AVM(ReportContext context)
		{
			DataTable machineTable = Database.ExecuteFill(context.database._MachineConnection, "SELECT machine_id, name, description, romof, cloneof FROM machine ORDER BY machine.name");
			DataTable romTable = Database.ExecuteFill(context.database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");
			DataTable diskTable = Database.ExecuteFill(context.database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM disk WHERE sha1 IS NOT NULL");

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
					if (context.romHashStore.Exists(sha1) == true)
						++romHaveCount;
				}

				int diskHaveCount = 0;
				foreach (DataRow diskRow in diskRows)
				{
					string sha1 = (string)diskRow["sha1"];
					if (context.diskHashStore.Exists(sha1) == true)
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
			view.RowFilter = $"DiskCount > 0 AND Complete = 1";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "With DISK";
			dataSet.Tables.Add(viewTable);

			view = new DataView(table);
			view.RowFilter = $"DiskCount = 0 AND Complete = 1";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Without DISK";
			dataSet.Tables.Add(viewTable);

			this.SaveHtmlReport(dataSet, "Avaliable Machine ROM and DISK");
		}

		public void Report_AVS(ReportContext context)
		{
			DataTable softwarelistTable = Database.ExecuteFill(context.database._SoftwareConnection,
				"SELECT softwarelist.name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");

			DataTable softwareTable = Database.ExecuteFill(context.database._SoftwareConnection,
				"SELECT softwarelist.name AS softwarelist_name, software.name, software.description FROM softwarelist " +
				"INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id ORDER BY softwarelist.name, software.name");

			DataTable romTable = Database.ExecuteFill(context.database._SoftwareConnection,
				"SELECT softwarelist.name AS softwarelist_name, software.name AS software_name, rom.sha1 " +
				"FROM (((softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) " +
				"INNER JOIN part ON software.software_id = part.software_id) INNER JOIN dataarea ON part.part_id = dataarea.part_id) " +
				"INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id " +
				"WHERE (rom.sha1 IS NOT NULL)");

			DataTable diskTable = Database.ExecuteFill(context.database._SoftwareConnection,
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
						if (context.romHashStore.Exists(sha1) == true)
							++romHaveCount;
					}

					int diskHaveCount = 0;
					foreach (DataRow diskRow in diskRows)
					{
						string sha1 = (string)diskRow["sha1"];
						if (context.diskHashStore.Exists(sha1) == true)
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
			view.RowFilter = $"DiskCount > 0";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "DISK Software";
			dataSet.Tables.Add(viewTable);

			view = new DataView(table);
			view.RowFilter = $"DiskCount = 0";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "ROM Software";
			dataSet.Tables.Add(viewTable);

			this.SaveHtmlReport(dataSet, "Avaliable Software ROM and DISK");

		}

		public void Report_IMSLM(ReportContext context)
		{
			DataTable machineListsTable = Database.ExecuteFill(context.database._MachineConnection,
				"SELECT machine.name AS machine_name, machine.description, softwarelist.name AS softwarelist_name FROM machine INNER JOIN softwarelist ON machine.machine_id = softwarelist.machine_id ORDER BY machine.name, softwarelist.name");

			DataTable softwareListsTable = Database.ExecuteFill(context.database._SoftwareConnection,
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

		public void Report_ISLWM(ReportContext context)
		{
			DataTable softwareListsTable = Database.ExecuteFill(context.database._SoftwareConnection,
				"SELECT softwarelist.name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");

			DataTable machinesListsTable = Database.ExecuteFill(context.database._MachineConnection,
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

			foreach (string filename in Directory.GetFiles(Path.Combine(context.versionDirectory, "hash"), "*.xml"))
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
