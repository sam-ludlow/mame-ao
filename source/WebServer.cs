using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Spludlow.MameAO
{
	public class WebServer
	{
		private string _UIHTML;
		private string _StyleSheet;

		private string _StyleSheetFilename = Path.Combine(Globals.RootDirectory, "_styles.css");

		private readonly string MACHINE_IMAGE_URL = "https://data.spludlow.co.uk/@core/machine/@machine.jpg";
		private readonly string SOFTWARE_IMAGE_URL = "https://data.spludlow.co.uk/@core/software/@softwarelist/@software.jpg";

		private Dictionary<string, string> _Images = new Dictionary<string, string>();

		public WebServer()
		{
			RefreshAssets();

			_Images.Add("/images/logo.svg", @"<?xml version=""1.0"" encoding=""UTF-8""?>
				<svg id=""Layer_1"" xmlns=""http://www.w3.org/2000/svg"" version=""1.1"" viewBox=""0 0 120 64"">
				  <defs>
					<style>
					  .st0 {
						fill: #fff;
					  }

					  .st1 {
						fill: #fff000;
					  }
					</style>
				  </defs>
				  <path d=""M21.16,32.99v-.24c0-2.7-2.16-4.88-4.83-4.88h-4.6c-2.67,0-4.83,2.19-4.83,4.88s2.16,4.88,4.83,4.88h4.6c1.14,0,2.06.93,2.06,2.08s-.92,2.08-2.06,2.08h-4.6c-1.14,0-2.06-.93-2.06-2.08v-.23h-2.77v.24c0,2.7,2.16,4.88,4.83,4.88h4.6c2.67,0,4.83-2.19,4.83-4.88s-2.16-4.88-4.83-4.88h-4.6c-1.14,0-2.06-.93-2.06-2.08s.92-2.08,2.06-2.08h4.6c1.14,0,2.06.93,2.06,2.08v.24h2.77v-.02Z""/>
				  <path d=""M43.9,46.44c-1.14,0-2.06-.93-2.06-2.08v-23.46h-2.77v23.46c0,2.7,2.16,4.88,4.83,4.88h.23v-2.8h-.23Z""/>
				  <path d=""M110.32,27.87v11.85c0,1.15-.92,2.08-2.06,2.08s-2.06-.93-2.06-2.08v-7.2h-2.77v7.2h0c0,1.14-.92,2.07-2.06,2.08-1.14,0-2.06-.93-2.06-2.08v-11.85h-2.77v11.85c0,2.7,2.16,4.88,4.83,4.88,1.35,0,2.57-.57,3.45-1.47.87.9,2.09,1.47,3.44,1.47,2.67,0,4.83-2.19,4.83-4.88v-11.85h-2.77Z""/>
				  <path d=""M55.16,27.87v9.53c0,1.21-.48,2.31-1.28,3.12-.79.8-1.87,1.29-3.08,1.29s-2.3-.49-3.08-1.29c-.79-.8-1.28-1.89-1.28-3.12v-9.53h-2.77v9.53c0,3.98,3.19,7.2,7.13,7.2,1.64,0,3.16-.57,4.36-1.51v1.51h2.77v-16.73h-2.76Z""/>
				  <path d=""M87.58,27.87c-3.94,0-7.14,3.23-7.14,7.2v2.32c0,3.98,3.19,7.2,7.14,7.2s7.13-3.23,7.13-7.2v-2.32c0-3.98-3.19-7.2-7.13-7.2M91.93,37.4c0,1.21-.48,2.31-1.27,3.12-.79.8-1.87,1.29-3.08,1.29s-2.3-.49-3.08-1.29c-.79-.8-1.28-1.89-1.28-3.12v-2.32c0-1.21.49-2.31,1.28-3.12.79-.8,1.87-1.29,3.08-1.29s2.3.49,3.08,1.29c.79.8,1.27,1.89,1.27,3.12v2.32Z""/>
				  <path d=""M80.68,46.44c-1.14,0-2.06-.93-2.06-2.08v-23.46h-2.77v23.46c0,2.7,2.16,4.88,4.83,4.88h.23v-2.8h-.23Z""/>
				  <path class=""st0"" d=""M58.84,0C47.93,0,39.07,8.95,39.07,19.98v.24h2.77v-.24c0-4.74,1.91-9.03,4.98-12.14s7.33-5.03,12.02-5.03,8.94,1.93,12.02,5.03,4.98,7.4,4.98,12.14v.24h2.77v-.24C78.62,8.95,69.76,0,58.84,0""/>
				  <path class=""st0"" d=""M38.3,25.39c-1.69-.84-3.23-1.95-4.55-3.28h0l-.26-.25h0c-3.55-3.46-8.4-5.59-13.72-5.59C8.85,16.26,0,25.21,0,36.24c0,7.19,5.76,13.01,12.88,13.01h9.43v-2.8h-9.44c-2.79,0-5.32-1.14-7.15-2.99-1.83-1.85-2.96-4.4-2.96-7.22,0-4.74,1.91-9.03,4.98-12.14,3.08-3.11,7.33-5.03,12.02-5.03s8.94,1.93,12.02,5.03l.17.17.17.17h0c1.72,1.65,3.73,3,5.95,3.95l.33.14v-3.06l-.12-.08Z""/>
				  <path class=""st0"" d=""M100.22,16.25c-5.33,0-10.17,2.13-13.72,5.59l-.26.26h0c-1.87,1.89-4.19,3.35-6.78,4.2l-.16.06v2.92l.3-.09c3.14-.9,5.96-2.56,8.25-4.77h0s.34-.33.34-.33h0c3.08-3.11,7.33-5.03,12.02-5.03s8.94,1.93,12.02,5.03c3.08,3.11,4.98,7.4,4.98,12.14,0,2.82-1.13,5.37-2.96,7.22-1.83,1.85-4.36,2.99-7.15,2.99h-25.52v2.8h25.52c7.11,0,12.88-5.83,12.88-13.01,0-11.04-8.86-19.98-19.78-19.98""/>
				  <path class=""st0"" d=""M77.38,48.81c-.76-.58-1.36-1.34-1.74-2.22l-.06-.14h-30.75v2.8h33.13l-.57-.43Z""/>
				  <path class=""st0"" d=""M40.6,48.81c-.76-.58-1.36-1.34-1.74-2.22l-.06-.14h-12.37v2.8h14.74l-.57-.43Z""/>
				  <path class=""st1"" d=""M30.11,27.87c-1.64,0-3.16.57-4.36,1.51v-1.51h-2.77v26.02h1.82l-1.94,4.65h2l-2.36,5.47,8.18-8.27h-3.95l3.71-4.41-.19-.17-.07-.06h-4.44v-7.99c1.2.95,2.72,1.51,4.36,1.51,3.94,0,7.13-3.23,7.13-7.2v-2.32c0-3.98-3.18-7.21-7.12-7.21M30.11,41.8c-2.41,0-4.36-1.97-4.36-4.4v-2.33c0-1.21.48-2.32,1.27-3.12s1.87-1.29,3.08-1.29,2.3.49,3.08,1.29,1.28,1.89,1.28,3.12v2.32c0,1.21-.49,2.31-1.28,3.12-.79.8-1.88,1.29-3.08,1.29""/>
				  <path class=""st1"" d=""M58.84,4.65h-.23v2.8h.23c3.43,0,6.53,1.4,8.77,3.67,2.24,2.26,3.63,5.4,3.63,8.86v9.4c-1.2-.95-2.72-1.51-4.36-1.51-3.94,0-7.13,3.23-7.13,7.2v2.32h0c0,3.98,3.19,7.2,7.13,7.2,1.64,0,3.16-.57,4.36-1.51v1.51h2.77v-7.2h0v-17.41c0-8.47-6.79-15.33-15.17-15.33M71.25,37.4h0c0,1.21-.48,2.32-1.28,3.12-.79.8-1.87,1.29-3.08,1.29s-2.3-.49-3.08-1.29c-.79-.8-1.27-1.89-1.27-3.12h0v-2.32c0-1.21.48-2.31,1.27-3.12.79-.8,1.87-1.29,3.08-1.29,2.41,0,4.36,1.97,4.36,4.4v2.32h0Z""/>
				</svg>
			");
			_Images.Add("/images/fav-off.svg", @"<?xml version=""1.0"" encoding=""UTF-8""?>
				<svg id=""Layer_1"" xmlns=""http://www.w3.org/2000/svg"" version=""1.1"" viewBox=""0 0 18 18"">
				  <defs>
					<style>
					  .st0 {
						fill: gray;
					  }
					</style>
				  </defs>
				  <path class=""st0"" d=""M9,1.5l1.51,2.61.22.38h5.21l-2.39,4.14-.22.38.22.38,2.39,4.14h-5.21l-.22.38-1.51,2.61-1.51-2.61-.22-.38H2.07l2.39-4.14.22-.38-.22-.38-2.39-4.14h5.21l.22-.38,1.51-2.61M9,0l-2.16,3.74H.77l3.04,5.26L.77,14.26h6.07l2.16,3.74,2.16-3.74h6.07l-3.04-5.26,3.04-5.26h-6.07l-2.16-3.74h0Z""/>
				</svg>
			");
			_Images.Add("/images/fav-on.svg", @"<?xml version=""1.0"" encoding=""UTF-8""?>
				<svg id=""Layer_1"" xmlns=""http://www.w3.org/2000/svg"" version=""1.1"" viewBox=""0 0 18 18"">
				  <defs>
					<style>
					  .st0 {
						fill: #ff0;
					  }
					</style>
				  </defs>
				  <polygon class=""st0"" points=""7.06 13.89 1.42 13.89 4.24 9 1.42 4.11 7.06 4.11 9 .75 10.94 4.11 16.58 4.11 13.76 9 16.58 13.89 10.94 13.89 9 17.25 7.06 13.89""/>
				  <path d=""M9,1.5l1.51,2.61.22.38h5.21l-2.39,4.14-.22.38.22.38,2.39,4.14h-5.21l-.22.38-1.51,2.61-1.51-2.61-.22-.38H2.07l2.39-4.14.22-.38-.22-.38-2.39-4.14h5.21l.22-.38,1.51-2.61M9,0l-2.16,3.74H.77l3.04,5.26L.77,14.26h6.07l2.16,3.74,2.16-3.74h6.07l-3.04-5.26,3.04-5.26h-6.07l-2.16-3.74h0Z""/>
				</svg>
			");
			_Images.Add("/images/back.svg", @"<?xml version=""1.0"" encoding=""UTF-8""?>
				<svg id=""Layer_1"" xmlns=""http://www.w3.org/2000/svg"" version=""1.1"" viewBox=""0 0 48 24"">
				  <polygon points=""24 24 0 12 24 0 24 6 48 6 48 18 24 18 24 24""/>
				</svg>
			");
			_Images.Add("/images/next.svg", @"<?xml version=""1.0"" encoding=""UTF-8""?>
				<svg id=""Layer_1"" xmlns=""http://www.w3.org/2000/svg"" version=""1.1"" viewBox=""0 0 48 24"">
				  <polygon points=""24 24 48 12 24 0 24 6 0 6 0 18 24 18 24 24""/>
				</svg>
			");
		}

		public void RefreshAssets()
		{
			_UIHTML = File.ReadAllText(@"UI.html", Encoding.UTF8);

			if (File.Exists(_StyleSheetFilename) == true)
				_StyleSheet = File.ReadAllText(_StyleSheetFilename);
			else
				_StyleSheet = _DefaultStyleSheet;
		}

		public void SaveStyle()
		{
			File.WriteAllText(_StyleSheetFilename, _StyleSheet, Encoding.UTF8);
		}

		public void StartListener()
		{
			if (HttpListener.IsSupported == false)
			{
				Console.WriteLine("!!! Http Listener Is not Supported");
				return;
			}

			HttpListener listener = new HttpListener();

			string listenSufix = Globals.ListenAddress.Substring(Globals.ListenAddress.Length - 7);

			if (Socket.OSSupportsIPv4 == true)
				listener.Prefixes.Add($"http://127.0.0.1{listenSufix}");

			if (Socket.OSSupportsIPv6 == true)
				listener.Prefixes.Add($"http://[::1]{listenSufix}");

			listener.Start();

			Task listenTask = new Task(() => {

				while (true)
				{
					HttpListenerContext context = listener.GetContext();

					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";

					string path = context.Request.Url.AbsolutePath.ToLower();

					using (StreamWriter writer = new StreamWriter(context.Response.OutputStream, new UTF8Encoding(false)))
					{
						try
						{
							if (context.Request.HttpMethod == "OPTIONS")
							{
								context.Response.Headers.Add("Allow", "OPTIONS, GET");
							}
							else
							{
								if (path.StartsWith("/api/") == true)
								{
									MethodInfo method = this.GetType().GetMethod(path.Replace("/", "_"));

									if (method == null)
									{
										ApplicationException exception = new ApplicationException($"Not found: {path}");
										exception.Data.Add("status", 404);
										throw exception;
									}

									method.Invoke(this, new object[] { context, writer });

								}
								else
								{
									switch (path)
									{
										case "/favicon.ico":
											context.Response.Headers["Content-Type"] = "image/x-icon";
											context.Response.OutputStream.Write(_FavIcon, 0, _FavIcon.Length);
											context.Response.Headers["Cache-Control"] = "max-age=60";
											break;

										case "/images/logo.svg":
										case "/images/fav-off.svg":
										case "/images/fav-on.svg":
										case "/images/next.svg":
										case "/images/back.svg":
											context.Response.Headers["Content-Type"] = "image/svg+xml";
											context.Response.Headers["Cache-Control"] = "max-age=60";
											writer.Write(_Images[path]);
											break;

										case "/styles.css":
											context.Response.Headers["Content-Type"] = "text/css";
											context.Response.Headers["Cache-Control"] = "max-age=60";
											writer.WriteLine(_StyleSheet);
											break;

										default:
											context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
											writer.WriteLine(_UIHTML);
											break;
									}
								}
							}

						}
						catch (Exception e)
						{
							if (e is TargetInvocationException && e.InnerException != null)
								e = e.InnerException;

							ErrorResponse(context, writer, e);
						}
					}	
				}
			});

			listenTask.Start();
		}

		private void ErrorResponse(HttpListenerContext context, StreamWriter writer, Exception e)
		{
			int status = 500;

			if (e is ApplicationException)
				status = 400;

			if (e.Data["status"] != null)
				status = (int)e.Data["status"];

			context.Response.StatusCode = status;

			dynamic json = new JObject();
			
			json.status = status;
			json.message = e.Message;
			json.error = e.ToString();

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_end_points(HttpListenerContext context, StreamWriter writer)
		{
			JArray results = new JArray();

			foreach (MethodInfo method in this.GetType().GetMethods())
			{
				if (method.Name.StartsWith("_api_") == false)
					continue;

				dynamic result = new JObject();

				result.name = method.Name.Substring(5);
				result.location = Globals.ListenAddress + "api/" + result.name;

				results.Add(result);

			}

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = results.Count;
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_command(HttpListenerContext context, StreamWriter writer)
		{
			string line = context.Request.QueryString["line"];

			if (line == null || line.Length == 0)
				throw new ApplicationException("No line given.");

			string[] parts = line.Split(' ');

			switch (parts[0])
			{
				case ".favm":
				case ".favmx":
				case ".favs":
				case ".favsx":
					Globals.Favorites.AddCommandLine(line);
					break;

				case ".set":
					Globals.Settings.Set(parts[1], parts[2]);
					break;

				default:
					Console.WriteLine();
					Tools.ConsoleHeading(1, new string[] {
						"Remote command recieved",
						line,
					});
					Console.WriteLine();

					bool started = Globals.AO.RunLineTask(line);

					if (started == false)
						throw new ApplicationException("I'm busy.");
					break;
			}

			dynamic json = new JObject();
			json.message = "OK";
			json.command = line;
			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_update(HttpListenerContext context, StreamWriter writer)
		{
			Console.WriteLine();
			Tools.ConsoleHeading(1, new string[] {
				"Remote update recieved",
			});
			Console.WriteLine();

			bool started = Globals.AO.RunLineTask(".up");

			writer.WriteLine(started == true ?
				"<html>Please wait, MAME-AO update has started.<br/><br/>Check the console to see what it's doing.<br/><br/>" +
				"The database will be re-created so give it a moment.<br/><br/>The updated Web UI will apear when finished.</html>"

				: "MAME-AO is busy. Is it already updating or running MAME? Kill all MAME-AO processes and try again.");

			context.Response.Headers["Content-Type"] = "text/html";
		}

		public void _api_profiles(HttpListenerContext context, StreamWriter writer)
		{
			dynamic results = new JArray();

			foreach (DataQueryProfile profile in Database.DataQueryProfiles)
			{
				dynamic result = new JObject();

				result.key = profile.Key;
				result.text = profile.Text;
				result.description = profile.Decription;

				results.Add(result);
			}

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = results.Count;
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_machines(HttpListenerContext context, StreamWriter writer)
		{
			string qs;

			string profile = context.Request.QueryString["profile"] ?? throw new ApplicationException("profile not passed");

			int offset = 0;
			qs = context.Request.QueryString["offset"];
			if (qs != null)
				offset = Int32.Parse(qs);

			int limit = 250;
			qs = context.Request.QueryString["limit"];
			if (qs != null)
				limit = Int32.Parse(qs);

			string search = "";
			qs = context.Request.QueryString["search"];
			if (qs != null)
				search = qs.Trim();
			if (search.Length == 0)
				search = null;

			string manufacturer = "";
			qs = context.Request.QueryString["manufacturer"];
			if (qs != null)
				manufacturer = qs.Trim();
			if (manufacturer.Length == 0)
				manufacturer = null;

			string[] status = new string[0];
			qs = context.Request.QueryString["status"];
			if (qs != null && qs != "")
				status = qs.Split(',');

			bool? mechanical = null;
			qs = context.Request.QueryString["mechanical"];
			if (qs != null)
				mechanical = Boolean.Parse(qs);

			bool? clone = null;
			qs = context.Request.QueryString["clone"];
			if (qs != null)
				clone = Boolean.Parse(qs);

			string order = context.Request.QueryString["order"] ?? "description";
			string sort = context.Request.QueryString["sort"] ?? "asc";

			DataTable table = Globals.Core.QueryMachines(profile, offset, limit, search, manufacturer, status, mechanical, clone, order, sort);

			JArray results = new JArray();

			foreach (DataRow row in table.Rows)
			{
				dynamic result = RowToJson(row);

				string name = (string)row["name"];

				result.ao_image = MACHINE_IMAGE_URL.Replace("@machine", name).Replace("@core", Globals.Core.Name);

				results.Add(result);
			}

			dynamic json = new JObject();
			json.profile = profile;
			json.offset = offset;
			json.limit = limit;
			json.total = table.Rows.Count == 0 ? 0 : (long)table.Rows[0]["ao_total"];
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_machine(HttpListenerContext context, StreamWriter writer)
		{
			string qs;

			string machine_name = null;
			qs = context.Request.QueryString["name"];
			if (qs != null)
				machine_name = qs;

			if (machine_name == null)
				throw new ApplicationException("machine not passed");

			DataRow machine = Globals.Core.GetMachine(machine_name);

			DataRow[] machineSoftwareListRows = Globals.Core.GetMachineSoftwareLists(machine);

			dynamic json = RowToJson(machine);

			json.ao_image = MACHINE_IMAGE_URL.Replace("@machine", machine_name).Replace("@core", Globals.Core.Name);

			if (machineSoftwareListRows.Length > 0)
			{
				JArray softwarelists = new JArray();

				foreach (DataRow row in machineSoftwareListRows)
				{
					dynamic softwarelist = new JObject();

					softwarelist.name = (string)row["name"];
					softwarelist.description = (string)row["description"];

					softwarelists.Add(softwarelist);
				}

				json.softwarelists = softwarelists;
			}

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_software(HttpListenerContext context, StreamWriter writer)
		{
			string qs;

			string softwarelist = null;
			qs = context.Request.QueryString["softwarelist"];
			if (qs != null)
				softwarelist = qs;

			if (softwarelist == null)
				throw new ApplicationException("softwarelist not passed");

			int offset = 0;
			qs = context.Request.QueryString["offset"];
			if (qs != null)
				offset = Int32.Parse(qs);

			int limit = 250;
			qs = context.Request.QueryString["limit"];
			if (qs != null)
				limit = Int32.Parse(qs);

			string search = "";
			qs = context.Request.QueryString["search"];
			if (qs != null)
				search = qs.Trim();
			if (search.Length == 0)
				search = null;

			string publisher = "";
			qs = context.Request.QueryString["publisher"];
			if (qs != null)
				publisher = qs.Trim();
			if (publisher.Length == 0)
				publisher = null;

			string order = context.Request.QueryString["order"] ?? "description";
			string sort = context.Request.QueryString["sort"] ?? "asc";

			string favorites_machine = context.Request.QueryString["favorites_machine"];
			if (favorites_machine != null)
				favorites_machine = favorites_machine.Trim();

			DataTable table = Globals.Core.QuerySoftware(softwarelist, offset, limit, search, publisher, order, sort, favorites_machine);

			JArray results = new JArray();

			foreach (DataRow row in table.Rows)
			{
				dynamic result = RowToJson(row);

				string name = (string)row["name"];

				result.ao_image = SOFTWARE_IMAGE_URL
					.Replace("@softwarelist", softwarelist == "@fav" ? (string)row["softwarelist_name"] : softwarelist)
					.Replace("@software", name)
					.Replace("@core", Globals.Core.Name);

				results.Add(result);
			}

			dynamic json = new JObject();
			json.softwarelist = softwarelist;
			json.offset = offset;
			json.limit = limit;
			json.total = table.Rows.Count == 0 ? 0 : (long)table.Rows[0]["ao_total"];
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_softwarelists(HttpListenerContext context, StreamWriter writer)
		{
			JArray results = new JArray();

			foreach (string key in Globals.Core.SoftwareListDescriptions.Keys)
			{
				dynamic item = new JObject();
				item.name = key;
				item.description = Globals.Core.SoftwareListDescriptions[key];
				results.Add (item);
			}

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = results.Count;
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_info(HttpListenerContext context, StreamWriter writer)
		{
			GitHubRepo mameAoRepo = Globals.GitHubRepos["mame-ao"];

			dynamic json = new JObject();

			json.time = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
			json.version = Globals.AssemblyVersion;
			json.cores = new JArray(Globals.CoreNames);
			json.core_name = Globals.Core.Name;
			json.core_version = Globals.Core.Version;
			json.directory = Globals.RootDirectory;
			json.rom_store_count = Globals.RomHashStore.Length;
			json.disk_store_count = Globals.DiskHashStore.Length;
			json.genre_version = Globals.Genre.Data != null ? Globals.Genre.Version : "";
			json.linking_enabled = Globals.LinkingEnabled;
			json.bit_torrent_enabled = Globals.BitTorrentAvailable;
			if (Globals.BitTorrentAvailable == true)
				json.bit_torrent_url = BitTorrent.ClientUrl;

			json.latest = mameAoRepo.tag_name;

			json.version_name_available = mameAoRepo.tag_name;
			json.version_name_current = Globals.AssemblyVersion;

			dynamic items = new JArray();

			foreach (ItemType itemType in Globals.ArchiveOrgItems.Keys)
			{
				foreach (ArchiveOrgItem sourceItem in Globals.ArchiveOrgItems[itemType])
				{
					dynamic item = new JObject();

					item.key = sourceItem.Key;
					item.type = itemType.ToString();

					item.status = sourceItem.Status;

					item.sub_directory = sourceItem.SubDirectory;
					item.tag = sourceItem.Tag;

					item.url_details = sourceItem.UrlDetails;
					item.url_metadata = sourceItem.UrlMetadata;
					item.url_download = sourceItem.UrlDownload;

					if (sourceItem.Files != null)
					{
						item.title = sourceItem.Title;
						item.file_count = sourceItem.Files.Count;
						item.item_last_updated = sourceItem.ItemLastUpdated.ToString("s");
					}

					Tools.CleanDynamic(item);

					items.Add(item);
				}
			}

			json.items = items;

			dynamic repos = new JArray();

			foreach (string key in Globals.GitHubRepos.Keys)
			{
				GitHubRepo sourceRepo = Globals.GitHubRepos[key];

				dynamic repo = new JObject();

				repo.key = key;
				repo.user_name = sourceRepo.UserName;
				repo.repo_name = sourceRepo.RepoName;
				repo.tag_name = sourceRepo.tag_name;
				repo.published_at = sourceRepo.published_at;
				repo.url_details = sourceRepo.UrlDetails;
				repo.url_api = sourceRepo.UrlApi;

				repos.Add(repo);
			}

			json.repos = repos;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_status(HttpListenerContext context, StreamWriter writer)
		{
			dynamic json = new JObject();

		
			lock (Globals.WorkerTaskInfo)
			{
				json.busy = Globals.WorkerTaskInfo.Command != "";
				json.command = Globals.WorkerTaskInfo.Command;

				json.bytesCurrent = Globals.WorkerTaskInfo.BytesCurrent;
				json.bytesTotal = Globals.WorkerTaskInfo.BytesTotal;
			}

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_source_files(HttpListenerContext context, StreamWriter writer)
		{
			string qs;

			string type = null;
			qs = context.Request.QueryString["type"];
			if (qs != null)
				type = qs;

			if (type == null)
				throw new ApplicationException("type not passed");

			ItemType setType = (ItemType)Enum.Parse(typeof(ItemType), type);

			ArchiveOrgItem[] sourceItems = Globals.ArchiveOrgItems[setType];
			
			JArray results = new JArray();

			foreach (ArchiveOrgItem sourceItem in sourceItems)
			{
				dynamic source = new JObject();
				
				source.tag = sourceItem.Tag;
				source.files = JArray.FromObject(sourceItem.Files.Values);

				results.Add(source);
			}

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = results.Count;
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_list(HttpListenerContext context, StreamWriter writer)
		{
			DataTable table = Mame.ListSavedState(Globals.Core);

			JArray results = new JArray();

			foreach (DataRow row in table.Rows)
			{
				dynamic result = RowToJson(row);
				results.Add(result);
			}

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = table.Rows.Count;
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_reports(HttpListenerContext context, StreamWriter writer)
		{
			JArray results = new JArray();

			foreach (string reportName in Globals.Reports.ListReports())
			{
				dynamic result = new JObject();

				result.name = reportName;

				int index = reportName.IndexOf("_");

				if (index != -1)
				{
					StringBuilder dateText = new StringBuilder(reportName.Substring(0, index));

					dateText[13] = ':';
					dateText[16] = ':';

					result.date = DateTime.Parse(dateText.ToString());
					result.description = reportName.Substring(index + 1);

					results.Add(result);
				}

			}

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = results.Count;
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_report(HttpListenerContext context, StreamWriter writer)
		{
			string name = context.Request.QueryString["name"] ?? throw new ApplicationException("name not passed");

			string html = Globals.Reports.GetHtml(name);

			context.Response.Headers["Content-Type"] = "text/html";

			writer.WriteLine(html);
		}

		public void _api_report_groups(HttpListenerContext context, StreamWriter writer)
		{
			JArray results = new JArray();

			foreach (Reports.ReportGroup reportGroup in Reports.ReportGroups)
			{
				dynamic group = new JObject();

				group.key = reportGroup.Key;
				group.text = reportGroup.Text;
				group.description = reportGroup.Decription;

				results.Add(group);
			}

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = results.Count;
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_report_types(HttpListenerContext context, StreamWriter writer)
		{
			string groupFilter = context.Request.QueryString["group"];

			JArray results = new JArray();

			foreach (Reports.ReportType reportType in Reports.ReportTypes)
			{
				if (groupFilter != null && groupFilter != reportType.Group)
					continue;

				dynamic type = new JObject();

				type.key = reportType.Key;
				type.group = reportType.Group;
				type.code = reportType.Code;
				type.text = reportType.Text;
				type.description = reportType.Decription;

				results.Add(type);
			}

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = results.Count;
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_what(HttpListenerContext context, StreamWriter writer)
		{
			context.Response.Headers["Content-Type"] = "text/plain; charset=utf-8";

			writer.Write(File.ReadAllText(Path.Combine(Globals.Core.Directory, "whatsnew.txt"), Encoding.UTF8));
		}

		public void _api_genre_groups(HttpListenerContext context, StreamWriter writer)
		{
			JArray results = new JArray();

			if (Globals.Genre.Data != null)
			{
				HashSet<string> keepColumnNames = new HashSet<string>(new string[] { "genre_id", "group_id" });

				foreach (DataRow row in Globals.Genre.Data.Tables["groups"].Rows)
				{
					dynamic result = RowToJson(row, keepColumnNames);
					results.Add(result);
				}
			}

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = results.Count;
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_genres(HttpListenerContext context, StreamWriter writer)
		{
			string qs;

			long group_id = 0;
			qs = context.Request.QueryString["group_id"];
			if (qs != null)
				group_id = Int64.Parse(qs);

			string group_name = null;
			qs = context.Request.QueryString["group_name"];
			if (qs != null)
				group_name = qs;

			string genre_name = null;
			qs = context.Request.QueryString["genre_name"];
			if (qs != null)
				genre_name = qs;

			long genre_id = 0;

			JArray results = new JArray();

			if (Globals.Genre.Data != null)
			{
				if (group_name != null)
				{
					DataRow[] rows = Globals.Genre.Data.Tables["groups"].Select($"group_name = '{group_name.Replace("'", "''")}'");

					if (rows.Length == 0)
						throw new ApplicationException($"group name not found: {group_name}");

					group_id = (long)rows[0]["group_id"];
				}

				if (genre_name != null)
				{
					DataRow[] rows = Globals.Genre.Data.Tables["genres"].Select($"genre_name = '{genre_name.Replace("'", "''")}'");

					if (rows.Length == 0)
						throw new ApplicationException($"genre name not found: {genre_name}");

					genre_id = (long)rows[0]["genre_id"];
				}

				HashSet<string> keepColumnNames = new HashSet<string>(new string[] { "genre_id", "group_id" });

				foreach (DataRow row in Globals.Genre.Data.Tables["genres"].Rows)
				{
					if (group_id != 0 && (long)row["group_id"] != group_id)
						continue;

					if (genre_id != 0 && (long)row["genre_id"] != genre_id)
						continue;

					dynamic result = RowToJson(row, keepColumnNames);
					results.Add(result);
				}
			}

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = results.Count;
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_settings(HttpListenerContext context, StreamWriter writer)
		{
			dynamic available_options = new JObject();
			foreach (string key in Globals.Settings.AvailableOptions.Keys)
				available_options[key] = new JArray(Globals.Settings.AvailableOptions[key].ToArray());

			dynamic option_descriptions = new JObject();
			foreach (string key in Globals.Settings.OptionDescriptions.Keys)
				option_descriptions[key] = Globals.Settings.OptionDescriptions[key];

			dynamic options = new JObject();
			foreach (string key in Globals.Settings.Options.Keys)
				options[key] = Globals.Settings.Options[key];

			dynamic json = new JObject();
			json.available_options = available_options;
			json.option_descriptions = option_descriptions;
			json.options = options;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_sql_query(HttpListenerContext context, StreamWriter writer)
		{
			string database = context.Request.QueryString["database"];
			bool save = Boolean.Parse(context.Request.QueryString["save"]);

			string commandText;
			using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
			{
				commandText = reader.ReadToEnd();
			}

			string connectionString = Globals.Core.ConnectionStrings[database == "machine" ? 0 : 1];

			DataTable table = Database.ExecuteFill(connectionString, commandText);

			if (save == true)
				Globals.Reports.SaveHtmlReport(table, "SQL Query", true);

			string[] columnNames = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
			HashSet<string> keepColumnNames = new HashSet<string>(columnNames);

			JArray results = new JArray(table.Rows.Cast<DataRow>().Select(row => RowToJson(row, keepColumnNames)));

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = results.Count;
			json.count = results.Count;
			json.results = results;

			json.column_names = new JArray(columnNames);

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		private dynamic RowToJson(DataRow row)
		{
			return RowToJson(row, null);
		}
		private dynamic RowToJson(DataRow row, HashSet<string> keepColumnNames)
		{
			dynamic json = new JObject();

			foreach (DataColumn column in row.Table.Columns)
			{
				if (keepColumnNames == null || keepColumnNames.Contains(column.ColumnName) == false)
				{
					if (column.ColumnName.EndsWith("_id") == true || column.ColumnName.EndsWith("_id1") == true)
						continue;
				}

				if (column.ColumnName == "ao_total")
					continue;

				if (row.IsNull(column.ColumnName) == true)
					continue;

				switch (column.DataType.Name)
				{
					case "String":
						json[column.ColumnName] = (string)row[column];
						break;

					case "Int64":
						json[column.ColumnName] = (long)row[column];
						break;

					case "Int32":
						json[column.ColumnName] = (int)row[column];
						break;

					case "DateTime":
						json[column.ColumnName] = ((DateTime)row[column]).ToString("s");
						break;

					case "Boolean":
						json[column.ColumnName] = (bool)row[column];
						break;

					default:
						throw new ApplicationException($"Unknown datatype {column.DataType.Name}");
				}
			}

			return json;
		}

		private readonly string _DefaultStyleSheet = @"
			body {
				font: small sans-serif;
				background: #c6eafb;
			}
			body.busy {
				background: #e6d4dc;
			}

			.header {
				background: #00adef;
				padding: 8px;
				box-sizing: border-box;
				display: flex;
				align-items: center;
				gap: 8px;
				border-radius: 24px 24px 0 0;
			}
			.header h1 { margin: 0; }

			td.good			{ background: #cfc; }
			td.imperfect	{ background: #ffc; }
			td.preliminary	{ background: #ffd9b3; }
			td.bad			{ background: #fcc; }

			td.yes			{ background: #cfc; }
			td.partial		{ background: #ffc; }
			td.no			{ background: #ffd9b3; }

			hr {
				background: #00adef;
				height: 6px;
				border: 0;
			}
			hr.px2 { height: 2px; }

			table { border-collapse: collapse; }
			th, td {
				padding: 2px;
				text-align: left;
				border: 1px solid #000;
			}
			th {
				background: #555;
				color: #fff;
			}
			tr { background: #ddd; }
			tr:nth-child(even) { background: #eee; }

			table.nav {
				width: 100%;
				border-collapse: separate;
				border-spacing: 2px;
				background: transparent;
				border: 0;
			}
			td.nav-off, td.nav-on {
				text-align: center;
				border: 0;
			}
			td.nav-off { background: #1a75bc; }
			td.nav-on  { background: #00adef; }

			a.nav-off, a.nav-on {
				text-decoration: none;
			}
			a.nav-off { color: #fff; }
			a.nav-on  { color: #ff0; }

			.card-grid {
				display: grid;
				grid-template-columns: repeat(auto-fit, minmax(192px, 1fr));
				gap: 8px;
			}

			.card,
			.card-good,
			.card-imperfect,
			.card-preliminary,
			.card-bad {
				width: 100%;
			}

			.card				{ background: #f2f2f2; }
			.card-good			{ background: #cfc; }
			.card-imperfect		{ background: #ffc; }
			.card-preliminary	{ background: #ffd9b3; }
			.card-bad			{ background: #fcc; }

			.card-yes			{ background: #cfc; }
			.card-partial		{ background: #ffc; }
			.card-no			{ background: #ffd9b3; }

			.card-thumb {
				width: 128px;
				height: 128px;
				margin: 0 auto;
				display: flex;
				align-items: center;
				justify-content: center;
				background: #262626;
				color: #fff;
				overflow: hidden;
			}
			.card-thumb img {
				width: auto;
				height: auto;
				max-width: 128px;
				max-height: 128px;
				display: block;
			}

			.card-link {
				display: block;
				text-decoration: none;
				color: inherit;
			}

			.card-body {
				display: flex;
				flex-direction: column;
				gap: 4px;
				align-items: center;
				text-align: center;
			}

			.card-name {
				font-weight: 600;
				font-size: 1.2em;
				display: flex;
				align-items: center;
				gap: 8px;
			}
			.card-year {
				font-weight: 600;
			}

			.toolbar {
				display: flex;
				flex-wrap: wrap;
				gap: 8px;
			}
			.checkbox-group {
				border: 1px solid #00adef;
				padding: 8px;
				display: flex;
				gap: 8px;
			}
			.toolbar-input {
				display: flex;
				gap: 8px;
				padding: 8px;
			}
			.toolbar-input input {
				flex: 1;
				padding: 4px;
				box-sizing: border-box;
			}

			.fav-checkbox {
				position: absolute;
				opacity: 0;
				width: 0;
				height: 0;
			}
			.star {
				display: inline-block;
				width: 18px;
				height: 18px;
				background: url('/images/fav-off.svg') no-repeat center/contain;
				cursor: pointer;
			}
			.fav-checkbox:checked + .star {
				background: url('/images/fav-on.svg') no-repeat center/contain;
			}

			.arrow {
				width: 48px;
				height: 24px;
			}
		";

		private readonly byte[] _FavIcon = Convert.FromBase64String(@"
			AAABAAEAEBAAAAAAGABoAwAAFgAAACgAAAAQAAAAIAAAAAEAGAAAAAAAAAMAAAAAAAAAAAAAAAAA
			AAAAAAD0tgDzuQDzsgD2xgD99NT++OP++OX++OX/+OPA67QA6t3j6KL/9tr++OP9+OX9+OX0vQD0
			vgD99dj///T/75P/6m7/6mv/6Wz/4ne+3G4A7Obg2EL/3F7/3Vv/32v84nnysAD99+P/9MThrQCV
			aACCXQCCXQCgcgDyoQC9vwAA8PesvwCDyQB/ygDQswD/rQD0uwD//e/vsgBEMgAJDiUdGh8bGh8H
			DCZzTADEwwAA8/8A8/8A8/8A8/8A8fjBwwD+/PX/1gC+hgAUFiLCjQDvrQDysACgdgAsGgyxtQAA
			+P873pbetQDbtQAN5LcA79X//vv2uwDkogDQlwDoqADdoADlpwCRawAtGwuwtgAA9v7AvAD/qgD/
			qQCpwgAA+f/+/PXztQD9tQCqfQAgHBwUFiIWFiIFCid8UgDAwwAA8PfXtgD3rQD7rAC+vQAA9//+
			/PX4ugDYmwAbGR9cRgCZcQCRagCtfwD/swC9wQAA8PvUtwD5rQD8rAC9vQAA+P///fn+wgC2gwAX
			FyHqqgD/xAD/xADcnwB8UwCytwAA9/+MywD/qAD/qAB10ToA9////fX7zwDYmAAeGx5vVACgdgCi
			dwBRPgA2IQG5vAAA9v8A8f9z0URv0kkA9v9p2Vj76Jv977v7sgCQaQASEyITFCISEyIdGh+6fwDH
			xQAA7uwg4a4A8/8A9P9U12/7swDzuQD//fn1wAD2rgDbngDUmQDTmQDhowD6swDqsQDSuADyrwDX
			tgDVswD5sgD/7KDxrgD977/98MbzsAD3sAD4swD4swD2sgDyrwD0rgD5rQD0rwD3qQD5swD+8MD/
			/vPxrADysAD+/fX75Y7ysgDxqwDyrgDyrwDyrwDyrwDyrgDxqgDztQD977n99+D0swDyrwDxqwDz
			sgD//fn98sz0vwDyrgDxqwDxqgDxqwDyrwD1xQD9+OL+/PXysgD1rADyrwDyrwDxrQDztQD889D/
			/fn989P75pT53mj76J399dv//fn87rjzswDyrADxrAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
			AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
		");

	}
}
