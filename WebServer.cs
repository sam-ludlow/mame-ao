using System;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spludlow.MameAO
{
	public class WebServer
	{
		private MameAOProcessor _AO;
		
		private Task _RunTask = null;

		public WebServer(MameAOProcessor ao)
		{
			_AO = ao;
		}

		public void StartListener()
		{
			if (HttpListener.IsSupported == false)
			{
				Console.WriteLine("!!! Http Listener Is not Supported");
				return;
			}

			HttpListener listener = new HttpListener();
			listener.Prefixes.Add(_AO._ListenAddress);
			listener.Start();

			Task listenTask = new Task(() => {

				while (true)
				{
					HttpListenerContext context = listener.GetContext();

					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");

					string path = context.Request.Url.AbsolutePath.ToLower();

					using (StreamWriter writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8))
					{
						try
						{
							if (context.Request.HttpMethod == "OPTIONS")
							{
								context.Response.Headers.Add("Allow", "OPTIONS, GET");
							}
							else
							{
								switch (path)
								{
									case "/":
										Root(context, writer);
										break;

									case "/command":
										if (_RunTask != null && _RunTask.Status != TaskStatus.RanToCompletion)
											throw new ApplicationException("I'm busy.");

										Command(context, writer);
										break;

									case "/api/profiles":
										ApiProfiles(context, writer);
										break;

									case "/api/machines":
										ApiMachines(context, writer);
										break;

									default:
										throw new ApplicationException($"404 {path}");
								}
							}

						}
						catch (ApplicationException e)
						{
							writer.WriteLine(e.ToString());
							context.Response.StatusCode = 400;
						}
						catch (Exception e)
						{
							writer.WriteLine(e.ToString());
							context.Response.StatusCode = 500;
						}
					}	
				}
			});

			listenTask.Start();
		}

		private void Root(HttpListenerContext context, StreamWriter writer)
		{
			string html = File.ReadAllText(@"UI.html", Encoding.UTF8);

			context.Response.Headers["Content-Type"] = "text/html";

			writer.WriteLine(html);
		}

		private void Command(HttpListenerContext context, StreamWriter writer)
		{
			string machine = context.Request.QueryString["machine"];
			string software = context.Request.QueryString["software"];
			string arguments = context.Request.QueryString["arguments"];

			if (machine == null)
				throw new ApplicationException("No machine given.");

			Console.WriteLine();
			Tools.ConsoleHeading(_AO._h1, new string[] {
				"Remote command recieved",
				$"machine: {machine}",
				$"software: {software}",
				$"arguments: {arguments}",
			});
			Console.WriteLine();

			_RunTask = new Task(() => {
				try
				{
					_AO.RunLine($"{machine} {software} {arguments}");
				}
				catch (Exception ee)
				{
					Console.WriteLine();
					Console.WriteLine("!!! REMOTE COMMAND ERROR: " + ee.Message);
					Console.WriteLine();
					Console.WriteLine(ee.ToString());
					Console.WriteLine();
					Console.WriteLine("If you want to submit an error report please copy and paste the text from here.");
					Console.WriteLine("Select All (Ctrl+A) -> Copy (Ctrl+C) -> notepad -> paste (Ctrl+V)");
				}
			});
			
			_RunTask.Start();
			
			writer.WriteLine("OK");
		}

		private void ApiProfiles(HttpListenerContext context, StreamWriter writer)
		{
			JArray results = new JArray();

			for (int index = 0; index < Database.DataQueryProfiles.Length; ++index)
			{
				dynamic result = new JObject();
				result.name = Database.DataQueryProfiles[index][0];
				result.command = Database.DataQueryProfiles[index][1];
				results.Add(result);
			}

			dynamic json = new JObject();
			json.results = results;

			context.Response.Headers["Content-Type"] = "application/json";
			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		private void ApiMachines(HttpListenerContext context, StreamWriter writer)
		{
			string qs;

			int offset = 0;
			qs = context.Request.QueryString["offset"];
			if (qs != null)
				offset = Int32.Parse(qs);

			int limit = 100;
			qs = context.Request.QueryString["limit"];
			if (qs != null)
				limit = Int32.Parse(qs);

			if (limit > 1000)
				throw new ApplicationException("Limit is limited to 1000");

			int profileIndex = 0;
			qs = context.Request.QueryString["profile"];
			if (qs != null)
				profileIndex = Int32.Parse(qs);

			if (profileIndex < 0 || profileIndex >= Database.DataQueryProfiles.Length)
				throw new ApplicationException("Bad profile index");

			string profileName = Database.DataQueryProfiles[profileIndex][0];

			DataTable table = _AO._Database.QueryMachine(profileIndex, offset, limit);

			JArray results = new JArray();

			foreach (DataRow row in table.Rows)
			{
				dynamic result = new JObject();

				result.image = $"https://mame.spludlow.co.uk/snap/machine/{(string)row["name"]}.jpg";
				result.name = (string)row["name"];
				result.description = (string)row["description"];
				result.year = row.IsNull("year") ? "" : (string)row["year"];
				result.manufacturer = row.IsNull("manufacturer") ? "" : (string)row["manufacturer"];

				result.ao_rom_count = row["ao_rom_count"];
				result.ao_disk_count = row["ao_disk_count"];
				result.ao_softwarelist_count = row["ao_softwarelist_count"];

				results.Add(result);
			}

			dynamic json = new JObject();
			json.profile = profileName;
			json.offset = offset;
			json.limit = limit;
			json.total = table.Rows.Count == 0 ? 0 : (long)table.Rows[0]["ao_total"];
			json.count = results.Count;
			json.results = results;

			context.Response.Headers["Content-Type"] = "application/json";
			writer.WriteLine(json.ToString(Formatting.Indented));
		}



	}
}
