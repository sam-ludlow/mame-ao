using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;

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

					json.display_name = Globals.DisplayName;

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
						if (verbose == true)
						{
							Tools.ConsoleHeading(2, new string[] {
								"Snap Home https://data.spludlow.co.uk/snap",
								$"Your Display Name: {Globals.DisplayName}",
								snapFilename,
							});
						}

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

			SqlConnection connection = new SqlConnection(connectionString);

			DataTable phoneHomesTable = new DataTable();
			using (SqlDataAdapter adapter = new SqlDataAdapter("SELECT * FROM [phone_home] WHERE ([process_time] IS NULL AND [token] IS NOT NULL) ORDER BY [phone_home_id]", connection))
				adapter.Fill(phoneHomesTable);

			DataTable targetTable = null;
			Dictionary<string, DataTable> snapIndexTables = null;

			foreach (DataRow phoneHomeRow in phoneHomesTable.Rows)
			{
				string token = (string)phoneHomeRow["token"];
				string snapFilename = Path.Combine(snapHomeDirectory, token + ".png");

				if (File.Exists(snapFilename) == false)
					continue;

				if (snapIndexTables == null)
				{
					snapIndexTables = new Dictionary<string, DataTable>();
					foreach (string core in new string[] { "mame", "hbmame" })
					{
						DataTable table = Snap.LoadSnapIndex(Path.Combine(directory, "snap"), core);
						if (table == null)
							throw new ApplicationException($"Snap index not available for core: {core}");
						snapIndexTables.Add(core, table);
					}

					targetTable = new DataTable("snap_submit");
					using (SqlDataAdapter adapter = new SqlDataAdapter("SELECT TOP 0 * FROM [snap_submit]", connection))
						adapter.Fill(targetTable);
				}

				long phone_home_id = (long)phoneHomeRow["phone_home_id"];
				DateTime request_time = (DateTime)phoneHomeRow["request_time"];

				dynamic json = JsonConvert.DeserializeObject<dynamic>((string)phoneHomeRow["body"]);

				int status = -1;

				try
				{
					string line = json.line;
					string core_version = json.core_version;

					string display_name = json.display_name ?? "anonymous";
					if (display_name.Length > 32)
						display_name = display_name.Substring(0, 32);

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

					targetTable.Rows.Add(DBNull.Value, request_time, display_name, core, core_version, machine, softwarelist, software, indexRow != null, image_token, 0, DBNull.Value, phone_home_id);

					Database.BulkInsert(connection, targetTable);

					string targetFilename = Path.Combine(snapSubmitDirectory, image_token + ".png");
					string thumbFilename = Path.Combine(snapSubmitDirectory, image_token + ".jpg");

					File.Copy(snapFilename, targetFilename);
					
					Snap.Resize(targetFilename, Snap.ThumbSize, thumbFilename, ImageFormat.Jpeg, PixelFormat.Format24bppRgb, 72);

					status = 1;
				}
				catch (Exception e)
				{
					Console.WriteLine(e.ToString());
				}
				finally
				{
					Database.ExecuteNonQuery(connection, $"UPDATE [phone_home] SET [process_time] = SYSDATETIME(), status = {status} WHERE ([phone_home_id] = {phone_home_id})");

					File.Delete(snapFilename);
				}


			}
		}

		public static void ApprovePhoneHome(string directory, string connectionString)
		{
			string snapSubmitDirectory = Path.Combine(directory, "snap-submit");	//	GUID.png in top directory
			string snapTempDirectory = Path.Combine(directory, "snap-temp");        //	Core name below

			SqlConnection connection = new SqlConnection(connectionString);

			DataTable snapSubmitTable = new DataTable();
			using (SqlDataAdapter adapter = new SqlDataAdapter("SELECT * FROM snap_submit WHERE (process_time IS NULL AND snap_submit.[status] <> 0) ORDER BY snap_submit_id;", connection))
				adapter.Fill(snapSubmitTable);

			foreach (DataRow row in snapSubmitTable.Rows)
			{
				long snap_submit_id = (long)row["snap_submit_id"];
				int status = (int)row["status"];
				string core_name = (string)row["core_name"];
				string machine_name = (string)row["machine_name"];
				string softwarelist_name = row.IsNull("softwarelist_name") == false ? (string)row["softwarelist_name"] : null;
				string software_name = row.IsNull("software_name") == false ? (string)row["software_name"] : null;
				string image_token = (string)row["image_token"];

				string sourceFilename = Path.Combine(snapSubmitDirectory, image_token + ".png");
				if (File.Exists(sourceFilename) == false)
					throw new ApplicationException($"sourceFilename not found: {sourceFilename}");

				switch (status)
				{
					case -1:	// Rejected
						break;

					case 1:     // Aproved
						string targetFilename = softwarelist_name == null ?
							Path.Combine(snapTempDirectory, core_name, machine_name + ".png") : Path.Combine(snapTempDirectory, core_name, softwarelist_name, software_name + ".png");

						Console.WriteLine($"{snap_submit_id}\t{sourceFilename}\t=>\t{targetFilename}");
						Directory.CreateDirectory(Path.GetDirectoryName(targetFilename));
						File.Copy(sourceFilename, targetFilename);
						break;

					default:
						throw new ApplicationException($"Unknown status: {status}");
				}

				Database.ExecuteNonQuery(connection, $"UPDATE [snap_submit] SET [process_time] = SYSDATETIME() WHERE ([snap_submit_id] = {snap_submit_id})");
			}

		}
	}
}
