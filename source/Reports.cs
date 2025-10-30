using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

using System.Data.SQLite;
using Newtonsoft.Json.Linq;

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
				Key = "available",
				Text = "Available & Missing Machines & Software",
				Decription = "Report on Machines & Software that are available, already downloaded & missing.",
			},
			new ReportGroup(){
				Key = "source-exists",
				Text = "Assets Exists on BitTorrent & Archive.org",
				Decription = "Check that files exist in asset source. Tests are NOT performed to check if ZIPs, ROMs, or CHDs are valid.",
			},
			new ReportGroup(){
				Key = "interesting",
				Text = "Interesting MAME Data",
				Decription = "Report on interesting things of MAME Data.",
			},
		};

		public static ReportType[] ReportTypes = new ReportType[] {
			
			//

			new ReportType(){
				Key = "summary",
				Group = "available",
				Code = "AVSUM",
				Text = "Summary",
				Decription = "Summary of store completeness.",
			},
			new ReportType(){
				Key = "software-rom-list",
				Group = "available",
				Code = "AVSRL",
				Text = "Software ROM by List",
				Decription = "Software ROM by List, view completeness of each software list.",
			},
			new ReportType(){
				Key = "software-disk-list",
				Group = "available",
				Code = "AVSDL",
				Text = "Software Disk by List",
				Decription = "Software Disk by List, view completeness of each software list.",
			},
			new ReportType(){
				Key = "machine",
				Group = "available",
				Code = "AVM",
				Text = "Machines",
				Decription = "Machine Available & Missing lists.",
			},
			new ReportType(){
				Key = "software",
				Group = "available",
				Code = "AVS",
				Text = "Software",
				Decription = "Software Available & Missing lists.",
			},

			//

			new ReportType(){
				Key = "torrent",
				Group = "source-exists",
				Code = "SET",
				Text = "BitTorrent Exists",
				Decription = "Check files exist on BitTorrent and provide sizes.",
			},
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

			//

			new ReportType(){
				Key = "machine-softwarelists-exist",
				Group = "interesting",
				Code = "IMSLM",
				Text = "Machine Lists Missing",
				Decription = "Machines Software Lists Missing.",
			},
			new ReportType(){
				Key = "softwarelists-without-machines",
				Group = "interesting",
				Code = "ISLWM",
				Text = "Software Lists no Machines",
				Decription = "Software Lists without Machines.",
			},
			new ReportType(){
				Key = "software-shared-features",
				Group = "interesting",
				Code = "ISSF",
				Text = "Software Shared Features",
				Decription = "Software with Shared Features.",
			},
			new ReportType(){
				Key = "software-mixed-media",
				Group = "interesting",
				Code = "ISMM",
				Text = "Software Mixed Media",
				Decription = "Software with Mixed Media.",
			},
			new ReportType(){
				Key = "silly-names",
				Group = "interesting",
				Code = "SILLY",
				Text = "Silly Names",
				Decription = "ROM & DISK Silly Names.",
			},

		};

		private static readonly string _HtmlReportStyle = @"
			body {
				font-family: sans-serif;
				font-size: small;
				background-color: #c6eafb;
			}
			hr {
				color: #00ADEF;
				background-color: #00ADEF;
				height: 6px;
				border: none;
				padding-left: 0px;
			}
			table {
				border-collapse: collapse;
			}
			th, td {
				padding: 2px;
				text-align: left;
			}
			table, th, td {
				border: 1px solid black;
			}
			th {
				background-color: #00ADEF;
				color: white;
			}
			tr:nth-child(even) {
				background-color: #b6daeb;
			}
		";

		public Reports()
		{
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
			SaveHtmlReport(table, title, false);
		}
		public void SaveHtmlReport(DataTable table, string title, bool keepIds)
		{
			DataSet dataSet = new DataSet();
			dataSet.Tables.Add(table);
			SaveHtmlReport(dataSet, title, keepIds);
		}

		public void SaveHtmlReport(DataSet dataSet, string title)
		{
			SaveHtmlReport(dataSet, title, false);
		}

		public void SaveHtmlReport(DataSet dataSet, string title, bool keepIds)
		{
			string name = DateTime.Now.ToString("s").Replace(":", "-") + "_" + Tools.ValidFileName(title);

			StringBuilder html = new StringBuilder();

			html.AppendLine("<!DOCTYPE html>");
			html.AppendLine("<html lang=\"en\">");
			html.AppendLine("<head>");
			html.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"/>");
			html.AppendLine($"<title>{title}</title>");
			html.AppendLine("<style type=\"text/css\">" + _HtmlReportStyle + "</style>");
			html.AppendLine("</head>");
			html.AppendLine($"<h1>{title}</h1>");

			for (int index = 0; index < dataSet.Tables.Count; ++index)
			{
				DataTable table = dataSet.Tables[index];

				html.AppendLine("<hr />");
				if (table.TableName.StartsWith("Table") == false)
					html.AppendLine($"<h2>{table.TableName} ({table.Rows.Count})</h2>");
				html.AppendLine(MakeHtmlTable(table, table.Rows.OfType<DataRow>(), "width:100%;", keepIds));
			}

			string footerInfo = $"MAME-AO {Globals.AssemblyVersion} - {Globals.Core.Name} {Globals.Core.Version} - {name}";

			html.AppendLine("<hr />");
			html.AppendLine($"<p style=\"width:100%;\">{footerInfo}<span style=\"float:right\"><a href=\"https://github.com/sam-ludlow/mame-ao\">Spludlow MAME-AO</a></span></p>");
			html.AppendLine("</html>");

			string filename = Path.Combine(Globals.ReportDirectory, name + ".htm");

			File.WriteAllText(filename, html.ToString(), Encoding.UTF8);

			Console.WriteLine();
			Console.WriteLine($"HTML Report saved \"{title}\" : {filename}");
			Console.WriteLine();

			if (Globals.WebServer != null)
				Process.Start($"http://localhost:12380/api/report?name={Path.GetFileNameWithoutExtension(filename)}");
			else
				Process.Start(filename);
		}

		public static string MakeHtmlTable(DataTable table, string tableStyle)
		{
			return MakeHtmlTable(table, table.Rows.OfType<DataRow>(), tableStyle);
		}

		public static string MakeHtmlTable(DataTable table, IEnumerable<DataRow> rows, string tableStyle)
		{
			return MakeHtmlTable(table, rows, tableStyle, false);
		}

		public static string MakeHtmlTable(DataTable table, IEnumerable<DataRow> rows, string tableStyle, bool keepIds)
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
				if (keepIds == false && column.ColumnName.EndsWith("_id") == true)
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
					if (keepIds == false && column.ColumnName.EndsWith("_id") == true)
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
			string filename = Path.Combine(Globals.ReportDirectory, reportName + ".htm");

			if (File.Exists(filename) == false)
				throw new ApplicationException($"Report does no exist: {reportName}");

			return File.ReadAllText(filename, Encoding.UTF8);
		}

		public string[] ListReports()
		{
			List<string> items = new List<string>();
			
			if (Directory.Exists(Globals.ReportDirectory) == true)
			{
				foreach (string filename in Directory.GetFiles(Globals.ReportDirectory))
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

		public static DataSet PlaceReportTemplate()
		{
			DataSet dataSet = new DataSet();

			dataSet.Tables.Add(Tools.MakeDataTable(
				"Require",
				"When		AssetType	Key1	Key2	sha1	require	name",
				"DateTime	String		String	String	String	Boolean	String"
			));

			dataSet.Tables.Add(Tools.MakeDataTable(
				"Download",
				"When		AssetType	Key1	Key2	url		size	seconds",
				"DateTime	String		String	String	String	Int64	Int64"
			));

			dataSet.Tables.Add(Tools.MakeDataTable(
				"Import",
				"When		AssetType	Key1	Key2	sha1	required	imported	name",
				"DateTime	String		String	String	String	Boolean		Boolean		String"
			));

			dataSet.Tables.Add(Tools.MakeDataTable(
				"Place",
				"When		AssetType	Key1	Key2	sha1	place	have	name",
				"DateTime	String		String	String	String	Boolean	Boolean	String"
			));

			foreach (DataTable table in dataSet.Tables)
				table.RowChanged += ReportTable_RowChanged;

			return dataSet;
		}
		private static void ReportTable_RowChanged(object sender, DataRowChangeEventArgs e)
		{
			if (e.Action != DataRowAction.Add)
				return;

			StringBuilder text = new StringBuilder();

			text.Append($"{e.Row.Table}:");

			foreach (DataColumn column in e.Row.Table.Columns)
			{
				text.Append("\t");

				if (e.Row.IsNull(column) == false)
					text.Append(Convert.ToString(e.Row[column]));
			}

			Console.WriteLine(text.ToString());
		}

		public void Report_SET()
		{
			DataSet dataSet = new DataSet();

			DataTable softwareListTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1], "SELECT softwarelist.name, softwarelist.description FROM softwarelist");
			softwareListTable.PrimaryKey = new DataColumn[] { softwareListTable.Columns["name"] };

			DataTable totalsTable = Tools.MakeDataTable("Totals",
				"Type	Hash	FileCount	Size	SizeText",
				"String	String	Int32		Int64	String");
			dataSet.Tables.Add(totalsTable);

			var torrentHashes = BitTorrent.TorrentHashes();

			foreach (string type in torrentHashes.Keys)
			{
				string hash = torrentHashes[type];

				JArray files = BitTorrent.Files(hash);

				long totalSize = 0;

				foreach (dynamic file in files)
				{
					long length = (long)file.length;
					totalSize += length;
				}

				totalsTable.Rows.Add(type.ToString(), hash, files.Count, totalSize, Tools.DataSize(totalSize));

				switch (type)
				{
					case "SoftwareRom":
					case "SoftwareDisk":

						SortedDictionary<string, long[]> lists = new SortedDictionary<string, long[]>();

						foreach (dynamic file in files)
						{
							string path = (string)file.path;
							long length = (long)file.length;

							string[] pathsParts = path.Split('\\');

							string listName = pathsParts[0];

							if (lists.ContainsKey(listName) == false)
								lists.Add(listName, new long[2]);

							lists[listName][0] += 1;
							lists[listName][1] += length;
						}

						DataTable table = Tools.MakeDataTable($"{type} Totals",
							"Name	Description	FileCount	Size	SizeText",
							"String	String		String		Int64	String");

						foreach (string listName in lists.Keys)
						{
							DataRow listRow = softwareListTable.Rows.Find(listName);
							string description = listRow != null ? (string)listRow["description"] : "";

							table.Rows.Add(listName, description, lists[listName][0], lists[listName][1], Tools.DataSize(lists[listName][1]));
						}

						DataView view = new DataView(table);
						view.Sort = "Size DESC";
						dataSet.Tables.Add(Tools.DataTableFromView(view, table.TableName));

						break;
				}
			}

			SaveHtmlReport(dataSet, "BitTorrent Exists");
		}
		public void Report_SEMR()
		{
			ArchiveOrgItem sourceItem = Globals.ArchiveOrgItems[ItemType.MachineRom][0];

			DataTable machineTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[0],
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

			view = new DataView(table)
			{
				RowFilter = "Status = 'MISSING'"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "Missing in Source"));

			view = new DataView(table)
			{
				RowFilter = "Status = ''"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "Available in Source"));

			SaveHtmlReport(dataSet, "Source Exists Machine Rom");
		}

		public void Report_SEMD()
		{
			ArchiveOrgItem sourceItem = Globals.ArchiveOrgItems[ItemType.MachineDisk][0];

			DataTable machineTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[0], "SELECT machine_id, name, description, romof FROM machine ORDER BY machine.name");
			DataTable diskTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[0], "SELECT machine_id, sha1, name, merge FROM disk WHERE sha1 IS NOT NULL");

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

					ArchiveOrgFile sourceFile = MachineDiskAvailableSourceFile(machineRow, diskRow, sourceItem);

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

			view = new DataView(table)
			{
				RowFilter = "Status = 'MISSING'"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "Missing in Source"));

			view = new DataView(table)
			{
				RowFilter = "Status = ''"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "Source Exists Machine Disk"));

			SaveHtmlReport(dataSet, "Source Exists Machine Disk");
		}
		private static ArchiveOrgFile MachineDiskAvailableSourceFile(DataRow machineRow, DataRow diskRow, ArchiveOrgItem sourceItem)
		{
			string machineName = Tools.DataRowValue(machineRow, "name");

			string diskName = Tools.DataRowValue(diskRow, "name");
			string merge = Tools.DataRowValue(diskRow, "merge");

			List<string> machineNames = new List<string>(new string[] { machineName });

			DataRow currentRow = machineRow;
			while (currentRow.IsNull("romof") == false)
			{
				string romof = (string)currentRow["romof"];
				machineNames.Add(romof);

				currentRow = Globals.Core.GetMachine(romof);
			}

			string availableDiskName = diskName;

			if (merge != null)
				availableDiskName = merge;

			foreach (string availableMachineName in machineNames)
			{
				string key = $"{availableMachineName}/{availableDiskName}";

				ArchiveOrgFile file = sourceItem.GetFile(key);

				if (file != null)
					return file;
			}

			return null;
		}

		public void Report_SESR()
		{
			ArchiveOrgItem sourceItem = Globals.ArchiveOrgItems[ItemType.SoftwareRom][0];

			DataTable softwareListTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1],
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

			DataSet dataSet = new DataSet();
			DataView view;

			view = new DataView(table)
			{
				RowFilter = "Status = 'MISSING'"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "Missing in Source"));

			view = new DataView(table)
			{
				RowFilter = "Status = ''"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "Source Exists Software Rom"));

			SaveHtmlReport(dataSet, "Source Exists Software Rom");
		}

		public void Report_SESD()
		{
			ArchiveOrgItem[] sourceItems = Globals.ArchiveOrgItems[ItemType.SoftwareDisk];

			DataTable softwareDiskTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1],
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
				string status = softwareDiskRow.IsNull("status") == false ? (string)softwareDiskRow["status"] : "unkown";

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

			view = new DataView(table)
			{
				RowFilter = "Status = 'MISSING'"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "Missing in Source"));

			view = new DataView(table)
			{
				RowFilter = "Status = ''"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "Available in Source"));

			SaveHtmlReport(dataSet, "Source Exists Software Disk");
		}

		public void Report_SEMS()
		{
			Globals.Samples.Initialize();

			DataSet dataSet = new DataSet();

			DataTable machineTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[0],
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

			SaveHtmlReport(dataSet, "Source Exists Machine Samples");
		}

		public void Report_SEMA()
		{
			DataSet dataSet = new DataSet();

			ArchiveOrgItem item = Globals.ArchiveOrgItems[ItemType.Support][0];

			foreach (ArtworkTypes artworkType in Enum.GetValues(typeof(ArtworkTypes)))
			{
				Globals.Artwork.Initialize(artworkType);

				if (Globals.Artwork.ArtworkDatas[artworkType].DataSet != null)
				{
					DataTable artworkMachinesTable = Tools.MakeDataTable($"Artwork Machines: {artworkType}",
						"Status		name	size",
						"String		String	Int64");
					dataSet.Tables.Add(artworkMachinesTable);

					string fileKey = $"{artworkType}/{artworkType}";
					ArchiveOrgFile file = item.GetFile(fileKey);
					if (file == null)
					{
						artworkMachinesTable.Rows.Add("MAIN ZIP MISSING", fileKey);
						continue;
					}
					
					Dictionary<string, long> zipSizes = item.GetZipContentsSizes(file, 0, 4);

					foreach (DataRow artworkMachineRow in Globals.Artwork.ArtworkDatas[artworkType].DataSet.Tables["machine"].Rows)
					{
						string name = (string)artworkMachineRow["name"];

						if (zipSizes.ContainsKey(name) == true)
							artworkMachinesTable.Rows.Add("", name, zipSizes[name]);
						else
							artworkMachinesTable.Rows.Add("ZIP MISSING", name, 0);
					}
				}
			}

			DataSet resultDataSet = new DataSet();

			for (int pass = 0; pass < 2; ++pass)
			{
				foreach (DataTable table in dataSet.Tables)
				{
					DataView view;

					if (pass == 0)
					{
						view = new DataView(table)
						{
							RowFilter = "Status <> ''"
						};
						resultDataSet.Tables.Add(Tools.DataTableFromView(view, $"{table.TableName} Bad Status"));
					}
					else
					{
						view = new DataView(table)
						{
							RowFilter = "Status = ''"
						};
						resultDataSet.Tables.Add(Tools.DataTableFromView(view, table.TableName));
					}
				}
			}

			SaveHtmlReport(resultDataSet, "Source Exists Machine Artwork");
		}

		public void Report_AVM()
		{
			DataTable machineTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[0], "SELECT machine_id, name, description, romof, cloneof FROM machine ORDER BY machine.name");
			DataTable romTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[0], "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");
			DataTable diskTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[0], "SELECT machine_id, sha1, name, merge FROM disk WHERE sha1 IS NOT NULL");

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

			DataSet dataSet;
			DataView view;

			dataSet = new DataSet();

			view = new DataView(table)
			{
				RowFilter = "DiskCount > 0 AND Complete = 1"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "With DISK"));

			view = new DataView(table)
			{
				RowFilter = "DiskCount = 0 AND Complete = 1"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "Without DISK"));

			SaveHtmlReport(dataSet, "Avaliable Machine ROM and DISK");


			dataSet = new DataSet();

			view = new DataView(table)
			{
				RowFilter = "DiskCount > 0 AND Complete = 0"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "Missing With DISK"));

			view = new DataView(table)
			{
				RowFilter = "DiskCount = 0 AND Complete = 0"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "Missing Without DISK"));

			SaveHtmlReport(dataSet, "Missing Machine ROM and DISK");
		}

		public void Report_AVS()
		{
			DataTable softwarelistTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1],
				"SELECT softwarelist.name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");

			DataTable softwareTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1],
				"SELECT softwarelist.name AS softwarelist_name, software.name, software.description FROM softwarelist " +
				"INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id ORDER BY softwarelist.name, software.name");

			DataTable romTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1],
				"SELECT softwarelist.name AS softwarelist_name, software.name AS software_name, rom.sha1 " +
				"FROM (((softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) " +
				"INNER JOIN part ON software.software_id = part.software_id) INNER JOIN dataarea ON part.part_id = dataarea.part_id) " +
				"INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id " +
				"WHERE (rom.sha1 IS NOT NULL)");

			DataTable diskTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1],
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

					table.Rows.Add("", softwarelist_name, softwarelist_description, software_name, software_description, complete, romRows.Length, diskRows.Length, romHaveCount, diskHaveCount);
				}
			}

			DataSet dataSet;
			DataView view;

			dataSet = new DataSet();

			view = new DataView(table)
			{
				RowFilter = "DiskCount > 0 AND Complete = 1"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "DISK Software"));

			view = new DataView(table)
			{
				RowFilter = "DiskCount = 0 AND Complete = 1"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "ROM Software"));

			SaveHtmlReport(dataSet, "Avaliable Software ROM and DISK");


			dataSet = new DataSet();

			view = new DataView(table)
			{
				RowFilter = "DiskCount > 0 AND Complete = 0"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "DISK Software"));

			view = new DataView(table)
			{
				RowFilter = "DiskCount = 0 AND Complete = 0"
			};
			dataSet.Tables.Add(Tools.DataTableFromView(view, "ROM Software"));

			SaveHtmlReport(dataSet, "Missing Software ROM and DISK");
		}

		public void Report_AVSUM()
		{
			List<string> names = new List<string>(new string[] {
				"Machine Rom",
				"Machine Disk",
				"Software Rom",
				"Software Disk",
				"Artworks",
				"Artworks Alt",
				"Artworks Wide Screen",
				"Samples",
			});

			List<HashStore> hashStores = new List<HashStore>(new HashStore[] {
				Globals.RomHashStore,
				Globals.DiskHashStore,
				Globals.RomHashStore,
				Globals.DiskHashStore,
				Globals.RomHashStore,
				Globals.RomHashStore,
				Globals.RomHashStore,
				Globals.RomHashStore,
			});

			foreach (ArtworkTypes type in new ArtworkTypes[] { ArtworkTypes.Artworks, ArtworkTypes.ArtworksAlt, ArtworkTypes.ArtworksWideScreen })
				Globals.Artwork.Initialize(type);

			Globals.Samples.Initialize();

			List<HashSet<string>> databaseHashes = new List<HashSet<string>>();

			databaseHashes.Add(new HashSet<string>(Database.ExecuteFill(Globals.Core.ConnectionStrings[0], "SELECT [sha1] FROM [rom] WHERE [sha1] IS NOT NULL").Rows.Cast<DataRow>().Select(row => (string)row["sha1"])));
			if (Globals.Core.Name == "mame")
				databaseHashes.Add(new HashSet<string>(Database.ExecuteFill(Globals.Core.ConnectionStrings[0], "SELECT [sha1] FROM [disk] WHERE [sha1] IS NOT NULL").Rows.Cast<DataRow>().Select(row => (string)row["sha1"])));
			databaseHashes.Add(new HashSet<string>(Database.ExecuteFill(Globals.Core.ConnectionStrings[1], "SELECT [sha1] FROM [rom] WHERE [sha1] IS NOT NULL").Rows.Cast<DataRow>().Select(row => (string)row["sha1"])));
			if (Globals.Core.Name == "mame")
				databaseHashes.Add(new HashSet<string>(Database.ExecuteFill(Globals.Core.ConnectionStrings[1], "SELECT [sha1] FROM [disk] WHERE [sha1] IS NOT NULL").Rows.Cast<DataRow>().Select(row => (string)row["sha1"])));
			databaseHashes.Add(new HashSet<string>(Globals.Artwork.ArtworkDatas[ArtworkTypes.Artworks].DataSet.Tables["rom"].Rows.Cast<DataRow>().Where(row => row.IsNull("sha1") == false).Select(row => (string)row["sha1"])));
			databaseHashes.Add(new HashSet<string>(Globals.Artwork.ArtworkDatas[ArtworkTypes.ArtworksAlt].DataSet.Tables["rom"].Rows.Cast<DataRow>().Where(row => row.IsNull("sha1") == false).Select(row => (string)row["sha1"])));
			databaseHashes.Add(new HashSet<string>(Globals.Artwork.ArtworkDatas[ArtworkTypes.ArtworksWideScreen].DataSet.Tables["rom"].Rows.Cast<DataRow>().Where(row => row.IsNull("sha1") == false).Select(row => (string)row["sha1"])));
			databaseHashes.Add(new HashSet<string>(Globals.Samples.DataSet.Tables["rom"].Rows.Cast<DataRow>().Where(row => row.IsNull("sha1") == false).Select(row => (string)row["sha1"])));

			if (Globals.Core.Name == "hbmame")
			{
				foreach (int removeIndex in new int[] { 3, 1 })
				{
					names.RemoveAt(removeIndex);
					hashStores.RemoveAt(removeIndex);
				}
			}

			DataTable table = Tools.MakeDataTable("Summary",
				"Asset Type	Total	Have	Missing	Complete",
				"String		Int32	Int32	Int32	String"
			);

			for (int index = 0; index < names.Count; ++index)
			{
				string name = names[index];
				HashSet<string> databaseHash = databaseHashes[index];
				HashStore hashStore = hashStores[index];

				HashSet<string> missingHashes = new HashSet<string>();
				foreach (string sha1 in databaseHash)
				{
					if (hashStore.Exists(sha1) == false)
						missingHashes.Add(sha1);
				}

				int have = databaseHash.Count - missingHashes.Count;

				decimal complete = Math.Round((100.0M / databaseHash.Count) * (databaseHash.Count - missingHashes.Count), 3);

				table.Rows.Add(name, databaseHash.Count, have, missingHashes.Count, $"{complete} %");
			}

			SaveHtmlReport(table, "Summary of all store completeness");
		}

		public void Report_AVSRL()
		{
			DataTable softwarelistTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1], @"
				SELECT softwarelist.softwarelist_id, softwarelist.name, softwarelist.description, COUNT(rom.rom_id) AS rom_count
				FROM (((softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) INNER JOIN part ON software.software_id = part.software_id) INNER JOIN dataarea ON part.part_id = dataarea.part_id) INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id
				WHERE (rom.sha1 IS NOT NULL)
				GROUP BY softwarelist.softwarelist_Id, softwarelist.name, softwarelist.description
				ORDER BY softwarelist.name
			");
			DataTable softwareTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1], @"
				SELECT software.software_id, software.softwarelist_id, software.name, software.description, COUNT(rom.rom_Id) AS rom_count
				FROM ((software INNER JOIN part ON software.software_id = part.software_id) INNER JOIN dataarea ON part.part_id = dataarea.part_id) INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id
				WHERE (rom.sha1 IS NOT NULL)
				GROUP BY software.software_Id, software.softwarelist_Id, software.name, software.description
				ORDER BY software.softwarelist_id, software.name
			");
			DataTable romTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1], @"
				SELECT software.software_Id, software.softwarelist_id, rom.rom_id, rom.name, rom.size, rom.sha1
				FROM ((software INNER JOIN part ON software.software_id = part.software_id) INNER JOIN dataarea ON part.part_id = dataarea.part_id) INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id
				WHERE (rom.sha1 IS NOT NULL)
				ORDER BY software.software_id, software.softwarelist_id, rom.name
			");

			DataTable resultTable = Tools.MakeDataTable("Software Lists",
				"SoftwareList	Description	TotalRom	HaveRom		NeedRom	TotalBytes	TotalSize	HaveBytes	HaveSize	Complete	Machines",
				"String			String		Int32		Int32		Int32	Int64		String		Int64		String		Decimal		String");

			Dictionary<string, List<string>> softwareListMachines = GetSoftwareListMachines();

			foreach (DataRow softwareListRow in softwarelistTable.Rows)
			{
				long softwarelist_id = (long)softwareListRow["softwarelist_id"];
				string softwarelist_name = (string)softwareListRow["name"];
				string softwarelist_description = (string)softwareListRow["description"];

				DataRow[] romRows = romTable.Select($"softwarelist_id = {softwarelist_id}");

				int totalRom = 0;
				int haveRom = 0;
				int needRom = 0;

				long totalBytes = 0;
				long haveBytes = 0;
				long needBytes = 0;

				foreach (DataRow romRow in romRows)
				{
					string sizeText = ((string)romRow["size"]).Trim().ToLower();
					long size = sizeText.StartsWith("0x") == true ? Int64.Parse(sizeText.Substring(2), System.Globalization.NumberStyles.HexNumber) : Int64.Parse(sizeText);

					string sha1 = (string)romRow["sha1"];

					++totalRom;
					totalBytes += size;

					if (Globals.RomHashStore.Exists(sha1) == true)
					{
						++haveRom;
						haveBytes += size;
					}
					else
					{
						++needRom;
						needBytes += size;
					}
				}

				decimal complete = (decimal)haveRom / totalRom * 100.0M;

				string machines = softwareListMachines.ContainsKey(softwarelist_name) == true ? String.Join(", ", softwareListMachines[softwarelist_name]) : "";

				resultTable.Rows.Add(softwarelist_name, softwarelist_description, totalRom, haveRom, needRom, totalBytes, Tools.DataSize(totalBytes), haveBytes, Tools.DataSize(haveBytes), Math.Round(complete, 3), machines);
			}

			SaveHtmlReport(resultTable, "Software ROM by List");
		}

		public void Report_AVSDL()
		{
			if (Globals.Core.Name == "hbmame")
			{
				Console.WriteLine("!!! No disk reports for HBMAME.");
				return;
			}

			DataTable softwarelistTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1],
				"SELECT softwarelist.softwarelist_id, softwarelist.name, softwarelist.description, COUNT(disk.disk_id) AS disk_count " +
				"FROM (((softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) INNER JOIN part ON software.software_id = part.software_id) INNER JOIN diskarea ON part.part_id = diskarea.part_id) INNER JOIN disk ON diskarea.diskarea_id = disk.diskarea_id " +
				"WHERE (disk.sha1 IS NOT NULL) " +
				"GROUP BY softwarelist.softwarelist_id, softwarelist.name, softwarelist.description " +
				"ORDER BY softwarelist.name");

			DataTable softwareTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1],
				"SELECT software.software_id, software.softwarelist_id, software.name, software.cloneof, software.description, COUNT(disk.disk_id) AS disk_count " +
				"FROM ((software INNER JOIN part ON software.software_id = part.software_id) INNER JOIN diskarea ON part.part_id = diskarea.part_id) INNER JOIN disk ON diskarea.diskarea_id = disk.diskarea_id " +
				"WHERE (disk.sha1 IS NOT NULL) " +
				"GROUP BY software.software_id, software.softwarelist_id, software.name, software.cloneof, software.description " +
				"ORDER BY software.softwarelist_id, software.name");

			DataTable diskTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1],
				"SELECT software.software_id, software.softwarelist_id, disk.name, disk.sha1 " +
				"FROM ((software INNER JOIN part ON software.software_id = part.software_id) INNER JOIN diskarea ON part.part_id = diskarea.part_id) INNER JOIN disk ON diskarea.diskarea_id = disk.diskarea_id " +
				"WHERE (disk.sha1 IS NOT NULL) " +
				"ORDER BY software.software_id, software.softwarelist_id, disk.name");

			DataTable resultTable = Tools.MakeDataTable("Software Lists",
				"SoftwareList	Description	DiskCount	DiskHave	DiskNeed	DiskDup	HaveBytes	HaveSize	Complete	ProjectedBytes	ProjectedSize	Machines",
				"String			String		Int32		Int32		Int32		Int32	Int64		String		Decimal		Int64			String			String");

			Dictionary<string, List<string>> softwareListMachines = GetSoftwareListMachines();

			foreach (DataRow softwareListRow in softwarelistTable.Rows)
			{
				long softwarelist_id = (long)softwareListRow["softwarelist_id"];
				string softwarelist_name = (string)softwareListRow["name"];
				string softwarelist_description = (string)softwareListRow["description"];

				DataRow[] diskRows = diskTable.Select($"softwarelist_id = {softwarelist_id}");

				HashSet<string> diskHashes = new HashSet<string>(diskRows.Select(i => (string)i["sha1"]));

				int diskCount = diskHashes.Count;

				int foundCount = 0;
				int missingCount = 0;
				int dupCount = diskRows.Length - diskCount;
				long foundBytes = 0;

				foreach (string sha1 in diskHashes)
				{
					if (Globals.DiskHashStore.Exists(sha1) == true)
					{
						foundBytes += new FileInfo(Globals.DiskHashStore.Filename(sha1)).Length;
						++foundCount;
					}
					else
					{
						++missingCount;
					}
				}

				decimal complete = (decimal)foundCount / diskCount * 100.0M;

				long projectedBytes = 0;
				if (foundBytes > 0)
					projectedBytes = (long)((decimal)foundBytes / (decimal)foundCount * (decimal)diskCount);

				string machines = softwareListMachines.ContainsKey(softwarelist_name) == true ? String.Join(", ", softwareListMachines[softwarelist_name]) : "";

				resultTable.Rows.Add(softwarelist_name, softwarelist_description, diskCount, foundCount, missingCount, dupCount, foundBytes, Tools.DataSize(foundBytes), Math.Round(complete, 3), projectedBytes, Tools.DataSize(projectedBytes), machines);
			}

			SaveHtmlReport(resultTable, "Software Disk by List");
		}

		public static Dictionary<string, List<string>> GetSoftwareListMachines()
		{
			DataTable listMachineTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[0], @"
				SELECT softwarelist.name, machine.name FROM machine INNER JOIN softwarelist ON machine.machine_id = softwarelist.machine_id ORDER BY softwarelist.name, machine.name
			");

			Dictionary<string, List<string>> softwareListMachines = new Dictionary<string, List<string>>();
			foreach (DataRow row in listMachineTable.Rows)
			{
				string softwarelist = (string)row[0];
				string machine = (string)row[1];
				if (softwareListMachines.ContainsKey(softwarelist) == false)
					softwareListMachines.Add(softwarelist, new List<string>());
				softwareListMachines[softwarelist].Add(machine);
			}

			return softwareListMachines;
		}

		public void Report_IMSLM()
		{
			DataTable machineListsTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[0],
				"SELECT machine.name AS machine_name, machine.description, softwarelist.name AS softwarelist_name FROM machine INNER JOIN softwarelist ON machine.machine_id = softwarelist.machine_id ORDER BY machine.name, softwarelist.name");

			DataTable softwareListsTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1],
				"SELECT softwarelist.name AS softwarelist_name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");

			softwareListsTable.PrimaryKey = new DataColumn[] { softwareListsTable.Columns["softwarelist_name"] };

			DataTable table = machineListsTable.Clone();

			foreach (DataRow machineListRow in machineListsTable.Rows)
			{
				string softwarelist_name = (string)machineListRow["softwarelist_name"];

				if (softwareListsTable.Rows.Find(softwarelist_name) == null)
					table.ImportRow(machineListRow);
			}

			SaveHtmlReport(table, "Machine's Software Lists Missing");
		}

		public void Report_ISLWM()
		{
			DataTable softwareListsTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1],
				"SELECT softwarelist.name, softwarelist.description FROM softwarelist ORDER BY softwarelist.name");

			DataTable machinesListsTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[0],
				"SELECT softwarelist.name FROM softwarelist GROUP BY softwarelist.name ORDER BY softwarelist.name");

			machinesListsTable.PrimaryKey = new DataColumn[] { machinesListsTable.Columns["name"] };

			DataTable table = softwareListsTable.Clone();

			foreach (DataRow softwareListRow in softwareListsTable.Rows)
			{
				string name = (string)softwareListRow["name"];

				if (machinesListsTable.Rows.Find(name) == null)
					table.Rows.Add(name, (string)softwareListRow["description"]);
			}

			SaveHtmlReport(table, "Software Lists without Machines");
		}

		public void Report_ISSF()
		{
			DataTable table = Database.ExecuteFill(Globals.Core.ConnectionStrings[1], @"
				SELECT softwarelist.softwarelist_id, softwarelist.name AS softwarelist_name, softwarelist.description AS softwarelist_description,
				software.software_id, software.name AS software_name, software.description AS software_description,
				sharedfeat.name, sharedfeat.value
				FROM (softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) INNER JOIN sharedfeat ON software.software_id = sharedfeat.software_id
				ORDER BY softwarelist.name, software.name, sharedfeat.name, sharedfeat.value
			");

			DataTable softwareTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1], @"
				SELECT softwarelist.name AS softwarelist_name, softwarelist.description AS softwarelist_description, software.name AS software_name, software.description AS software_description
				FROM softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id
				ORDER BY softwarelist.name, software.name;
			");
			softwareTable.PrimaryKey = new DataColumn[] { softwareTable.Columns["softwarelist_name"], softwareTable.Columns["software_name"] };

			DataTable machineSoftwareListTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[0], @"
				SELECT machine.name AS machine_name, softwarelist.name AS softwarelist_name, softwarelist.status
				FROM machine INNER JOIN softwarelist ON machine.machine_id = softwarelist.machine_id
				ORDER BY machine.name, softwarelist.name, softwarelist.status DESC;
			");

			DataTable softwarePartTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1], @"
				SELECT softwarelist.name AS softwarelist_name, software.name AS software_name, part.name AS part_name, part.interface
				FROM (softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) INNER JOIN part ON software.software_id = part.software_id
				ORDER BY softwarelist.name, software.name, part.name;
			");

			DataSet dataSet = new DataSet();

			foreach (string sharedfeatName in table.Rows.Cast<DataRow>().Select(row => (string)row["name"]).Distinct().OrderBy(name => name))
			{
				DataView view = new DataView(table);
				view.RowFilter = $"[name] = '{sharedfeatName}'";
				dataSet.Tables.Add(Tools.DataTableFromView(view, sharedfeatName));
			}

			SaveHtmlReport(dataSet, "Software with Shared Features");

			table = dataSet.Tables["requirement"];
			table.DataSet.Tables.Remove(table);

			table.Columns.Add("status", typeof(string));

			table.Columns.Add("machine_names", typeof(string));
			table.Columns.Add("part_names", typeof(string));
			table.Columns.Add("short_names", typeof(string));

			table.Columns.Add("require_softwarelist_name", typeof(string));
			table.Columns.Add("require_softwarelist_description", typeof(string));
			table.Columns.Add("require_software_name", typeof(string));
			table.Columns.Add("require_software_description", typeof(string));

			foreach (DataRow row in table.Rows)
			{
				string software_name = (string)row["software_name"];
				string softwarelist_name = (string)row["softwarelist_name"];

				string[] parts = ((string)row["value"]).Split(':');

				string require_softwarelist_name = null;
				string require_software_name = parts[parts.Length - 1];
				if (parts.Length > 1)
				{
					require_softwarelist_name = parts[0];
					if (softwareTable.Rows.Find(new object[] { require_softwarelist_name, require_software_name }) == null)
					{
						Console.WriteLine($"!!! Could not find required software from sharedfeat.value (ignoring list): {software_name}, {softwarelist_name} => {require_softwarelist_name}, {require_software_name}");
						require_softwarelist_name = null;
					}
				}

				DataRow requireSoftwareRow;

				if (require_softwarelist_name != null)
				{
					requireSoftwareRow = softwareTable.Rows.Find(new object[] { require_softwarelist_name, require_software_name });

					if (requireSoftwareRow != null)
						row["status"] = "- Found with 2 part value";
					else
						row["status"] = "NOT Found with 2 part value";	//	<<< dont hapern - becuase of above
				}
				else
				{
					requireSoftwareRow = softwareTable.Rows.Find(new object[] { softwarelist_name, require_software_name });

					if (requireSoftwareRow != null)
						row["status"] = "- Found with 1 part value in current software list";
					else
						row["status"] = "NOT Found with 1 part value in current software list";
				}

				if (requireSoftwareRow == null)
				{
					var machineNames = machineSoftwareListTable.Select($"[softwarelist_name] = '{softwarelist_name}' AND [status] = 'original'").Cast<DataRow>().Select(r => (string)r["machine_name"]);

					row["machine_names"] = String.Join(", ", machineNames);

					List<string> allMachineSoftwareLists = new List<string>();
					foreach (string machineName in machineNames)	//	Will have machine name when placing
					{
						foreach (string listName in machineSoftwareListTable.Select($"[machine_name] = '{machineName}'").Cast<DataRow>().Select(r => (string)r["softwarelist_name"]))
						{
							if (allMachineSoftwareLists.Contains(listName) ==  false)
								allMachineSoftwareLists.Add(listName);
						}
					}

					List<DataRow> foundRequireSoftwareRows = new List<DataRow>();
					foreach (string listName in allMachineSoftwareLists)
					{
						requireSoftwareRow = softwareTable.Rows.Find(new object[] { listName, require_software_name });
						if (requireSoftwareRow != null)
							foundRequireSoftwareRows.Add(requireSoftwareRow);
					}

					// Assume first only affects pce/tg16 will have the machine at place time anyway
					requireSoftwareRow = null;
					if (foundRequireSoftwareRows.Count > 0)
						requireSoftwareRow = foundRequireSoftwareRows[0];

					row["status"] = $"count:{foundRequireSoftwareRows.Count}";


					row["require_softwarelist_name"] = String.Join(", ", foundRequireSoftwareRows.Select(r => $"{(string)r["softwarelist_name"]}:{(string)r["software_name"]}"));

				}

				if (requireSoftwareRow == null)
				{
					row["status"] = "NOT FOUND";
					continue;
				}
				row["status"] = "";

				row["require_softwarelist_name"] = (string)requireSoftwareRow["softwarelist_name"];
				row["require_softwarelist_description"] = (string)requireSoftwareRow["softwarelist_description"];
				row["require_software_name"] = (string)requireSoftwareRow["software_name"];
				row["require_software_description"] = (string)requireSoftwareRow["software_description"];

				require_softwarelist_name = (string)requireSoftwareRow["softwarelist_name"];

				List<string> partNames = new List<string>();
				List<string> shortNames = new List<string>();

				DataRow[] partRows = softwarePartTable.Select($"softwarelist_name = '{require_softwarelist_name}' AND software_name = '{require_software_name}'");

				foreach (string partName in partRows.Select(r => (string)r["part_name"]))
				{
					string shortName = partName;
					while (Char.IsDigit(shortName[shortName.Length - 1]) == true)
						shortName = shortName.Substring(0, shortName.Length - 1);

					partNames.Add($"{require_softwarelist_name}/{require_software_name}/{partName}/{shortName}");

					if (shortNames.Contains(shortName) == false)
						shortNames.Add(shortName);
				}

				row["part_names"] = String.Join(", ", partNames);
				row["short_names"] = String.Join(", ", shortNames);

				if (shortNames.Count > 1)
					row["status"] = "MIXED";

			}

			SaveHtmlReport(table, "Software with Shared Features (requirement)");
		}

		public void Report_ISMM()
		{
			DataTable table = Database.ExecuteFill(Globals.Core.ConnectionStrings[1], @"
				SELECT softwarelist.name AS softwarelist_name, softwarelist.description AS softwarelist_description,
				software.software_id, software.name, software.cloneof, software.description
				FROM softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id
				ORDER BY softwarelist.name, software.name;
			");
			DataTable partTable = Database.ExecuteFill(Globals.Core.ConnectionStrings[1], @"
				SELECT part.* FROM part ORDER BY part.software_id, part.name, part.interface;
			");

			table.Columns.Add("interface_count", typeof(int));
			table.Columns.Add("interfaces", typeof(string));

			foreach (DataRow softwareRow in table.Rows)
			{
				long software_id = (long)softwareRow["software_id"];
				DataRow[] partRows = partTable.Select($"software_id = {software_id}");

				var interfaces = partRows.Select(row => (string)row["interface"]).Distinct();

				softwareRow["interface_count"] = interfaces.Count();
				softwareRow["interfaces"] = String.Join(", ", interfaces);
			}

			DataView view = new DataView(table);
			view.RowFilter = $"[interface_count] <> 1";

			DataTable viewTable = Tools.DataTableFromView(view, table.TableName);

			SaveHtmlReport(viewTable, "Software with Mixed Media");
		}

		public void Report_SILLY()
		{
			string[][] details = new string[][] {
				new string[] {
					"Machine ROM",
					Globals.Core.ConnectionStrings[0],
					"SELECT machine.name AS machine_name, rom.name FROM machine INNER JOIN rom ON machine.machine_id = rom.machine_id WHERE (rom.name IS NOT NULL) ORDER BY machine.name, rom.name",
				},
				new string[] {
					"Machine DISK",
					Globals.Core.ConnectionStrings[0],
					"SELECT machine.name AS machine_name, disk.name FROM machine INNER JOIN DISK ON machine.machine_id = disk.machine_id WHERE (disk.name IS NOT NULL) ORDER BY machine.name, disk.name",
				},
				new string[] {
					"Software ROM",
					Globals.Core.ConnectionStrings[1],
					"SELECT softwarelist.name AS softwarelist_name, software.name AS software_name, rom.name FROM (((softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) INNER JOIN part ON software.software_id = part.software_id) INNER JOIN dataarea ON part.part_id = dataarea.part_id) INNER JOIN rom ON dataarea.dataarea_id = rom.dataarea_id WHERE (rom.name IS NOT NULL) ORDER BY softwarelist.name, software.name, rom.name",
				},
				new string[] {
					"Software DISK",
					Globals.Core.ConnectionStrings[1],
					"SELECT softwarelist.name AS softwarelist_name, software.name AS software_name, disk.name FROM (((softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id) INNER JOIN part ON software.software_id = part.software_id) INNER JOIN diskarea ON part.part_id = diskarea.part_id) INNER JOIN DISK ON diskarea.diskarea_id = disk.diskarea_id WHERE (disk.name IS NOT NULL) ORDER BY softwarelist.name, software.name, disk.name",
				},
			};

			DataSet dataSet = new DataSet();

			foreach (string[] detail in details)
			{
				string title = detail[0];
				string connectionString = detail[1];
				string commandText = detail[2];

				DataTable sourceTable;
				using (SQLiteConnection connection = new SQLiteConnection(connectionString))
				{
					sourceTable = Database.ExecuteFill(connection, commandText);
				}
				DataTable table = sourceTable.Clone();
				table.TableName = title;
				table.Columns.Add("Issues", typeof(string));

				foreach (DataRow row in sourceTable.Rows)
				{
					string name = (string)row["name"];

					List<string> issues = new List<string>();

					if (name.Length > 128)
						issues.Add("Length");

					if (Tools.IsASCII(name) == false)
						issues.Add("ASCII");

					if (Tools.ValidFileName(name) != name)
						issues.Add("Name");

					if (issues.Count > 0)
					{
						table.ImportRow(row);
						table.Rows[table.Rows.Count - 1]["Issues"] = String.Join(", ", issues);
					}
				}

				dataSet.Tables.Add(table);
			}

			SaveHtmlReport(dataSet, "Silly Names");
		}
	}
}
