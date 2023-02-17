using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Spludlow.MameAO
{
	public class MameChdMan
	{
		private readonly string _ChdManPath;

		public MameChdMan(string mameBinPath)
		{
			_ChdManPath = Path.Combine(mameBinPath, "chdman.exe");

			if (File.Exists(_ChdManPath) == false)
				throw new ApplicationException($"CHD man, Program not found: '{_ChdManPath}'");
		}

		public string Hash(string filename)
		{
			Dictionary<string, string> info = Info(filename);

			string sha1 = "";
			if (info.ContainsKey("SHA1") == true)
				sha1 = info["SHA1"];

			if (sha1.Length != 40)
				throw new ApplicationException($"MameChdMan, hash not found in output: {filename}");

			return sha1;
		}

		public bool Verify(string filename)
		{
			string output = VerifyRaw(filename);
			bool result = VerifyOutput(output);

			return result;
		}
		public string VerifyRaw(string filename)
		{
			return Run("verify -i \"" + filename + "\"");
		}
		public bool VerifyOutput(string output)
		{
			if (output.Contains("Raw SHA1 verification successful!") == true &&
				output.Contains("Overall SHA1 verification successful!") == true)
			{
				return true;
			}
			return false;
		}

		public Dictionary<string, string> Info(string filename)
		{
			return ParseResult(Run("info -i \"" + filename + "\""));
		}

		public static Dictionary<string, string> ParseResult(string text)
		{
			Dictionary<string, string> result = new Dictionary<string, string>();

			using (StringReader reader = new StringReader(text))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					int index = line.IndexOf(":");

					if (index == -1)
						continue;

					string key = line.Substring(0, index).Trim();
					string value = line.Substring(index + 1).Trim();

					if (result.ContainsKey(key) == true)
					{
						int count = 2;
						while (result.ContainsKey(key + count) == true)
						{
							++count;
						}
						key += count;
					}

					result.Add(key, value);
				}
			}

			return result;
		}

		public string Run(string arguments)
		{
			StringBuilder output = new StringBuilder();
			StringBuilder errorOutput = new StringBuilder();

			ProcessStartInfo startInfo = new ProcessStartInfo(_ChdManPath)
			{
				Arguments = arguments,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
			};

			using (Process process = new Process())
			{
				process.StartInfo = startInfo;

				process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
				{
					if (e.Data != null)
						output.AppendLine(e.Data);
				});

				process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
				{
					if (e.Data != null)
						errorOutput.AppendLine(e.Data);
				});

				process.Start();
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();
				process.WaitForExit();

				if (process.ExitCode != 0)
					throw new ApplicationException($"CHD man Bad exit code: {process.ExitCode} output:{output} error:{errorOutput}");
			}

			return output.ToString();
		}

	}
}
