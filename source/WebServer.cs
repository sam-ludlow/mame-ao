using System;
using System.Collections.Generic;
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
										Command(context, writer);
										break;

									case "/api/profiles":
										ApiProfiles(context, writer);
										break;

									case "/api/machine":
										ApiMachine(context, writer);
										break;

									case "/api/machines":
										ApiMachines(context, writer);
										break;

									case "/api/software":
										ApiSoftware(context, writer);
										break;

									case "/api/info":
										ApiInfo(context, writer);
										break;

									case "/api/source_files":
										ApiListSourceFiles(context, writer);
										break;

									case "/api/list":
										ApiList(context, writer);
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
			Tools.ConsoleHeading(1, new string[] {
				"Remote command recieved",
				$"machine: {machine}",
				$"software: {software}",
				$"arguments: {arguments}",
			});
			Console.WriteLine();

			bool started = _AO.RunLineTask($"{machine} {software} {arguments}");

			writer.WriteLine(started == true ? "OK" : "BUSY");

			if (started == false)
				throw new ApplicationException("I'm busy.");
		}

		private void ApiProfiles(HttpListenerContext context, StreamWriter writer)
		{
			JArray results = new JArray();

			for (int index = 0; index < Database.DataQueryProfiles.Length; ++index)
			{
				dynamic result = new JObject();
				result.index = index;
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

			string search = "";
			qs = context.Request.QueryString["search"];
			if (qs != null)
				search = qs.Trim();
			if (search.Length == 0)
				search = null;

			int profileIndex = 0;
			qs = context.Request.QueryString["profile"];
			if (qs != null)
				profileIndex = Int32.Parse(qs);

			if (profileIndex < 0 || profileIndex >= Database.DataQueryProfiles.Length)
				throw new ApplicationException("Bad profile index");

			string profileName = Database.DataQueryProfiles[profileIndex][0];

			DataTable table = _AO._Database.QueryMachine(profileIndex, offset, limit, search);

			JArray results = new JArray();

			foreach (DataRow row in table.Rows)
			{
				dynamic result = RowToJson(row);

				string name = (string)row["name"];

				result.ao_image = $"https://mame.spludlow.co.uk/snap/machine/{name}.jpg";

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


		private void ApiMachine(HttpListenerContext context, StreamWriter writer)
		{
			string qs;

			string machine = null;
			qs = context.Request.QueryString["machine"];
			if (qs != null)
				machine = qs;

			if (machine == null)
				throw new ApplicationException("machine not passed");

			DataRow machineRow = _AO._Database.GetMachine(machine);

			DataRow[] machineSoftwareListRows = _AO._Database.GetMachineSoftwareLists(machineRow);

			dynamic json = RowToJson(machineRow);

			json.ao_image = $"https://mame.spludlow.co.uk/snap/machine/{machine}.jpg";

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

			context.Response.Headers["Content-Type"] = "application/json";
			writer.WriteLine(json.ToString(Formatting.Indented));
		}


		private void ApiSoftware(HttpListenerContext context, StreamWriter writer)
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

			int limit = 100;
			qs = context.Request.QueryString["limit"];
			if (qs != null)
				limit = Int32.Parse(qs);

			if (limit > 1000)
				throw new ApplicationException("Limit is limited to 1000");

			string search = "";
			qs = context.Request.QueryString["search"];
			if (qs != null)
				search = qs.Trim();
			if (search.Length == 0)
				search = null;

			DataRow[] rows = _AO._Database.GetSoftwareListsSoftware(softwarelist, offset, limit, search);

			JArray results = new JArray();

			foreach (DataRow row in rows)
			{
				dynamic result = RowToJson(row);

				string name = (string)row["name"];

				result.ao_image = $"https://mame.spludlow.co.uk/snap/software/{softwarelist}/{name}.jpg";

				results.Add(result);
			}

			dynamic json = new JObject();
			json.softwarelist = softwarelist;
			json.offset = offset;
			json.limit = limit;
			json.total = rows.Length == 0 ? 0 : (long)rows[0]["ao_total"];
			json.count = results.Count;
			json.results = results;

			context.Response.Headers["Content-Type"] = "application/json";
			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		private void ApiInfo(HttpListenerContext context, StreamWriter writer)
		{
			dynamic json = new JObject();

			json.time = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
			json.version = _AO._AssemblyVersion;
			json.mame_version = _AO._Version;
			json.directory = _AO._RootDirectory;
			json.rom_store_count = _AO._RomHashStore.Length;
			json.disk_store_count = _AO._DiskHashStore.Length;

			dynamic sources = new JArray();

			foreach (Sources.MameSourceSet sourceSet in Sources.GetSourceSets())
			{
				dynamic source = new JObject();

				source.type = sourceSet.SetType.ToString();
				source.version = sourceSet.Version;
				source.download = sourceSet.DownloadUrl;
				source.metadata = sourceSet.MetadataUrl;

				sources.Add(source);
			}

			json.sources = sources;

			context.Response.Headers["Content-Type"] = "application/json";
			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		private void ApiListSourceFiles(HttpListenerContext context, StreamWriter writer)
		{
			string qs;

			string type = null;
			qs = context.Request.QueryString["type"];
			if (qs != null)
				type = qs;

			if (type == null)
				throw new ApplicationException("type not passed");

			Sources.MameSetType setType = (Sources.MameSetType)Enum.Parse(typeof(Sources.MameSetType), type);

			Sources.MameSourceSet sourceSet = Sources.GetSourceSets(setType)[0];

			JArray files = JArray.FromObject(sourceSet.AvailableDownloadFileInfos.Values);

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = files.Count;
			json.count = files.Count;
			json.results = files;

			context.Response.Headers["Content-Type"] = "application/json";
			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		private void ApiList(HttpListenerContext context, StreamWriter writer)
		{
			DataTable table = Mame.ListSavedState(_AO._RootDirectory, _AO._Database);

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

			context.Response.Headers["Content-Type"] = "application/json";
			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		private dynamic RowToJson(DataRow row)
		{
			dynamic json = new JObject();

			foreach (DataColumn column in row.Table.Columns)
			{
				if (column.ColumnName.EndsWith("_id") == true || column.ColumnName.EndsWith("_id1") == true)
					continue;

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

					case "DateTime":
						json[column.ColumnName] = ((DateTime)row[column]).ToString("s");
						break;

					default:
						throw new ApplicationException($"Unknown datatype {column.DataType.Name}");
				}
			}

			return json;
		}


	}
}
