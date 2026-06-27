using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
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

			//	XML			380 Megabytes (MiB)
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

			//	XML			2.3 Gigabytes (GiB)
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

			//	XML			1.1 Gigabytes (GiB)
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

			//	XML			1.4 Gigabytes (GiB)
			//	Source Data	1.5 Gigabytes (GiB)

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

			Tools.ConsolePrintMemory();

			return 0;
		}
	}
}
