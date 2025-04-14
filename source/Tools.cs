using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spludlow.MameAO
{
	public class Tools
	{
		private static readonly string[] _SystemOfUnits =
		{
			"Bytes (B)",
			"Kilobytes (KiB)",
			"Megabytes (MiB)",
			"Gigabytes (GiB)",
			"Terabytes (TiB)",
			"Petabytes (PiB)",
			"Exabytes (EiB)"
		};

		private static readonly char[] _HeadingChars = new char[] { ' ', '#', '=', '-' };

		private static readonly SHA1Managed _SHA1Managed = new SHA1Managed();

		public static string DataRowValue(DataRow row, string columnName)
		{
			if (row.IsNull(columnName))
				return null;
			return (string)row[columnName];
		}

		public static void ConsoleRule(int head)
		{
			Console.WriteLine(new String(_HeadingChars[head], Console.WindowWidth - 1));
		}

		public static void ConsoleHeading(int head, string line)
		{
			ConsoleHeading(head, new string[] { line });
		}
		public static void ConsoleHeading(int head, string[] lines)
		{
			ConsoleRule(head);

			char ch = _HeadingChars[head];

			foreach (string line in lines)
			{
				int pad = Console.WindowWidth - 3 - line.Length;
				if (pad < 1)
					pad = 1;
				int odd = pad % 2;
				pad /= 2;

				Console.Write(ch);
				Console.Write(new String(' ', pad));
				Console.Write(line);
				Console.Write(new String(' ', pad + odd));
				Console.Write(ch);
				Console.WriteLine();
			}

			ConsoleRule(head);
		}

		public static void ReportError(Exception e, string title, bool fatal)
		{
			Console.WriteLine();
			Console.WriteLine($"!!! {title}: " + e.Message);
			Console.WriteLine();
			Console.WriteLine(e.ToString());
			Console.WriteLine();
			Console.WriteLine("If you want to submit an error report please copy and paste the text from here.");
			Console.WriteLine("Select All (Ctrl+A) -> Copy (Ctrl+C) -> notepad -> paste (Ctrl+V)");
			Console.WriteLine();
			Console.WriteLine("Report issues here https://github.com/sam-ludlow/mame-ao/issues");

			if (fatal == true)
			{
				Console.WriteLine();
				Console.WriteLine("Press any key to continue, program has crashed and will exit.");
				Console.ReadKey();
				Environment.Exit(1);
			}
		}

		public static string CleanWhiteSpace(string text)
		{
			return Regex.Replace(text, @"\s+", " ").Trim();
		}

		public static void CleanDynamic(dynamic data)
		{
			List<string> deleteList = new List<string>();
			foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(data))
			{
				if (descriptor.GetValue(data) == null)
					deleteList.Add(descriptor.Name);
			}

			foreach (string key in deleteList)
				((JObject)data).Remove(key);
		}

		public static void PopText(DataSet dataSet)
		{
			StringBuilder text = new StringBuilder();

			foreach (DataTable table in dataSet.Tables)
			{
				string hr = new string('-', table.TableName.Length);
				text.AppendLine(hr);
				text.AppendLine(table.TableName);
				text.AppendLine(hr);
				text.AppendLine(TextTable(table));
				text.AppendLine();
			}

			PopText(text.ToString());
		}
		public static void PopText(DataTable table)
		{
			PopText(TextTable(table));
		}
		public static void PopText(string text)
		{
			string filename = Path.GetTempFileName();
			File.WriteAllText(filename, text, Encoding.UTF8);
			Process.Start("notepad.exe", filename);
		}

		public static string TextTable(DataTable table)
		{
			StringBuilder result = new StringBuilder();

			foreach (DataColumn column in table.Columns)
			{
				if (column.Ordinal != 0)
					result.Append('\t');

				result.Append(column.ColumnName);
			}
			result.AppendLine();

			foreach (DataColumn column in table.Columns)
			{
				if (column.Ordinal != 0)
					result.Append('\t');

				result.Append(column.DataType.Name);

				if (table.PrimaryKey.Contains(column) == true)
					result.Append("*");
			}
			result.AppendLine();

			foreach (DataRow row in table.Rows)
			{
				foreach (DataColumn column in table.Columns)
				{
					if (column.Ordinal != 0)
						result.Append('\t');

					object value = row[column];

					if (value != null)
						result.Append(Convert.ToString(value));
				}
				result.AppendLine();
			}

			return result.ToString();
		}

		public static DataTable TextTableReadFile(string filename)
		{
			using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
				return TextTableReadStream(stream);
		}
		public static DataTable TextTableReadStream(Stream stream)
		{
			DataTable table = null;

			using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
			{
				string[] columnNames = null;

				string line;
				while ((line = reader.ReadLine()) != null)
				{
					string[] words = line.Split('\t');

					if (table == null)
					{
						if (columnNames == null)
						{
							columnNames = words;
						}
						else
						{
							table = new DataTable();
							List<DataColumn> primaryKeyColumns = new List<DataColumn>();

							for (int index = 0; index < columnNames.Length; ++index)
							{
								string name = columnNames[index];
								string type = words[index];
								bool pk = false;
								if (type.EndsWith("*") == true)
								{
									type = type.Substring(0, type.Length - 1);
									pk = true;
								}
								DataColumn column = new DataColumn(name, Type.GetType($"System.{type}", true));
								table.Columns.Add(column);
								if (pk == true)
									primaryKeyColumns.Add(column);
							}

							if (primaryKeyColumns.Count > 0)
								table.PrimaryKey = primaryKeyColumns.ToArray();
						}
					}
					else
					{
						DataRow row = table.NewRow();

						for (int index = 0; index < table.Columns.Count; ++index)
							row[index] = index < words.Length ? words[index] : "";

						table.Rows.Add(row);
					}
				}
			}

			return table;
		}

		public static string ValidFileName(string name)
		{
			return ValidName(name, _InvalidFileNameChars, "_");
		}
		private static readonly List<char> _InvalidFileNameChars = new List<char>(Path.GetInvalidFileNameChars());

		private static string ValidName(string name, List<char> invalidChars, string replaceBadWith)
		{
			StringBuilder sb = new StringBuilder();

			foreach (char c in name)
			{
				if (invalidChars.Contains(c) == true)
					sb.Append(replaceBadWith);
				else
					sb.Append(c);
			}

			return sb.ToString();
		}

		public static string SHA1HexFile(string filename)
		{
			using (FileStream stream = File.OpenRead(filename))
				return SHA1Hex(stream);
		}

		public static string SHA1HexText(string text)
		{
			return SHA1HexText(text, Encoding.UTF8);
		}

		public static string SHA1HexText(string text, Encoding encoding)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (StreamWriter writer = new StreamWriter(stream, encoding, 4096, true))
					writer.Write(text);

				stream.Position = 0;
				return SHA1Hex(stream);
			}
		}

		public static string SHA1Hex(Stream stream)
		{
			byte[] hash = _SHA1Managed.ComputeHash(stream);
			StringBuilder hex = new StringBuilder();
			foreach (byte b in hash)
				hex.Append(b.ToString("x2"));
			return hex.ToString();
		}

		public static void ClearAttributes(string directory)
		{
			foreach (string filename in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
				File.SetAttributes(filename, FileAttributes.Normal);
		}

		public static string PrettyJSON(string json)
		{
			dynamic obj = JsonConvert.DeserializeObject<dynamic>(json);
			return JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
		}

		public static DataTable MakeDataTable(string columnNames, string columnTypes)
		{
			return MakeDataTable("untitled", columnNames, columnTypes);
		}

		public static DataTable MakeDataTable(string tableName, string columnNames, string columnTypes)
		{
			string[] names = columnNames.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
			string[] types = columnTypes.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

			if (names.Length != types.Length)
				throw new ApplicationException("Make Data Table Bad definition.");

			DataTable table = new DataTable(tableName);

			List<int> keyColumnIndexes = new List<int>();

			for (int index = 0; index < names.Length; ++index)
			{
				string name = names[index];
				string typeName = "System." + types[index];

				if (typeName.EndsWith("*") == true)
				{
					typeName = typeName.Substring(0, typeName.Length - 1);
					keyColumnIndexes.Add(index);
				}

				table.Columns.Add(name, Type.GetType(typeName, true));
			}

			if (keyColumnIndexes.Count > 0)
			{
				List<DataColumn> keyColumns = new List<DataColumn>();
				foreach (int index in keyColumnIndexes)
					keyColumns.Add(table.Columns[index]);
				table.PrimaryKey = keyColumns.ToArray();
			}

			return table;
		}

		public static string FetchTextCached(string url)
		{
			string filename = Path.Combine(Globals.CacheDirectory, Tools.ValidFileName(url.Substring(8)));
		
			string result = null;

			if (File.Exists(filename) == false || (DateTime.Now - File.GetLastWriteTime(filename) > TimeSpan.FromHours(3)))
			{
				try
				{
					Console.Write($"Downloading {url} ...");
					result = Query(url);
					Console.WriteLine("...done");

					string extention = Path.GetExtension(filename).ToLower();

					if (extention == ".json" && (result.StartsWith("{") == true || result.StartsWith("[") == true))
						result = PrettyJSON(result);
				}
				catch (TaskCanceledException e)
				{
					Console.WriteLine($"ERROR Fetch client timeout: {url} {e.Message}");
				}
				catch (HttpRequestException e)
				{
					Console.WriteLine($"ERROR Fetch request: {url} {e.Message} {e.InnerException?.Message}");
				}
				catch (Exception e)
				{
					Console.WriteLine($"ERROR Fetch: {url} {e.Message} {e.InnerException?.Message}");
				}

				if (result != null)
					File.WriteAllText(filename, result, Encoding.UTF8);
			}

			if (result == null && File.Exists(filename) == true)
				result = File.ReadAllText(filename, Encoding.UTF8);

			return result;
		}

		public static string Query(string url)
		{
			try
			{
				using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, url))
				{
					if (url.StartsWith("https://archive.org/") == true)
						requestMessage.Headers.Add("Cookie", Globals.AuthCookie);

					Task<HttpResponseMessage> requestTask = Globals.HttpClient.SendAsync(requestMessage);
					requestTask.Wait();
					HttpResponseMessage responseMessage = requestTask.Result;

					responseMessage.EnsureSuccessStatusCode();

					Task<string> responseMessageTask = responseMessage.Content.ReadAsStringAsync();
					responseMessageTask.Wait();
					string responseBody = responseMessageTask.Result;

					return responseBody;
				}
			}
			catch (AggregateException e)
			{
				throw e.InnerException ?? e;
			}
		}


		public static long Download(string url, string filename)
		{
			return Download(url, filename, 0);
		}

		public static long Download(string url, string filename, long expectedSize)
		{
			if (expectedSize > 0)
				lock (Globals.WorkerTaskInfo)
					Globals.WorkerTaskInfo.BytesTotal = expectedSize;

			long total = 0;
			byte[] buffer = new byte[64 * 1024];

			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = "GET";
			request.Timeout = Globals.AssetDownloadTimeoutMilliseconds;

			if (url.StartsWith("https://archive.org/") == true)
				request.Headers.Add("Cookie", Globals.AuthCookie);

			long progress = 0;

			using (WebResponse response = request.GetResponse())
			{
				using (Stream sourceStream = response.GetResponseStream())
				{
					using (FileStream targetStream = new FileStream(filename, FileMode.Create, FileAccess.Write))
					{
						int bytesRead;
						while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
						{
							total += bytesRead;
							targetStream.Write(buffer, 0, bytesRead);

							progress += bytesRead;
							if (progress >= Globals.DownloadDotSize)
							{
								Console.Write(".");
								progress = 0;
							}

							if (expectedSize > 0)
								lock (Globals.WorkerTaskInfo)
									Globals.WorkerTaskInfo.BytesCurrent = total;
						}
					}
				}
			}

			return total;
		}

		public static void LinkFiles(string[][] linkTargetFilenames)
		{
			StringBuilder batch = new StringBuilder();

			for (int index = 0; index < linkTargetFilenames.Length; ++index)
			{
				string link = linkTargetFilenames[index][0];
				string target = linkTargetFilenames[index][1];

				//	Escape cmd special characters, may be more ?
				link = link.Replace("%", "%%");

				batch.Append("mklink ");
				batch.Append('\"');
				batch.Append(link);
				batch.Append("\" \"");
				batch.Append(target);
				batch.Append('\"');
				batch.AppendLine();
			}

			using (TempDirectory tempDir = new TempDirectory())
			{
				string batchFilename = tempDir.Path + @"\link.bat";
				File.WriteAllText(batchFilename, batch.ToString(), new UTF8Encoding(false));

				string input = "chcp 65001" + Environment.NewLine + batchFilename + Environment.NewLine;

				ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe")
				{
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardInput = true,
				};

				using (Process process = new Process())
				{
					process.StartInfo = startInfo;
					process.Start();
					process.StandardInput.WriteLine(input);
					process.StandardInput.Close();
					process.WaitForExit();
				}
			}
		}

		public static DateTime FromEpochDate(string epoch)
		{
			return FromEpochDate(double.Parse(epoch));
		}
		public static DateTime FromEpochDate(double epoch)
		{
			return EpochDateTime.AddSeconds(epoch);
		}
		private static readonly DateTime EpochDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		public static string DataSize(long sizeBytes)
		{
			return DataSize((ulong)sizeBytes);
		}
		public static string DataSize(ulong sizeBytes)
		{
			for (int index = 0; index < _SystemOfUnits.Length; ++index)
			{
				ulong nextUnit = (ulong)Math.Pow(2, (index + 1) * 10);

				if (sizeBytes < nextUnit || nextUnit == 0 || index == (_SystemOfUnits.Length - 1))
				{
					ulong unit = (ulong)Math.Pow(2, index * 10);
					decimal result = (decimal)sizeBytes / (decimal)unit;
					int decimalPlaces = 0;
					if (result <= 9.9M)
						decimalPlaces = 1;
					result = Math.Round(result, decimalPlaces);
					return result.ToString() + " " + _SystemOfUnits[index];
				}
			}

			throw new ApplicationException("Failed to find Data Size: " + sizeBytes.ToString());
		}

		public static void Bitmap2SVG(string filenameOrDirectory)
		{
			string[] filenames = new string[] { filenameOrDirectory };

			if (Directory.Exists(filenameOrDirectory) == true)
				filenames = Directory.GetFiles(filenameOrDirectory, "*.png");

			foreach (string filename in filenames)
			{
				string targetFilename = Path.Combine(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename) + ".svg");

				Bitmap2SVG(filename, targetFilename);
			}

		}
		public static void Bitmap2SVG(string filename, string targetFilename)
		{
			using (StreamWriter writer = new StreamWriter(targetFilename, false, Encoding.UTF8))
			{
				using (Image image = Image.FromFile(filename))
				{
					writer.WriteLine("<svg version=\"1.1\" id=\"mame-ao\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xml:space=\"preserve\" " +
						$"x=\"{0}px\" y=\"{0}px\" width=\"{image.Width}px\" height=\"{image.Height}px\">");

					using (Bitmap bitmap = new Bitmap(image))
					{
						for (int y = 0; y < image.Height; ++y)
						{
							for (int x = 0; x < image.Width; ++x)
							{
								Color colour = bitmap.GetPixel(x, y);

								if (colour.A == 0)
									continue;

								string fill = String.Format("{0:x6}", colour.ToArgb() & 0xFFFFFF);

								writer.WriteLine($"<rect x=\"{x}\" y=\"{y}\" width=\"1\" height=\"1\" fill=\"#{fill}\"/>");
							}
						}
					}

					Console.WriteLine($"SVG: {image.Width} X {image.Height} : {targetFilename}");

					writer.WriteLine("</svg>");
				}
			}
		}
	}

	public class TempDirectory : IDisposable
	{
		private readonly string _LockFilePath;
		private readonly string _Path;

		public TempDirectory()
		{
			_LockFilePath = System.IO.Path.GetTempFileName();
			_Path = _LockFilePath + ".dir";

			Directory.CreateDirectory(this._Path);
		}

		public void Dispose()
		{
			if (Directory.Exists(_Path) == true)
				Directory.Delete(_Path, true);

			if (_LockFilePath != null)
				File.Delete(_LockFilePath);
		}

		public string Path
		{
			get => _Path;
		}

		public override string ToString()
		{
			return _Path;
		}
	}

}
