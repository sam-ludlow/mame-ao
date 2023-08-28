using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

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
				Decription = "Check that files exist in the archive.org sources.",
			},
			new ReportGroup(){
				Key = "file-size",
				Text = "File Size",
				Decription = "Report om ROM & DISK sizes. ",
			},
		};

		public static ReportType[] ReportTypes = new ReportType[] {
			new ReportType(){
				Key = "machine-rom",
				Group = "source-exists",
				Code = "SEMR",
				Text = "Source Exists - Machine Rom",
				Decription = "Source Exists - Machine Rom",
			},
			new ReportType(){
				Key = "machine-disk",
				Group = "source-exists",
				Code = "SEMD",
				Text = "Source Exists - Machine Disk",
				Decription = "Source Exists - Machine Disk",
			},
			new ReportType(){
				Key = "software-rom",
				Group = "source-exists",
				Code = "SESR",
				Text = "Source Exists - Software Rom",
				Decription = "Source Exists - Software Rom",
			},
			new ReportType(){
				Key = "software-disk",
				Group = "source-exists",
				Code = "SESD",
				Text = "Source Exists - Software Disk",
				Decription = "Source Exists - Software Disk",
			},

			new ReportType(){
				Key = "machine",
				Group = "file-size",
				Code = "FSM",
				Text = "File Size - Machine",
				Decription = "File Size - Machine",
			},
			new ReportType(){
				Key = "software",
				Group = "file-size",
				Code = "FSS",
				Text = "File Size - Software",
				Decription = "File Size - Software",
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

			foreach (ReportType reportType in ReportTypes)
				results.Add($"{reportType.Code} - {reportType.Text}");

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
				html.Append("<th>");
				html.Append(column.ColumnName);
				html.Append("</th>");
			}
			html.AppendLine("</tr>");

			foreach (DataRow row in table.Rows)
			{
				html.Append("<tr>");
				foreach (DataColumn column in table.Columns)
				{
					html.Append("<td>");
					if (row.IsNull(column) == false)
						html.Append(Convert.ToString(row[column]));
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

		public bool RunReport(string reportCode, Database database)
		{
			reportCode = reportCode.ToUpper();

			MethodInfo method = this.GetType().GetMethod($"Report_{reportCode}");

			if (method == null)
				return false;

			try
			{
				method.Invoke(this, new object[] { database });
			}
			catch (Exception e)
			{
				Console.WriteLine("REPORT ERROR: " + e.ToString());
				throw;
			}

			return true;
		}


		public void Report_SEMR(Database database)
		{
			Sources.MameSourceSet soureSet = Sources.GetSourceSets(Sources.MameSetType.MachineRom)[0];

			DataTable machineTable = Database.ExecuteFill(database._MachineConnection, "SELECT machine_id, name, description, romof, cloneof FROM machine ORDER BY machine.name");

			DataTable table = Tools.MakeDataTable(
				"Status	Machine	RomOf	CloneOf	Filename	Size	FileSHA1	ModifiedTime",
				"String	String	String	String	String		Int64	String		DateTime");

			foreach (DataRow machineRow in machineTable.Rows)
			{
				string machineName = (string)machineRow["name"];
				string romof = Tools.DataRowValue(machineRow, "romof");
				string cloneof = Tools.DataRowValue(machineRow, "cloneof");

				DataRow row = table.Rows.Add("", machineName, romof, cloneof);

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

			//view = new DataView(table);
			//view.RowFilter = $"Status = 'MISSING'";
			//viewTable = table.Clone();
			//foreach (DataRowView rowView in view)
			//	viewTable.ImportRow(rowView.Row);
			//viewTable.TableName = "Missing in Source";
			//dataSet.Tables.Add(viewTable);

			view = new DataView(table);
			view.RowFilter = $"Status = ''";
			viewTable = table.Clone();
			foreach (DataRowView rowView in view)
				viewTable.ImportRow(rowView.Row);
			viewTable.TableName = "Available in Source";
			dataSet.Tables.Add(viewTable);


			this.SaveHtmlReport(dataSet, "Source Exists Machine Rom");
		}

		public void Report_SEMD(Database database)
		{
			Sources.MameSourceSet soureSet = Sources.GetSourceSets(Sources.MameSetType.MachineDisk)[0];

			DataTable machineTable = Database.ExecuteFill(database._MachineConnection, "SELECT machine_id, name, description, romof FROM machine ORDER BY machine.name");
			DataTable diskTable = Database.ExecuteFill(database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM disk WHERE sha1 IS NOT NULL");

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

					Sources.SourceFileInfo sourceFile = MameAOProcessor.MachineDiskAvailableSourceFile(machineRow, diskRow, soureSet, database);

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






		public void Report_SESR(Database database)
		{
			DataTable table = Tools.MakeDataTable(
				"Status",
				"String");

			table.Rows.Add("TODO");

			table.TableName = "The report is coming soon";

			DataSet dataSet = new DataSet();
			dataSet.Tables.Add(table);

			this.SaveHtmlReport(dataSet, "Source Exists Software Rom");
		}

		public void Report_SESD(Database database)
		{
			DataTable table = Tools.MakeDataTable(
				"Status",
				"String");

			table.Rows.Add("TODO");

			table.TableName = "The report is coming soon";

			DataSet dataSet = new DataSet();
			dataSet.Tables.Add(table);

			this.SaveHtmlReport(dataSet, "Source Exists Software Disk");
		}


	}
}
