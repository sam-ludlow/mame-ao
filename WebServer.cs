using System;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using System.Data.SQLite;

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
				_AO.RunLine($"{machine} {software} {arguments}");
			});
			
			_RunTask.Start();
			
			writer.WriteLine("OK");
		}

		private void ApiProfiles(HttpListenerContext context, StreamWriter writer)
		{
			StringBuilder json = new StringBuilder();

			json.AppendLine("{ \"results\": [");

			for (int index = 0; index < Database.DataQueryProfiles.Length; ++index)
			{
				string name = Database.DataQueryProfiles[index][0];
				string command = Database.DataQueryProfiles[index][1];

				json.Append($"{{ \"name\": \"{name}\", \"command\": \"{command}\" }}");

				json.AppendLine(index == (Database.DataQueryProfiles.Length - 1) ? "" : ",");
			}

			json.AppendLine("] }");

			context.Response.Headers["Content-Type"] = "application/json";

			writer.WriteLine(json.ToString());

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
			string commandText = Database.DataQueryProfiles[profileIndex][1];

			commandText = commandText.Replace("@LIMIT", limit.ToString());
			commandText = commandText.Replace("@OFFSET", offset.ToString());

			DataSet dataSet = new DataSet();

			using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(commandText, _AO._Database._MachineConnection))
			{
				adapter.Fill(dataSet);
			}

			StringBuilder json = new StringBuilder();

			json.AppendLine($"{{ \"profile\": \"{profileName}\", \"results\": [");

			for (int index = 0; index < dataSet.Tables[0].Rows.Count; ++index)
			{
				DataRow row = dataSet.Tables[0].Rows[index];

				bool last = index == (dataSet.Tables[0].Rows.Count - 1);


				string name = (string)row["name"];
				string description = (string)row["description"];
				string year = row.IsNull("year") ? "" : (string)row["year"];
				string manufacturer = row.IsNull("manufacturer") ? "" : (string)row["manufacturer"];

				description = description.Replace("\"", "\\\"");
				manufacturer = manufacturer.Replace("\"", "\\\"");

				string image = $"https://mame.spludlow.co.uk/snap/machine/{name}.jpg";

				json.Append($"{{ \"name\": \"{name}\", \"description\": \"{description}\", \"year\": \"{year}\", \"manufacturer\": \"{manufacturer}\", \"image\": \"{image}\" }}");

				json.AppendLine(last == true ? "" : ",");

			}

			json.AppendLine("] }");

			context.Response.Headers["Content-Type"] = "application/json";

			writer.WriteLine(json.ToString());

		}



	}
}
