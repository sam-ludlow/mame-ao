using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;

using Newtonsoft.Json.Linq;

namespace Spludlow.MameAO
{
	public class OperationsMameish
	{
		public static int MameMSSQLPayloads(string directory, string version, string serverConnectionString, string[] databaseNames)
		{
			return MameishMSSQLPayloads(directory, version, serverConnectionString, databaseNames, "mame");
		}

		public static int HbMameMSSQLPayloads(string directory, string version, string serverConnectionString, string[] databaseNames)
		{
			return MameishMSSQLPayloads(directory, version, serverConnectionString, databaseNames, "hbmame");
		}

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

			DataTable snapTable = Snap.LoadSnapIndex(Path.Combine(Path.GetDirectoryName(directory), "snap"), coreName);

			DataSet machineDataSet = Operations.SourceData(connections[0], new Dictionary<string, string>(){
				{ "machine",		"description" },
				{ "disk",			"name" },
				{ "rom",			"name" },
				{ "sample",			"name" },
				{ "softwarelist",   "name" },
			});

			DataSet softwareDataSet = Operations.SourceData(connections[1], new Dictionary<string, string>(){
				{ "softwarelist",   "description" },
				{ "software",       "description" },
				{ "disk",           "name" },
				{ "rom",            "name" },
			});

			MameishMSSQLMachinePayloads(version, connections, coreName, versionDirectory, exeTime, snapTable, machineDataSet);
			
			MameishMSSQLMachinePayloadsSearch(connections, coreName, snapTable);

			MameishMSSQLSoftwarePayloads(directory, version, connections, coreName, versionDirectory, exeTime, snapTable, softwareDataSet);

			MameishMSSQLSoftwarePayloadsSearch(connections, coreName, snapTable, softwareDataSet);

			return 0;
		}


		public static void MameishMSSQLMachinePayloadsSearch(SqlConnection[] connections, string coreName, DataTable snapTable)
		{
			//	TODO ???	feature 0-5, chip


			//
			// machine, driver, sound, input
			//
			DataTable searchTable = Database.ExecuteFill(connections[0], @"
				SELECT
					machine.name,
					machine.machine_id,
					machine.sourcefile,
					machine.sampleof,
					CAST(CASE WHEN machine.isbios = 'yes' THEN 1 ELSE 0 END AS BIT) AS [isbios],
					CAST(CASE WHEN machine.isdevice = 'yes' THEN 1 ELSE 0 END AS BIT) AS [isdevice],
					CAST(CASE WHEN machine.ismechanical = 'yes' THEN 1 ELSE 0 END AS BIT) AS [ismechanical],
					CAST(CASE WHEN machine.ismechanical = 'no' THEN 1 ELSE 0 END AS BIT) AS [iselectronic],
					CAST(CASE WHEN machine.cloneof IS NULL THEN 0 ELSE 1 END AS BIT) AS [isclone],
					'' AS [type],
					'' AS [ao_status],
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
			");

			searchTable.TableName = "machine_search_payload";
			searchTable.PrimaryKey = new DataColumn[] { searchTable.Columns["name"] };

			foreach (DataRow row in searchTable.Rows)
			{
				if ((bool)row["isdevice"] == true)
				{
					row["ismechanical"] = false;
					row["iselectronic"] = false;
				}
			}

			//
			// display
			//
			DataTable displayTable = Database.ExecuteFill(connections[0], @"
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
			");

			//
			// device_ref
			//
			DataTable deviceRefTable = Database.ExecuteFill(connections[0], @"
				SELECT
					device_ref.machine_id,
					device_ref.name
				FROM
					device_ref
				ORDER BY
					device_ref.machine_id,
					device_ref.name;
			");

			//
			// softwarelist
			//
			DataTable softwarelistTable = Database.ExecuteFill(connections[0], @"
				SELECT
					softwarelist.machine_id,
					softwarelist.name
				FROM
					softwarelist
				ORDER BY
					softwarelist.machine_id,
					softwarelist.name;
			");

			//
			// display types
			//
			List<string> displayTypes = Database.ExecuteFill(connections[0],
				"SELECT [type] FROM [display] GROUP BY [type] ORDER BY [type]").Rows.Cast<DataRow>().Select(row => (string)row[0]).ToList();

			foreach (string displayType in displayTypes)
				searchTable.Columns.Add($"{ displayType}", typeof(int));

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
			DataTable inputControlTable = Database.ExecuteFill(connections[0], @"
				SELECT
					machine.name,
					control.*
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
			");


			//
			// control types
			//
			List<string> controlTypes = Database.ExecuteFill(connections[0],
				"SELECT [type] FROM [control] GROUP BY [type] ORDER BY [type]").Rows.Cast<DataRow>().Select(row => (string)row[0]).ToList();

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
			// Build line payloads
			//
			foreach (string name in new string[] { "xml", "json", "html", "html_card" })
				searchTable.Columns.Add(name, typeof(string));

			string[] columnNames = new string[] { "name", "description", "year", "manufacturer", "cloneof", "romof" };

			foreach (DataRow row in searchTable.Rows)
			{
				StringBuilder item;

				//
				// Table row
				//
				item = new StringBuilder();
				item.Append("<tr>");

				foreach (string columnName in columnNames)
				{
					DataColumn column = searchTable.Columns[columnName];
					item.Append("<td>");
					if (row.IsNull(column) == false)
					{
						switch (columnName)
						{
							case "name":
							case "cloneof":
							case "romof":
								item.Append($"<a href=\"/{coreName}/machine/{row[column]}\">{row[column]}</a>");
								break;
							default:
								item.Append(WebUtility.HtmlEncode(Convert.ToString(row[column])));
								break;
						}
					}

					item.Append("</td>");
				}

				item.Append("</tr>");
				row["html"] = item.ToString();

				//
				// Div card
				//
				string machine_name = (string)row["name"];
				string machine_description = (string)row["description"];
				string machine_year = row.IsNull("year") ? "" : (string)row["year"];
				string machine_manufacturer = row.IsNull("manufacturer") ? "" : (string)row["manufacturer"];
				bool machine_isdevice = (bool)row["isdevice"];

				string cardClass = machine_isdevice == true ? "card" : $"card-{((string)row["status"])[0]}{((string)row["emulation"])[0]}";

				DataRow snapRow = snapTable?.Rows.Find(machine_name);

				item = new StringBuilder();
				item.Append($"<div class=\"{cardClass}\">");

				item.Append($"<div class=\"card-thumb\"><a href=\"/{coreName}/machine/{machine_name}\" class=\"card-link\">");
				if (snapRow != null)
				{
					item.Append($"<img src=\"/{coreName}/machine/{machine_name}.jpg\" alt=\"{machine_description}\" loading=\"lazy\" class=\"card-img\" />");
				}
				else
				{
					if (machine_isdevice == true)
						item.Append($"<p>DEVICE</p>");
					else
						item.Append($"<p>NO SNAP</p>");
				}
					
				item.Append("</a></div>");

				item.Append("<div class=\"card-body\">");
				item.Append($"<div class=\"card-name\">{machine_name}</div>");
				item.Append($"<div class=\"card-description\">{machine_description}</div>");
				item.Append($"<div class=\"card-year\">{machine_year}</div>");
				item.Append($"<div class=\"card-manufacturer\">{machine_manufacturer}</div>");
				item.Append("</div>");

				item.Append("</div>");
				row["html_card"] = item.ToString();

				//
				// Machine Type
				//
				int coins = row.IsNull("coins") == false ? Int32.Parse((string)row["coins"]) : 0;
				row["type"] = MameishMachineType(row, machine_isdevice, coins, deviceRefTable, softwarelistTable, inputControlTable);

				//
				// Status
				//
				if (machine_isdevice == false)
					row["ao_status"] = MachineAoStatusLookup[$"{(string)row["status"]}-{(string)row["emulation"]}"];

			}

			//
			// Insert database table
			//
			Tools.SetDataTableStringLengths(searchTable);

			Operations.MakeMSSQLPayloadsInsert(connections[0], searchTable);

			//
			// Create indexes
			//
			Database.ExecuteNonQuery(connections[0], @"
				CREATE FULLTEXT INDEX ON [machine_search_payload]
				(
					[name],
					[description],
					[year],
					[manufacturer]
				)
				KEY INDEX [PK_machine_search_payload]
				ON [ao_catalog]
				WITH CHANGE_TRACKING AUTO;
			");

			Database.ExecuteNonQuery(connections[0], @"
				CREATE NONCLUSTERED INDEX [IX_machine_search_payload_type_description]
				ON [machine_search_payload]
				(
					[type],
					[description]
				)
				INCLUDE (
					[ao_status],	
					[ismechanical],
					[isclone],

					[html],
					[html_card]
				);
			");

			Database.ExecuteNonQuery(connections[0], @"
				CREATE NONCLUSTERED INDEX [IX_machine_search_payload_type_status_description]
				ON [machine_search_payload]
				(
					[type],
					[ao_status],
					[description]
				)
				INCLUDE (
					[ismechanical],
					[isclone],

					[html],
					[html_card]
				);
			");

			//	TODO: Indexes for sorting by year and others .......



			//
			//	hash & name search
			//
			if (Database.IndexExists(connections[0], "rom", "IX_rom_name") == false)
			{
				Database.ExecuteNonQuery(connections[0], @"
					CREATE NONCLUSTERED INDEX IX_rom_name
					ON [rom] (name, machine_id)
					INCLUDE (size, sha1, crc);

					CREATE NONCLUSTERED INDEX IX_rom_sha1
					ON [rom] (sha1, machine_id)
					INCLUDE (name, size, crc);

					CREATE NONCLUSTERED INDEX IX_rom_crc
					ON [rom] (crc, machine_id)
					INCLUDE (name, size, sha1);
				");

				if (Database.TableExists(connections[0], "disk") == true)
					Database.ExecuteNonQuery(connections[0], @"
						CREATE NONCLUSTERED INDEX IX_disk_name
						ON [disk] (name, machine_id)
						INCLUDE (sha1);

						CREATE NONCLUSTERED INDEX IX_disk_sha1
						ON [disk] (sha1, machine_id)
						INCLUDE (name);
					");
			}
			if (Database.IndexExists(connections[1], "rom", "IX_rom_name") == false)
			{
				Database.ExecuteNonQuery(connections[1], @"
					CREATE NONCLUSTERED INDEX IX_rom_name
					ON [rom] (name, dataarea_id)
					INCLUDE (size, sha1, crc);

					CREATE NONCLUSTERED INDEX IX_rom_sha1
					ON [rom] (sha1, dataarea_id)
					INCLUDE (name, size, crc);

					CREATE NONCLUSTERED INDEX IX_rom_crc
					ON [rom] (crc, dataarea_id)
					INCLUDE (name, size, sha1);
				");

				if (Database.TableExists(connections[1], "disk") == true)
					Database.ExecuteNonQuery(connections[1], @"
						CREATE NONCLUSTERED INDEX IX_disk_sha1
						ON [disk] (sha1, diskarea_id)
						INCLUDE (name);

						CREATE NONCLUSTERED INDEX IX_disk_name
						ON [disk] (name, diskarea_id)
						INCLUDE (sha1);
					");
			}
		}

		public static readonly Dictionary<string, string> MachineAoStatusLookup = new Dictionary<string, string> {
			{ "good-good",					"good" },
			{ "imperfect-good",				"imperfect" },
			{ "preliminary-good",			"preliminary" },
			{ "preliminary-preliminary",	"bad" },
		};

		public static string MameishMachineType(DataRow row, bool isdevice, int coins, DataTable deviceRefTable, DataTable softwarelistTable, DataTable inputControlTable)
		{
			long machine_id = (long)row["machine_id"];
			string name = (string)row["name"];
			string sourcefile = (string)row["sourcefile"];

			var deviceRefNames = deviceRefTable.Select($"machine_id = {machine_id}").Select(r => (string)r["name"]).Distinct();
			var softwareLists = softwarelistTable.Select($"machine_id = {machine_id}").Select(r => (string)r["name"]).ToArray();
			var controlTypes = inputControlTable.Select($"[name] = '{name}'").Select(r => (string)r["type"]).Distinct();

			if (isdevice)
				return "device";

			if (sourcefile.StartsWith("pinball/"))
				return "pinball";

			if (controlTypes.Contains("gambling"))
				return "gamble";

			if (sourcefile.StartsWith("barcrest/") || sourcefile.StartsWith("bfm/") || sourcefile.StartsWith("maygay/") || sourcefile.StartsWith("jpm/")
				 || sourcefile.StartsWith("misc/ecoin") || sourcefile.StartsWith("misc/proconn") || sourcefile.StartsWith("misc/acesp")
				 || sourcefile.StartsWith("misc/globalfr") || sourcefile.StartsWith("misc/astrafr"))
				return "gamble";

			if (coins > 0)
			{
				if (deviceRefNames.Contains("coin_hopper") || deviceRefNames.Contains("meters") || deviceRefNames.Contains("stepper"))
					return "gamble";
				else
					return "arcade";
			}
			else
			{
				if (softwareLists.Length > 0)
					return "software";
				else
					return "other";
			}
		}


		/// <summary>
		/// <mame build="0.287 (mame0287)" debug="no" mameconfig="10"> <machine name="005"
		/// </summary>
		public static void MameishMSSQLMachinePayloads(string version, SqlConnection[] connections, string coreName, string versionDirectory, string exeTime, DataTable snapTable, DataSet dataSet)
		{
			Tools.ConsolePrintMemory();

			//
			// XML/JSON
			//
			var xmlJsonPayloads_machine = new Dictionary<string, string[]>();

			Console.Write("Loading XML ...");
			var mameElement = XElement.Load(Path.Combine(versionDirectory, "_machine.xml"), LoadOptions.None);
			foreach (var machineElement in mameElement.Elements("machine"))
				xmlJsonPayloads_machine.Add(machineElement.Attribute("name").Value, new string[] { machineElement.ToString(), Tools.XML2JSON(machineElement) });
			Console.WriteLine("...done");

			Tools.ConsolePrintMemory();

			//
			// Source Data - Merge condition tables
			//
			foreach (string conditionTableName in dataSet.Tables.Cast<DataTable>().Where(t => t.TableName.EndsWith("_condition") == true).Select(t => t.TableName))
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
			// Get clones
			//
			Dictionary<string, List<string>> parentCloneDescriptionNames = new Dictionary<string, List<string>>();

			foreach (DataRow machineRow in dataSet.Tables["machine"].Rows)
			{
				if (machineRow.IsNull("cloneof") == true)
					continue;

				string name = (string)machineRow["name"];
				string description = (string)machineRow["description"];
				string cloneof = (string)machineRow["cloneof"];

				if (parentCloneDescriptionNames.ContainsKey(cloneof) == false)
					parentCloneDescriptionNames.Add(cloneof, new List<string>());
				parentCloneDescriptionNames[cloneof].Add($"{description.Replace("\t", " ")}\t{name}");
			}
			foreach (string key in parentCloneDescriptionNames.Keys)
				parentCloneDescriptionNames[key].Sort();

			//
			// Traverse - Main
			//
			var level_machine = new PayloadLevelInfo(PayloadLevel.Machine, xmlJsonPayloads_machine);

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

			var rowLookups = Operations.PerformanceDictionaries(dataSet);

			foreach (DataRow machineRow in dataSet.Tables["machine"].Rows)
			{
				long machine_id = (long)machineRow["machine_id"];
				string machine_name = (string)machineRow["name"];
				string machine_description = (string)machineRow["description"];

				level_machine.Start($"{coreName} ({version}) &bull; machine {machine_name} &bull; {machine_description}");

				//
				// Simple joins
				//
				foreach (string tableName in simpleTableNames)
				{
					if (dataSet.Tables.Contains(tableName) == false)
						continue;

					List<DataRow> rows = tableName == "machine" ? new List<DataRow>(new DataRow[] { machineRow }) : rowLookups[tableName][machine_id];

					if (rows.Count == 0)
						continue;

					DataTable table = dataSet.Tables[tableName].Clone();

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
						level_machine.Append("<br />");
						level_machine.Append($"<div><h2 style=\"display:inline;\">machine</h2> &bull; <a href=\"{machine_name}.xml\">XML</a> &bull; <a href=\"{machine_name}.json\">JSON</a> &bull; <a href=\"#\" onclick=\"mameAO('{machine_name}@{coreName}'); return false\">RUN</a></div>");
						level_machine.Append("<br />");
					}
					else
					{
						level_machine.Append("<hr />");
						level_machine.Append($"<h2>{tableName}</h2>");
					}

					level_machine.Append(Reports.MakeHtmlTable(table, null));

					if (tableName == "machine" && snapTable != null)
					{
						DataRow snapRow = snapTable.Rows.Find(machine_name);
						if (snapRow != null)
						{
							level_machine.Append("<hr />");
							level_machine.Append("<h2>snap</h2>");
							level_machine.Append($"<img src=\"/{coreName}/machine/{machine_name}.png\" alt=\"{(string)machineRow["description"]} png snap\">");
							level_machine.Append($"<img src=\"/{coreName}/machine/{machine_name}.jpg\" alt=\"{(string)machineRow["description"]} jpg snap thumbnail\">");
							level_machine.Append(Reports.MakeHtmlTable(snapTable, new DataRow[] { snapRow }, null));
						}
					}
				}

				List<DataRow> deviceRows = rowLookups["device"][machine_id];
				if (deviceRows.Count > 0)
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

						foreach (DataRow instanceRow in rowLookups["instance"][device_id])
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
						level_machine.Append("<hr />");
						level_machine.Append("<h2>device, instance</h2>");
						level_machine.Append(Reports.MakeHtmlTable(table, null));
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

						DataRow row = table.NewRow();
						foreach (DataColumn column in deviceRow.Table.Columns)
							if (column.ColumnName.EndsWith("_id") == false)
								row[column.ColumnName] = deviceRow[column.ColumnName];

						row["extension_names"] = String.Join(", ", rowLookups["extension"][device_id].Select(r => (string)r["name"]));

						table.Rows.Add(row);
					}

					if (table.Rows.Count > 0)
					{
						level_machine.Append("<hr />");
						level_machine.Append("<h2>device, extension</h2>");
						level_machine.Append(Reports.MakeHtmlTable(table, null));
					}
				}

				//
				// input, control
				//
				List<DataRow> inputRows = rowLookups["input"][machine_id];
				if (inputRows.Count > 0)
				{
					if (inputRows.Count != 1)
						throw new ApplicationException("Not one [input] row.");

					long input_id = (long)inputRows[0]["input_id"];

					level_machine.Append("<hr />");
					level_machine.Append("<h2>input</h2>");
					level_machine.Append(Reports.MakeHtmlTable(dataSet.Tables["input"], inputRows, null));

					List<DataRow> controlRows = rowLookups["control"][input_id];
					if (controlRows.Count > 0)
					{
						level_machine.Append("<h3>control</h3>");
						level_machine.Append(Reports.MakeHtmlTable(dataSet.Tables["control"], controlRows, null));
					}
				}

				//
				// port, analog
				//
				List<DataRow> portRows = rowLookups["port"][machine_id];
				if (portRows.Count > 0)
				{
					level_machine.Append("<hr />");
					level_machine.Append("<h2>port, analog</h2>");

					DataTable table = Tools.MakeDataTable(
						"port_tag	analog_masks",
						"String		String"
					);
					foreach (DataRow portRow in portRows)
					{
						long port_id = (long)portRow["port_id"];

						List<DataRow> analogRows = rowLookups["analog"][port_id];

						string masks = String.Join(", ", analogRows.Select(row => (string)row["mask"]));

						table.Rows.Add((string)portRow["tag"], masks);
					}

					level_machine.Append(Reports.MakeHtmlTable(table, null));
				}

				//
				// slot, slotoption
				//
				List<DataRow> slotRows = rowLookups["slot"][machine_id];
				if (slotRows.Count > 0)
				{
					level_machine.Append("<hr />");
					level_machine.Append("<h2>slot, slotoption</h2>");

					DataTable table = Tools.MakeDataTable(
						"slot_name	slotoption_name	slotoption_devname	slotoption_default",
						"String		String			String				String"
					);

					foreach (DataRow slotRow in slotRows)
					{
						long slot_id = (long)slotRow["slot_id"];
						List<DataRow> slotoptionRows = rowLookups["slotoption"][slot_id];

						if (slotoptionRows.Count == 0)
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

					level_machine.Append(Reports.MakeHtmlTable(table, null));
				}

				//
				// configuration
				//
				List<DataRow> configurationRows = rowLookups["configuration"][machine_id];
				if (configurationRows.Count > 0)
				{
					level_machine.Append("<hr />");
					level_machine.Append("<h2>configuration</h2>");

					foreach (DataRow configurationRow in configurationRows)
					{
						long configuration_id = (long)configurationRow["configuration_id"];

						level_machine.Append("<hr class='px2' />");
						level_machine.Append($"<h3>{(string)configurationRow["name"]}</h3>");
						level_machine.Append(Reports.MakeHtmlTable(dataSet.Tables["configuration"], new[] { configurationRow }, null));

						if (dataSet.Tables.Contains("conflocation") == true)
						{
							List<DataRow> conflocationRows = rowLookups["conflocation"][configuration_id];
							if (conflocationRows.Count > 0)
							{
								level_machine.Append("<h4>location</h4>");
								level_machine.Append(Reports.MakeHtmlTable(dataSet.Tables["conflocation"], conflocationRows, null));
							}
						}

						List<DataRow> confsettingRows = rowLookups["confsetting"][configuration_id];
						if (confsettingRows.Count > 0)
						{
							level_machine.Append("<h4>setting</h4>");
							level_machine.Append(Reports.MakeHtmlTable(dataSet.Tables["confsetting"], confsettingRows, null));
						}
					}
				}

				//
				// dipswitch
				//
				List<DataRow> dipswitchRows = rowLookups["dipswitch"][machine_id];
				if (dipswitchRows.Count > 0)
				{
					level_machine.Append("<hr />");
					level_machine.Append("<h2>dipswitch</h2>");

					foreach (DataRow dipswitchRow in dipswitchRows)
					{
						long dipswitch_id = (long)dipswitchRow["dipswitch_id"];

						level_machine.Append("<hr class='px2' />");
						level_machine.Append($"<h3>{(string)dipswitchRow["name"]}</h3>");
						level_machine.Append(Reports.MakeHtmlTable(dataSet.Tables["dipswitch"], new[] { dipswitchRow }, null));

						List<DataRow> diplocationRows = rowLookups["diplocation"][dipswitch_id];
						if (diplocationRows.Count > 0)
						{
							level_machine.Append("<h4>location</h4>");
							level_machine.Append(Reports.MakeHtmlTable(dataSet.Tables["diplocation"], diplocationRows, null));
						}

						List<DataRow> dipvalueRows = rowLookups["dipvalue"][dipswitch_id];
						if (dipvalueRows.Count > 0)
						{
							level_machine.Append("<h4>value</h4>");
							level_machine.Append(Reports.MakeHtmlTable(dataSet.Tables["dipvalue"], dipvalueRows, null));
						}
					}
				}

				//
				// clones
				//
				if (parentCloneDescriptionNames.ContainsKey(machine_name) == true)
				{
					level_machine.Append("<hr />");
					level_machine.Append("<h2>clones</h2>");

					level_machine.Append("<table>");
					level_machine.Append("<tr><th>name</th><th>description</th></tr>");

					foreach (string descriptionNameLine in parentCloneDescriptionNames[machine_name])
					{
						string[] descriptionName = descriptionNameLine.Split('\t');
						level_machine.Append($"<tr><td><a href=\"/{coreName}/machine/{descriptionName[1]}\">{descriptionName[1]}</a></td><td>{descriptionName[0]}</td></tr>");
					}
					level_machine.Append("</table>");
				}

				level_machine.Finish(machine_name);
			}

			Tools.ConsolePrintMemory();

			//
			// Metadata				//	TODO disk(not hbmame) - software and lists
			//
			string info = $"{coreName} ({version}) &bull; released: {exeTime} &bull; machines: {dataSet.Tables["machine"].Rows.Count} &bull; rom: {dataSet.Tables["rom"].Rows.Count}";
			Operations.CreateMetaDataTable(connections[0], coreName, version, info);

			//
			// Save payload tables
			//
			level_machine.Save(connections[0]);

			Tools.ConsolePrintMemory();
		}

		/// <summary>
		/// <softwarelists> <softwarelist name="vc4000" description="Interton VC 4000 cartridges"> <software name="carraces" >
		/// </summary>
		public static void MameishMSSQLSoftwarePayloads(string directory, string version, SqlConnection[] connections, string coreName, string versionDirectory, string exeTime, DataTable snapTable, DataSet dataSet)
		{
			Tools.ConsolePrintMemory();

			//
			// XML/JSON
			//
			var xmlJsonPayloads_softwarelist = new Dictionary<string, string[]>();
			var xmlJsonPayloads_software = new Dictionary<string, string[]>();

			Console.Write("Loading XML ...");
			var softwarelistsElement = XElement.Load(Path.Combine(versionDirectory, "_software.xml"), LoadOptions.None);
			foreach (var softwarelistElement in softwarelistsElement.Elements("softwarelist"))
			{
				var softwarelist_name = softwarelistElement.Attribute("name").Value;
				xmlJsonPayloads_softwarelist.Add(softwarelist_name, new string[] { softwarelistElement.ToString(), Tools.XML2JSON(softwarelistElement) });

				foreach (var softwareElement in softwarelistElement.Elements("software"))
				{
					var software_name = softwareElement.Attribute("name").Value;
					xmlJsonPayloads_software.Add($"{softwarelist_name}\t{software_name}", new string[] { softwareElement.ToString(), Tools.XML2JSON(softwareElement) });
				}
			}
			Console.WriteLine("...done");

			Tools.ConsolePrintMemory();

			//
			//	CHD Sizes
			//
			bool usingDisk = Database.TableExists(connections[1], "disk") && coreName != "hbmame";

			Dictionary<string, long> torrentDiskSizes = new Dictionary<string, long>();
			if (usingDisk == true)
			{
				Globals.GitHubRepos.Add("dome-bt", new GitHubRepo("sam-ludlow", "dome-bt"));

				Globals.BitTorrentDirectory = Path.Combine(Globals.RootDirectory, "_BT");
				Directory.CreateDirectory(Globals.BitTorrentDirectory);

				try
				{

					BitTorrent.Initialize();
					BitTorrent.WaitReady();
					BitTorrent.EnableCore(coreName);
					BitTorrent.WaitReady();

					var torrentHashes = BitTorrent.TorrentHashes(coreName);
					string torrentHash = torrentHashes[ItemType.SoftwareDisk];

					JArray torrentFiles = BitTorrent.Files(torrentHash);

					foreach (dynamic torrentFile in torrentFiles)
						torrentDiskSizes.Add((string)torrentFile.path, (long)torrentFile.length);
				}
				finally
				{
					BitTorrent.Stop();
				}
			}

			//
			// Source Data
			//
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
			// Traverse - Main
			//
			var rowLookups = Operations.PerformanceDictionaries(dataSet);

			PayloadLevelInfo level_root = new PayloadLevelInfo(PayloadLevel.Root, null);
			PayloadLevelInfo level_softwarelist = new PayloadLevelInfo(PayloadLevel.Softwarelist, xmlJsonPayloads_softwarelist);
			PayloadLevelInfo level_software = new PayloadLevelInfo(PayloadLevel.Software, xmlJsonPayloads_software);

			level_root.Start($"{coreName.ToUpper()} ({version}) software lists");
			level_root.Append("<h2>Software Lists</h2>");
			level_root.TableStart("name", "description", "roms", "disks", "rom_size", "rom_size_text", "disk_size", "disk_size_text");

			foreach (DataRow softwarelistRow in dataSet.Tables["softwarelist"].Rows)
			{
				long softwarelist_id = (long)softwarelistRow["softwarelist_id"];
				string softwarelist_name = (string)softwarelistRow["name"];
				string softwarelist_description = (string)softwarelistRow["description"];

				//
				// SoftwareLists
				//
				level_softwarelist.Start($"{softwarelist_description} - {coreName} ({version}) software list");

				level_softwarelist.Append("<br />");
				level_softwarelist.Append($"<div><h2 style=\"display:inline;\">softwarelist</h2> &bull; <a href=\"{softwarelist_name}.xml\">XML</a> &bull; <a href=\"{softwarelist_name}.json\">JSON</a> </div>");
				level_softwarelist.Append("<br />");
				level_softwarelist.Append(softwarelistRow);

				level_softwarelist.Append("<hr />");
				level_softwarelist.Append("<h2>software</h2>");
				level_softwarelist.TableStart("name", "description", "roms", "disks", "rom_size", "rom_size_text", "disk_size", "disk_size_text");

				//
				// Software
				//
				foreach (DataRow softwareRow in rowLookups["software"][softwarelist_id])
				{
					long software_id = (long)softwareRow["software_id"];
					string software_name = (string)softwareRow["name"];

					string software_cloneof = softwareRow.Table.Columns.Contains("cloneof") ? softwareRow.Field<string>("cloneof") : null;
					string software_notes = softwareRow.Table.Columns.Contains("notes") ? softwareRow.Field<string>("notes") : null;

					level_software.Start($"{(string)softwareRow["description"]} - {(string)softwarelistRow["description"]} - {coreName} ({version}) software");
					level_software.Append("<br />");
					level_software.Append($"<div><h2 style=\"display:inline;\">software</h2> &bull; <a href=\"{software_name}.xml\">XML</a> &bull; <a href=\"{software_name}.json\">JSON</a> </div>");
					level_software.Append("<br />");
					level_software.TableStart("name", "description", "supported", "year", "publisher", "cloneof", "notes");
					level_software.TableRow(software_name, softwareRow.Field<string>("description"),
						softwareRow.Field<string>("supported"), softwareRow.Field<string>("year"), softwareRow.Field<string>("publisher"),
						software_cloneof != null ? $"<a href=\"/{coreName}/software/{softwarelist_name}/{software_cloneof}\">{software_cloneof}</a>" : null,
						software_notes);
					level_software.TableEnd();

					DataRow snapRow = snapTable?.Rows.Find($"{softwarelist_name}\\{software_name}");
					if (snapRow != null)
					{
						level_software.Append("<hr />");
						level_software.Append("<h2>snap</h2>");
						level_software.Append($"<img src=\"/{coreName}/software/{softwarelist_name}/{software_name}.png\" alt=\"{softwarelist_name}/{software_name} png snap\">");
						level_software.Append($"<img src=\"/{coreName}/software/{softwarelist_name}/{software_name}.jpg\" alt=\"{softwarelist_name}/{software_name} jpg snap thumbnail\">");
						level_software.Append(snapRow);
					}

					level_software.Append("<hr />");
					level_software.Append("<h2>softwarelist</h2>");
					level_software.TableStart("name", "description");
					level_software.TableRow($"<a href=\"/{coreName}/software/{softwarelist_name}\">{softwarelist_name}</a>", softwareRow.Field<string>("description"));
					level_software.TableEnd();

					List<DataRow> rows;

					if (dataSet.Tables.Contains("info") == true)
					{
						rows = rowLookups["info"][software_id];
						if (rows.Count > 0)
						{
							level_software.Append("<hr />");
							level_software.Append("<h2>info</h2>");
							level_software.Append(rows);
						}
					}

					if (dataSet.Tables.Contains("sharedfeat") == true)
					{
						rows = rowLookups["sharedfeat"][software_id];
						if (rows.Count > 0)
						{
							level_software.Append("<hr />");
							level_software.Append("<h2>sharedfeat</h2>");
							level_software.Append(rows);
						}
					}

					List<DataRow> partRows = rowLookups["part"][software_id];
					if (partRows.Count > 0)
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

								foreach (DataRow featureRow in rowLookups["feature"][part_id])
									table.Rows.Add(part_name, part_interface, featureRow["name"], featureRow["value"]);
							}
							if (table.Rows.Count > 0)
							{
								level_software.Append("<hr />");
								level_software.Append("<h2>part, feature</h2>");
								level_software.Append(table.Rows.Cast<DataRow>());
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

							foreach (DataRow dataareaRow in rowLookups["dataarea"][part_id])
							{
								long dataarea_id = (long)dataareaRow["dataarea_id"];

								foreach (DataRow romRow in rowLookups["rom"][dataarea_id])
								{
									long rom_size = Tools.ParseMameSize(romRow);

									level_software.Counts.Roms += 1;
									level_software.Counts.Size += rom_size;

									DataRow row = table.Rows.Add(part_name, part_interface,
										(string)dataareaRow["name"], (string)dataareaRow["size"], "", "");   //	TODO: fix for hbmame (string)dataareaRow["databits"], (string)dataareaRow["endian"]

									row["size_text"] = Tools.DataSize(rom_size);

									foreach (DataColumn column in dataSet.Tables["rom"].Columns)
										if (column.ColumnName.EndsWith("_id") == false)
											row[column.ColumnName] = romRow[column.ColumnName];
								}
							}
						}
						if (table.Rows.Count > 0)
						{
							level_software.Append("<hr />");
							level_software.Append("<h2>part, dataarea, rom</h2>");
							level_software.Append(table.Rows.Cast<DataRow>());
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

								foreach (DataRow diskareaRow in rowLookups["diskarea"][part_id])
								{
									long diskarea_id = (long)diskareaRow["diskarea_id"];

									foreach (DataRow diskRow in rowLookups["disk"][diskarea_id])
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

										level_software.Counts.Disks += 1;
										level_software.Counts.DiskSize += disk_size;
									}
								}
							}
							if (table.Rows.Count > 0)
							{
								level_software.Append("<hr />");
								level_software.Append("<h2>part, diskarea, disk</h2>");
								level_software.Append(table.Rows.Cast<DataRow>());
							}
						}

						level_softwarelist.Counts.Add(level_software.Counts);

						//
						// Software on SoftwareList - TODO NOT USED HTML
						//
						level_softwarelist.TableRow($"<a href=\"/{coreName}/software/{softwarelist_name}/{software_name}\">{software_name}</a>", (string)softwareRow["description"],
							level_software.Counts.Roms.ToString(), level_software.Counts.DiskSize.ToString(),
							level_software.Counts.Size.ToString(), Tools.DataSize(level_software.Counts.Size),
							level_software.Counts.DiskSize.ToString(), Tools.DataSize(level_software.Counts.DiskSize));
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

							level_software.Append("<hr />");
							level_software.Append($"<h2>machines ({status})</h2>");
							level_software.Append(machinesTable.Rows.Cast<DataRow>());
						}
					}

					level_software.Finish(softwarelist_name, software_name);
				}

				level_root.TableRow($"<a href=\"/{coreName}/software/{softwarelist_name}\">{softwarelist_name}</a>", softwarelist_description,
					level_softwarelist.Counts.Roms.ToString(), level_softwarelist.Counts.DiskSize.ToString(),
					level_softwarelist.Counts.Size.ToString(), Tools.DataSize(level_softwarelist.Counts.Size),
					level_softwarelist.Counts.DiskSize.ToString(), Tools.DataSize(level_softwarelist.Counts.DiskSize));

				level_softwarelist.TableEnd();
				level_softwarelist.Finish(softwarelist_name);
			}

			level_root.TableEnd();
			level_root.Finish("1");

			//
			// Save payloads
			//

			level_root.Save(connections[1]);
			level_softwarelist.Save(connections[1]);
			level_software.Save(connections[1]);

			//
			// Metadata				//	TODO dont get used in this db?
			//
			string info = $"{coreName} ({version}) &bull; released: {exeTime} &bull; software";
			Operations.CreateMetaDataTable(connections[1], coreName, version, info);

			Tools.ConsolePrintMemory();
		}

		public static void MameishMSSQLSoftwarePayloadsSearch(SqlConnection[] connections, string coreName, DataTable snapTable, DataSet dataSet)
		{
			//
			//	Search Payloads - software
			//
			string commandText = @"
				SELECT
					CONCAT(softwarelist.name, '@', software.name) AS [key],
					softwarelist.name AS softwarelist_name,
					software.name AS software_name,
					CAST(CASE WHEN software.supported = 'yes' THEN 1 ELSE 0 END AS BIT) AS [supported],
					software.description,
					software.year,
					software.publisher
				FROM
					softwarelist
					INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id
				ORDER BY
					softwarelist.name,
					software.name;
			";

			DataTable searchTable = new DataTable("software_search_payload");
			using (SqlDataAdapter adapter = new SqlDataAdapter(commandText, connections[1]))
				adapter.Fill(searchTable);
			searchTable.PrimaryKey = new DataColumn[] { searchTable.Columns["key"] };

			foreach (string name in new string[] { "xml", "json", "html", "html_card" })
				searchTable.Columns.Add(name, typeof(string));

			//
			// Traverse
			//
			var rowLookups = Operations.PerformanceDictionaries(dataSet);

			foreach (DataRow softwarelist_row in dataSet.Tables["softwarelist"].Rows)
			{
				long softwarelist_id = (long)softwarelist_row["softwarelist_id"];
				string softwarelist_name = (string)softwarelist_row["name"];

				Counts softwarelist_counts = new Counts();

				foreach (DataRow software_row in rowLookups["software"][softwarelist_id])
				{
					long software_id = (long)software_row["software_id"];
					string software_name = (string)software_row["name"];
					string software_description = (string)software_row["description"];
					string software_year = (string)software_row["year"];
					string software_publisher = (string)software_row["publisher"];
					string software_cloneof = software_row.Table.Columns.Contains("cloneof") ? software_row.Field<string>("cloneof") : null;

					DataRow snapRow = snapTable?.Rows.Find($"{softwarelist_name}\\{software_name}");

					Counts software_counts = new Counts();

					foreach (DataRow part_row in rowLookups["part"][software_id])
					{
						long part_id = (long)part_row["part_id"];

						foreach (DataRow dataarea_row in rowLookups["dataarea"][part_id])
						{
							foreach (DataRow rom_row in rowLookups["rom"][(long)dataarea_row["dataarea_id"]])
							{
								software_counts.Roms += 1;
								software_counts.Size += Tools.ParseMameSize(rom_row);
							}
						}

						if (dataSet.Tables.Contains("disk") == true)
						{
							foreach (DataRow diskarea_row in rowLookups["diskarea"][part_id])
							{
								foreach (DataRow disk_row in rowLookups["disk"][(long)diskarea_row["diskarea_id"]])
								{
									software_counts.Disks += 1;

									// TODO: Sizes in torrents
								}
							}
						}
					}

					softwarelist_counts.Games += 1;
					softwarelist_counts.Add(software_counts);

					DataRow searchRow = searchTable.Rows.Find($"{softwarelist_name}@{software_name}") ?? throw new ApplicationException($"Did not find search row: {softwarelist_name}@{software_name}");

					StringBuilder item = new StringBuilder();
					//
					// Search - Table row
					//
					item.Append("<tr>");

					foreach (string columnName in new string[] { "software_name", "description", "year", "publisher" })
					{
						DataColumn column = searchTable.Columns[columnName];
						item.Append("<td>");
						if (searchRow.IsNull(column) == false)
						{
							switch (columnName)
							{
								case "software_name":
									item.Append($"<a href=\"/{coreName}/software/{softwarelist_name}/{searchRow[column]}\">{searchRow[column]}</a>");
									break;
								default:
									item.Append(WebUtility.HtmlEncode(Convert.ToString(searchRow[column])));
									break;
							}
						}

						item.Append("</td>");
					}

					item.Append($"<td>{(software_cloneof != null ? $"<a href=\"/{coreName}/software/{softwarelist_name}/{software_cloneof}\">{software_cloneof}</a>" : "")}</td>");

					item.Append($"<td>{(softwarelist_counts.Roms > 0 ? softwarelist_counts.Roms.ToString() : "")}</td>");
					item.Append($"<td>{(softwarelist_counts.Disks > 0 ? softwarelist_counts.Disks.ToString() : "")}</td>");

					item.Append($"<td>{(softwarelist_counts.Roms > 0 ? softwarelist_counts.Size.ToString() : "")}</td>");
					item.Append($"<td>{(softwarelist_counts.Roms > 0 ? Tools.DataSize(softwarelist_counts.Size) : "")}</td>");
					item.Append($"<td>{(softwarelist_counts.Disks > 0 ? softwarelist_counts.DiskSize.ToString() : "")}</td>");
					item.Append($"<td>{(softwarelist_counts.Disks > 0 ? Tools.DataSize(softwarelist_counts.DiskSize) : "")}</td>");

					item.Append("</tr>");
					searchRow["html"] = item.ToString();

					//
					// Search - Div card
					//
					item = new StringBuilder();
					item.Append("<div class=\"card\">");

					item.Append($"<div class=\"card-thumb\"><a href=\"/{coreName}/software/{softwarelist_name}/{software_name}\" class=\"card-link\">");
					if (snapRow != null)
						item.Append($"<img src=\"/{coreName}/software/{softwarelist_name}/{software_name}.jpg\" alt=\"{software_description}\" loading=\"lazy\" class=\"card-img\" />");
					else
						item.Append($"<p>NO SNAP</p>");

					item.Append("</a></div>");

					item.Append("<div class=\"card-body\">");
					item.Append($"<div class=\"card-name\">{software_name}</div>");
					item.Append($"<div class=\"card-description\">{software_description}</div>");
					item.Append($"<div class=\"card-year\">{software_year}</div>");
					item.Append($"<div class=\"card-manufacturer\">{software_publisher}</div>");
					item.Append("</div>");

					item.Append("</div>");
					searchRow["html_card"] = item.ToString();
				}
			}


			//
			// Save Tables
			//

			Tools.SetDataTableStringLengths(searchTable);

			Operations.MakeMSSQLPayloadsInsert(connections[1], searchTable);

			//
			// Indexes
			//

			Database.ExecuteNonQuery(connections[1], @"
				CREATE FULLTEXT INDEX ON [software_search_payload]
				(
					[software_name],
					[description],
					[year],
					[publisher]
				)
				KEY INDEX [PK_software_search_payload]
				ON [ao_catalog]
				WITH CHANGE_TRACKING AUTO;
			");

			Database.ExecuteNonQuery(connections[1], @"
				CREATE NONCLUSTERED INDEX [IX_software_search_payload_softwarelist_name_description]
				ON [software_search_payload]
				(
					[softwarelist_name],
					[description]
				)
				INCLUDE (
					[html],
					[html_card]
				);
			");


		}


	}
}
