using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spludlow.MameAO
{
	public class WebServer
	{

		private string _UIHTML;

		private readonly string MACHINE_IMAGE_URL = "https://mame.spludlow.co.uk/snap/machine/@machine.jpg";
		//private readonly string MACHINE_IMAGE_URL = "https://raw.githubusercontent.com/AntoPISA/MAME_SnapTitles/main/snap/@machine.png";

		private readonly string SOFTWARE_IMAGE_URL = "https://mame.spludlow.co.uk/snap/software/@softwarelist/@software.jpg";

		private byte[] _FavIcon = Convert.FromBase64String(@"
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

		public WebServer()
		{
			RefreshAssets();
		}

		public void RefreshAssets()
		{
			_UIHTML = File.ReadAllText(@"UI.html", Encoding.UTF8);
		}

		public void StartListener()
		{
			if (HttpListener.IsSupported == false)
			{
				Console.WriteLine("!!! Http Listener Is not Supported");
				return;
			}

			HttpListener listener = new HttpListener();
			listener.Prefixes.Add(Globals.ListenAddress);
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

		public void _api_command(HttpListenerContext context, StreamWriter writer)
		{
			string line = context.Request.QueryString["line"];

			if (line == null)
				throw new ApplicationException("No line given.");

			// Silent commands

			if (line.StartsWith(".fav") == true)
			{
				Globals.Favorites.AddCommandLine(line);
			}
			else
			{
				Console.WriteLine();
				Tools.ConsoleHeading(1, new string[] {
					"Remote command recieved",
					line,
				});
				Console.WriteLine();

				bool started = Globals.AO.RunLineTask(line);

				if (started == false)
					throw new ApplicationException("I'm busy.");
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

			foreach (Database.DataQueryProfile profile in Database.DataQueryProfiles)
			{
				dynamic result = new JObject();

				result.key = profile.Key;
				result.text = profile.Text;
				result.description = profile.Decription;
				result.command = profile.CommandText;

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

			string profile = context.Request.QueryString["profile"];
			if (profile == null)
				throw new ApplicationException("profile not passed");

			Database.DataQueryProfile dataQueryProfile = Globals.Database.GetDataQueryProfile(profile);

			DataTable table = Globals.Database.QueryMachine(dataQueryProfile.Key, offset, limit, search);

			JArray results = new JArray();

			foreach (DataRow row in table.Rows)
			{
				dynamic result = RowToJson(row);

				string name = (string)row["name"];

				result.ao_image = MACHINE_IMAGE_URL.Replace("@machine", name);

				results.Add(result);
			}

			dynamic json = new JObject();
			json.profile = dataQueryProfile.Key;
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

			string machine = null;
			qs = context.Request.QueryString["name"];
			if (qs != null)
				machine = qs;

			if (machine == null)
				throw new ApplicationException("machine not passed");

			DataRow machineRow = Globals.Database.GetMachine(machine);

			DataRow[] machineSoftwareListRows = Globals.Database.GetMachineSoftwareLists(machineRow);

			dynamic json = RowToJson(machineRow);

			json.ao_image = MACHINE_IMAGE_URL.Replace("@machine", machine);

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

			string favorites_machine = context.Request.QueryString["favorites_machine"];
			if (favorites_machine != null)
				favorites_machine = favorites_machine.Trim();

			DataRow[] rows = Globals.Database.GetSoftwareListsSoftware(softwarelist, offset, limit, search, favorites_machine);

			JArray results = new JArray();

			foreach (DataRow row in rows)
			{
				dynamic result = RowToJson(row);

				string name = (string)row["name"];

				result.ao_image = SOFTWARE_IMAGE_URL
					.Replace("@softwarelist", softwarelist == "@fav" ? (string)row["softwarelist_name"] : softwarelist)
					.Replace("@software", name);

				results.Add(result);
			}

			dynamic json = new JObject();
			json.softwarelist = softwarelist;
			json.offset = offset;
			json.limit = limit;
			json.total = rows.Length == 0 ? 0 : (long)rows[0]["ao_total"];
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_info(HttpListenerContext context, StreamWriter writer)
		{
			GitHubRepo repo = Globals.GitHubRepos["sam-ludlow/mame-ao"];

			dynamic json = new JObject();

			json.time = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
			json.version = Globals.AssemblyVersion;
			json.mame_version = Globals.MameVersion;
			json.directory = Globals.RootDirectory;
			json.rom_store_count = Globals.RomHashStore.Length;
			json.disk_store_count = Globals.DiskHashStore.Length;
			json.genre_version = Globals.Genre.Data != null ? Globals.Genre.Version : "";
			json.linking_enabled = Globals.LinkingEnabled;

			json.latest = repo.tag_name;

			json.version_name_available = $"mame-ao-{repo.tag_name}";
			json.version_name_current = $"mame-ao-{Globals.AssemblyVersion}";

			dynamic items = new JArray();

			foreach (ItemType itemType in Globals.ArchiveOrgItems.Keys)
			{
				foreach (ArchiveOrgItem sourceItem in Globals.ArchiveOrgItems[itemType])
				{
					dynamic item = new JObject();

					item.key = sourceItem.Key;
					item.type = itemType.ToString();

					item.status = sourceItem.Status;
					item.version = sourceItem.Version;

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

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_status(HttpListenerContext context, StreamWriter writer)
		{
			dynamic json = new JObject();

		
			lock (Globals.AO._TaskInfo)
			{
				json.busy = Globals.AO._TaskInfo.Command != "";
				json.command = Globals.AO._TaskInfo.Command;

				json.bytesCurrent = Globals.AO._TaskInfo.BytesCurrent;
				json.bytesTotal = Globals.AO._TaskInfo.BytesTotal;
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
				source.version = sourceItem.Version;
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
			DataTable table = Mame.ListSavedState(Globals.RootDirectory, Globals.Database);

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

			writer.Write(Mame.WhatsNew(Globals.RootDirectory));
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


	}
}
