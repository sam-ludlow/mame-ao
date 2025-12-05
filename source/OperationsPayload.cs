using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Newtonsoft.Json.Linq;

namespace Spludlow.MameAO
{
	public class OperationsPayload
	{
		private static readonly XmlReaderSettings _XmlReaderSettings = new XmlReaderSettings()
		{
			DtdProcessing = DtdProcessing.Parse,
			IgnoreComments = false,
			IgnoreWhitespace = true,
		};

		//
		// Common
		//
		public static void CreateMetaDataTable(SqlConnection connection, string coreName, string version, string info)
		{
			string agent = $"mame-ao/{Globals.AssemblyVersion} (https://github.com/sam-ludlow/mame-ao)";

			string tableName = "_metadata";

			string[] columnDefs = new string[] {
				$"[{tableName}_id] BIGINT NOT NULL PRIMARY KEY",
				"[dataset] NVARCHAR(1024) NOT NULL",
				"[subset] NVARCHAR(1024) NOT NULL",
				"[version] NVARCHAR(1024) NOT NULL",
				"[info] NVARCHAR(1024) NOT NULL",
				"[processed] DATETIME NOT NULL",
				"[agent] NVARCHAR(1024) NOT NULL",
			};
			string commandText = $"CREATE TABLE [{tableName}] ({String.Join(", ", columnDefs)});";

			Console.WriteLine(commandText);
			Database.ExecuteNonQuery(connection, commandText);

			DataTable table = Database.ExecuteFill(connection, $"SELECT * FROM [{tableName}] WHERE (0 = 1)");
			table.TableName = tableName;

			table.Rows.Add(1L, coreName, "", version, info, DateTime.Now, agent);

			Database.BulkInsert(connection, table);
		}

		public static DataTable MakePayloadDataTable(string tableName, string[] keyNames)
		{
			string[] columnNames = new string[] { "title", "xml", "json", "html" };
			
			DataTable table = new DataTable(tableName);

			List<DataColumn> pks = new List<DataColumn>();
			foreach (string keyName in keyNames)
				pks.Add(table.Columns.Add(keyName, typeof(string)));

			table.PrimaryKey = pks.ToArray();

			foreach (string columnName in columnNames)
				table.Columns.Add(columnName, typeof(string));

			return table;
		}

		public static void MakeMSSQLPayloadsInsert(SqlConnection connection, DataTable table)
		{
			List<string> columnDefs = new List<string>();
			List<string> pkNames = new List<string>();

			foreach (DataColumn column in table.PrimaryKey)
			{
				int max = 1;
				foreach (DataRow row in table.Rows)
				{
					if (row.IsNull(column) == false)
					{
						int len = ((string)row[column]).Length;
						if (len > max)
							max = len;
					}
				}
				column.MaxLength = max;

				columnDefs.Add($"[{column.ColumnName}] VARCHAR({column.MaxLength})");

				pkNames.Add(column.ColumnName);
			}
			foreach (DataColumn column in table.Columns)
			{
				if (pkNames.Contains(column.ColumnName) == true)
					continue;

				switch (Type.GetTypeCode(column.DataType))
				{
					case TypeCode.Int32:
						columnDefs.Add($"[{column.ColumnName}] [int]");
						break;

					default:
						columnDefs.Add($"[{column.ColumnName}] nvarchar({(column.MaxLength == -1 ? "max" : column.MaxLength.ToString())})");
						break;
				}		
			}

			columnDefs.Add($"CONSTRAINT [PK_{table.TableName}] PRIMARY KEY NONCLUSTERED ([{String.Join("], [", pkNames)}])");

			string commandText = $"CREATE TABLE [{table.TableName}] ({String.Join(", ", columnDefs)});";

			Console.WriteLine(commandText);

			Database.ExecuteNonQuery(connection, commandText);
			Database.BulkInsert(connection, table);
		}

		public static void DeleteExistingPayloadTables(SqlConnection connection)
		{
			foreach (string tableName in Database.TableList(connection))
			{
				if (tableName == "_metadata" || tableName.EndsWith("_payload") == true)
					Database.ExecuteNonQuery(connection, $"DROP TABLE [{tableName}]");
			}
		}

		//
		// MAME
		//

		public static int MameMSSQLPayloads(string directory, string version, string serverConnectionString, string[] databaseNames)
		{
			return MameishMSSQLPayloads(directory, version, serverConnectionString, databaseNames, "mame");
		}
		//
		// HBMAME
		//
		public static int HbMameMSSQLPayloads(string directory, string version, string serverConnectionString, string[] databaseNames)
		{
			return MameishMSSQLPayloads(directory, version, serverConnectionString, databaseNames, "hbmame");
		}

		//
		// MAMEish
		//

		public static int MameishMSSQLPayloads(string directory, string version, string serverConnectionString, string[] databaseNames, string coreName)
		{
			string versionDirectory = Path.Combine(directory, version);

			SqlConnection[] connections = new SqlConnection[]
			{
				new SqlConnection(serverConnectionString + $"Database='{databaseNames[0]}';"),
				new SqlConnection(serverConnectionString + $"Database='{databaseNames[1]}';")
			};
			
			string exePath = Path.Combine(versionDirectory, $"{coreName}.exe");
			string exeTime = File.GetLastWriteTime(exePath).ToString("s");

			//MameishMSSQLMachinePayloads(directory, version, connections, coreName, versionDirectory, exeTime);

			//MameishMSSQLSoftwarePayloads(directory, version, connections, coreName, versionDirectory, exeTime);

			MameishMSSQLMachinePayloadsSearch(directory, version, connections, coreName, versionDirectory, exeTime);

			return 0;
		}

		public static void MameishMSSQLMachinePayloadsSearch(string directory, string version, SqlConnection[] connections, string coreName, string versionDirectory, string exeTime)
		{

			//	feature 0-5	TODO
			//	chip ?



			DataTable table;
			string commandText;


			//
			// machine, driver, sound, input
			//
			commandText = @"
SELECT
    machine.name,
    machine.sourcefile,
    machine.sampleof,
    machine.isbios,
    machine.isdevice,
    machine.ismechanical,
    machine.runnable,
    machine.description,
    machine.year,
    machine.manufacturer,
    machine.cloneof,
    machine.romof,
    driver.status,
    driver.emulation,
    driver.savestate,
    driver.requiresartwork,
    driver.unofficial,
    driver.nosoundhardware,
    driver.incomplete,
    driver.cocktail,
    sound.channels,
    input.players,
    input.coins,
    input.service,
    input.tilt
FROM
    (
        (
            machine
            LEFT JOIN driver ON machine.machine_id = driver.machine_id
        )
        LEFT JOIN sound ON machine.machine_id = sound.machine_id
    )
    LEFT JOIN [input] ON machine.machine_id = input.machine_id;
";

			DataTable searchTable = new DataTable("machine_search_payload");
			using (SqlDataAdapter adapter = new SqlDataAdapter(commandText, connections[0]))
				adapter.Fill(searchTable);
			searchTable.PrimaryKey = new DataColumn[] { searchTable.Columns["name"] };

			//
			// display
			//
			commandText = @"
SELECT
    machine.name,
    display.tag,
    display.type,
    display.rotate,
    display.width,
    display.height,
    display.refresh,
    display.pixclock,
    display.htotal,
    display.hbend,
    display.hbstart,
    display.vtotal,
    display.vbend,
    display.vbstart,
    display.flipx
FROM
    machine
    INNER JOIN display ON machine.machine_id = display.machine_id
ORDER BY
    machine.name,
    display.type,
    display.tag;
";

			DataTable displayTable = new DataTable();
			using (SqlDataAdapter adapter = new SqlDataAdapter(commandText, connections[0]))
				adapter.Fill(displayTable);

			commandText = "SELECT [type] FROM [display] GROUP BY [type] ORDER BY [type]";
			table = new DataTable();
			using (SqlDataAdapter adapter = new SqlDataAdapter(commandText, connections[0]))
				adapter.Fill(table);
			List<string> displayTypes = table.Rows.Cast<DataRow>().Select(row => (string)row[0]).ToList();

			foreach (string displayType in displayTypes)
				searchTable.Columns.Add($"{displayType}", typeof(int));

			foreach (DataRow searchRow in searchTable.Rows)
			{
				string name = (string)searchRow["name"];

				Dictionary<string, int> displayTypeCount = new Dictionary<string, int>();

				foreach (DataRow displayRow in displayTable.Select($"[name] = '{name}'"))
				{
					string type = (string)displayRow["type"];

					if (displayTypeCount.ContainsKey(type) == false)
					{
						displayTypeCount.Add(type, 1);
					}
					else
					{
						displayTypeCount[type] += 1;
					}
				}

				foreach (string type in displayTypeCount.Keys)
				{
					searchRow[type] = displayTypeCount[type];
				}
			}

			//
			//	control (input)
			//
			commandText = @"
SELECT
    machine.name,
    control.type,
    control.player,
    control.buttons,
    control.ways,
    control.reverse,
    control.minimum,
    control.maximum,
    control.sensitivity,
    control.keydelta,
    control.ways2,
    control.ways3
FROM
    (
        machine
        INNER JOIN [input] ON machine.machine_id = input.machine_id
    )
    INNER JOIN control ON input.input_id = control.input_id
ORDER BY
    machine.name,
    control.type,
    control.player;
";
			DataTable inputControlTable = new DataTable();
			using (SqlDataAdapter adapter = new SqlDataAdapter(commandText, connections[0]))
				adapter.Fill(inputControlTable);

			commandText = "SELECT [type] FROM [control] GROUP BY [type] ORDER BY [type]";
			table = new DataTable();
			using (SqlDataAdapter adapter = new SqlDataAdapter(commandText, connections[0]))
				adapter.Fill(table);
			List<string> controlTypes = table.Rows.Cast<DataRow>().Select(row => (string)row[0]).ToList();

			foreach (string controlType in controlTypes)
				searchTable.Columns.Add($"{controlType}", typeof(int));

			foreach (DataRow searchRow in searchTable.Rows)
			{
				string name = (string)searchRow["name"];

				Dictionary<string, int> controlTypeButtonCount = new Dictionary<string, int>();

				foreach (DataRow controlRow in inputControlTable.Select($"[name] = '{name}'"))
				{
					string type = (string)controlRow["type"];
					int buttons = controlRow.IsNull("buttons") == true ? 0 : Int32.Parse((string)controlRow["buttons"]);

					if (controlTypeButtonCount.ContainsKey(type) == false)
					{
						controlTypeButtonCount.Add(type, buttons);
					}
					else
					{
						if (controlTypeButtonCount[type] < buttons)
							controlTypeButtonCount[type] = buttons;
					}	
				}

				foreach (string type in controlTypeButtonCount.Keys)
				{
					searchRow[type] = controlTypeButtonCount[type];
				}
			}

			//
			// column lengths for database columns
			//
			foreach (DataColumn column in searchTable.Columns)
			{
				if (column.DataType != typeof(string))
					continue;

				int max = 1;
				foreach (DataRow row in searchTable.Rows)
				{
					if (row.IsNull(column) == false)
					{
						int len = ((string)row[column]).Length;
						if (len > max)
							max = len;
					}
				}
				column.MaxLength = max;
			}

			//
			// Build line payloads
			//
			foreach (string name in new string[] { "xml", "json", "html" })
				searchTable.Columns.Add(name, typeof(string));

			foreach (DataRow row in searchTable.Rows)
			{
				StringBuilder tr = new StringBuilder();
				tr.Append("<tr>");

				foreach (DataColumn column in searchTable.Columns)
				{
					tr.Append("<td>");
					if (row.IsNull(column) == false)
						tr.Append(WebUtility.HtmlEncode(Convert.ToString(row[column])));
					tr.Append("</td>");
				}

				tr.Append("</tr>");
				row["html"] = tr.ToString();
			}

			//
			// Insert database table
			//
			foreach (string tableName in Database.TableList(connections[0]))
			{
				if (tableName == searchTable.TableName)
					Database.ExecuteNonQuery(connections[0], $"DROP TABLE [{tableName}]");
			}

			MakeMSSQLPayloadsInsert(connections[0], searchTable);
		}

		public static void MameishMSSQLMachinePayloads(string directory, string version, SqlConnection[] connections, string coreName, string versionDirectory, string exeTime)
		{
			DeleteExistingPayloadTables(connections[0]);

			//
			// Metadata
			//
			string info;

			int machineCount = (int)Database.ExecuteScalar(connections[0], "SELECT COUNT(*) FROM machine");
			int romCount = (int)Database.ExecuteScalar(connections[0], "SELECT COUNT(*) FROM rom");
			int diskCount = Database.TableExists(connections[0], "disk") == true ? (int)Database.ExecuteScalar(connections[0], "SELECT COUNT(*) FROM disk") : 0;

			info = $"{coreName.ToUpper()}: {version} - Released: {exeTime} - Machines: {machineCount} - rom: {romCount} - disk: {diskCount}";

			CreateMetaDataTable(connections[0], coreName, version, info);

			//
			// JSON/XML
			//
			Dictionary<string, string[]> machine_XmlJsonPayloads = MameishMachineXmlJsonPayloads(Path.Combine(versionDirectory, "_machine.xml"));

			//
			// Source Data
			//
			DataSet dataSet = new DataSet();
			List<string> conditionTableNames = new List<string>();

			foreach (string tableName in Database.TableList(connections[0]))
			{
				if (tableName.EndsWith("_payload") == true || tableName == "sysdiagrams")
					continue;

				using (SqlDataAdapter adapter = new SqlDataAdapter($"SELECT * from [{tableName}]", connections[0]))
					adapter.Fill(dataSet);

				dataSet.Tables[dataSet.Tables.Count - 1].TableName = tableName;

				if (tableName.EndsWith("_condition") == true)
					conditionTableNames.Add(tableName);
			}

			//
			// Source Data - Merge condition tables
			//
			foreach (string conditionTableName in conditionTableNames)
			{
				string parentTableName = conditionTableName.Substring(0, conditionTableName.Length - 10);

				DataTable parentTable = dataSet.Tables[parentTableName];
				DataTable conditionTable = dataSet.Tables[conditionTableName];

				conditionTable.PrimaryKey = new DataColumn[] { conditionTable.Columns[1] };

				foreach (DataColumn column in conditionTable.Columns)
				{
					string newColumnName = $"condition_{column.ColumnName}";
					parentTable.Columns.Add(newColumnName, column.DataType);
				}

				string keyColumnName = parentTable.Columns[0].ColumnName;

				foreach (DataRow parentRow in parentTable.Rows)
				{
					long key = (long)parentRow[0];

					DataRow conditionRow = conditionTable.Rows.Find(key);

					if (conditionRow == null)
						continue;

					foreach (DataColumn column in conditionTable.Columns)
					{
						string newColumnName = $"condition_{column.ColumnName}";
						parentRow[newColumnName] = conditionRow[column];
					}
				}
			}

			//
			// Payloads
			//
			DataTable machine_payload_table = MakePayloadDataTable("machine_payload", new string[] { "machine_name" });

			string[] simpleTableNames = new string[] {
				"machine",
				"display",
				"driver",
				"rom",
				"disk",
				"chip",
				"softwarelist",
				"device_ref",
				"sample",
				"adjuster",
				"biosset",
				"sound",
				"feature",
				"ramoption",
			};

			foreach (DataRow machineRow in dataSet.Tables["machine"].Rows)
			{
				long machine_id = (long)machineRow["machine_id"];
				string machine_name = (string)machineRow["name"];

				//if (machine_name != "bbcb")
				//	continue;

				StringBuilder html = new StringBuilder();

				//
				// Simple joins
				//
				foreach (string tableName in simpleTableNames)
				{
					if (dataSet.Tables.Contains(tableName) == false)
						continue;

					DataTable sourceTable = dataSet.Tables[tableName];

					DataRow[] rows = sourceTable.Select("machine_id = " + machine_id);

					if (rows.Length == 0)
						continue;

					DataTable table = sourceTable.Clone();

					foreach (DataRow row in rows)
					{
						table.ImportRow(row);

						DataRow targetRow = table.Rows[table.Rows.Count - 1];

						switch (tableName)
						{
							case "machine":
								if (targetRow.IsNull("sourcefile") == false)
								{
									string value = (string)targetRow["sourcefile"];

									string baseUrl;
									switch (coreName)
									{
										case "mame":
											baseUrl = $"https://github.com/mamedev/mame/blob/mame{version}/src";

											if (value.Split(new char[] { '/' }).Length == 2 && value.StartsWith("emu/") == false)
												value = $"<a href=\"{baseUrl}/{coreName}/{value}\" target=\"_blank\">{value}</a>";
											else
												value = $"<a href=\"{baseUrl}/{value}\" target=\"_blank\">{value}</a>";
											break;
										case "hbmame":
											baseUrl = $"https://github.com/Robbbert/hbmame/blob/tag{version.Substring(2).Replace(".", "")}/src/hbmame/drivers";

											value = $"<a href=\"{baseUrl}/{value}\" target=\"_blank\">{value}</a>";
											break;

										default:
											throw new ApplicationException($"Unknown core: {coreName}");
									}

									targetRow["sourcefile"] = value;
								}
								if (targetRow.IsNull("romof") == false)
								{
									string value = (string)targetRow["romof"];
									targetRow["romof"] = $"<a href=\"/{coreName}/machine/{value}\">{value}</a>";
								}
								if (targetRow.IsNull("cloneof") == false)
								{
									string value = (string)targetRow["cloneof"];
									targetRow["cloneof"] = $"<a href=\"/{coreName}/machine/{value}\">{value}</a>";
								}
								break;

							case "device_ref":
								if (targetRow.IsNull("name") == false)
								{
									string value = (string)targetRow["name"];
									targetRow["name"] = $"<a href=\"/{coreName}/machine/{value}\">{value}</a>";
								}
								break;


							case "softwarelist":
								if (targetRow.IsNull("name") == false)
								{
									string value = (string)targetRow["name"];
									targetRow["name"] = $"<a href=\"/{coreName}/software/{value}\">{value}</a>";
								}
								break;
						}
					}

					if (tableName == "machine")
					{
						html.AppendLine("<br />");
						html.AppendLine($"<div><h2 style=\"display:inline;\">machine</h2> &bull; <a href=\"{machine_name}.xml\">XML</a> &bull; <a href=\"{machine_name}.json\">JSON</a> &bull; <a href=\"#\" onclick=\"mameAO('{machine_name}@{coreName}'); return false\">RUN</a></div>");
						html.AppendLine("<br />");
					}
					else
					{
						html.AppendLine("<hr />");
						html.AppendLine($"<h2>{tableName}</h2>");
					}

					html.AppendLine(Reports.MakeHtmlTable(table, null));
				}

				DataRow[] deviceRows = dataSet.Tables["device"].Select("machine_id = " + machine_id);
				if (deviceRows.Length > 0)
				{
					//	device, instance
					DataTable table = new DataTable();
					foreach (DataTable columnTable in new DataTable[] { dataSet.Tables["device"], dataSet.Tables["instance"] })
						foreach (DataColumn column in columnTable.Columns)
							if (column.ColumnName.EndsWith("_id") == false)
								table.Columns.Add(column.ColumnName, typeof(string));

					foreach (DataRow deviceRow in deviceRows)
					{
						long device_id = (long)deviceRow["device_id"];

						DataRow[] instanceRows = dataSet.Tables["instance"].Select("device_id = " + device_id);
						foreach (DataRow instanceRow in instanceRows)
						{
							DataRow row = table.NewRow();
							foreach (DataColumn column in deviceRow.Table.Columns)
								if (column.ColumnName.EndsWith("_id") == false)
									row[column.ColumnName] = deviceRow[column.ColumnName];

							foreach (DataColumn column in instanceRow.Table.Columns)
								if (column.ColumnName.EndsWith("_id") == false)
									row[column.ColumnName] = instanceRow[column.ColumnName];
							table.Rows.Add(row);
						}
					}

					if (table.Rows.Count > 0)
					{
						html.AppendLine("<hr />");
						html.AppendLine("<h2>device, instance</h2>");
						html.AppendLine(Reports.MakeHtmlTable(table, null));
					}

					//	device, extension
					table = new DataTable();
					foreach (DataColumn column in dataSet.Tables["device"].Columns)
						if (column.ColumnName.EndsWith("_id") == false)
							table.Columns.Add(column.ColumnName, typeof(string));
					table.Columns.Add("extension_names", typeof(string));

					foreach (DataRow deviceRow in deviceRows)
					{
						long device_id = (long)deviceRow["device_id"];

						DataRow[] extensionRows = dataSet.Tables["extension"].Select("device_id = " + device_id);

						DataRow row = table.NewRow();
						foreach (DataColumn column in deviceRow.Table.Columns)
							if (column.ColumnName.EndsWith("_id") == false)
								row[column.ColumnName] = deviceRow[column.ColumnName];

						row["extension_names"] = String.Join(", ", extensionRows.Select(r => (string)r["name"]));

						table.Rows.Add(row);
					}

					if (table.Rows.Count > 0)
					{
						html.AppendLine("<hr />");
						html.AppendLine("<h2>device, extension</h2>");
						html.AppendLine(Reports.MakeHtmlTable(table, null));
					}
				}

				//
				// input, control
				//
				DataRow[] inputRows = dataSet.Tables["input"].Select("machine_id = " + machine_id);
				if (inputRows.Length > 0)
				{
					if (inputRows.Length != 1)
						throw new ApplicationException("Not one [input] row.");

					long input_id = (long)inputRows[0]["input_id"];

					html.AppendLine("<hr />");
					html.AppendLine("<h2>input</h2>");
					html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["input"], inputRows, null));

					DataRow[] controlRows = dataSet.Tables["control"].Select("input_id = " + input_id);
					if (controlRows.Length > 0)
					{
						html.AppendLine("<h3>control</h3>");
						html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["control"], controlRows, null));
					}
				}

				//
				// port, analog
				//
				DataRow[] portRows = dataSet.Tables["port"].Select("machine_id = " + machine_id);
				if (portRows.Length > 0)
				{
					html.AppendLine("<hr />");
					html.AppendLine("<h2>port, analog</h2>");

					DataTable table = Tools.MakeDataTable(
						"port_tag	analog_masks",
						"String		String"
					);
					foreach (DataRow portRow in portRows)
					{
						long port_id = (long)portRow["port_id"];

						DataRow[] analogRows = dataSet.Tables["analog"].Select("port_id = " + port_id);

						string masks = String.Join(", ", analogRows.Select(row => (string)row["mask"]));

						table.Rows.Add((string)portRow["tag"], masks);
					}

					html.AppendLine(Reports.MakeHtmlTable(table, null));
				}

				//
				// slot, slotoption
				//
				DataRow[] slotRows = dataSet.Tables["slot"].Select("machine_id = " + machine_id);
				if (slotRows.Length > 0)
				{
					html.AppendLine("<hr />");
					html.AppendLine("<h2>slot, slotoption</h2>");

					DataTable table = Tools.MakeDataTable(
						"slot_name	slotoption_name	slotoption_devname	slotoption_default",
						"String		String			String				String"
					);

					foreach (DataRow slotRow in slotRows)
					{
						long slot_id = (long)slotRow["slot_id"];
						DataRow[] slotoptionRows = dataSet.Tables["slotoption"].Select("slot_id = " + slot_id);

						if (slotoptionRows.Length == 0)
							table.Rows.Add(slotRow["name"], null, null, null);

						foreach (DataRow slotoptionRow in slotoptionRows)
						{
							DataRow row = table.Rows.Add(slotRow["name"], slotoptionRow["name"], slotoptionRow["devname"], slotoptionRow["default"]);

							if (row.IsNull("slotoption_devname") == false)
							{
								string value = (string)row["slotoption_devname"];
								row["slotoption_devname"] = $"<a href=\"/{coreName}/machine/{value}\">{value}</a>";
							}
						}
					}

					html.AppendLine(Reports.MakeHtmlTable(table, null));
				}

				//
				// configuration
				//
				DataRow[] configurationRows = dataSet.Tables["configuration"].Select("machine_id = " + machine_id);
				if (configurationRows.Length > 0)
				{
					html.AppendLine("<hr />");
					html.AppendLine("<h2>configuration</h2>");

					foreach (DataRow configurationRow in configurationRows)
					{
						long configuration_id = (long)configurationRow["configuration_id"];

						html.AppendLine("<hr class='px2' />");

						html.AppendLine($"<h3>{(string)configurationRow["name"]}</h3>");

						html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["configuration"], new[] { configurationRow }, null));

						if (dataSet.Tables.Contains("conflocation") == true)
						{
							DataRow[] conflocationRows = dataSet.Tables["conflocation"].Select("configuration_id = " + configuration_id);
							if (conflocationRows.Length > 0)
							{
								html.AppendLine("<h4>location</h4>");

								html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["conflocation"], conflocationRows, null));
							}
						}

						DataRow[] confsettingRows = dataSet.Tables["confsetting"].Select("configuration_id = " + configuration_id);
						if (confsettingRows.Length > 0)
						{
							html.AppendLine("<h4>setting</h4>");

							html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["confsetting"], confsettingRows, null));
						}

					}
				}

				//
				// dipswitch
				//
				DataRow[] dipswitchRows = dataSet.Tables["dipswitch"].Select("machine_id = " + machine_id);
				if (dipswitchRows.Length > 0)
				{
					html.AppendLine("<hr />");
					html.AppendLine("<h2>dipswitch</h2>");

					foreach (DataRow dipswitchRow in dipswitchRows)
					{
						long dipswitch_id = (long)dipswitchRow["dipswitch_id"];

						html.AppendLine("<hr class='px2' />");

						html.AppendLine($"<h3>{(string)dipswitchRow["name"]}</h3>");

						html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["dipswitch"], new[] { dipswitchRow }, null));

						DataRow[] diplocationRows = dataSet.Tables["diplocation"].Select("dipswitch_id = " + dipswitch_id);
						if (diplocationRows.Length > 0)
						{
							html.AppendLine("<h4>location</h4>");

							html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["diplocation"], diplocationRows, null));
						}

						DataRow[] dipvalueRows = dataSet.Tables["dipvalue"].Select("dipswitch_id = " + dipswitch_id);
						if (dipvalueRows.Length > 0)
						{
							html.AppendLine("<h4>value</h4>");

							html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["dipvalue"], dipvalueRows, null));
						}
					}
				}

				string[] xmlJson = machine_XmlJsonPayloads[machine_name];

				string title = $"{(string)machineRow["description"]} - {coreName} ({version}) machine";

				machine_payload_table.Rows.Add(machine_name, title, xmlJson[0], xmlJson[1], html.ToString());
			}

			MakeMSSQLPayloadsInsert(connections[0], machine_payload_table);

			Tools.ConsolePrintMemory();
		}

		public static void MameishMSSQLSoftwarePayloads(string directory, string version, SqlConnection[] connections, string coreName, string versionDirectory, string exeTime)
		{
			DeleteExistingPayloadTables(connections[1]);

			bool usingDisk = Database.TableExists(connections[1], "disk");

			//
			//	CHD Sizes
			//
			Dictionary<string, long> torrentDiskSizes = new Dictionary<string, long>();
			if (usingDisk == true)
			{
				Globals.GitHubRepos.Add("dome-bt", new GitHubRepo("sam-ludlow", "dome-bt"));

				Globals.BitTorrentDirectory = Path.Combine(Globals.RootDirectory, "_BT");
				Directory.CreateDirectory(Globals.BitTorrentDirectory);

				BitTorrent.Initialize();
				BitTorrent.WaitReady();

				var torrentHashes = BitTorrent.TorrentHashes();
				string torrentHash = torrentHashes["SoftwareDisk"];

				JArray torrentFiles = BitTorrent.Files(torrentHash);

				foreach (dynamic torrentFile in torrentFiles)
					torrentDiskSizes.Add((string)torrentFile.path, (long)torrentFile.length);

				BitTorrent.Stop();
			}

			//
			// Metadata
			//
			string info;

			int softwarelistCount = (int)Database.ExecuteScalar(connections[1], "SELECT COUNT(*) FROM softwarelist");
			int softwareCount = (int)Database.ExecuteScalar(connections[1], "SELECT COUNT(*) FROM software");
			int softRomCount = (int)Database.ExecuteScalar(connections[1], "SELECT COUNT(*) FROM rom");
			int softDiskCount = usingDisk == true ? (int)Database.ExecuteScalar(connections[1], "SELECT COUNT(*) FROM disk") : 0;

			info = $"{coreName.ToUpper()}: {version} - Released: {exeTime} - Lists: {softwarelistCount} - Software: {softwareCount} - rom: {softRomCount} - disk: {softDiskCount}";

			CreateMetaDataTable(connections[1], coreName, version, info);

			//
			// JSON/XML
			//
			Dictionary<string, string[]>[] payloads = MameishSoftwareXmlJsonPayloads(Path.Combine(versionDirectory, "_software.xml"));
			Dictionary<string, string[]> softwarelist_XmlJsonPayloads = payloads[0];
			Dictionary<string, string[]> software_XmlJsonPayloads = payloads[1];

			//
			// Source Data
			//
			DataSet dataSet = new DataSet();

			foreach (string tableName in Database.TableList(connections[1]))
			{
				if (tableName.EndsWith("_payload") == true || tableName == "sysdiagrams")
					continue;

				using (SqlDataAdapter adapter = new SqlDataAdapter($"SELECT * from [{tableName}]", connections[1]))
					adapter.Fill(dataSet);

				dataSet.Tables[dataSet.Tables.Count - 1].TableName = tableName;
			}

			DataTable machineListTable = Database.ExecuteFill(connections[0], "SELECT machine.name AS machine_name, driver.status, softwarelist.name AS softwarelist_name " +
				"FROM (machine LEFT JOIN driver ON machine.machine_id = driver.machine_id) INNER JOIN softwarelist ON machine.machine_id = softwarelist.machine_id");

			foreach (DataRow row in machineListTable.Rows)
			{
				if (row.IsNull("status") == true)
					row["status"] = "no driver";
			}

			DataTable machineDetailTable = Database.ExecuteFill(connections[0], "SELECT machine.name, machine.description FROM machine");
			machineDetailTable.PrimaryKey = new DataColumn[] { machineDetailTable.Columns["name"] };

			//
			// Payloads
			//
			DataTable softwarelists_payload_table = MakePayloadDataTable("softwarelists_payload", new string[] { "key_1" });
			DataTable softwarelist_payload_table = MakePayloadDataTable("softwarelist_payload", new string[] { "softwarelist_name" });
			DataTable software_payload_table = MakePayloadDataTable("software_payload", new string[] { "softwarelist_name", "software_name" });

			DataTable romTable = new DataTable();
			foreach (DataColumn column in dataSet.Tables["dataarea"].Columns)
				if (column.ColumnName.EndsWith("_id") == false)
					romTable.Columns.Add("data_" + column.ColumnName);
			foreach (DataColumn column in dataSet.Tables["rom"].Columns)
				if (column.ColumnName.EndsWith("_id") == false)
					romTable.Columns.Add(column.ColumnName);

			DataTable diskTable = new DataTable();
			if (dataSet.Tables.Contains("diskarea") == true)
			{
				foreach (DataColumn column in dataSet.Tables["diskarea"].Columns)
					if (column.ColumnName.EndsWith("_id") == false)
						diskTable.Columns.Add("data_" + column.ColumnName);
				foreach (DataColumn column in dataSet.Tables["disk"].Columns)
					if (column.ColumnName.EndsWith("_id") == false)
						diskTable.Columns.Add(column.ColumnName);
			}

			DataTable listTable = Tools.MakeDataTable(
				"name	description	roms	disks	rom_size	rom_size_text	disk_size	disk_size_text",
				"String	String		Int32	Int32	Int64		String			Int64		String"
			);

			foreach (DataRow softwarelistRow in dataSet.Tables["softwarelist"].Select(null, "description"))
			{
				long softwarelist_id = (long)softwarelistRow["softwarelist_id"];
				string softwarelist_name = (string)softwarelistRow["name"];
				string softwarelist_description = (string)softwarelistRow["description"];

				//if (softwarelist_name != "x68k_flop" && softwarelist_name != "cdtv")
				//	continue;

				DataRow[] softwareRows = dataSet.Tables["software"].Select($"softwarelist_id = {softwarelist_id}");

				//
				// SoftwareLists
				//

				StringBuilder softwarelist_html = new StringBuilder();

				softwarelist_html.AppendLine("<br />");
				softwarelist_html.AppendLine($"<div><h2 style=\"display:inline;\">softwarelist</h2> &bull; <a href=\"{softwarelist_name}.xml\">XML</a> &bull; <a href=\"{softwarelist_name}.json\">JSON</a> </div>");
				softwarelist_html.AppendLine("<br />");
				softwarelist_html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["softwarelist"], new DataRow[] { softwarelistRow }, null));

				softwarelist_html.AppendLine("<hr />");
				softwarelist_html.AppendLine("<h2>software</h2>");
				DataTable softwareTable = dataSet.Tables["software"].Clone();
				softwareTable.Columns.Add("roms", typeof(int));
				softwareTable.Columns.Add("disks", typeof(int));
				softwareTable.Columns.Add("rom_size", typeof(long));
				softwareTable.Columns.Add("rom_size_text", typeof(string));
				softwareTable.Columns.Add("disk_size", typeof(long));
				softwareTable.Columns.Add("disk_size_text", typeof(string));

				long softwarelist_rom_count = 0;
				long softwarelist_rom_size = 0;
				long softwarelist_disk_count = 0;
				long softwarelist_disk_size = 0;

				//
				// Software
				//

				softwarelistRow["name"] = $"<a href=\"/{coreName}/software/{softwarelist_name}\">{softwarelist_name}</a>";

				foreach (DataRow softwareRow in softwareRows)
				{
					long software_id = (long)softwareRow["software_id"];
					string software_name = (string)softwareRow["name"];

					long software_rom_count = 0;
					long software_rom_size = 0;
					long software_disk_count = 0;
					long software_disk_size = 0;

					string software_cloneof = null;
					if (softwareTable.Columns.Contains("cloneof") == true && softwareRow.IsNull("cloneof") == false)
						software_cloneof = (string)softwareRow["cloneof"];

					if (software_cloneof != null)
						softwareRow["cloneof"] = $"<a href=\"/{coreName}/software/{softwarelist_name}/{software_cloneof}\">{software_cloneof}</a>";

					StringBuilder html = new StringBuilder();

					html.AppendLine("<br />");
					html.AppendLine($"<div><h2 style=\"display:inline;\">software</h2> &bull; <a href=\"{software_name}.xml\">XML</a> &bull; <a href=\"{software_name}.json\">JSON</a> </div>");
					html.AppendLine("<br />");
					html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["software"], new[] { softwareRow }, null));

					html.AppendLine("<hr />");

					html.AppendLine("<h2>softwarelist</h2>");
					html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["softwarelist"], new[] { softwarelistRow }, null));

					DataRow[] rows;

					if (dataSet.Tables.Contains("info") == true)
					{
						rows = dataSet.Tables["info"].Select($"software_id = {software_id}");
						if (rows.Length > 0)
						{
							html.AppendLine("<hr />");
							html.AppendLine("<h2>info</h2>");
							html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["info"], rows, null));
						}
					}

					if (dataSet.Tables.Contains("sharedfeat") == true)
					{
						rows = dataSet.Tables["sharedfeat"].Select($"software_id = {software_id}");
						if (rows.Length > 0)
						{
							html.AppendLine("<hr />");
							html.AppendLine("<h2>sharedfeat</h2>");
							html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["sharedfeat"], rows, null));
						}
					}

					DataRow[] partRows = dataSet.Tables["part"].Select($"software_id = {software_id}");
					if (partRows.Length > 0)
					{
						DataTable table;

						// part, feature
						if (dataSet.Tables.Contains("feature") == true)
						{
							table = Tools.MakeDataTable(
								"part_name	part_interface	feature_name	feature_value",
								"String		String			String			String"
							);

							foreach (DataRow partRow in partRows)
							{
								long part_id = (long)partRow["part_id"];
								string part_name = (string)partRow["name"];
								string part_interface = (string)partRow["interface"];

								foreach (DataRow featureRow in dataSet.Tables["feature"].Select($"part_id = {part_id}"))
									table.Rows.Add(part_name, part_interface, featureRow["name"], featureRow["value"]);
							}
							if (table.Rows.Count > 0)
							{
								html.AppendLine("<hr />");
								html.AppendLine("<h2>part, feature</h2>");
								html.AppendLine(Reports.MakeHtmlTable(table, null));
							}
						}

						// part, dataarea, rom
						table = Tools.MakeDataTable(
							"part_name	part_interface	dataarea_name	dataarea_size	dataarea_databits	dataarea_endian",
							"String		String			String			String			String				String"
						);
						foreach (DataColumn column in dataSet.Tables["rom"].Columns)
							if (column.ColumnName.EndsWith("_id") == false)
								table.Columns.Add(column.ColumnName, typeof(string));

						table.Columns.Add("size_text", typeof(string));

						foreach (DataRow partRow in partRows)
						{
							long part_id = (long)partRow["part_id"];
							string part_name = (string)partRow["name"];
							string part_interface = (string)partRow["interface"];

							foreach (DataRow dataareaRow in dataSet.Tables["dataarea"].Select($"part_id = {part_id}"))
							{
								long dataarea_id = (long)dataareaRow["dataarea_id"];

								foreach (DataRow romRow in dataSet.Tables["rom"].Select($"dataarea_id = {dataarea_id}"))
								{
									string rom_size_string = (string)romRow["size"];
									long rom_size = rom_size_string.StartsWith("0x") == true ? Int64.Parse(rom_size_string.Substring(2), NumberStyles.HexNumber) : Int64.Parse(rom_size_string);

									software_rom_count += 1;
									software_rom_size += rom_size;

									DataRow row = table.Rows.Add(part_name, part_interface,
										(string)dataareaRow["name"], (string)dataareaRow["size"], (string)dataareaRow["databits"], (string)dataareaRow["endian"]);

									row["size_text"] = Tools.DataSize(rom_size);

									foreach (DataColumn column in dataSet.Tables["rom"].Columns)
										if (column.ColumnName.EndsWith("_id") == false)
											row[column.ColumnName] = romRow[column.ColumnName];
								}
							}
						}
						if (table.Rows.Count > 0)
						{
							html.AppendLine("<hr />");
							html.AppendLine("<h2>part, dataarea, rom</h2>");
							html.AppendLine(Reports.MakeHtmlTable(table, null));
						}

						// part, diskarea, disk
						if (dataSet.Tables.Contains("disk") == true)
						{
							table = Tools.MakeDataTable(
								"part_name	part_interface	diskarea_name",
								"String		String			String"
							);
							foreach (DataColumn column in dataSet.Tables["disk"].Columns)
								if (column.ColumnName.EndsWith("_id") == false)
									table.Columns.Add(column.ColumnName, typeof(string));

							table.Columns.Add("chd_size", typeof(long));
							table.Columns.Add("chd_size_text", typeof(string));

							foreach (DataRow partRow in partRows)
							{
								long part_id = (long)partRow["part_id"];
								string part_name = (string)partRow["name"];
								string part_interface = (string)partRow["interface"];

								foreach (DataRow diskareaRow in dataSet.Tables["diskarea"].Select($"part_id = {part_id}"))
								{
									long diskarea_id = (long)diskareaRow["diskarea_id"];

									foreach (DataRow diskRow in dataSet.Tables["disk"].Select($"diskarea_id = {diskarea_id}"))
									{
										DataRow row = table.Rows.Add(part_name, part_interface, (string)diskareaRow["name"]);

										foreach (DataColumn column in dataSet.Tables["disk"].Columns)
											if (column.ColumnName.EndsWith("_id") == false)
												row[column.ColumnName] = diskRow[column.ColumnName];

										long disk_size = 0;
										string disk_name = (string)diskRow["name"];
										foreach (string try_software_name in (new string[] { software_name, software_cloneof }).Where(name => name != null))
										{
											string torrentKey = $"{softwarelist_name}\\{try_software_name}\\{disk_name}.chd";
											if (torrentDiskSizes.ContainsKey(torrentKey) == true)
											{
												disk_size = torrentDiskSizes[torrentKey];
												row["chd_size"] = disk_size;
												row["chd_size_text"] = Tools.DataSize(disk_size);
												break;
											}
										}

										if (disk_size == 0)
											Console.WriteLine($"!!! Did not find software disk in torrents: {softwarelist_name}/{software_name}/{disk_name}");

										software_disk_count += 1;
										software_disk_size += disk_size;
									}
								}
							}
							if (table.Rows.Count > 0)
							{
								html.AppendLine("<hr />");
								html.AppendLine("<h2>part, diskarea, disk</h2>");
								html.AppendLine(Reports.MakeHtmlTable(table, null));
							}
						}

						softwarelist_rom_count += software_rom_count;
						softwarelist_rom_size += software_rom_size;
						softwarelist_disk_count += software_disk_count;
						softwarelist_disk_size += software_disk_size;

						//
						// Software on SoftwareList
						//
						softwareTable.ImportRow(softwareRow);
						DataRow software_row = softwareTable.Rows[softwareTable.Rows.Count - 1];
						software_row["name"] = $"<a href=\"/{coreName}/software/{softwarelist_name}/{software_name}\">{software_name}</a>";
						if (software_rom_count > 0)
						{
							software_row["roms"] = software_rom_count;
							software_row["rom_size"] = software_rom_size;
							software_row["rom_size_text"] = Tools.DataSize(software_rom_size);
						}
						if (software_disk_count > 0)
						{
							software_row["disks"] = software_disk_count;
							software_row["disk_size"] = software_disk_size;
							software_row["disk_size_text"] = Tools.DataSize(software_disk_size);
						}
					}

					DataRow[] machineListRows = machineListTable.Select($"softwarelist_name = '{softwarelist_name}'");

					foreach (string status in new string[] { "good", "imperfect", "preliminary", "no driver" })
					{
						DataRow[] statusRows = machineListRows.Where(row => (string)row["status"] == status).ToArray();

						if (statusRows.Length > 0)
						{
							DataTable machinesTable = new DataTable();
							machinesTable.Columns.Add("name", typeof(string));
							machinesTable.Columns.Add("description (RUN on machine)", typeof(string));

							foreach (DataRow statusRow in statusRows)
							{
								string name = (string)statusRow["machine_name"];
								DataRow detailRow = machineDetailTable.Rows.Find(name);
								string description = detailRow != null ? (string)detailRow["description"] : "not found";

								machinesTable.Rows.Add($"<a href=\"/{coreName}/machine/{name}\">{name}</a>", $"<a href=\"#\" onclick=\"mameAO('{name}@{coreName} {software_name}@{softwarelist_name}'); return false\">{description}</a>");
							}

							html.AppendLine("<hr />");
							html.AppendLine($"<h2>machines ({status})</h2>");
							html.AppendLine(Reports.MakeHtmlTable(machinesTable, null));
						}
					}

					string software_title = $"{(string)softwareRow["description"]} - {(string)softwarelistRow["description"]} - {coreName} ({version}) software";

					string[] software_xmlJson = software_XmlJsonPayloads[$"{softwarelist_name}\t{software_name}"];

					software_payload_table.Rows.Add(softwarelist_name, software_name, software_title, software_xmlJson[0], software_xmlJson[1], html.ToString());
				}

				softwarelist_html.AppendLine(Reports.MakeHtmlTable(softwareTable, null));

				DataRow softwarelist_row = listTable.Rows.Add($"<a href=\"/{coreName}/software/{softwarelist_name}\">{softwarelist_name}</a>", softwarelist_description);

				if (softwarelist_rom_count > 0)
				{
					softwarelist_row["roms"] = softwarelist_rom_count;
					softwarelist_row["rom_size"] = softwarelist_rom_size;
					softwarelist_row["rom_size_text"] = Tools.DataSize(softwarelist_rom_size);
				}
				if (softwarelist_disk_count > 0)
				{
					softwarelist_row["disks"] = softwarelist_disk_count;
					softwarelist_row["disk_size"] = softwarelist_disk_size;
					softwarelist_row["disk_size_text"] = Tools.DataSize(softwarelist_disk_size);
				}

				string softwarelist_title = $"{softwarelist_description} - {coreName} ({version}) software list";
				string[] xmlJson = softwarelist_XmlJsonPayloads[softwarelist_name];

				softwarelist_payload_table.Rows.Add(softwarelist_name, softwarelist_title, xmlJson[0], xmlJson[1], softwarelist_html.ToString());
			}

			string softwarelists_title = $"{coreName.ToUpper()} ({version}) software";
			string softwarelists_html = Reports.MakeHtmlTable(listTable, null);

			softwarelists_payload_table.Rows.Add('1', softwarelists_title, "", "", softwarelists_html);

			MakeMSSQLPayloadsInsert(connections[1], softwarelists_payload_table);
			MakeMSSQLPayloadsInsert(connections[1], softwarelist_payload_table);
			MakeMSSQLPayloadsInsert(connections[1], software_payload_table);

			Tools.ConsolePrintMemory();
		}

		public static Dictionary<string, string[]> MameishMachineXmlJsonPayloads(string xmlFilename)
		{
			Dictionary<string, string[]> payloads = new Dictionary<string, string[]>();

			using (XmlReader reader = XmlReader.Create(xmlFilename, _XmlReaderSettings))
			{
				reader.MoveToContent();

				while (reader.Read())
				{
					while (reader.NodeType == XmlNodeType.Element && reader.Name == "machine")
					{
						if (XElement.ReadFrom(reader) is XElement element)
						{
							string key = element.Attribute("name").Value;
							string xml = element.ToString();
							string json = Tools.XML2JSON(element);

							payloads.Add(key, new string[] { xml, json });
						}
					}
				}
			}

			return payloads;
		}

		public static Dictionary<string, string[]>[] MameishSoftwareXmlJsonPayloads(string xmlFilename)
		{
			Dictionary<string, string[]> softwarelist_payloads = new Dictionary<string, string[]>();
			Dictionary<string, string[]> software_payloads = new Dictionary<string, string[]>();

			using (XmlReader reader = XmlReader.Create(xmlFilename, _XmlReaderSettings))
			{
				reader.MoveToContent();

				while (reader.Read())
				{
					while (reader.NodeType == XmlNodeType.Element && reader.Name == "softwarelist")
					{
						if (XElement.ReadFrom(reader) is XElement listElement)
						{
							string softwarelist_name = listElement.Attribute("name").Value;
							string xml = listElement.ToString();
							string json = Tools.XML2JSON(listElement);

							softwarelist_payloads.Add(softwarelist_name, new string[] { xml, json });

							foreach (XElement element in listElement.Elements("software"))
							{
								string software_name = element.Attribute("name").Value;
								string key = $"{softwarelist_name}\t{software_name}";
								xml = element.ToString();
								json = Tools.XML2JSON(element);

								software_payloads.Add(key, new string[] { xml, json });
							}
						}
					}
				}
			}

			return new Dictionary<string, string[]>[] { softwarelist_payloads, software_payloads };
		}

		//
		// FBNeo
		//
		public static int FBNeoMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName)
		{
			directory = Path.Combine(directory, version);

			//
			// Metadata
			//
			string info;
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Database='{databaseName}';"))
			{
				DeleteExistingPayloadTables(connection);

				int datafileCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM datafile");
				int gameCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM game");
				int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");

				info = $"FBNeo: {version} - datafiles: {datafileCount} - games: {gameCount} - roms: {softRomCount}";

				CreateMetaDataTable(connection, "fbneo", version, info);
			}

			//
			// JSON/XML
			//
			Dictionary<string, string[]> payloadsXmlJson_datafile = FBNeoMSSQLPayloadsXmlJson_Datafile(directory);
			Dictionary<string, string[]> payloadsXmlJson_game = FBNeoMSSQLPayloadsXmlJson_Game(directory);

			//
			// Source data
			//
			DataSet dataSet = new DataSet();
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Database='{databaseName}';"))
			{
				foreach (string tableName in new string[] { "datafile", "driver", "game", "rom", "sample", "video" })
				{
					DataTable table = new DataTable(tableName);
					using (SqlDataAdapter adapter = new SqlDataAdapter($"SELECT * FROM [{tableName}]", connection))
						adapter.Fill(table);
					dataSet.Tables.Add(table);
				}
			}

			dataSet.Tables["rom"].Columns.Add("data_size", typeof(string));
			foreach (DataRow romRow in dataSet.Tables["rom"].Select("[size] IS NOT NULL"))
				romRow["data_size"] = Tools.DataSize(Int64.Parse((string)romRow["size"]));

			//
			// Payloads
			//
			DataTable root_payload_table = MakePayloadDataTable("root_payload", new string[] { "key_1" });
			DataTable datafile_payload_table = MakePayloadDataTable("datafile_payload", new string[] { "key" });
			DataTable game_payload_table = MakePayloadDataTable("game_payload", new string[] { "datafile_key", "game_name" });

			StringBuilder root_html = new StringBuilder();
			string root_title = $"FBNeo ({version})";
			root_html.AppendLine("<h2>FBNeo Systems</h2>");
			root_html.AppendLine("<table>");
			root_html.AppendLine("<tr><th>Name</th><th>Game Count</th><th>Rom Count</th><th>Bytes</th><th>Size</th></tr>");

			foreach (DataRow dataFileRow in dataSet.Tables["datafile"].Rows)
			{
				long datafile_id = (long)dataFileRow["datafile_id"];
				string datafile_key = (string)dataFileRow["key"];
				string datafile_name = (string)dataFileRow["name"];
				datafile_name = datafile_name.Substring(16);
				datafile_name = datafile_name.Substring(0, datafile_name.Length - 6);

				long datafile_size_total = 0;
				long datafile_game_count = 0;
				long datafile_rom_count = 0;

				StringBuilder datafile_html = new StringBuilder();
				string datafile_title = $"FBNeo ({version}) {datafile_name}";

				datafile_html.AppendLine("<br />");
				datafile_html.AppendLine($"<div><h2 style=\"display:inline;\">datafile</h2> &bull; <a href=\"{datafile_key}.xml\">XML</a> &bull; <a href=\"{datafile_key}.json\">JSON</a></div>");
				datafile_html.AppendLine("<br />");

				datafile_html.AppendLine(Reports.MakeHtmlTable(dataFileRow.Table, new[] { dataFileRow }, null));
				datafile_html.AppendLine("<hr />");

				datafile_html.AppendLine("<h2>game</h2>");
				datafile_html.AppendLine("<table>");
				datafile_html.AppendLine("<tr><th>Name</th><th>Description</th><th>Year</th><th>Manufacturer</th>" +
					"<th>cloneof</th><th>romof</th><th>roms</th><th>bytes</th><th>size</th></tr>");

				foreach (DataRow gameRow in dataSet.Tables["game"].Select($"datafile_id = {datafile_id}"))
				{
					long game_id = (long)gameRow["game_id"];
					string game_name = (string)gameRow["name"];
					string game_description = (string)gameRow["description"];
					string game_year = (string)gameRow["year"];
					string game_manufacturer = (string)gameRow["manufacturer"];
					string game_cloneof = Tools.DataRowValue(gameRow, "cloneof");
					string game_romof = Tools.DataRowValue(gameRow, "romof");

					long game_size_total = 0;
					long game_rom_count = 0;

					DataRow[] driverRows = dataSet.Tables["driver"].Select($"game_id = {game_id}");
					DataRow[] romRows = dataSet.Tables["rom"].Select($"game_id = {game_id}");
					DataRow[] videoRows = dataSet.Tables["video"].Select($"game_id = {game_id}");
					DataRow[] sampleRows = dataSet.Tables["sample"].Select($"game_id = {game_id}");

					string game_cloneof_datafile_link = game_cloneof == null ? "" : $"<a href=\"{datafile_key}/{game_cloneof}\">{game_cloneof}</a>";
					string game_romof_datafile_link = game_romof == null ? "" : $"<a href=\"{datafile_key}/{game_romof}\">{game_romof}</a>";

					long rom_size_total = 0;

					StringBuilder game_html = new StringBuilder();
					string game_title = $"{game_description} ({datafile_name})";

					game_html.AppendLine("<br />");
					game_html.AppendLine($"<div><h2 style=\"display:inline;\">game</h2> &bull; <a href=\"{game_name}.xml\">XML</a> &bull; <a href=\"{game_name}.json\">JSON</a></div>");
					game_html.AppendLine("<br />");

					DataTable table = gameRow.Table.Clone();
					table.ImportRow(gameRow);
					DataRow row = table.Rows[0];
					if (game_cloneof != null)
						row["cloneof"] = $"<a href=\"{game_cloneof}\">{game_cloneof}</a>";
					if (game_romof != null)
						row["romof"] = $"<a href=\"{game_cloneof}\">{game_cloneof}</a>";

					game_html.AppendLine(Reports.MakeHtmlTable(table, null));
					game_html.AppendLine("<hr />");

					game_html.AppendLine("<h2>datafile</h2>");
					game_html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["datafile"], new[] { dataFileRow }, null));
					game_html.AppendLine("<hr />");

					if (driverRows.Length > 0)
					{
						game_html.AppendLine("<h2>driver</h2>");
						game_html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["driver"], driverRows, null));
						game_html.AppendLine("<hr />");
					}
					if (romRows.Length > 0)
					{
						foreach (DataRow romRow in romRows.Where(r => r.IsNull("size") == false))
							rom_size_total += Int64.Parse((string)romRow["size"]);

						game_html.AppendLine("<h2>rom</h2>");
						game_html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["rom"], romRows, null));
						game_html.AppendLine("<hr />");
					}
					if (videoRows.Length > 0)
					{
						game_html.AppendLine("<h2>video</h2>");
						game_html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["video"], videoRows, null));
						game_html.AppendLine("<hr />");
					}
					if (sampleRows.Length > 0)
					{
						game_html.AppendLine("<h2>sample</h2>");
						game_html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["sample"], sampleRows, null));
						game_html.AppendLine("<hr />");
					}

					game_size_total += rom_size_total;
					game_rom_count += romRows.Length;

					datafile_size_total += game_size_total;
					datafile_game_count += 1;
					datafile_rom_count += game_rom_count;

					string gameKey = $"{datafile_key}\t{game_name}";
					if (payloadsXmlJson_game.ContainsKey(gameKey) == false)
						throw new ApplicationException($"payloadsXmlJson_game not found: {gameKey}");

					string[] xmlJsonGame = payloadsXmlJson_game[gameKey];

					game_payload_table.Rows.Add(datafile_key, game_name, game_title, xmlJsonGame[0], xmlJsonGame[1], game_html.ToString());

					datafile_html.AppendLine($"<tr><td><a href=\"{datafile_key}/{game_name}\">{game_name}</a></td><td>{game_description}</td><td>{game_year}</td><td>{game_manufacturer}</td>" +
						$"<td>{game_cloneof_datafile_link}</td><td>{game_romof_datafile_link}</td><td>{romRows.Length}</td><td>{rom_size_total}</td><td>{Tools.DataSize(rom_size_total)}</td></tr>");
				}

				datafile_html.AppendLine("</table>");

				if (payloadsXmlJson_datafile.ContainsKey(datafile_key) == false)
					throw new ApplicationException($"payloadsXmlJson_datafile not found: {datafile_key}");

				string[] xmlJsonDatafile = payloadsXmlJson_datafile[datafile_key];

				datafile_payload_table.Rows.Add(datafile_key, datafile_title, xmlJsonDatafile[0], xmlJsonDatafile[1], datafile_html.ToString());

				root_html.AppendLine($"<tr><td><a href=\"/fbneo/{datafile_key}\">{datafile_name}</a></td><td>{datafile_game_count}</td><td>{datafile_rom_count}</td><td>{datafile_size_total}</td><td>{Tools.DataSize(datafile_size_total)}</td></tr>");
			}

			root_html.AppendLine("</table>");
			root_payload_table.Rows.Add("1", root_title, "", "", root_html.ToString());

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Database='{databaseName}';"))
			{
				MakeMSSQLPayloadsInsert(connection, root_payload_table);
				MakeMSSQLPayloadsInsert(connection, datafile_payload_table);
				MakeMSSQLPayloadsInsert(connection, game_payload_table);

				Tools.ConsolePrintMemory();
			}

			return 0;
		}

		public static Dictionary<string, string[]> FBNeoMSSQLPayloadsXmlJson_Datafile(string directory)
		{
			Dictionary<string, string[]> payloads = new Dictionary<string, string[]>();

			using (XmlReader reader = XmlReader.Create(Path.Combine(directory, "_fbneo.xml"), _XmlReaderSettings))
			{
				reader.MoveToContent();

				while (reader.Read())
				{
					while (reader.NodeType == XmlNodeType.Element && reader.Name == "datafile")
					{
						if (XElement.ReadFrom(reader) is XElement datafileElement)
						{
							string datafile_key = datafileElement.Attribute("key").Value;

							string xml = datafileElement.ToString();
							string json = Tools.XML2JSON(datafileElement);

							payloads.Add(datafile_key, new string[] { xml, json });
						}
					}
				}
			}

			return payloads;
		}

		public static Dictionary<string, string[]> FBNeoMSSQLPayloadsXmlJson_Game(string directory)
		{
			Dictionary<string, string[]> payloads = new Dictionary<string, string[]>();

			using (XmlReader reader = XmlReader.Create(Path.Combine(directory, "_fbneo.xml"), _XmlReaderSettings))
			{
				reader.MoveToContent();

				while (reader.Read())
				{
					while (reader.NodeType == XmlNodeType.Element && reader.Name == "datafile")
					{
						if (XElement.ReadFrom(reader) is XElement datafileElement)
						{
							string datafile_key = datafileElement.Attribute("key").Value;

							foreach (XElement gameElement in datafileElement.Descendants("game"))
							{
								string game_name = gameElement.Attribute("name").Value;

								string xml = gameElement.ToString();
								string json = Tools.XML2JSON(gameElement);

								payloads.Add($"{datafile_key}\t{game_name}", new string[] { xml, json });
							}
						}
					}
				}
			}

			return payloads;
		}

		//
		// TOSEC
		//

		public static int TosecMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName)
		{
			directory = Path.Combine(directory, version);

			//
			// Metadata
			//
			string info;
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Database='{databaseName}';"))
			{
				DeleteExistingPayloadTables(connection);

				int datafileCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM datafile");
				int gameCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM game");
				int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");

				info = $"TOSEC: {version} - Datafiles: {datafileCount} - Games: {gameCount} - rom: {softRomCount}";

				CreateMetaDataTable(connection, "tosec", version, info);
			}

			//
			// Archive.org URLs
			//
			Dictionary<long, string> datafileUrls = GetTosecDatafileUrls(serverConnectionString, databaseName);
			Dictionary<long, string> gameUrls = GetTosecGameUrls(serverConnectionString, databaseName);

			//
			// Source Data
			//
			DataSet dataSet = TosecMSSQLPayloadsLoadDataSet(serverConnectionString, databaseName);

			//
			// Payloads
			//
			DataTable category_payload_table = MakePayloadDataTable("category_payload", new string[] { "category" });
			DataTable datafile_payload_table = MakePayloadDataTable("datafile_payload", new string[] { "category", "name" });
			DataTable game_payload_table = MakePayloadDataTable("game_payload", new string[] { "category", "datafile_name", "game_name" });

			foreach (string category in new string[] { "tosec", "tosec-iso", "tosec-pix" })
			{
				//if (category != "tosec-pix")
				//	continue;

				//
				// JSON/XML
				//
				Dictionary<string, string[]>[] payloadParts = TosecMSSQLPayloadsGetDatafileGameXmlJsonPayloads(directory, category);
				Dictionary<string, string[]> datafilePayloads = payloadParts[0];
				Dictionary<string, string[]> gamePayloads = payloadParts[1];

				StringBuilder category_html = new StringBuilder();

				string category_title = $"{category.ToUpper()} ({version})";

				category_html.AppendLine($"<h2>{category}</h2>");
				category_html.AppendLine("<table>");
				category_html.AppendLine("<tr><th>Name</th><th>Version</th><th>Game Count</th><th>Rom Count</th><th>Rom Size</th><th>Rom Bytes</th><th>Extentions</th><th>IA Archive</th></tr>");

				foreach (DataRow datafileRow in dataSet.Tables["datafile"].Select($"[category] = '{category}'"))
				{
					long datafile_id = (long)datafileRow["datafile_id"];
					string datafile_name = (string)datafileRow["name"];
					string datafile_version = (string)datafileRow["version"];
					string datafile_name_enc = Uri.EscapeDataString(datafile_name);

					long datafile_game_count = 0;
					long datafile_rom_count = 0;
					long datafile_rom_size = 0;

					long game_url_count = 0;

					Dictionary<string, int> datafileExtentions = new Dictionary<string, int>();

					StringBuilder datafile_html = new StringBuilder();

					string datafile_title = $"{datafile_name} ({category} {datafile_version})";

					datafile_html.AppendLine("<br />");
					datafile_html.AppendLine($"<div><h2 style=\"display:inline;\">datafile</h2> &bull; <a href=\"{datafile_name_enc}.xml\">XML</a> &bull; <a href=\"{datafile_name_enc}.json\">JSON</a></div>");
					datafile_html.AppendLine("<br />");

					datafile_html.AppendLine(Reports.MakeHtmlTable(datafileRow.Table, new[] { datafileRow }, null));
					datafile_html.AppendLine("<hr />");

					datafile_html.AppendLine($"<h2>game</h2>");
					datafile_html.AppendLine("<table>");
					datafile_html.AppendLine("<tr><th>Name</th><th>Rom Count</th><th>Rom Size</th><th>Rom Bytes</th><th>Extentions</th><th>IA Archive</th></tr>");

					foreach (DataRow gameRow in dataSet.Tables["game"].Select($"[datafile_id] = {datafile_id}"))
					{
						long game_id = (long)gameRow["game_id"];
						string game_name = (string)gameRow["name"];
						string game_name_enc = Uri.EscapeDataString(game_name);

						long game_rom_count = 0;
						long game_rom_size = 0;

						long rom_url_count = 0;

						Dictionary<string, int> gameExtentions = new Dictionary<string, int>();

						++datafile_game_count;

						StringBuilder game_html = new StringBuilder();

						string game_title = $"{datafile_name} - {game_name} ({category} {datafile_version})";

						game_html.AppendLine("<br />");
						game_html.AppendLine($"<div><h2 style=\"display:inline;\">game</h2> &bull; <a href=\"{game_name_enc}.xml\">XML</a> &bull; <a href=\"{game_name_enc}.json\">JSON</a></div>");
						game_html.AppendLine("<br />");

						game_html.AppendLine(Reports.MakeHtmlTable(gameRow.Table, new[] { gameRow }, null));
						game_html.AppendLine("<hr />");

						game_html.AppendLine($"<h2>rom</h2>");
						game_html.AppendLine("<table>");
						game_html.AppendLine("<tr><th>Name</th><th>Size</th><th>Size Bytes</th><th>CRC32</th><th>MD5</th><th>SHA1</th><th>IA File</th></tr>");

						foreach (DataRow romRow in dataSet.Tables["rom"].Select($"[game_id] = {game_id}"))
						{
							string rom_name = (string)romRow["name"];
							string rom_extention = Path.GetExtension(rom_name).ToLower();
							long rom_size = Int64.Parse((string)romRow["size"]);
							string crc = rom_size == 0 ? "" : (string)romRow["crc"];
							string md5 = rom_size == 0 ? "" : (string)romRow["md5"];
							string sha1 = rom_size == 0 ? "" : (string)romRow["sha1"];

							datafile_rom_count += 1;
							datafile_rom_size += rom_size;

							foreach (var extentions in new Dictionary<string, int>[] { datafileExtentions, gameExtentions })
							{
								if (extentions.ContainsKey(rom_extention) == false)
									extentions[rom_extention] = 0;
								extentions[rom_extention] += 1;
							}

							game_rom_count += 1;
							game_rom_size += rom_size;

							string rom_url = "";
							if (datafileUrls.ContainsKey(datafile_id) == true)
							{
								rom_url = datafileUrls[datafile_id];
								rom_url = $"{rom_url}/{Uri.EscapeDataString(game_name)}%2F{Uri.EscapeDataString(rom_name)}";
								rom_url = $"<a href=\"{rom_url}\" target=\"_blank\">{rom_extention}</a>";
							}
							if (gameUrls.ContainsKey(game_id) == true)
							{
								rom_url = gameUrls[game_id];
								rom_url = $"{rom_url}/{Uri.EscapeDataString(rom_name)}";
								rom_url = $"<a href=\"{rom_url}\" target=\"_blank\">{rom_extention}</a>";
							}
							if (rom_url != "")
								rom_url_count += 1;

							game_html.AppendLine($"<tr><td>{rom_name}</td><td>{Tools.DataSize(rom_size)}</td><td>{rom_size}</td><td>{crc}</td><td>{md5}</td><td>{sha1}</td><td>{rom_url}</td></tr>");
						}

						game_html.AppendLine("</table>");

						string gamePayloadKey = $"{datafile_name}\t{game_name}";
						string[] gamePayload;
						if (gamePayloads.ContainsKey(gamePayloadKey) == true)
						{
							gamePayload = gamePayloads[gamePayloadKey];
						}
						else
						{
							gamePayload = new string[] { "", "" };
							Console.WriteLine($"!!! Did not find game payload:{gamePayloadKey}");
						}
						game_payload_table.Rows.Add(category, datafile_name, game_name, game_title, gamePayload[0], gamePayload[1], game_html.ToString());

						string game_extentions = TosecExtentionsLink(gameExtentions);

						string game_url = "";
						if (gameUrls.ContainsKey(game_id) == true)
						{
							game_url = gameUrls[game_id];
							game_url = $"<a href=\"{game_url}\">{Path.GetExtension(game_url)}</a>";
							game_url_count += 1;
						}
						else
						{
							if (rom_url_count > 0)
								game_url = $"{rom_url_count}";
						}

						datafile_html.AppendLine($"<tr><td><a href=\"{datafile_name_enc}/{game_name_enc}\">{game_name}</a></td><td>{game_rom_count}</td><td>{Tools.DataSize(game_rom_size)}</td><td>{game_rom_size}</td><td>{game_extentions}</td><td>{game_url}</td></tr>");
					}

					datafile_html.AppendLine("</table>");

					string[] datafilePayload;
					if (datafilePayloads.ContainsKey(datafile_name) == true)
					{
						datafilePayload = datafilePayloads[datafile_name];
					}
					else
					{
						datafilePayload = new string[] { "", "" };
						Console.WriteLine($"!!! Did not find datafile payload:{datafile_name}");
					}
					datafile_payload_table.Rows.Add(category, datafile_name, datafile_title, datafilePayload[0], datafilePayload[1], datafile_html.ToString());

					string datafile_extentions = TosecExtentionsLink(datafileExtentions);

					string datafile_url = "";
					if (datafileUrls.ContainsKey(datafile_id) == true)
					{
						datafile_url = datafileUrls[datafile_id];
						datafile_url = $"<a href=\"{datafile_url}\">{Path.GetExtension(datafile_url)}</a>";
					}
					else
					{
						if (game_url_count > 0)
							datafile_url = $"{game_url_count}";
					}

					category_html.AppendLine($"<tr><td><a href=\"{category.ToLower()}/{datafile_name_enc}\">{datafile_name}</a></td><td>{datafile_version}</td><td>{datafile_game_count}</td><td>{datafile_rom_count}</td><td>{Tools.DataSize(datafile_rom_size)}</td><td>{datafile_rom_size}</td><td>{datafile_extentions}</td><td>{datafile_url}</td></tr>");
				}

				category_html.AppendLine("</table>");

				category_payload_table.Rows.Add(category, category_title, "", "", category_html.ToString());
			}

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Database='{databaseName}';"))
			{
				MakeMSSQLPayloadsInsert(connection, category_payload_table);
				MakeMSSQLPayloadsInsert(connection, datafile_payload_table);
				MakeMSSQLPayloadsInsert(connection, game_payload_table);

				Tools.ConsolePrintMemory();
			}

			return 0;
		}

		public static DataSet TosecMSSQLPayloadsLoadDataSet(string serverConnectionString, string databaseName)
		{
			DataSet dataSet = new DataSet();
			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				//
				//	Datafix for duplicates in source data
				//
				DataTable dupTable = Database.ExecuteFill(connection,
					"SELECT datafile.category, datafile.name AS datafile_name, game.name AS game_name FROM datafile INNER JOIN game ON datafile.datafile_id = game.datafile_id " +
					"GROUP BY datafile.category, datafile.name, game.name HAVING (COUNT(*) > 1)");

				List<DataRow> dupGameRows = new List<DataRow>();
				foreach (DataRow dupRow in dupTable.Rows)
				{
					using (SqlCommand command = new SqlCommand("SELECT game.game_id, datafile.category, datafile.name AS datafile_name, game.name AS game_name FROM datafile INNER JOIN game ON datafile.datafile_id = game.datafile_id "
						+ "WHERE ((datafile.category = @category) AND (datafile.[name] = @datafile_name) AND (game.[name] = @game_name))", connection))
					{
						command.Parameters.AddWithValue("@category", (string)dupRow["category"]);
						command.Parameters.AddWithValue("@datafile_name", (string)dupRow["datafile_name"]);
						command.Parameters.AddWithValue("@game_name", (string)dupRow["game_name"]);

						DataTable dupGameTable = new DataTable();
						using (SqlDataAdapter adapter = new SqlDataAdapter(command))
							adapter.Fill(dupGameTable);

						for (int index = 1; index < dupGameTable.Rows.Count; ++index)
							dupGameRows.Add(dupGameTable.Rows[index]);
					}
				}

				string game_commandText = "SELECT * from [game] @WHERE ORDER BY [name]";
				if (dupGameRows.Count > 0)
				{
					Console.WriteLine($"!!! Warning DATA Duplicate TOSEC games:{Environment.NewLine}{String.Join(Environment.NewLine, dupGameRows.Select(row => $"{(string)row["category"]} / {(string)row["datafile_name"]} / {(string)row["game_name"]}"))}))");
					game_commandText = game_commandText.Replace("@WHERE", $"WHERE ([game_id] NOT IN ({String.Join(", ", dupGameRows.Select(row => (long)row["game_id"]))}))");
				}
				else
				{
					game_commandText = game_commandText.Replace("@WHERE", "");
				}

				Console.Write("Loading all data...");

				DataTable table;
				table = Database.ExecuteFill(connection, "SELECT * FROM [datafile] ORDER BY [name]");
				table.TableName = "datafile";
				dataSet.Tables.Add(table);
				Console.Write("datafile.");
				table = Database.ExecuteFill(connection, game_commandText);
				table.TableName = "game";
				dataSet.Tables.Add(table);
				Console.Write("game.");
				table = Database.ExecuteFill(connection, "SELECT * FROM [rom] ORDER BY [name]");
				table.TableName = "rom";
				dataSet.Tables.Add(table);
				Console.Write("rom.");

				Console.WriteLine("...done");
			}

			return dataSet;
		}

		public static Dictionary<long, string> GetTosecDatafileUrls(string serverConnectionString, string databaseName)
		{
			Dictionary<string, string[]> categorySources = new Dictionary<string, string[]>() {
				{ "TOSEC", new string[] { "tosec-main" } },
				{ "TOSEC-PIX", new string[] { "tosec-pix-part2", "tosec-pix" } }
			};

			Dictionary<long, string> datafileUrls = new Dictionary<long, string>();

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				foreach (string category in categorySources.Keys)
				{
					DataTable table = Database.ExecuteFill(connection, $"SELECT [datafile_id], [name], [description] FROM [datafile] WHERE ([category] = '{category}') ORDER BY [name]");

					table.Columns.Add("status", typeof(string));
					table.Columns.Add("url", typeof(string));

					foreach (string itemKey in categorySources[category])
					{
						ArchiveOrgItem item = new ArchiveOrgItem(itemKey, null, null)
						{
							DontIgnore = true
						};
						item.GetFile(null);

						foreach (DataRow row in table.Rows)
						{
							long datafile_id = (long)row["datafile_id"];
							//string datafile_name = (string)row["name"];
							string datafile_description = (string)row["description"];

							string match = $"/{datafile_description}";

							var keys = item.Files.Keys.Where(key => key.EndsWith(match));

							if (keys.Any() == false)
								continue;

							if (keys.Take(2).Count() > 1)
								throw new ApplicationException($"Matched many items: {match}");

							ArchiveOrgFile file = item.GetFile(keys.First());

							string url = item.UrlDownload + String.Join("/", item.DownloadLink(file).Substring(item.UrlDownload.Length).Split('/').Select(part => Uri.EscapeDataString(part)));

							row["status"] = item.Key;
							row["url"] = url;

							if (datafileUrls.ContainsKey(datafile_id) == false)
								datafileUrls.Add(datafile_id, url);
							else
								Console.WriteLine($"Matched before: {category} {datafile_description}");

						}
					}
					//Tools.PopText(table);
				}
			}

			return datafileUrls;
		}

		public static Dictionary<long, string> GetTosecGameUrls(string serverConnectionString, string databaseName)
		{
			Dictionary<string, string[]> categorySources = new Dictionary<string, string[]>(){
				{ "TOSEC-ISO", new string[] {
					"noaen-tosec-iso-3do",
					"noaen-tosec-iso-acorn",
					"noaen-tosec-iso-american-laser-games",
					"noaen-tosec-iso-apple",
					"noaen-tosec-iso-atari",
					"noaen-tosec-iso-bandai",
					"noaen-tosec-iso-capcom",
					"noaen-tosec-iso-commodore-amiga",
					"noaen-tosec-iso-commodore-amiga-cd32",
					"noaen-tosec-iso-commodore-amiga-cdtv",
					"noaen-tosec-iso-commodore-c64",
					"noaen-tosec-iso-fujitsu",
					"noaen-tosec-iso-ibm",
					"noaen-tosec-iso-incredible-technologies",
					"noaen-tosec-iso-konami",
					"noaen-tosec-iso-mattel",
					"noaen-tosec-iso-memorex",
					"noaen-tosec-iso-nec",
					"noaen-tosec-iso-nintendo",
					"noaen-tosec-iso-philips",
					"noaen-tosec-iso-snk",
					"noaen-tosec-iso-sega-32x",
					"noaen-tosec-iso-sega-chihiro",
					"noaen-tosec-iso-sega-dreamcast-applications",
					"noaen-tosec-iso-sega-dreamcast-firmware",
					"noaen-tosec-iso-sega-dreamcast-games-dev-builds",
					"noaen-tosec-iso-sega-dreamcast-games-jp",
					"noaen-tosec-iso-sega-dreamcast-games-pal",
					"noaen-tosec-iso-sega-dreamcast-games-us",
					"noaen-tosec-iso-sega-dreamcast-homebrew",
					"noaen-tosec-iso-sega-dreamcast-multimedia",
					"noaen-tosec-iso-sega-dreamcast-samplers",
					"noaen-tosec-iso-sega-dreamcast-various-unverified-dumps",
					"noaen-tosec-iso-sega-mega-cd-sega-cd",
					"noaen-tosec-iso-sega-naomi",
					"noaen-tosec-iso-sega-naomi-2",
					"noaen-tosec-iso-sega-saturn",
					"noaen-tosec-iso-sega-wondermega",
					"noaen-tosec-iso-sinclair",
					"noaen-tosec-iso-sony",
					"noaen-tosec-iso-tomy",
					"noaen-tosec-iso-vm-labs",
					"noaen-tosec-iso-vtech",
					"noaen-tosec-iso-zapit-games",
					}
				}
			};

			Dictionary<long, string> gameUrls = new Dictionary<long, string>();

			using (SqlConnection connection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseName}';"))
			{
				foreach (string category in categorySources.Keys)
				{
					DataTable table = Database.ExecuteFill(connection,
						"SELECT datafile.datafile_id, datafile.name AS datafile_name, datafile.description AS datafile_description, game.game_id, game.name AS game_name, game.description AS game_description " +
						$"FROM datafile INNER JOIN game ON datafile.datafile_id = game.datafile_id WHERE (datafile.category = '{category}') ORDER BY datafile.name, game.name");

					table.Columns.Add("status", typeof(string));
					table.Columns.Add("url", typeof(string));

					foreach (string itemKey in categorySources[category])
					{
						ArchiveOrgItem item = new ArchiveOrgItem(itemKey, null, null)
						{
							DontIgnore = true
						};
						item.GetFile(null);

						// Data Fix - parent directory mismatch
						if (itemKey == "noaen-tosec-iso-sony")
						{
							foreach (string oldKey in item.Files.Keys.Where(key => key.Contains("/[BIN]/")).ToArray())
							{
								string newKey = oldKey.Replace("/[BIN]/", "/[BIN-CUE]/");
								item.Files.Add(newKey, item.Files[oldKey]);
								item.Files.Remove(oldKey);
							}
						}

						foreach (DataRow row in table.Rows)
						{
							long game_id = (long)row["game_id"];
							string game_name = (string)row["game_name"];

							string match = $"/{game_name}";

							var keys = item.Files.Keys.Where(key => key.EndsWith(match));

							if (keys.Any() == false)
								continue;

							if (keys.Take(2).Count() > 1)
							{
								string datafile_name = (string)row["datafile_name"];

								keys = keys.Where(key => {
									string[] parts = key.Split('/');
									return datafile_name.Contains(parts[parts.Length - 2]);
								});

								if (keys.Any() == false)
									throw new ApplicationException("had it and lost it");

								if (keys.Count() > 1)
									throw new ApplicationException($"Matched many files: {match}");
							}

							ArchiveOrgFile file = item.GetFile(keys.First());

							string url = item.UrlDownload + String.Join("/", item.DownloadLink(file).Substring(item.UrlDownload.Length).Split('/').Select(part => Uri.EscapeDataString(part)));

							row["status"] = item.Key;
							row["url"] = url;

							if (gameUrls.ContainsKey(game_id) == false)
								gameUrls.Add(game_id, url);
							else
								Console.WriteLine($"Matched before: {category} {game_name}");
						}
					}
					//Tools.PopText(table);
				}
			}

			return gameUrls;
		}

		public static Dictionary<string, string[]>[] TosecMSSQLPayloadsGetDatafileGameXmlJsonPayloads(string directory, string category)
		{
			string xmlFilename = Path.Combine(directory, $"_{category}.xml");
			Console.WriteLine(xmlFilename);

			Dictionary<string, string[]> datafilePayloads = new Dictionary<string, string[]>();
			Dictionary<string, string[]> gamePayloads = new Dictionary<string, string[]>();

			using (XmlReader reader = XmlReader.Create(xmlFilename, _XmlReaderSettings))
			{
				reader.MoveToContent();

				while (reader.Read())
				{
					while (reader.NodeType == XmlNodeType.Element && reader.Name == "datafile")
					{
						if (XElement.ReadFrom(reader) is XElement datafileElement)
						{
							string datafile_name = datafileElement.Element("header").Element("name").Value;

							string xml = datafileElement.ToString();
							string json = Tools.XML2JSON(datafileElement);

							datafilePayloads.Add(datafile_name, new string[] { xml, json });

							HashSet<string> gameNames = new HashSet<string>();  // Duplicates in source data fix

							foreach (XElement element in datafileElement.Elements("game"))
							{
								string game_name = element.Attribute("name").Value;

								xml = element.ToString();
								json = Tools.XML2JSON(element);

								if (gameNames.Add(game_name) == true)
									gamePayloads.Add($"{datafile_name}\t{game_name}", new string[] { xml, json });
								else
									Console.WriteLine($"!!! Warning XML Duplicate TOSEC game: {category}, {datafile_name}, {game_name}");
							}
						}
					}
				}
			}

			return new Dictionary<string, string[]>[] { datafilePayloads, gamePayloads };
		}

		public static string TosecExtentionsLink(Dictionary<string, int> sourceExtentions)
		{
			var extentions = sourceExtentions.OrderByDescending(pair => pair.Value).Cast<KeyValuePair<string, int>>();
			if (extentions.Count() > 10)
			{
				int remainingCount = 0;
				foreach (int count in extentions.Skip(10).Select(pair => pair.Value))
					remainingCount += count;

				sourceExtentions = new Dictionary<string, int>();
				foreach (var pair in extentions.Take(10))
					sourceExtentions.Add(pair.Key, pair.Value);

				sourceExtentions.Add("....", remainingCount);

				extentions = sourceExtentions.OrderByDescending(pair => pair.Value).Cast<KeyValuePair<string, int>>();
			}

			return String.Join(", ", extentions.Select(pair => $"{pair.Key}({pair.Value})"));
		}
	}
}
