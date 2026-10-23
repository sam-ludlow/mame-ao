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
			{
				DatishMSSQLPayloads(connection, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game, Path.GetDirectoryName(Path.GetDirectoryName(directory)));
				DatishMSSQLPayloadsSearch(connection, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game, Path.GetDirectoryName(Path.GetDirectoryName(directory)));
			}

			return 0;
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
			DataSet dataSet = Operations.SourceData(connection, new Dictionary<string, string>() {
				{ "subset",		"name" },
				{ "datafile",	"name" },
				{ "game",		"name" },
				{ "rom",		"name" },
				{ "sample",		"name" },
			});

			var rowLookups = Operations.PerformanceDictionaries(dataSet);

			DataTable snapTable = Snap.LoadSnapIndex(Path.Combine(serverDirectory, "snap"), coreName);

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

				foreach (DataRow datafileRow in rowLookups["datafile"][subset_id])
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

					foreach (DataRow gameRow in rowLookups["game"][datafile_id])
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

						foreach (DataRow romRow in rowLookups["rom"][game_id])
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

			foreach (string columnName in new string[] { "merge", "status" })
			{
				if (dataSet.Tables["rom"].Columns.Contains(columnName) == false)
					dataSet.Tables["rom"].Columns.Add(columnName, typeof(string));
			}

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

				foreach (DataRow datafileRow in rowLookups["datafile"][subset_id])
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

					foreach (DataRow gameRow in rowLookups["game"][datafile_id])
					{
						long game_id = (long)gameRow["game_id"];
						string game_name = (string)gameRow["name"];
						string game_description = (string)gameRow["description"];
						string game_name_enc = Uri.EscapeDataString(game_name);
						string game_ia_link = gameRow.Field<string>("ia_link");

						level_game.Start($"{coreName} ({version}) &bull; {subset_name} &bull; {WebUtility.HtmlEncode(datafile_name)} &bull; {WebUtility.HtmlEncode(game_name)}");

						level_game.Counts.Games = 1;

						string runLink = coreName == "fbneo" ? $" &bull; <a href=\"#\" onclick=\"mameAO('{game_name}@{datafile_name}@{coreName}'); return false\">RUN</a>" : "";

						level_game.Append($"<div style=\"margin: 1em 0;\"><h2 style=\"display:inline;\">Game</h2> &bull; <a href=\"{game_name_enc}.xml\">XML</a> &bull; <a href=\"{game_name_enc}.json\">JSON</a>{runLink}</div>");
						level_game.Append(gameRow);

						if (snapTable != null && coreName == "fbneo")
						{
							DataRow snapRow = snapTable.Rows.Find($"{datafile_name}\\{game_name}");
							if (snapRow != null)
							{
								level_game.Append("<hr />");
								level_game.Append("<h2>snap</h2>");
								level_game.Append($"<img src=\"/{coreName}/{datafile_name}/{game_name}.png\" alt=\"{game_description} png snap\">");
								level_game.Append($"<img src=\"/{coreName}/{datafile_name}/{game_name}.jpg\" alt=\"{game_description} jpg snap thumbnail\">");
								level_game.TableStart("Size", "Modified", "Width", "Height", "X res", "Y res", "Palette Size", "Pixel Format", "Metadata", "Pixel SHA1");
								level_game.TableRow(snapRow.Field<long>("Length").ToString(), snapRow.Field<DateTime>("LastWriteTime").ToString(),
									snapRow.Field<int>("Width").ToString(), snapRow.Field<int>("Height").ToString(),
									snapRow.Field<float>("HorizontalResolution").ToString(), snapRow.Field<float>("VerticalResolution").ToString(),
									snapRow.Field<long>("PaletteLength").ToString(),
									snapRow.Field<string>("PixelFormat"), snapRow.Field<string>("PropertyItems"), snapRow.Field<string>("PixelSHA1"));
								level_game.TableEnd();
							}
						}
						
						level_game.Append("<hr />");

						level_game.Append($"<h2>Roms</h2>");
						level_game.TableStart("Name", "Merge", "Status", "Bytes", "Size", "CRC", "SHA1", "MD5", "IA");

						foreach (DataRow romRow in rowLookups["rom"][game_id])
						{
							string rom_name = (string)romRow["name"];
							string rom_merge = romRow.Field<string>("merge");
							string rom_status = romRow.Field<string>("status");
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

							level_game.TableRow(rom_name, rom_merge, rom_status, rom_size.ToString(), Tools.DataSize(rom_size), crc, sha1, md5, rom_ia_link);
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

							var rows = rowLookups[tableName][game_id];

							if (rows.Count == 0)
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

		private static int DatishMSSQLPayloadsSearch(
			SqlConnection connection,
			string coreName,
			string version,
			Dictionary<string, string[]> xmlJsonPayloads_datafile,
			Dictionary<string, string[]> xmlJsonPayloads_game,
			string serverDirectory)
		{

			DataTable searchTable = Database.ExecuteFill(connection, @"
				SELECT
					CONCAT(datafile.name, '@', game.name) AS [key],
					game.name,
					game.game_id,
					game.sourcefile,
					game.description,
					game.year,
					game.manufacturer,
					game.cloneof,
					game.romof,
					game.comment,
					game.sampleof,
					game.isbios,
					datafile.name AS [type],
					driver.status,
					video.type AS video_type,
					video.orientation,
					video.width,
					video.height,
					video.aspectx,
					video.aspecty
				FROM
					(
						(
							datafile
							INNER JOIN game ON datafile.datafile_id = game.datafile_id
						)
						LEFT JOIN driver ON game.game_id = driver.game_id
					)
					LEFT JOIN video ON game.game_id = video.game_id
				ORDER BY
					datafile.name,
					game.name;
			");

			searchTable.TableName = "game_search_payload";
			searchTable.PrimaryKey = new DataColumn[] { searchTable.Columns["key"] };

			//
			// Build line payloads
			//
			DataTable snapTable = Snap.LoadSnapIndex(Path.Combine(serverDirectory, "snap"), coreName);

			foreach (string name in new string[] { "xml", "json", "html", "html_card" })
				searchTable.Columns.Add(name, typeof(string));

			string[] columnNames = new string[] { "name", "description", "year", "manufacturer", "cloneof", "romof" };

			foreach (DataRow row in searchTable.Rows)
			{
				string datafile_name = (string)row["type"];

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
								item.Append($"<a href=\"/{coreName}/{datafile_name}/{row[column]}\">{row[column]}</a>");
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
				bool machine_isdevice = row.Field<string>("isbios") == "yes";

				string cardClass = "card";	// machine_isdevice == true ? "card" : $"card-{((string)row["status"])[0]}{((string)row["emulation"])[0]}";

				DataRow snapRow = snapTable?.Rows.Find($"{datafile_name}\\{machine_name}");

				item = new StringBuilder();
				item.Append($"<div class=\"{cardClass}\">");

				item.Append($"<div class=\"card-thumb\"><a href=\"/{coreName}/{datafile_name}/{machine_name}\" class=\"card-link\">");
				if (snapRow != null)
				{
					item.Append($"<img src=\"/{coreName}/{datafile_name}/{machine_name}.jpg\" alt=\"{machine_description}\" loading=\"lazy\" class=\"card-img\" />");
				}
				else
				{
					if (machine_isdevice == true)
						item.Append("<img src=\"/images/device.jpg\" alt=\"device\" class=\"card-img\" />");
					else
						item.Append("<p>NO SNAP</p>");
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
				//if (row.Field<string>("isbios") == "yes")
				//	row["type"] = "device";

				//
				// Status
				//
				//if (machine_isdevice == false)
				//	row["ao_status"] = MachineAoStatusLookup[$"{(string)row["status"]}-{(string)row["emulation"]}"];

			}

			//
			// Insert database table
			//
			Tools.SetDataTableStringLengths(searchTable);

			Operations.MakeMSSQLPayloadsInsert(connection, searchTable);

			//
			// Create indexes
			//

			Database.ExecuteNonQuery(connection, @"
				CREATE FULLTEXT INDEX ON [game_search_payload]
				(
					[name],
					[description],
					[year],
					[manufacturer]
				)
				KEY INDEX [PK_game_search_payload]
				ON [ao_catalog]
				WITH CHANGE_TRACKING AUTO;
			");

			Database.ExecuteNonQuery(connection, @"
				CREATE NONCLUSTERED INDEX [IX_game_search_payload_type_description]
				ON [game_search_payload]
				(
					[type],
					[description]
				)
				INCLUDE (
					[html],
					[html_card]
				);
			");


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
