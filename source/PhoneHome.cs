using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace Spludlow.MameAO
{
	public class PhoneHome
	{
		private DateTime StartTime;
		private DateTime? ReadyTime;
		private DateTime EndTime;

		private string Line;
		private Exception Exception;

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

		private void Submit()
		{
			EndTime = DateTime.Now;

			Task task = new Task(() => {
				try
				{
					dynamic json = new JObject();

					json.start_time = StartTime;

					if (ReadyTime != null)
						json.ready_time = ReadyTime;

					json.end_time = EndTime;

					json.line = Line;

					if (Exception != null)
						json.exception = Exception.ToString();


					using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://mame.spludlow.co.uk/mame-ao-phone-home.aspx"))
					{
						requestMessage.Content = new StringContent(json.ToString(Formatting.Indented));

						Task<HttpResponseMessage> requestTask = Globals.HttpClient.SendAsync(requestMessage);
						requestTask.Wait();
						HttpResponseMessage responseMessage = requestTask.Result;

						responseMessage.EnsureSuccessStatusCode();
					}

				}
				catch (Exception e)
				{
					Console.WriteLine($"!!! Phone Home Error: {e.Message}");
					Console.WriteLine(e.ToString());
				}
			});

			task.Start();
		}
	}
}
