using System;
using System.Net.Http;
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

		public void Success()
		{
			Submit();
		}

		public void Error(Exception exception)
		{
			Exception = exception;
			Submit();
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

		private void Submit()
		{
			if (Globals.Settings.Options["PhoneHome"] == "No" || _MameExitCode == null)
				return;

			EndTime = DateTime.Now;

			bool verbose = Globals.Settings.Options["PhoneHome"] == "YesVerbose";

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

					using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://mame.spludlow.co.uk/mame-ao-phone-home.aspx"))
					{
						requestMessage.Content = new StringContent(body);

						Task<HttpResponseMessage> requestTask = Globals.HttpClient.SendAsync(requestMessage);
						requestTask.Wait();
						HttpResponseMessage responseMessage = requestTask.Result;

						responseMessage.EnsureSuccessStatusCode();
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
	}
}
