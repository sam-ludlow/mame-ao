using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web.UI.HtmlControls;
using System.Xml;
using System.Xml.Linq;

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

			using (var reader = XmlReader.Create(Path.Combine(directory, "_fbneo.xml"), _XmlReaderSettings))
			{
				while (reader.ReadToFollowing("datafile"))
				{
					var datafile = (XElement)XElement.ReadFrom(reader);
					var datafile_key = (string)datafile.Attribute("key");

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
				return DatishMSSQLPayloads(connection, directory, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game, null);
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
				return DatishMSSQLPayloads(connection, directory, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game, "category");
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
				return DatishMSSQLPayloads(connection, directory, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game, null);
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
				return DatishMSSQLPayloads(connection, directory, coreName, version, xmlJsonPayloads_datafile, xmlJsonPayloads_game, "subset");
		}



		private static int DatishMSSQLPayloads(
			SqlConnection connection,
			string directory,
			string coreName,
			string version,
			Dictionary<string, string[]> xmlJsonPayloads_datafile,
			Dictionary<string, string[]> xmlJsonPayloads_game,
			string subsetColumnName)
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
			HashSet<string> sortTableNames = new HashSet<string>(new string[] { "datafile", "game", "rom", "sample" });

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
			// Subsets
			//
			string[] subsetNames = new string[] { coreName };
			if (subsetColumnName != null)
				subsetNames = Database.ExecuteFill(connection,
					$"SELECT DISTINCT [{subsetColumnName}] FROM [datafile] ORDER BY [{subsetColumnName}];").Rows.Cast<DataRow>().Select(row => (string)row[0]).ToArray();

			//
			// Traverse
			//
			PayloadLevelInfo level_root = new PayloadLevelInfo(PayloadLevel.Root, null);
			PayloadLevelInfo level_subset = new PayloadLevelInfo(PayloadLevel.Subset, null);
			PayloadLevelInfo level_datafile = new PayloadLevelInfo(PayloadLevel.Datafile, xmlJsonPayloads_datafile);
			PayloadLevelInfo level_game = new PayloadLevelInfo(PayloadLevel.Game, xmlJsonPayloads_game);

			level_root.StartHtmlTable($"{coreName} ({version})", $"{coreName} ({version}) Subsets",
				new string[] { "Subset Name", "Game Count", "Rom Count", "Bytes", "Size" });

			foreach (string subset_name in subsetNames)
			{
				Console.WriteLine($"SUBSET:\t{subset_name}");

				level_subset.StartHtmlTable($"{coreName}/{subset_name} ({version})", $"{coreName}/{subset_name} ({version}) Datafiles",
					new string[] { "Datafile Name", "Game Count", "Rom Count", "Bytes", "Size" });

				IEnumerable<DataRow> datafileRows = subsetColumnName == null ?
					dataSet.Tables["datafile"].Rows.Cast<DataRow>() : dataSet.Tables["datafile"].Select($"[{subsetColumnName}] = '{subset_name}'");
				foreach (DataRow datafileRow in datafileRows)
				{
					long datafile_id = (long)datafileRow["datafile_id"];
					string datafile_name = (string)datafileRow["name"];

					level_datafile.StartHtmlTable($"{datafile_name}", $"{datafile_name} Games",
						new string[] { "Game Name", "Rom Count", "Bytes", "Size" });

					foreach (DataRow gameRow in dataSet.Tables["game"].Select($"[datafile_id] = {datafile_id}"))
					{
						long game_id = (long)gameRow["game_id"];
						string game_name = (string)gameRow["name"];


						level_game.StartHtmlTable($"{game_name}", $"{game_name}", null);

						StringBuilder game_html = new StringBuilder();
						game_html.AppendLine($"<h2>{game_name} DETAILS<h2>");

						level_game.FinishHtmlTable(new string[] { subset_name, datafile_name, game_name }, game_html.ToString());




						level_datafile.AppendHtmlTable(new string[] { game_name, "", "", "" });
					}

					level_datafile.FinishHtmlTable(new string[] { subset_name, datafile_name });

					level_subset.AppendHtmlTable(new string[] { datafile_name, "", "", "", "" });
				}

				level_subset.FinishHtmlTable(new string[] { subset_name });

				level_root.AppendHtmlTable(subset_name, "", "", "", "");
			}

			level_root.FinishHtmlTable(new string[] { "1" });

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
			private PayloadLevel Level;

			public DataTable DataTable;

			private string HtmlTitle;
			private StringBuilder HtmlPage = new StringBuilder();
			private int Width = 0;

			private Dictionary<string, string[]> XmlJsonPayloads;

			public PayloadLevelInfo(
				PayloadLevel level,
				Dictionary<string, string[]> xmlJsonPayloads)
			{
				Level = level;

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

			public void StartHtmlTable(string title, string heading, string[] columnNames)
			{
				HtmlTitle = title;

				if (columnNames == null)
					return;

				Width = columnNames.Length;

				HtmlPage = new StringBuilder();
				HtmlPage.AppendLine($"<h2>{heading}</h2>");
				HtmlPage.AppendLine("<table>");
				HtmlPage.AppendLine($"<tr>{String.Join("", columnNames.Select(n => $"<th>{n}</th>"))}</tr>");
			}

			public void AppendHtmlTable(params string[] values)
			{
				if (values.Length != Width)
					throw new ApplicationException("Bad values width");

				HtmlPage.AppendLine($"<tr>{String.Join("", values.Select(n => $"<td>{n}</td>"))}</tr>");
			}

			public void FinishHtmlTable(string[] keys)
			{
				if (keys.Length != DataTable.PrimaryKey.Length )
					throw new ApplicationException("Bad keys width");

				HtmlPage.AppendLine("</table>");

				FinishHtmlTable(keys, HtmlPage.ToString());

				HtmlPage.Length = 0;
			}

			public void FinishHtmlTable(string[] keys, string html)
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
				rowData.AddRange(new string[] { HtmlTitle, xmlJson[0], xmlJson[1], html });

				DataTable.Rows.Add(rowData.ToArray());
			}

			public void Save(SqlConnection connection)
			{
				OperationsPayload.MakeMSSQLPayloadsInsert(connection, DataTable);
			}


		}
	}
}
