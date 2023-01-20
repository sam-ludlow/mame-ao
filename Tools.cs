using System;
using System.Collections.Generic;
using System.Diagnostics;
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

		public static void LinkFiles(string[][] linkTargetFilenames)
		{
			HashSet<string> linkDirectories = new HashSet<string>();

			StringBuilder batch = new StringBuilder();

			for (int index = 0; index < linkTargetFilenames.Length; ++index)
			{
				string link = linkTargetFilenames[index][0];
				string target = linkTargetFilenames[index][1];

				string linkDirectory = Path.GetDirectoryName(link);
				linkDirectories.Add(linkDirectory);

				//	Escape characters, may be more
				link = link.Replace("%", "%%");

				batch.Append("mklink ");
				batch.Append('\"');
				batch.Append(link);
				batch.Append("\" \"");
				batch.Append(target);
				batch.Append('\"');
				batch.AppendLine();
			}

			foreach (string linkDirectory in linkDirectories)
			{
				if (Directory.Exists(linkDirectory) == false)
					Directory.CreateDirectory(linkDirectory);
			}

			using (TempDirectory tempDir = new TempDirectory())
			{
				string batchFilename = tempDir.Path + @"\link.bat";
				File.WriteAllText(batchFilename, batch.ToString(), new UTF8Encoding(false));

				string input = "chcp 65001" + Environment.NewLine + batchFilename + Environment.NewLine;

				ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe");
				startInfo.UseShellExecute = false;
				startInfo.CreateNoWindow = true;
				startInfo.RedirectStandardInput = true;

				using (Process process = new Process())
				{
					process.StartInfo = startInfo;

					process.Start();

					process.StandardInput.WriteLine(input);
					process.StandardInput.Close();

					process.WaitForExit();
				}
			}
		}
	}

	public class TempDirectory : IDisposable
	{
		private string _LockFilePath;
		private string _Path;

		public TempDirectory()
		{
			//			LockFilePath = @"\\?\" + System.IO.Path.GetTempFileName(); //	Long filename support

			_LockFilePath = System.IO.Path.GetTempFileName();
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

		public string Path
		{
			get
			{
				return _Path;
			}
		}

		public override string ToString()
		{
			return _Path;
		}
	}

}
