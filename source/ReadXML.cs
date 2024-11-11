using System;
using System.Collections.Generic;
using System.Data;
using System.Xml.Linq;

namespace mame_ao.source
{
	public class ReadXML
	{
		private static readonly HashSet<string> RequiredMachineTables = new HashSet<string>(new string[] {
			"mame",
			"machine",
			"rom",
			"device_ref",
			"sample",
			//"chip",
			//"display",
			//"sound",
			"input",
			//"control",
			//"dipswitch",
			//"diplocation",
			//"dipvalue",
			//"port",
			"driver",
			"feature",
			//"biosset",
			//"dipswitch_condition",
			//"analog",
			//"configuration",
			//"confsetting",
			//"device",
			//"instance",
			//"extension",
			//"slot",
			"softwarelist",
			//"dipvalue_condition",
			//"slotoption",
			//"adjuster",
			"disk",
			//"ramoption",
			//"configuration_condition",
			//"confsetting_condition",
			//"conflocation",
		});

		private static readonly HashSet<string> RequiredSoftwareTables = new HashSet<string>(new string[] {
			"softwarelists",
			"softwarelist",
			"software",
			//"info",
			"part",
			//"feature",
			"dataarea",
			"rom",
			"sharedfeat",
			"diskarea",
			"disk",
		});

		public static DataSet ImportXML(XElement document)
		{
			string rootElementName = document.Name.LocalName;

			HashSet<string> keepTables;

			switch (rootElementName)
			{
				case "mame":
					keepTables = RequiredMachineTables;
					break;

				case "softwarelists":
					keepTables = RequiredSoftwareTables;
					break;

				default:
					throw new ApplicationException($"Unknown XML root element: {rootElementName}");
			}

			DataSet dataSet = new DataSet();

			ImportXMLWork(document, dataSet, null, keepTables);

			return dataSet;
		}
		public static void ImportXMLWork(XElement element, DataSet dataSet, DataRow parentRow, HashSet<string> keepTables)
		{
			string tableName = element.Name.LocalName;

			if (tableName == "condition")
				tableName = $"{parentRow.Table.TableName}_{tableName}";

			if (keepTables != null && keepTables.Contains(tableName) == false)
				return;

			string forignKeyName = null;
			if (parentRow != null)
				forignKeyName = parentRow.Table.TableName + "_id";

			DataTable table;

			if (dataSet.Tables.Contains(tableName) == false)
			{
				table = new DataTable(tableName);
				DataColumn pkColumn = table.Columns.Add(tableName + "_id", typeof(long));
				pkColumn.AutoIncrement = true;
				pkColumn.AutoIncrementSeed = 1;

				table.PrimaryKey = new DataColumn[] { pkColumn };

				if (parentRow != null)
					table.Columns.Add(forignKeyName, parentRow.Table.Columns[forignKeyName].DataType);

				dataSet.Tables.Add(table);
			}
			else
			{
				table = dataSet.Tables[tableName];
			}

			Dictionary<string, string> rowValues = new Dictionary<string, string>();

			foreach (XAttribute attribute in element.Attributes())
				rowValues.Add(attribute.Name.LocalName, attribute.Value);

			foreach (XElement childElement in element.Elements())
			{
				if (childElement.HasAttributes == false && childElement.HasElements == false)
					rowValues.Add(childElement.Name.LocalName, childElement.Value);
			}

			foreach (string columnName in rowValues.Keys)
			{
				if (table.Columns.Contains(columnName) == false)
					table.Columns.Add(columnName, typeof(string));
			}

			DataRow row = table.NewRow();

			if (parentRow != null)
				row[forignKeyName] = parentRow[forignKeyName];

			foreach (string columnName in rowValues.Keys)
				row[columnName] = rowValues[columnName];

			table.Rows.Add(row);

			foreach (XElement childElement in element.Elements())
			{
				if (childElement.HasAttributes == true || childElement.HasElements == true)
					ImportXMLWork(childElement, dataSet, row, keepTables);
			}
		}
	}
}
