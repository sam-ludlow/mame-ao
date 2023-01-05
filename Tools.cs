using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Spludlow.MameAO
{
	public class Tools
	{
		private static SHA1Managed _SHA1Managed = new SHA1Managed();

		public static string SHA1HexFile(string filename)
		{
			using (FileStream stream = File.OpenRead(filename))
			{
				byte[] hash = _SHA1Managed.ComputeHash(stream);
				StringBuilder hex = new StringBuilder();
				foreach (byte b in hash)
					hex.Append(b.ToString("x2"));
				return hex.ToString();
			}
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

		public static string Query(HttpClient client, string url)
		{
			using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{url}"))
			{
				Task<HttpResponseMessage> requestTask = client.SendAsync(requestMessage);
				requestTask.Wait();
				HttpResponseMessage responseMessage = requestTask.Result;

				responseMessage.EnsureSuccessStatusCode();

				Task<string> responseMessageTask = responseMessage.Content.ReadAsStringAsync();
				responseMessageTask.Wait();
				string responseBody = responseMessageTask.Result;

				return responseBody;
			}
		}
		public static byte[] Download(HttpClient client, string url)
		{
			using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{url}"))
			{
				Task<HttpResponseMessage> requestTask = client.SendAsync(requestMessage);
				requestTask.Wait();
				HttpResponseMessage responseMessage = requestTask.Result;

				responseMessage.EnsureSuccessStatusCode();

				Task<byte[]> responseMessageTask = responseMessage.Content.ReadAsByteArrayAsync();
				responseMessageTask.Wait();

				return responseMessageTask.Result;
			}
		}
	}

	public class TempDirectory : IDisposable
	{
		private string _LockFilePath;
		public string _Path;

		public TempDirectory()
		{
			//			LockFilePath = @"\\?\" + System.IO.Path.GetTempFileName(); //	Long filename support

			_LockFilePath = Path.GetTempFileName();
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

		public override string ToString()
		{
			return _Path;
		}
	}

}
