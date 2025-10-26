using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Spludlow.MameAO
{
	public class OperationsHtml
	{
		public static int MSSQLPayloadHtml(string serverConnectionString, string databaseNames, string coreName)
		{
			string[] databaseNamesEach = databaseNames.Split(new char[] { ',' });
			for (int index = 0; index < databaseNamesEach.Length; ++index)
				databaseNamesEach[index] = databaseNamesEach[index].Trim();

			using (SqlConnection machineConnection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseNamesEach[0]}';"))
			{
				MakeMSSQLPayloadHtmlMachine(machineConnection);

				using (SqlConnection softwareConnection = new SqlConnection(serverConnectionString + $"Initial Catalog='{databaseNamesEach[1]}';"))
					MakeMSSQLPayloadHtmlSoftware(softwareConnection, machineConnection, coreName);
			}

			return 0;
		}

		public static void MakeMSSQLPayloadHtmlMachine(SqlConnection connection)
		{
			//
			// Load all database
			//
			DataSet dataSet = new DataSet();

			List<string> conditionTableNames = new List<string>();

			foreach (string tableName in Database.TableList(connection))
			{
				if (tableName.EndsWith("_payload") == true || tableName == "sysdiagrams")
					continue;

				using (SqlDataAdapter adapter = new SqlDataAdapter($"SELECT * from [{tableName}]", connection))
					adapter.Fill(dataSet);

				dataSet.Tables[dataSet.Tables.Count - 1].TableName = tableName;

				if (tableName.EndsWith("_condition") == true)
					conditionTableNames.Add(tableName);
			}

			//ReportRelations(dataSet);

			//
			// Merge condition tables
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

			DataRow metaRow = dataSet.Tables["_metadata"].Rows[0];

			string datasetName = (string)metaRow["dataset"];
			string version = (string)metaRow["version"];

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

			using (SqlCommand command = new SqlCommand("UPDATE machine_payload SET [title] = @title, [html] = @html WHERE [machine_name] = @machine_name", connection))
			{
				command.Parameters.Add("@title", SqlDbType.NVarChar);
				command.Parameters.Add("@html", SqlDbType.NVarChar);
				command.Parameters.Add("@machine_name", SqlDbType.VarChar);

				connection.Open();

				try
				{
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
											switch (datasetName)
											{
												case "mame":
													baseUrl = $"https://github.com/mamedev/mame/blob/mame{version}/src";

													if (value.Split(new char[] { '/' }).Length == 2 && value.StartsWith("emu/") == false)
														value = $"<a href=\"{baseUrl}/{datasetName}/{value}\" target=\"_blank\">{value}</a>";
													else
														value = $"<a href=\"{baseUrl}/{value}\" target=\"_blank\">{value}</a>";
													break;
												case "hbmame":
													baseUrl = $"https://github.com/Robbbert/hbmame/blob/tag{version.Substring(2).Replace(".", "")}/src/hbmame/drivers";

													value = $"<a href=\"{baseUrl}/{value}\" target=\"_blank\">{value}</a>";
													break;

												default:
													throw new ApplicationException($"Unknown dataset: {datasetName}");
											}

											targetRow["sourcefile"] = value;
										}
										if (targetRow.IsNull("romof") == false)
										{
											string value = (string)targetRow["romof"];
											targetRow["romof"] = $"<a href=\"/{datasetName}/machine/{value}\">{value}</a>";
										}
										if (targetRow.IsNull("cloneof") == false)
										{
											string value = (string)targetRow["cloneof"];
											targetRow["cloneof"] = $"<a href=\"/{datasetName}/machine/{value}\">{value}</a>";
										}
										break;

									case "device_ref":
										if (targetRow.IsNull("name") == false)
										{
											string value = (string)targetRow["name"];
											targetRow["name"] = $"<a href=\"/{datasetName}/machine/{value}\">{value}</a>";
										}
										break;


									case "softwarelist":
										if (targetRow.IsNull("name") == false)
										{
											string value = (string)targetRow["name"];
											targetRow["name"] = $"<a href=\"/{datasetName}/software/{value}\">{value}</a>";
										}
										break;
								}
							}

							if (tableName == "machine")
							{
								html.AppendLine("<br />");
								html.AppendLine($"<div><h2 style=\"display:inline;\">machine</h2> &bull; <a href=\"{machine_name}.xml\">XML</a> &bull; <a href=\"{machine_name}.json\">JSON</a> &bull; <a href=\"#\" onclick=\"mameAO('{machine_name}@{datasetName}'); return false\">AO</a></div>");
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
										row["slotoption_devname"] = $"<a href=\"/{datasetName}/machine/{value}\">{value}</a>";
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

						command.Parameters["@title"].Value = $"{(string)machineRow["description"]} - {datasetName} ({version}) machine";
						command.Parameters["@html"].Value = html.ToString();
						command.Parameters["@machine_name"].Value = machine_name;

						command.ExecuteNonQuery();

					}
				}
				finally
				{
					connection.Close();
				}
			}
		}

		public static void MakeMSSQLPayloadHtmlSoftware(SqlConnection connection, SqlConnection machineConnection, string coreName)
		{
			DataSet dataSet = new DataSet();

			foreach (string tableName in Database.TableList(connection))
			{
				if (tableName.EndsWith("_payload") == true || tableName == "sysdiagrams")
					continue;

				using (SqlDataAdapter adapter = new SqlDataAdapter($"SELECT * from [{tableName}]", connection))
					adapter.Fill(dataSet);

				dataSet.Tables[dataSet.Tables.Count - 1].TableName = tableName;
			}

			DataTable machineListTable = Database.ExecuteFill(machineConnection, "SELECT machine.name AS machine_name, driver.status, softwarelist.name AS softwarelist_name " +
				"FROM (machine LEFT JOIN driver ON machine.machine_id = driver.machine_id) INNER JOIN softwarelist ON machine.machine_id = softwarelist.machine_id");

			foreach (DataRow row in machineListTable.Rows)
			{
				if (row.IsNull("status") == true)
					row["status"] = "no driver";
			}

			DataTable machineDetailTable = Database.ExecuteFill(machineConnection, "SELECT machine.name, machine.description FROM machine");
			machineDetailTable.PrimaryKey = new DataColumn[] { machineDetailTable.Columns["name"] };

			//ReportRelations(dataSet);

			DataRow metaRow = dataSet.Tables["_metadata"].Rows[0];

			string version = (string)metaRow["version"];

			connection.Open();

			try
			{
				SqlCommand softwarelistCommand = new SqlCommand("UPDATE softwarelist_payload SET [title] = @title, [html] = @html WHERE [softwarelist_name] = @softwarelist_name", connection);
				softwarelistCommand.Parameters.Add("@title", SqlDbType.NVarChar);
				softwarelistCommand.Parameters.Add("@html", SqlDbType.NVarChar);
				softwarelistCommand.Parameters.Add("@softwarelist_name", SqlDbType.VarChar);

				SqlCommand softwareCommand = new SqlCommand("UPDATE software_payload SET [title] = @title, [html] = @html WHERE ([softwarelist_name] = @softwarelist_name AND [software_name] = @software_name)", connection);
				softwareCommand.Parameters.Add("@title", SqlDbType.NVarChar);
				softwareCommand.Parameters.Add("@html", SqlDbType.NVarChar);
				softwareCommand.Parameters.Add("@softwarelist_name", SqlDbType.VarChar);
				softwareCommand.Parameters.Add("@software_name", SqlDbType.VarChar);

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
					"name	description",
					"String	String"
				);

				foreach (DataRow softwarelistRow in dataSet.Tables["softwarelist"].Select(null, "description"))
				{
					long softwarelist_id = (long)softwarelistRow["softwarelist_id"];
					string softwarelist_name = (string)softwarelistRow["name"];
					string softwarelist_description = (string)softwarelistRow["description"];

					//if (softwarelist_name != "x68k_flop" && softwarelist_name != "amiga_cd")
					//	continue;

					DataRow[] softwareRows = dataSet.Tables["software"].Select($"softwarelist_id = {softwarelist_id}");

					//
					// SoftwareLists
					//

					StringBuilder html = new StringBuilder();

					html.AppendLine("<br />");
					html.AppendLine($"<div><h2 style=\"display:inline;\">softwarelist</h2> &bull; <a href=\"{softwarelist_name}.xml\">XML</a> &bull; <a href=\"{softwarelist_name}.json\">JSON</a> </div>");
					html.AppendLine("<br />");
					html.AppendLine(Reports.MakeHtmlTable(dataSet.Tables["softwarelist"], new DataRow[] { softwarelistRow }, null));

					html.AppendLine("<hr />");
					html.AppendLine("<h2>software</h2>");
					DataTable softwareTable = dataSet.Tables["software"].Clone();
					foreach (DataRow softwareRow in softwareRows)
					{
						softwareTable.ImportRow(softwareRow);
						DataRow row = softwareTable.Rows[softwareTable.Rows.Count - 1];
						string value = (string)row["name"];
						row["name"] = $"<a href=\"/{coreName}/software/{softwarelist_name}/{value}\">{value}</a>";
						if (softwareTable.Columns.Contains("cloneof") == true && row.IsNull("cloneof") == false)
						{
							value = (string)row["cloneof"];
							row["cloneof"] = $"<a href=\"/{coreName}/software/{softwarelist_name}/{value}\">{value}</a>";
						}
					}
					html.AppendLine(Reports.MakeHtmlTable(softwareTable, null));

					listTable.Rows.Add($"<a href=\"/{coreName}/software/{softwarelist_name}\">{softwarelist_name}</a>", softwarelist_description);

					softwarelistCommand.Parameters["@title"].Value = $"{softwarelist_description} - {coreName} ({version}) software list";
					softwarelistCommand.Parameters["@html"].Value = html.ToString();
					softwarelistCommand.Parameters["@softwarelist_name"].Value = softwarelist_name;

					softwarelistCommand.ExecuteNonQuery();

					//
					// Software
					//

					softwarelistRow["name"] = $"<a href=\"/{coreName}/software/{softwarelist_name}\">{softwarelist_name}</a>";

					foreach (DataRow softwareRow in softwareRows)
					{
						long software_id = (long)softwareRow["software_id"];
						string software_name = (string)softwareRow["name"];

						if (softwareTable.Columns.Contains("cloneof") == true && softwareRow.IsNull("cloneof") == false)
						{
							string value = (string)softwareRow["cloneof"];
							softwareRow["cloneof"] = $"<a href=\"/{coreName}/software/{softwarelist_name}/{value}\">{value}</a>";
						}

						html = new StringBuilder();

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
										DataRow row = table.Rows.Add(part_name, part_interface,
											(string)dataareaRow["name"], (string)dataareaRow["size"], (string)dataareaRow["databits"], (string)dataareaRow["endian"]);

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
						}

						DataRow[] machineListRows = machineListTable.Select($"softwarelist_name = '{softwarelist_name}'");

						foreach (string status in new string[] { "good", "imperfect", "preliminary", "no driver" })
						{
							DataRow[] statusRows = machineListRows.Where(row => (string)row["status"] == status).ToArray();

							if (statusRows.Length > 0)
							{
								DataTable machinesTable = new DataTable();
								machinesTable.Columns.Add("name", typeof(string));
								machinesTable.Columns.Add("description (AO)", typeof(string));

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

						softwareCommand.Parameters["@title"].Value = $"{(string)softwareRow["description"]} - {(string)softwarelistRow["description"]} - {coreName} ({version}) software";
						softwareCommand.Parameters["@html"].Value = html.ToString();
						softwareCommand.Parameters["@softwarelist_name"].Value = softwarelist_name;
						softwareCommand.Parameters["@software_name"].Value = software_name;

						softwareCommand.ExecuteNonQuery();
					}
				}

				SqlCommand softwarelistsCommand = new SqlCommand("UPDATE softwarelists_payload SET [title] = @title, [html] = @html WHERE [key_1] = '1'", connection);
				softwarelistsCommand.Parameters.Add("@title", SqlDbType.NVarChar);
				softwarelistsCommand.Parameters.Add("@html", SqlDbType.NVarChar);

				softwarelistsCommand.Parameters["@title"].Value = $"Software Lists - {coreName} ({version}) software";
				softwarelistsCommand.Parameters["@html"].Value = Reports.MakeHtmlTable(listTable, null);

				softwarelistsCommand.ExecuteNonQuery();
			}
			finally
			{
				connection.Close();
			}

		}

	}
}
