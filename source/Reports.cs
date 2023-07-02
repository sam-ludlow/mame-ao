using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;

namespace Spludlow.MameAO
{
	public class Reports
	{
		private string _OutputDirectory;
		public Reports(string outputDirectory)
		{
			_OutputDirectory = outputDirectory;
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
	}
}
