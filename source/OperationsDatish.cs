using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Newtonsoft.Json.Linq;

namespace Spludlow.MameAO
{
	public class OperationsDatish
	{
		private static readonly XmlReaderSettings _XmlReaderSettings = new XmlReaderSettings()
		{
			DtdProcessing = DtdProcessing.Parse,
			IgnoreComments = false,
			IgnoreWhitespace = true,
		};

		/// <summary>
		/// xml: single file conatining all datafiles <datafiles version="2026-04-24T07-20-15">	<datafile key="arcade">
		/// subset: single
		/// </summary>
		public static int FBNeoMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName)
		{
			string coreName = "fbneo";

			directory = Path.Combine(directory, version);

			var xmlJsonPayloads_datafile = new Dictionary<string, string[]>();
			var xmlJsonPayloads_game = new Dictionary<string, string[]>();

			using (var reader = XmlReader.Create(Path.Combine(directory, "_fbneo.xml")))	//, _XmlReaderSettings))
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

			//	XML			---
			//	Source Data	429 Megabytes (MiB)

			using (SqlConnection connection = new SqlConnection($"{serverConnectionString}Database='{databaseName}';"))
				return DatishMSSQLPayloads(connection, directory, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game);
		}

		/// <summary>
		/// xml: file for each subset (category) (filename is subset key) <category name="TOSEC-ISO"> <datafile>
		/// subset: 3 each category
		/// </summary>
		public static int TosecMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName)
		{
			string coreName = "tosec";

			directory = Path.Combine(directory, version);

			var xmlJsonPayloads_datafile = new Dictionary<string, string[]>();
			var xmlJsonPayloads_game = new Dictionary<string, string[]>();

			foreach (string filename in Directory.GetFiles(directory, "*.xml"))
			{
				string subset = Path.GetFileNameWithoutExtension(filename).Substring(1);

				using (var reader = XmlReader.Create(filename, _XmlReaderSettings))
				{
					while (reader.ReadToFollowing("datafile"))
					{
						var datafile = (XElement)XElement.ReadFrom(reader);
						var datafile_name = (string)datafile.Element("header").Element("name");

						xmlJsonPayloads_datafile.Add($"{subset}\t{datafile_name}", new string[] { datafile.ToString(), Tools.XML2JSON(datafile) });

						foreach (var game in datafile.Elements("game"))
						{
							var game_name = (string)game.Attribute("name");

							string key = $"{subset}\t{datafile_name}\t{game_name}";

							if (xmlJsonPayloads_game.ContainsKey(key) == false)
								xmlJsonPayloads_game.Add(key, new string[] { game.ToString(), Tools.XML2JSON(game) });
							else
								Console.WriteLine($"!!! Warning Duplicate XML game: {key}");
						}
					}
				}
			}

			//	XML			---
			//	Sourcedata	3.6 Gigabytes (GiB)

			using (SqlConnection connection = new SqlConnection($"{serverConnectionString}Database='{databaseName}';"))
				return DatishMSSQLPayloads(connection, directory, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game);
		}

		/// <summary>
		/// xml: file for each datafile <datafile>
		/// subset: single
		/// </summary>
		public static int RedumpMSSQLPayloads(string directory, string version, string serverConnectionString, string databaseName)
		{
			string coreName = "redump";

			directory = Path.Combine(directory, version);

			var xmlJsonPayloads_datafile = new Dictionary<string, string[]>();
			var xmlJsonPayloads_game = new Dictionary<string, string[]>();

			foreach (string filename in Directory.GetFiles(directory, "*.xml"))
			{
				using (var reader = XmlReader.Create(filename, _XmlReaderSettings))
				{
					while (reader.ReadToFollowing("datafile"))
					{
						var datafile = (XElement)XElement.ReadFrom(reader);
						var datafile_name = (string)datafile.Element("header").Element("name");

						xmlJsonPayloads_datafile.Add($"{coreName}\t{datafile_name}", new string[] { datafile.ToString(), Tools.XML2JSON(datafile) });

						foreach (var game in datafile.Elements("game"))
						{
							var game_name = (string)game.Attribute("name");

							string key = $"{coreName}\t{datafile_name}\t{game_name}";
							xmlJsonPayloads_game.Add(key, new string[] { game.ToString(), Tools.XML2JSON(game) });
						}
					}
				}
			}

			//	XML			---
			//	Source Data	1.3 Gigabytes (GiB)

			using (SqlConnection connection = new SqlConnection($"{serverConnectionString}Database='{databaseName}';"))
				return DatishMSSQLPayloads(connection, directory, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game);
		}

		/// <summary>
		/// xml: file for each subset (filename is subset key) <subset name="Source Code"> <datafile
		/// subset:4 each subset
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

				using (var reader = XmlReader.Create(filename)) //, _XmlReaderSettings)) skips every other datafile ?
				{
					while (reader.ReadToFollowing("datafile"))
					{
						var datafile = (XElement)XElement.ReadFrom(reader);
						var datafile_name = (string)datafile.Element("header").Element("name");

						Console.WriteLine(datafile_name);

						xmlJsonPayloads_datafile.Add($"{subset}\t{datafile_name}", new string[] { datafile.ToString(), Tools.XML2JSON(datafile) });

						foreach (var game in datafile.Elements("game"))
						{
							var game_name = (string)game.Attribute("name");

							string key = $"{subset}\t{datafile_name}\t{game_name}";

							if (xmlJsonPayloads_game.ContainsKey(key) == false)
								xmlJsonPayloads_game.Add(key, new string[] { game.ToString(), Tools.XML2JSON(game) });
							else
								Console.WriteLine($"!!! Warning Duplicate XML game: {key}");
						}
					}
				}
			}




			using (SqlConnection connection = new SqlConnection($"{serverConnectionString}Database='{databaseName}';"))
				return DatishMSSQLPayloads(connection, directory, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game);
		}



		private static int DatishMSSQLPayloads(
			SqlConnection connection,
			string directory,
			string coreName,
			string version,
			Dictionary<string, string[]> xmlJsonPayloads_datafile,
			Dictionary<string, string[]> xmlJsonPayloads_game)
		{
			Tools.ConsolePrintMemory();

			//
			// Metadata
			//
			int datafileCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM datafile");
			int gameCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM game");
			int softRomCount = (int)Database.ExecuteScalar(connection, "SELECT COUNT(*) FROM rom");

			string info = $"{coreName} {version} - datafiles: {datafileCount} - games: {gameCount} - rom: {softRomCount}";

			OperationsPayload.CreateMetaDataTable(connection, coreName, version, info);

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
			level_root.TableStart("Name", "Description", "Games", "Roms", "Bytes", "Size");

			long total_games = 0;
			long total_roms = 0;
			long total_size = 0;

			foreach (DataRow subsetRow in dataSet.Tables["subset"].Rows)
			{
				long subset_id = (long)subsetRow["subset_id"];
				string subset_name = (string)subsetRow["name"];
				string subset_description = (string)subsetRow["description"];
				
				long subset_games = 0;
				long subset_roms = 0;
				long subset_size = 0;

				Tools.ConsoleHeading(2, $"{subset_name}\t{subset_description}");

				level_subset.Start($"{coreName} ({version}) &bull; {subset_name}");
				level_subset.Append($"<h2>Datafiles</h2>");
				level_subset.TableStart("Name", "Description", "Games", "Roms", "Bytes", "Size", "Key");

				foreach (DataRow datafileRow in dataSet.Tables["datafile"].Select($"[subset_id] = {subset_id}"))
				{
					long datafile_id = (long)datafileRow["datafile_id"];
					string datafile_name = (string)datafileRow["name"];
					string datafile_description = (string)datafileRow["description"];
					string datafile_name_enc = Uri.EscapeDataString(datafile_name);
					string datafile_key = (string)datafileRow["key"];

					long datafile_games = 0;
					long datafile_roms = 0;
					long datafile_size = 0;

					level_datafile.Start($"{coreName} ({version}) &bull; {subset_name} &bull; {datafile_name}");
					level_datafile.Append($"<h2>Games</h2>");
					level_datafile.TableStart("Name", "Description", "Roms", "Bytes", "Size");

					foreach (DataRow gameRow in dataSet.Tables["game"].Select($"[datafile_id] = {datafile_id}"))
					{
						long game_id = (long)gameRow["game_id"];
						string game_name = (string)gameRow["name"];
						string game_description = (string)gameRow["description"];
						string game_name_enc = Uri.EscapeDataString(game_name);

						long game_roms = 0;
						long game_size = 0;

						level_game.Start($"{coreName} ({version}) &bull; {subset_name} &bull; {datafile_name} &bull; {game_name}");
						level_game.Append($"<h2>Roms</h2>");
						level_game.TableStart("Name", "Bytes", "Size", "CRC", "SHA1", "MD5");

						StringBuilder game_html = new StringBuilder();
						game_html.AppendLine($"<h2>{game_name} DETAILS<h2>");

						foreach (DataRow romRow in dataSet.Tables["rom"].Select($"[game_id] = {game_id}"))
						{
							string rom_name = (string)romRow["name"];
							string crc = romRow.Field<string>("crc");
							string sha1 = romRow.Field<string>("sha1");
							string md5 = romRow.Field<string>("md5");
							long rom_size = Int64.Parse(romRow.Field<string>("size") ?? "0");

							game_roms += 1;
							game_size += rom_size;

							level_game.TableRow(rom_name, rom_size.ToString(), Tools.DataSize(rom_size), crc, sha1, md5);
						}

						datafile_games += 1;
						datafile_roms += game_roms;
						datafile_size += game_size;

						level_game.TableEnd();
						level_game.Finish(subset_name, datafile_name, game_name);

						level_datafile.TableRow($"<a href=\"/{coreName}/{subset_name}/{datafile_name_enc}/{game_name_enc}\">{game_name}</a>",
							game_description, game_roms.ToString(), game_size.ToString(), Tools.DataSize(game_size));
					}

					subset_games += datafile_games;
					subset_roms += datafile_roms;
					subset_size += datafile_size;

					level_datafile.TableEnd();
					level_datafile.Finish(subset_name, datafile_name);

					level_subset.TableRow($"<a href=\"/{coreName}/{subset_name}/{datafile_name_enc}\">{datafile_name}</a>",
						datafile_description, datafile_games.ToString(), datafile_roms.ToString(), datafile_size.ToString(), Tools.DataSize(datafile_size), datafile_key);
				}

				total_games += subset_games;
				total_roms += subset_roms;
				total_size += subset_size;

				level_subset.TableEnd();
				level_subset.Finish(subset_name);

				level_root.TableRow($"<a href=\"/{coreName}/{subset_name}\">{subset_name}</a>",
					subset_description, subset_games.ToString(), subset_roms.ToString(), subset_size.ToString(), Tools.DataSize(subset_size));
			}

			level_root.TableEnd();
			level_root.Finish("1");

			//
			// Save payload tables
			//
			level_root.Save(connection);
			level_subset.Save(connection);
			level_datafile.Save(connection);
			level_game.Save(connection);

			//
			// TODO indexes ...........
			//

			Tools.ConsolePrintMemory();

			return 0;
		}

		public enum PayloadLevel { Root, Subset, Datafile, Game };

		public class PayloadLevelInfo
		{
			public DataTable DataTable;

			private string HtmlTitle;
			private StringBuilder HtmlPage = new StringBuilder();
			private int Width = 0;

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

				HtmlTitle = title;
			}
			public void Finish(params string[] keys)
			{
				if (keys.Length != DataTable.PrimaryKey.Length)
					throw new ApplicationException("Bad keys width");

				if (DataTable.Rows.Find(keys) != null)
				{
					Console.WriteLine($"!!! Warning Duplicate Item {DataTable.TableName}:\t{String.Join("\t", keys)}");
					return;
				}

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

				HtmlPage.Length = 0;
			}

			public void Append(string html)
			{
				HtmlPage.Append(html);
			}
			public void TableStart(params string[] columnNames)
			{
				Width = columnNames.Length;

				HtmlPage.AppendLine("<table>");
				HtmlPage.AppendLine($"<tr>{String.Join("", columnNames.Select(name => $"<th>{name}</th>"))}</tr>");
			}
			public void TableRow(params string[] values)
			{
				if (values.Length != Width)
					throw new ApplicationException("Bad values width");

				HtmlPage.AppendLine($"<tr>{String.Join("", values.Select(n => $"<td>{n}</td>"))}</tr>");
			}

			public void TableEnd()
			{
				HtmlPage.AppendLine("</table>");
			}

			public void Save(SqlConnection connection)
			{
				OperationsPayload.MakeMSSQLPayloadsInsert(connection, DataTable);
			}


		}
	}
}
