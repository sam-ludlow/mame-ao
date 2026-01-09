using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spludlow.MameAO
{
	public class PhoneHome
	{
		private DateTime StartTime;
		private DateTime? ReadyTime;
		private DateTime EndTime;

		private string Line;
		private Exception Exception;

		private StringBuilder _MameOutput = new StringBuilder();
		private StringBuilder _MameError = new StringBuilder();
		private int? _MameExitCode = null;
		private string _core_name = null;
		private string _core_version = null;
		private string _arguments = null;

		public PhoneHome(string line)
		{
			StartTime = DateTime.Now;
			Line = line;
		}

		public void Ready()
		{
			ReadyTime = DateTime.Now;
		}

		public void Success(string snapFilename)
		{
			Submit(snapFilename);
		}

		public void Error(Exception exception)
		{
			Exception = exception;
			Submit(null);
		}

		public void MameOutputLine(string line)
		{
			_MameOutput.AppendLine(line);
		}

		public void MameErrorLine(string line)
		{
			_MameError.AppendLine(line);
		}
		public void MameExitCode(int code, string core_name, string core_version, string arguments)
		{
			_MameExitCode = code;
			_core_name = core_name;
			_core_version = core_version;
			_arguments = arguments;
		}

		private void Submit(string snapFilename)
		{
			if (Globals.Settings.Options["PhoneHome"] == "No" || _MameExitCode == null)
				return;

			EndTime = DateTime.Now;

			bool verbose = Globals.Settings.Options["PhoneHome"] == "YesVerbose";

			string url = "https://data.spludlow.co.uk/api/phone-home";

			Task task = new Task(() => {
				try
				{
					dynamic json = new JObject();

					json.message_id = Guid.NewGuid().ToString();

					json.start_time = StartTime;
					json.ready_time = ReadyTime;
					json.end_time = EndTime;
					json.line = Line;

					json.mame_exit_code = _MameExitCode;
					json.core_name = _core_name;
					json.core_version = _core_version;
					json.arguments = _arguments;

					json.ao_available = Globals.AuthCookie != null;
					json.bt_available = Globals.BitTorrentAvailable;
					json.linking_enabled = Globals.LinkingEnabled;

					if (Exception != null)
						json.exception = Exception.ToString();

					if (_MameOutput.Length > 0)
						json.mame_output = _MameOutput.ToString();
					if (_MameError.Length > 0)
						json.mame_error = _MameError.ToString();

					Tools.CleanDynamic(json);

					string body = json.ToString(Formatting.Indented);

					if (verbose == true)
					{
						Console.WriteLine();
						Tools.ConsoleHeading(2, "Phone Home");
						Console.WriteLine(body);
					}

					string token = null;

					using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, url))
					{
						requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");

						Task<HttpResponseMessage> requestTask = Globals.HttpClient.SendAsync(requestMessage);
						requestTask.Wait();
						HttpResponseMessage responseMessage = requestTask.Result;

						responseMessage.EnsureSuccessStatusCode();

						Task<string> responseBody = responseMessage.Content.ReadAsStringAsync();
						responseBody.Wait();
						dynamic responseData = JsonConvert.DeserializeObject<dynamic>(responseBody.Result);
						token = responseData.token;
					}

					if (snapFilename != null && token != null)
					{
						using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, url))
						{
							requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

							using (var fileStream = new FileStream(snapFilename, FileMode.Open))
							{
								requestMessage.Content = new StreamContent(fileStream);
								requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

								Task<HttpResponseMessage> requestTask = Globals.HttpClient.SendAsync(requestMessage);
								requestTask.Wait();
								HttpResponseMessage responseMessage = requestTask.Result;

								responseMessage.EnsureSuccessStatusCode();
							}
						}
					}
				}
				catch (Exception e)
				{
					if (verbose == true)
					{
						Console.WriteLine();
						Tools.ConsoleHeading(2, new string[] { "!!! Phone Home Error", e.Message });
						Console.WriteLine(e.ToString());
					}
				}
			});

			task.Start();
		}

		public static void ProcessPhoneHome(string directory, string connectionString)
		{
			string snapHomeDirectory = Path.Combine(directory, "snap-home");
			string snapSubmitDirectory = Path.Combine(directory, "snap-submit");

			Dictionary<string, DataTable> snapIndexTables = new Dictionary<string, DataTable>();
			foreach (string core in new string[] { "mame", "hbmame" })
			{
				DataTable table = Snap.LoadSnapIndex(Path.Combine(directory, "snap"), core);
				if (table == null)
					throw new ApplicationException($"Snap index not available for core: {core}");
				snapIndexTables.Add(core, table);
			}

			SqlConnection connection = new SqlConnection(connectionString);

			DataTable phoneHomesTable = new DataTable();
			using (SqlDataAdapter adapter = new SqlDataAdapter("SELECT * FROM [PhoneHomes] WHERE ([ProcessTime] IS NULL AND [token] IS NOT NULL) ORDER BY [PhoneHomeId]", connection))
				adapter.Fill(phoneHomesTable);

			DataTable targetTable = new DataTable("snap_submit");
			using (SqlDataAdapter adapter = new SqlDataAdapter("SELECT TOP 0 * FROM [snap_submit]", connection))
				adapter.Fill(targetTable);

			foreach (DataRow phoneHomeRow in phoneHomesTable.Rows)
			{
				string token = (string)phoneHomeRow["token"];
				string snapFilename = Path.Combine(snapHomeDirectory, token + ".png");

				if (File.Exists(snapFilename) == false)
					continue;

				long PhoneHomeId = (long)phoneHomeRow["PhoneHomeId"];
				DateTime RequestTime = (DateTime)phoneHomeRow["RequestTime"];

				dynamic json = JsonConvert.DeserializeObject<dynamic>((string)phoneHomeRow["Body"]);

				string line = json.line;
				string core_version = json.core_version;

				string[] parts = line.Split(new char[] { ' ', '@' });
				if (parts.Length != 2 && parts.Length != 4)
					throw new ApplicationException("Bad line");

				string machine = parts[0];
				string core = parts[1];
				string software = parts.Length == 4 ? parts[2] : null;
				string softwarelist = parts.Length == 4 ? parts[3] : null;

				DataTable indexTable = snapIndexTables[core];
				DataRow indexRow = indexTable.Rows.Find(software == null ? machine : $"{softwarelist}\\{software}");

				string existingSnapPngUrl = null;
				if (indexRow != null)
				{
					if (software == null)
						existingSnapPngUrl = $"https://data.spludlow.co.uk/{core}/machine/{machine}.png";
					else
						existingSnapPngUrl = $"https://data.spludlow.co.uk/{core}/software/{softwarelist}/{software}.png";
				}

				string image_token = Guid.NewGuid().ToString();

				Console.WriteLine($"{machine}\t{core}\t{core_version}\t{software}\t{softwarelist}\t{snapFilename}\t{existingSnapPngUrl}\t{image_token}");

				targetTable.Clear();

				//	snap_submit_id	snap_uploaded	core_name	core_version	machine_name	softwarelist_name	software_name	existing	image_token
				targetTable.Rows.Add(DBNull.Value, RequestTime, core, core_version, machine, softwarelist, software, indexRow != null, image_token);

				Database.BulkInsert(connection, targetTable);

				string targetFilename = Path.Combine(snapSubmitDirectory, image_token + ".png");

				Database.ExecuteNonQuery(connection, $"UPDATE [PhoneHomes] SET [ProcessTime] = SYSDATETIME() WHERE ([PhoneHomeId] = {PhoneHomeId})");

				File.Move(snapFilename, targetFilename);
			}
		}
	}
}
