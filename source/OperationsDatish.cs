using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;

using Newtonsoft.Json.Linq;

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

			var datafilesElement = XElement.Load(Path.Combine(directory, "_fbneo.xml"), LoadOptions.None);

			foreach (var datafileElement in datafilesElement.Elements("datafile"))
			{
				var datafile_name = datafileElement.Attribute("key").Value;

				xmlJsonPayloads_datafile.Add($"{coreName}\t{datafile_name}", new string[] { datafileElement.ToString(), Tools.XML2JSON(datafileElement) });

				foreach (var game in datafileElement.Elements("game"))
				{
					var game_name = game.Attribute("name").Value;
					string key = $"{coreName}\t{datafile_name}\t{game_name}";

					xmlJsonPayloads_game.Add(key, new string[] { game.ToString(), Tools.XML2JSON(game) });
				}
			}

			using (SqlConnection connection = new SqlConnection($"{serverConnectionString}Database='{databaseName}';"))
				return DatishMSSQLPayloads(connection, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game, Path.GetDirectoryName(Path.GetDirectoryName(directory)));
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
				return DatishMSSQLPayloads(connection, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game, Path.GetDirectoryName(Path.GetDirectoryName(directory)));
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
				return DatishMSSQLPayloads(connection, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game, Path.GetDirectoryName(Path.GetDirectoryName(directory)));
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
				return DatishMSSQLPayloads(connection, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game, Path.GetDirectoryName(Path.GetDirectoryName(directory)));
		}

		private static int DatishMSSQLPayloads(
			SqlConnection connection,
			string coreName,
			string version,
			Dictionary<string, string[]> xmlJsonPayloads_datafile,
			Dictionary<string, string[]> xmlJsonPayloads_game,
			string serverDirectory)
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
			//	Performance Dictionaries
			//
			var gameRowsByDatafileId = new Dictionary<long, List<DataRow>>();
			foreach (DataRow gameRow in dataSet.Tables["game"].Rows)
			{
				long datafile_id = (long)gameRow["datafile_id"];
				if (gameRowsByDatafileId.ContainsKey(datafile_id) == false)
					gameRowsByDatafileId.Add(datafile_id, new List<DataRow>());
				gameRowsByDatafileId[datafile_id].Add(gameRow);
			}
			// no-intro some empty datafiles
			foreach (long datafile_id in dataSet.Tables["datafile"].Rows.Cast<DataRow>().Select(row => (long)row["datafile_id"]))
			{
				if (gameRowsByDatafileId.ContainsKey(datafile_id) == false)
					gameRowsByDatafileId.Add(datafile_id, new List<DataRow>());
			}
			var romRowsByGameId = new Dictionary<long, List<DataRow>>();
			foreach (DataRow romRow in dataSet.Tables["rom"].Rows)
			{
				long game_id = (long)romRow["game_id"];
				if (romRowsByGameId.ContainsKey(game_id) == false)
					romRowsByGameId.Add(game_id, new List<DataRow>());
				romRowsByGameId[game_id].Add(romRow);
			}

			//
			//	Traverse - Archive.org links
			//
			Tools.ConsolePrintMemory();

			// ??? From old TOSEC code - Data Fix - parent directory mismatch
			//if (itemKey == "noaen-tosec-iso-sony")
			//{
			//	foreach (string oldKey in item.Files.Keys.Where(key => key.Contains("/[BIN]/")).ToArray())
			//	{
			//		string newKey = oldKey.Replace("/[BIN]/", "/[BIN-CUE]/");
			//		item.Files.Add(newKey, item.Files[oldKey]);
			//		item.Files.Remove(oldKey);
			//	}
			//}

			string iaLinksFilename = Path.Combine(serverDirectory, "archive.org-link-items.txt");

			dataSet.Tables["datafile"].Columns.Add("ia_link");
			dataSet.Tables["game"].Columns.Add("ia_link");
			dataSet.Tables["rom"].Columns.Add("ia_link");

			foreach (DataRow subsetRow in dataSet.Tables["subset"].Rows)
			{
				long subset_id = (long)subsetRow["subset_id"];
				string subset_name = (string)subsetRow["name"];

				JArray ia_datafiles = GetArchiveOrgFiles(iaLinksFilename, coreName, subset_name, "d");
				JArray ia_games = GetArchiveOrgFiles(iaLinksFilename, coreName, subset_name, "g");

				foreach (DataRow datafileRow in dataSet.Tables["datafile"].Select($"[subset_id] = {subset_id}"))
				{
					long datafile_id = (long)datafileRow["datafile_id"];
					string datafile_name = (string)datafileRow["name"];
					string datafile_description = (string)datafileRow["description"];

					string datafile_ia_archive_url = null;

					List<JToken> files;

					//	TODO: Refine
					files = ia_datafiles.Where(f => ((string)f["base_name"]).StartsWith(datafile_name, StringComparison.OrdinalIgnoreCase) == true).ToList();
					
					if (files.Count > 1)
					{
						files = ia_datafiles.Where(f => ((string)f["base_name"]).StartsWith(datafile_description, StringComparison.OrdinalIgnoreCase) == true).ToList();
					}

					if (files.Count == 1)
						datafile_ia_archive_url = files[0]["url"].Value<string>();

					int game_ia_count = 0;

					foreach (DataRow gameRow in gameRowsByDatafileId[datafile_id])
					{
						long game_id = (long)gameRow["game_id"];
						string game_name = (string)gameRow["name"];
						string game_description = (string)gameRow["description"];

						string game_ai_archive_url = null;

						//	TODO: Refine
						files = ia_games.Where(f => ((string)f["base_name"]).StartsWith(game_name + ".", StringComparison.OrdinalIgnoreCase) == true).ToList();

						if (files.Count > 1)
						{
							//	redump is just ZIP no directories (matches accross everything)

							files = files.Where(file => {
								string path = file["name"].Value<string>();
								string[] parts = path.Split('/');
								return parts.Length > 1 ? datafile_name.Contains(parts[parts.Length - 2]) : false;
							}).ToList();
						}

						if (files.Count == 1)
							game_ai_archive_url = files[0]["url"].Value<string>();

						int rom_ia_count = 0;

						foreach (DataRow romRow in romRowsByGameId[game_id])
						{
							string rom_name = (string)romRow["name"];
							string rom_extention = Path.GetExtension(rom_name);

							if (datafile_ia_archive_url != null)
								romRow["ia_link"] = $"<a href=\"{datafile_ia_archive_url}/{Uri.EscapeDataString(game_name)}%2F{Uri.EscapeDataString(rom_name)}\">{rom_extention}</a>";
							
							if (game_ai_archive_url != null)
								romRow["ia_link"] = $"<a href=\"{game_ai_archive_url}/{Uri.EscapeDataString(rom_name)}\">{rom_extention}</a>";

							if (romRow.IsNull("ia_link") == false)
								++rom_ia_count;
						}

						if (datafile_ia_archive_url != null)
							gameRow["ia_link"] = $"{rom_ia_count}";

						if (game_ai_archive_url != null)
							gameRow["ia_link"] = $"<a href=\"{game_ai_archive_url}\">{Path.GetExtension(game_ai_archive_url)}</a>";

						if (gameRow.IsNull("ia_link") == false)
							++game_ia_count;
					}

					if (datafile_ia_archive_url != null)
						datafileRow["ia_link"] = $"<a href=\"{datafile_ia_archive_url}\">{Path.GetExtension(datafile_ia_archive_url)}</a>";
					else
						if (game_ia_count > 0)
							datafileRow["ia_link"] = $"{game_ia_count}";

					//break;
				}
			}

			Tools.ConsolePrintMemory();

			//
			// Traverse - Main
			//
			PayloadLevelInfo level_root = new PayloadLevelInfo(PayloadLevel.Root, null);
			PayloadLevelInfo level_subset = new PayloadLevelInfo(PayloadLevel.Subset, null);
			PayloadLevelInfo level_datafile = new PayloadLevelInfo(PayloadLevel.Datafile, xmlJsonPayloads_datafile);
			PayloadLevelInfo level_game = new PayloadLevelInfo(PayloadLevel.Game, xmlJsonPayloads_game);

			level_root.Start($"{coreName} ({version})");
			level_root.Append($"<h2>Subsets</h2>");
			level_root.TableStart("Name", "Description", "Datafiles", "Games", "Roms", "Bytes", "Size", "Extentions");

			string core_url = coreName != "fbneo" ? $"/{coreName}" : "";	//	No subsets on Web for FBNeo

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
				level_subset.TableStart("Name", "Description", "Games", "Roms", "Bytes", "Size", "Extentions", "IA");

				foreach (DataRow datafileRow in dataSet.Tables["datafile"].Select($"[subset_id] = {subset_id}"))
				{
					long datafile_id = (long)datafileRow["datafile_id"];
					string datafile_name = (string)datafileRow["name"];
					string datafile_description = (string)datafileRow["description"];
					string datafile_name_enc = Uri.EscapeDataString(datafile_name);
					string datafile_ia_link = datafileRow.Field<string>("ia_link");

					level_datafile.Start($"{coreName} ({version}) &bull; {subset_name} &bull; {WebUtility.HtmlEncode(datafile_name)}");

					level_datafile.Counts.Datafiles = 1;

					level_datafile.Append($"<div style=\"margin: 1em 0;\"><h2 style=\"display:inline;\">Datafile</h2> &bull; <a href=\"{datafile_name_enc}.xml\">XML</a> &bull; <a href=\"{datafile_name_enc}.json\">JSON</a></div>");
					level_datafile.Append(datafileRow);

					level_datafile.Append("<hr />");

					level_datafile.Append($"<h2>Games</h2>");
					level_datafile.TableStart("Name", "Description", "Roms", "Bytes", "Size", "Extentions", "IA");

					foreach (DataRow gameRow in gameRowsByDatafileId[datafile_id])
					{
						long game_id = (long)gameRow["game_id"];
						string game_name = (string)gameRow["name"];
						string game_description = (string)gameRow["description"];
						string game_name_enc = Uri.EscapeDataString(game_name);
						string game_ia_link = gameRow.Field<string>("ia_link");

						level_game.Start($"{coreName} ({version}) &bull; {subset_name} &bull; {WebUtility.HtmlEncode(datafile_name)} &bull; {WebUtility.HtmlEncode(game_name)}");

						level_game.Counts.Games = 1;

						level_game.Append($"<div style=\"margin: 1em 0;\"><h2 style=\"display:inline;\">Game</h2> &bull; <a href=\"{game_name_enc}.xml\">XML</a> &bull; <a href=\"{game_name_enc}.json\">JSON</a></div>");
						level_game.Append(gameRow);
						
						level_game.Append("<hr />");

						level_game.Append($"<h2>Roms</h2>");
						level_game.TableStart("Name", "Bytes", "Size", "CRC", "SHA1", "MD5", "IA");

						foreach (DataRow romRow in romRowsByGameId[game_id])
						{
							string rom_name = (string)romRow["name"];
							string crc = romRow.Field<string>("crc");
							string sha1 = romRow.Field<string>("sha1");
							string md5 = romRow.Field<string>("md5");
							string rom_ia_link = romRow.Field<string>("ia_link");

							string size = romRow.Field<string>("size");
							long rom_size = Int64.Parse(string.IsNullOrEmpty(size) ? "0" : size);

							level_game.Counts.Roms += 1;
							level_game.Counts.Size += rom_size;

							string extention = Path.GetExtension(rom_name).ToLower();
							level_game.Counts.AddExtention(extention);

							level_game.TableRow(rom_name, rom_size.ToString(), Tools.DataSize(rom_size), crc, sha1, md5, rom_ia_link);
						}

						level_datafile.Counts.Add(level_game.Counts);

						level_game.TableEnd();

						//	fbneo		driver, sample, video
						//	tosec		n/a
						//	redump		n/a
						//	no-intro	category, game_code

						foreach (string tableName in new string[] { "driver", "sample", "video", "category", "game_code" })
						{
							if (dataSet.Tables.Contains(tableName) == false)
								continue;

							DataRow[] rows = dataSet.Tables[tableName].Select($"game_id = {game_id}");

							if (rows.Length == 0)
								continue;

							level_game.Append("<hr />");
							level_game.Append($"<h2>{tableName}</h2>");
							level_game.Append(Reports.MakeHtmlTable(dataSet.Tables[tableName], rows, null));
						}

						level_game.Finish(subset_name, datafile_name, game_name);

						level_datafile.TableRow($"<a href=\"{core_url}/{subset_name}/{datafile_name_enc}/{game_name_enc}\">{game_name}</a>", game_description,
							level_game.Counts.Roms.ToString(), level_game.Counts.Size.ToString(), Tools.DataSize(level_game.Counts.Size),
							level_game.Counts.ExtentionsToString(), game_ia_link
						);
					}

					level_subset.Counts.Add(level_datafile.Counts);

					level_datafile.TableEnd();
					level_datafile.Finish(subset_name, datafile_name);

					level_subset.TableRow($"<a href=\"{core_url}/{subset_name}/{datafile_name_enc}\">{datafile_name}</a>", datafile_description,
						level_datafile.Counts.Games.ToString(), level_datafile.Counts.Roms.ToString(), level_datafile.Counts.Size.ToString(), Tools.DataSize(level_datafile.Counts.Size),
						level_datafile.Counts.ExtentionsToString(), datafile_ia_link
					);

					//break;
				}

				level_root.Counts.Add(level_subset.Counts);

				level_subset.TableEnd();
				level_subset.Finish(subset_name);

				level_root.TableRow($"<a href=\"{core_url}/{subset_name}\">{subset_name}</a>", subset_description,
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

			Operations.CreateMetaDataTable(connection, coreName, version, info);

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

		private static JArray GetArchiveOrgFiles(string filename, string core, string subset, string type)
		{
			var archiveExtentions = new HashSet<string>(new string[] { ".zip", ".7z" });

			string current_core = null;
			string current_subset = null;
			string current_type = null;

			var itemNames = new List<string>();
			foreach (string rawLine in File.ReadAllLines(filename))
			{
				string line = rawLine.Trim();
				if (line.Length == 0 || line.StartsWith("#") == true)
					continue;

				string[] parts = line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

				if (parts.Length == 3)
				{
					current_core = parts[0];
					current_subset = parts[1];
					current_type = parts[2];
					continue;
				}

				if (parts.Length != 1)
					throw new ApplicationException($"Bad line {line}");

				if (current_core == core && current_subset == subset && current_type == type)
					itemNames.Add(line);
			}
			itemNames.Sort();

			JArray files = new JArray();

			foreach (string itemName in itemNames)
			{
				JObject item = JObject.Parse(Tools.FetchTextCached($"https://archive.org/metadata/{itemName}"));
				JArray item_files = (JArray)item["files"];

				foreach (JObject file in item_files)
				{
					string name = (string)file["name"];
					string extention = Path.GetExtension(name).ToLower();

					if (archiveExtentions.Contains(extention) == false)
					{
						//Console.WriteLine($"Ignore extention: '{extention}' {name}");
						continue;
					}

					//	use partial match for datafile, search for game with '.' on end to prevent sub contains matches
					file["base_name"] = type == "d" ? Path.GetFileNameWithoutExtension(name) : Path.GetFileName(name);

					file["extention"] = extention;
					file["url"] = $"https://archive.org/download/{itemName}/" + String.Join("/", ((string)file["name"]).Split('/').Select(n => Uri.EscapeDataString(n)));

					files.Add(file);
				}
			}
			return files;
		}

	}
}
