using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Spludlow.MameAO
{
	public class OperationsDatish
	{
		/// <summary>
		/// xml: single file conatining all datafiles <datafiles version="2026-04-24T07-20-15">	<datafile key="arcade">
		/// </summary>
		public static int FBNeoMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName)
		{
			string coreName = "fbneo";

			directory = Path.Combine(directory, version);

			var xmlJsonPayloads_datafile = new Dictionary<string, string[]>();
			var xmlJsonPayloads_game = new Dictionary<string, string[]>();

			using (var reader = XmlReader.Create(Path.Combine(directory, "_fbneo.xml")))    //, _XmlReaderSettings))	//	TODO dont use reader
			{
				while (reader.ReadToFollowing("datafile"))
				{
					var datafile = (XElement)XElement.ReadFrom(reader);
					var datafile_key = (string)datafile.Element("header").Element("name");

					xmlJsonPayloads_datafile.Add($"{coreName}\t{datafile_key}", new string[] { datafile.ToString(), Tools.XML2JSON(datafile) });

					foreach (var game in datafile.Elements("game"))
					{
						var game_name = (string)game.Attribute("name");

						xmlJsonPayloads_game.Add($"{coreName}\t{datafile_key}\t{game_name}", new string[] { game.ToString(), Tools.XML2JSON(game) });
					}
				}
			}

			using (SqlConnection connection = new SqlConnection($"{serverConnectionString}Database='{databaseName}';"))
				return DatishMSSQLPayloads(connection, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game);
		}

		/// <summary>
		/// xml: file for each subset (category) (filename is subset key) <category name="TOSEC-ISO"> <datafile>
		/// </summary>
		public static int TosecMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName)
		{
			string coreName = "tosec";

			directory = Path.Combine(directory, version);

			var xmlJsonPayloads_datafile = new Dictionary<string, string[]>();
			var xmlJsonPayloads_game = new Dictionary<string, string[]>();

			foreach (string filename in Directory.GetFiles(directory, "*.xml"))
			{
				string subset_name = Path.GetFileNameWithoutExtension(filename).Substring(1);

				var categoryElement = XElement.Load(filename, LoadOptions.None);

				foreach (var datafileElement in categoryElement.Elements("datafile"))
				{
					var datafile_name = datafileElement.Element("header").Element("name").Value;

					Console.WriteLine(datafile_name);

					xmlJsonPayloads_datafile.Add($"{subset_name}\t{datafile_name}", new string[] { datafileElement.ToString(), Tools.XML2JSON(datafileElement) });

					foreach (var game in datafileElement.Elements("game"))
					{
						var game_name = game.Attribute("name").Value;
						string key = $"{subset_name}\t{datafile_name}\t{game_name}";

						if (xmlJsonPayloads_game.ContainsKey(key) == false)
							xmlJsonPayloads_game.Add(key, new string[] { game.ToString(), Tools.XML2JSON(game) });
						else
							Console.WriteLine($"!!! Warning Duplicate XML game: {key}");
					}
				}
			}

			using (SqlConnection connection = new SqlConnection($"{serverConnectionString}Database='{databaseName}';"))
				return DatishMSSQLPayloads(connection, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game);
		}

		/// <summary>
		/// xml: file for each datafile <datafile>
		/// </summary>
		public static int RedumpMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName)
		{
			string coreName = "redump";

			directory = Path.Combine(directory, version);

			var xmlJsonPayloads_datafile = new Dictionary<string, string[]>();
			var xmlJsonPayloads_game = new Dictionary<string, string[]>();

			foreach (string filename in Directory.GetFiles(directory, "*.xml"))
			{
				var datafile = XElement.Load(filename, LoadOptions.None);
				var datafile_name = (string)datafile.Element("header").Element("name");

				xmlJsonPayloads_datafile.Add($"{coreName}\t{datafile_name}", new string[] { datafile.ToString(), Tools.XML2JSON(datafile) });

				foreach (var game in datafile.Elements("game"))
				{
					var game_name = (string)game.Attribute("name");
					string key = $"{coreName}\t{datafile_name}\t{game_name}";
					xmlJsonPayloads_game.Add(key, new string[] { game.ToString(), Tools.XML2JSON(game) });
				}
			}

			using (SqlConnection connection = new SqlConnection($"{serverConnectionString}Database='{databaseName}';"))
				return DatishMSSQLPayloads(connection, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game);
		}

		/// <summary>
		/// xml: file for each subset (filename is subset key) <subset name="Source Code"> <datafile
		/// </summary>
		public static int NoIntroMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName)
		{
			string coreName = "no-intro";

			directory = Path.Combine(directory, version);

			var xmlJsonPayloads_datafile = new Dictionary<string, string[]>();
			var xmlJsonPayloads_game = new Dictionary<string, string[]>();

			foreach (string filename in Directory.GetFiles(directory, "*.xml"))
			{
				string subset = Path.GetFileNameWithoutExtension(filename);

				var subsetElement = XElement.Load(filename, LoadOptions.None);

				foreach (var datafileElement in subsetElement.Elements("datafile"))
				{
					foreach (var element in datafileElement.Elements("machine"))    //	Everything else is "game"
						element.Name = "game";

					var datafile_name = datafileElement.Element("header").Element("name").Value;

					Console.WriteLine(datafile_name);

					xmlJsonPayloads_datafile.Add($"{subset}\t{datafile_name}", new string[] { datafileElement.ToString(), Tools.XML2JSON(datafileElement) });

					foreach (var game in datafileElement.Elements("game"))
					{
						var game_name = game.Attribute("name").Value;
						string key = $"{subset}\t{datafile_name}\t{game_name}";

						if (xmlJsonPayloads_game.ContainsKey(key) == false)
							xmlJsonPayloads_game.Add(key, new string[] { game.ToString(), Tools.XML2JSON(game) });
						else
							Console.WriteLine($"!!! Warning Duplicate XML game: {key}");
					}
				}
			}

			using (SqlConnection connection = new SqlConnection($"{serverConnectionString}Database='{databaseName}';"))
				return DatishMSSQLPayloads(connection, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game);
		}



		private static int DatishMSSQLPayloads(
			SqlConnection connection,
			string coreName,
			string version,
			Dictionary<string, string[]> xmlJsonPayloads_datafile,
			Dictionary<string, string[]> xmlJsonPayloads_game)
		{
			Tools.ConsolePrintMemory();

			//
			// Source Data
			//
			HashSet<string> sortTableNames = new HashSet<string>(new string[] { "subset", "datafile", "game", "rom", "sample" });

			var tableNames = Database.TableList(connection).Where(n => n.StartsWith("_") == false && n.EndsWith("_payload") == false).OrderBy(n => n).ToList();

			var dataSet = new DataSet();
			foreach (string tableName in tableNames)
			{
				var commandText = $"SELECT * FROM [{tableName}]";
				if (sortTableNames.Contains(tableName))
					commandText += " ORDER BY [name]";

				Console.Write($"{commandText} ...");
				
				var table = Database.ExecuteFill(connection, commandText);
				table.TableName = tableName;
				dataSet.Tables.Add(table);

				Console.WriteLine("...done");
			}

			//
			// Traverse
			//
			PayloadLevelInfo level_root = new PayloadLevelInfo(PayloadLevel.Root, null);
			PayloadLevelInfo level_subset = new PayloadLevelInfo(PayloadLevel.Subset, null);
			PayloadLevelInfo level_datafile = new PayloadLevelInfo(PayloadLevel.Datafile, xmlJsonPayloads_datafile);
			PayloadLevelInfo level_game = new PayloadLevelInfo(PayloadLevel.Game, xmlJsonPayloads_game);

			level_root.Start($"{coreName} ({version})");
			level_root.Append($"<h2>Subsets</h2>");
			level_root.TableStart("Name", "Description", "Datafiles", "Games", "Roms", "Bytes", "Size", "Extentions");

			foreach (DataRow subsetRow in dataSet.Tables["subset"].Rows)
			{
				long subset_id = (long)subsetRow["subset_id"];
				string subset_name = (string)subsetRow["name"];
				string subset_description = (string)subsetRow["description"];

				Tools.ConsoleHeading(2, $"{subset_name} : {subset_description}");

				level_subset.Start($"{coreName} ({version}) &bull; {subset_name}");

				level_subset.Append("<h2>Subset</h2>");
				level_subset.Append(subsetRow);

				level_subset.Append("<hr />");

				level_subset.Append($"<h2>Datafiles</h2>");
				level_subset.TableStart("Name", "Description", "Games", "Roms", "Bytes", "Size", "Extentions");

				foreach (DataRow datafileRow in dataSet.Tables["datafile"].Select($"[subset_id] = {subset_id}"))
				{
					long datafile_id = (long)datafileRow["datafile_id"];
					string datafile_name = (string)datafileRow["name"];
					string datafile_description = (string)datafileRow["description"];
					string datafile_name_enc = Uri.EscapeDataString(datafile_name);

					level_datafile.Start($"{coreName} ({version}) &bull; {subset_name} &bull; {WebUtility.HtmlEncode(datafile_name)}");

					level_datafile.Counts.Datafiles = 1;

					level_datafile.Append($"<div style=\"margin: 1em 0;\"><h2 style=\"display:inline;\">Datafile</h2> &bull; <a href=\"{datafile_name_enc}.xml\">XML</a> &bull; <a href=\"{datafile_name_enc}.json\">JSON</a></div>");
					level_datafile.Append(datafileRow);

					level_datafile.Append("<hr />");

					level_datafile.Append($"<h2>Games</h2>");
					level_datafile.TableStart("Name", "Description", "Roms", "Bytes", "Size", "Extentions");

					foreach (DataRow gameRow in dataSet.Tables["game"].Select($"[datafile_id] = {datafile_id}"))
					{
						long game_id = (long)gameRow["game_id"];
						string game_name = (string)gameRow["name"];
						string game_description = (string)gameRow["description"];
						string game_name_enc = Uri.EscapeDataString(game_name);

						level_game.Start($"{coreName} ({version}) &bull; {subset_name} &bull; {WebUtility.HtmlEncode(datafile_name)} &bull; {WebUtility.HtmlEncode(game_name)}");

						level_game.Counts.Games = 1;
						//

						level_game.Append($"<div style=\"margin: 1em 0;\"><h2 style=\"display:inline;\">Game</h2> &bull; <a href=\"{game_name_enc}.xml\">XML</a> &bull; <a href=\"{game_name_enc}.json\">JSON</a></div>");
						level_game.Append(gameRow);
						
						level_game.Append("<hr />");

						level_game.Append($"<h2>Roms</h2>");
						level_game.TableStart("Name", "Bytes", "Size", "CRC", "SHA1", "MD5");

						foreach (DataRow romRow in dataSet.Tables["rom"].Select($"[game_id] = {game_id}"))
						{
							string rom_name = (string)romRow["name"];
							string crc = romRow.Field<string>("crc");
							string sha1 = romRow.Field<string>("sha1");
							string md5 = romRow.Field<string>("md5");

							string size = romRow.Field<string>("size");
							long rom_size = Int64.Parse(string.IsNullOrEmpty(size) ? "0" : size);

							level_game.Counts.Roms += 1;
							level_game.Counts.Size += rom_size;

							string extention = Path.GetExtension(rom_name).ToLower();
							level_game.Counts.AddExtention(extention);

							level_game.TableRow(rom_name, rom_size.ToString(), Tools.DataSize(rom_size), crc, sha1, md5);
						}

						level_datafile.Counts.Add(level_game.Counts);

						level_game.TableEnd();
						level_game.Finish(subset_name, datafile_name, game_name);

						//	TODO level_game ... rest of tables

						level_datafile.TableRow($"<a href=\"/{coreName}/{subset_name}/{datafile_name_enc}/{game_name_enc}\">{game_name}</a>", game_description,
							level_game.Counts.Roms.ToString(), level_game.Counts.Size.ToString(), Tools.DataSize(level_game.Counts.Size),
							level_game.Counts.ExtentionsToString()
						);
					}

					level_subset.Counts.Add(level_datafile.Counts);

					level_datafile.TableEnd();
					level_datafile.Finish(subset_name, datafile_name);

					level_subset.TableRow($"<a href=\"/{coreName}/{subset_name}/{datafile_name_enc}\">{datafile_name}</a>", datafile_description,
						level_datafile.Counts.Games.ToString(), level_datafile.Counts.Roms.ToString(), level_datafile.Counts.Size.ToString(), Tools.DataSize(level_datafile.Counts.Size),
						level_datafile.Counts.ExtentionsToString()
					);
				}

				level_root.Counts.Add(level_subset.Counts);

				level_subset.TableEnd();
				level_subset.Finish(subset_name);

				level_root.TableRow($"<a href=\"/{coreName}/{subset_name}\">{subset_name}</a>", subset_description,
					level_subset.Counts.Datafiles.ToString(), level_subset.Counts.Games.ToString(), level_subset.Counts.Roms.ToString(), level_subset.Counts.Size.ToString(), Tools.DataSize(level_subset.Counts.Size),
					level_subset.Counts.ExtentionsToString()
				);
			}

			level_root.TableEnd();
			level_root.Finish("1");

			//
			// Metadata
			//
			string info = $"{coreName} ({version}) &bull; {Tools.DataSize(level_root.Counts.Size)} &bull; subsets: {dataSet.Tables["subset"].Rows.Count} &bull; datafiles: {dataSet.Tables["datafile"].Rows.Count} &bull; games: {dataSet.Tables["game"].Rows.Count} &bull; roms: {dataSet.Tables["rom"].Rows.Count}";

			OperationsPayload.CreateMetaDataTable(connection, coreName, version, info);

			//
			// Save payload tables
			//
			level_root.Save(connection);
			level_subset.Save(connection);
			level_datafile.Save(connection);
			level_game.Save(connection);

			//
			// Indexes
			//
			Console.Write("Create Indexes ...");

			if (Database.IndexExists(connection, "rom", "IX_rom_name") == false)
			{
				Database.ExecuteNonQuery(connection, @"
						CREATE NONCLUSTERED INDEX IX_rom_name
						ON [rom] (name, game_id)
						INCLUDE (size, sha1, crc);

						CREATE NONCLUSTERED INDEX IX_rom_sha1
						ON [rom] (sha1, game_id)
						INCLUDE (name, size, crc);

						CREATE NONCLUSTERED INDEX IX_rom_crc
						ON [rom] (crc, game_id)
						INCLUDE (name, size, sha1);
					");
			}

			if (Database.FullTextColumnExists(connection, "game", "name") == false)
				Database.ExecuteNonQuery(connection, @"
						CREATE FULLTEXT INDEX ON [game]
						(
							[name],
							[description]
						)
						KEY INDEX [PK_game]
						ON [ao_catalog]
						WITH CHANGE_TRACKING AUTO;
					");

			Console.WriteLine("...done");

			Tools.ConsolePrintMemory();

			return 0;
		}

		public enum PayloadLevel { Root, Subset, Datafile, Game };

		public class Counts
		{
			public long Datafiles = 0;
			public long Games = 0;
			public long Roms = 0;
			public long Size = 0;
			public Dictionary<string, int> Extentions = new Dictionary<string, int>();

			public void Add(Counts counts)
			{
				Datafiles += counts.Datafiles;
				Games += counts.Games;
				Roms += counts.Roms;
				Size += counts.Size;

				foreach (var extention in counts.Extentions)
				{
					if (Extentions.ContainsKey(extention.Key) == false)
						Extentions.Add(extention.Key, 0);
					Extentions[extention.Key] += extention.Value;
				}
			}

			public void AddExtention(string extention)
			{
				if (extention.Length == 0)
					extention = "_";
				else
					extention = extention.Substring(1);

				if (Extentions.ContainsKey(extention) == false)
					Extentions.Add(extention, 0);

				Extentions[extention] += 1;
			}

			public string ExtentionsToString()
			{
				int max = 10;

				var extentions = Extentions.OrderByDescending(pair => pair.Value).Cast<KeyValuePair<string, int>>();
				
				if (extentions.Count() > max)
				{
					int remainingCount = 0;
					foreach (int count in extentions.Skip(10).Select(pair => pair.Value))
						remainingCount += count;

					extentions = extentions.Take(max);
					extentions = extentions.Append(new KeyValuePair<string, int>("...", remainingCount));
					extentions = extentions.OrderByDescending(pair => pair.Value);
				}

				return String.Join(", ", extentions.Select(pair => $"{pair.Key}({pair.Value})"));
			}
		}

		public class PayloadLevelInfo
		{
			public DataTable DataTable;

			public Counts Counts = new Counts();

			private string HtmlTitle;
			private StringBuilder HtmlPage = new StringBuilder();

			private int TableWidth = 0;

			private Dictionary<string, string[]> XmlJsonPayloads;

			public PayloadLevelInfo(
				PayloadLevel level,
				Dictionary<string, string[]> xmlJsonPayloads)
			{
				XmlJsonPayloads = xmlJsonPayloads;

				switch (level)
				{
					case PayloadLevel.Root:
						DataTable = OperationsPayload.MakePayloadDataTable("root_payload", new string[] { "key_1" });
						break;

					case PayloadLevel.Subset:
						DataTable = OperationsPayload.MakePayloadDataTable("subset_payload", new string[] { "subset_name" });
						break;

					case PayloadLevel.Datafile:
						DataTable = OperationsPayload.MakePayloadDataTable("datafile_payload", new string[] { "subset_name", "datafile_name" });
						break;

					case PayloadLevel.Game:
						DataTable = OperationsPayload.MakePayloadDataTable("game_payload", new string[] { "subset_name", "datafile_name", "game_name" });
						break;

					default:
						throw new ApplicationException("On another level.");
				}
			}

			public void Start(string title)
			{
				if (HtmlPage.Length != 0)
					throw new ApplicationException("Unfinished Business");

				Counts = new Counts();

				HtmlTitle = title;
			}
			public void Finish(params string[] keys)
			{
				if (keys.Length != DataTable.PrimaryKey.Length)
					throw new ApplicationException("Bad keys width");

				if (DataTable.Rows.Find(keys) != null)
				{
					Console.WriteLine($"!!! Warning Duplicate Item {DataTable.TableName}:\t{String.Join("\t", keys)}");
				}
				else
				{
					HtmlPage.AppendLine("<br />");

					string[] xmlJson = new string[] { "", "" };
					if (XmlJsonPayloads != null)
					{
						string key = String.Join("\t", keys);

						if (XmlJsonPayloads.ContainsKey(key) == false)
							throw new ApplicationException($"Did not find xml json lookup {key}");
						xmlJson = XmlJsonPayloads[key];
					}

					var rowData = new List<object>();
					rowData.AddRange(keys);
					rowData.AddRange(new string[] { HtmlTitle, xmlJson[0], xmlJson[1], HtmlPage.ToString() });

					DataTable.Rows.Add(rowData.ToArray());
				}

				HtmlPage.Length = 0;
			}

			public void Append(string html)
			{
				HtmlPage.AppendLine(html);
			}
			public void Append(DataRow row)
			{
				string[] columnNames = row.Table.Columns.Cast<DataColumn>().Select(col => col.ColumnName).Where(name => name.EndsWith("_id") == false).ToArray();

				TableStart(columnNames);
				TableRow(columnNames.Select(col => row.IsNull(col) ? "" : (string)row[col]).ToArray());
				TableEnd();
			}
			public void TableStart(params string[] columnNames)
			{
				TableWidth = columnNames.Length;
				HtmlPage.AppendLine("<table>");
				HtmlPage.AppendLine(EncodeTableRow(columnNames, "th"));
			}
			public void TableRow(params string[] values)
			{
				if (values.Length != TableWidth)
					throw new ApplicationException("Bad values width");

				HtmlPage.AppendLine(EncodeTableRow(values, "td"));
			}

			public void TableEnd()
			{
				HtmlPage.AppendLine("</table>");
			}

			private string EncodeTableRow(IEnumerable<string> values, string type)
			{
				values = values.Select(value => {
					if (value != null && value.StartsWith("<a href") == false)
						value = WebUtility.HtmlEncode(value);
					return value;
				});

				return $"<tr>{String.Join("", values.Select(value => $"<{type}>{value}</{type}>"))}</tr>";
			}

			public void Save(SqlConnection connection)
			{
				OperationsPayload.MakeMSSQLPayloadsInsert(connection, DataTable);
			}
		}
	}
}
