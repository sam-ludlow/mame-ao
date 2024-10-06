﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.IO.Compression;
using System.Net;

namespace Spludlow.MameAO
{
	public class Upload
	{
		public static readonly DateTime MagicModifiedDate = new DateTime(1996, 12, 24, 23, 32, 0, DateTimeKind.Utc);

		private static string ApiAuth;

		private static string ItemName = "spludlow-test-0002";
		static Upload()
		{
			string authFilename = Path.Combine(Globals.RootDirectory, "_api-auth.txt");

			if (File.Exists(authFilename) == false)
				throw new ApplicationException($"API Auth file missing:'{authFilename}'. Format: 'LOW <Your_S3_access_key>:<Your_S3_secret_key>'");

			ApiAuth = File.ReadAllText(authFilename);
		}

		public static void Machine()
		{
			ArchiveOrgItem item = new ArchiveOrgItem(ItemName, null, null);
			item.DontCache = true;
			item.GetFile(null);

			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name, cloneof, description FROM machine ORDER BY machine.name");
			DataTable romTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");

			machineTable.PrimaryKey = new DataColumn[] { machineTable.Columns["name"] };

			DataTable report = Tools.MakeDataTable("Parent Machine ZIP Files",
				"status	name	description	sha1",
				"String	String	String		String");

			Dictionary<string, string> fileManifestSHA1s = new Dictionary<string, string>();

			ArchiveOrgFile manifestFile = item.GetFile("_manifest-sha1");
			if (manifestFile != null)
			{
				using (StringReader reader = new StringReader(Tools.Query(item.DownloadLink(manifestFile))))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						string[] parts = line.Split('\t');
						fileManifestSHA1s.Add(parts[0], parts[1]);
					}
				}
			}

			List<string> parentNames = new List<string>();

			foreach (DataRow parentMachineRow in machineTable.Select("cloneof IS NULL"))
			{
				Dictionary<string, string> nameHashes = GetRomNameHashes(parentMachineRow, machineTable, romTable);

				if (nameHashes.Count == 0)
					continue;

				string name = (string)parentMachineRow["name"];
				string description = (string)parentMachineRow["description"];

				parentNames.Add(name);

				string manifest = CreateRomManifest(nameHashes);
				string manifestSha1 = Tools.SHA1HexText(manifest, Encoding.ASCII);

				DataRow reportRow = report.Rows.Add("", name, description, manifestSha1);

				bool noHave = false;
				foreach (string key in nameHashes.Keys)
				{
					if (Globals.RomHashStore.Exists(nameHashes[key]) == false)
					{
						noHave = true;
						break;
					}
				}

				if (noHave == true)
				{
					reportRow["Status"] = "NOHAVE";
					continue;
				}

				if (item.Files.ContainsKey(name) == false)
				{
					reportRow["Status"] = "CREATE";
					continue;
				}

				ArchiveOrgFile file = item.Files[name];

				if (fileManifestSHA1s.ContainsKey(file.sha1) == false)
				{
					reportRow["Status"] = "MISSING";
					continue;
				}

				string remoteManifestSha1 = fileManifestSHA1s[file.sha1];

				if (manifestSha1 != remoteManifestSha1)
				{
					reportRow["Status"] = "UPDATE";
					continue;
				}

				reportRow["Status"] = "OK";
			}

			foreach (string name in item.Files.Keys)
			{
				if (name != "_manifest-sha1" && parentNames.Contains(name) == false)
					report.Rows.Add("DELETE", name);
			}

			foreach (string action in new string[] { "CREATE", "UPDATE" })
			{
				foreach (DataRow reportRow in report.Select($"Status = '{action}'"))
				{
					string machneName = (string)reportRow["name"];
					DataRow machineRow = machineTable.Rows.Find(machneName);

					Dictionary<string, string> nameHashes = GetRomNameHashes(machineRow, machineTable, romTable);

					using (TempDirectory tempDir = new TempDirectory())
					{
						string archiveFilename = Path.Combine(tempDir.Path, machneName + ".zip");
						string tempDirectory = Path.Combine(tempDir.Path, machneName);

						foreach (string name in nameHashes.Keys)
						{
							List<string> pathParts = new List<string>(name.Split('/'));
							pathParts.Insert(0, tempDirectory);
							string tempFilename = Path.Combine(pathParts.ToArray());

							Directory.CreateDirectory(Path.GetDirectoryName(tempFilename));

							File.Copy(Globals.RomHashStore.Filename(nameHashes[name]), tempFilename);
						}

						foreach (string tempFilename in Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories))
							File.SetLastWriteTimeUtc(tempFilename, MagicModifiedDate);

						ZipFile.CreateFromDirectory(tempDirectory, archiveFilename);

						string fileSha1 = Tools.SHA1HexFile(archiveFilename);

						string manifest = CreateRomManifest(nameHashes);
						string manifestSha1 = Tools.SHA1HexText(manifest, Encoding.ASCII);

						UploadFile(ItemName, archiveFilename);

						if (fileManifestSHA1s.ContainsKey(fileSha1) == false)
							fileManifestSHA1s.Add(fileSha1, manifestSha1);
						else
							if (fileManifestSHA1s[fileSha1] != manifestSha1)
								throw new ApplicationException($"Manifest SHA1 Missmatch fileSha1:{fileSha1}.");
					}
				}
			}

			foreach (string action in new string[] { "DELETE", "MISSING" })
			{
				foreach (DataRow reportRow in report.Select($"Status = '{action}'"))
				{
					string machneName = (string)reportRow["name"];

					DeleteFile(ItemName, $"{machneName}.zip");
				}
			}

			using (TempDirectory tempDir = new TempDirectory())
			{
				string filename = Path.Combine(tempDir.Path, "_manifest-sha1.txt");

				using (StreamWriter writer = new StreamWriter(filename, false))
					foreach (string key in fileManifestSHA1s.Keys)
						writer.WriteLine($"{key}\t{fileManifestSHA1s[key]}");

				UploadFile(ItemName, filename);
			}



			string[] headings = new string[] { "CREATE", "UPDATE", "DELETE", "MISSING", "OK", "NOHAVE" };

			List<DataView> views = new List<DataView>();
			foreach (string heading in headings)
				views.Add(new DataView(report, $"Status = '{heading}'", null, DataViewRowState.CurrentRows));

			Globals.Reports.SaveHtmlReport(views.ToArray(), headings, report.TableName);
		}


		public static void MachineCreate()
		{
			//string man = InspectRomManifest(@"C:\Users\Sam\Downloads\cpc464.zip");
			//string hash = Tools.SHA1HexText(man, Encoding.ASCII);
			//throw new ApplicationException(hash);


			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name, cloneof, description FROM machine ORDER BY machine.name");
			DataTable romTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");

			DataTable report = Tools.MakeDataTable("Machine manifests",
				"name	sha1",
				"String	String");

			string targetDirectory = @"D:\TMP";

			string hashLookupFilename = Path.Combine(targetDirectory, "_manifest-sha1.txt");

			Dictionary<string, string> fileManifestSHA1s = new Dictionary<string, string>();

			using (TempDirectory tempDir = new TempDirectory())
			{
				foreach (DataRow parentMachineRow in machineTable.Select("cloneof IS NULL"))
				{
					string parent_machine_name = (string)parentMachineRow["name"];
					Console.WriteLine(parent_machine_name);

					// test
					//if (parent_machine_name.StartsWith("b") == true)
					//	break;

					Dictionary<string, string> nameHashes = GetRomNameHashes(parentMachineRow, machineTable, romTable);

					if (nameHashes.Count == 0)
						continue;

					bool missing = false;
					foreach (string name in nameHashes.Keys)
					{
						if (Globals.RomHashStore.Exists(nameHashes[name]) == false)
						{
							missing = true;
							break;
						}
					}
					if (missing == true)
						continue;

					string manifest = CreateRomManifest(nameHashes);
					string manifestSha1 = Tools.SHA1HexText(manifest, Encoding.ASCII);

					report.Rows.Add(parent_machine_name, manifestSha1);

					//File.WriteAllText(Path.Combine(targetDirectory, parent_machine_name + ".txt"), manifest, Encoding.ASCII);

					string tempDirectory = Path.Combine(tempDir.Path, parent_machine_name);

					using (StringReader reader = new StringReader(manifest))
					{
						string line;
						while ((line = reader.ReadLine()) != null)
						{
							string[] parts = line.Split('\t');
							List<string> names = new List<string>(parts[0].Split('/'));
							string sha1 = parts[1];

							names.Insert(0, tempDirectory);

							string tempFilename = Path.Combine(names.ToArray());
							Directory.CreateDirectory(Path.GetDirectoryName(tempFilename));
							File.Copy(Globals.RomHashStore.Filename(sha1), tempFilename);
						}
					}

					foreach (string tempFilename in Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories))
						File.SetLastWriteTimeUtc(tempFilename, MagicModifiedDate);

					string targetFilename = Path.Combine(targetDirectory, parent_machine_name + ".zip");

					ZipFile.CreateFromDirectory(tempDirectory, targetFilename);
					Directory.Delete(tempDirectory, true);

					string fileSha1 = Tools.SHA1HexFile(targetFilename);

					string inspectManifest = InspectRomManifest(targetFilename);
					string inspectManifestSha1 = Tools.SHA1HexText(inspectManifest, Encoding.ASCII);

					if (manifestSha1 != inspectManifestSha1)
						throw new ApplicationException($"Bad Minifest Inspection: ${targetFilename}");

					if (fileManifestSHA1s.ContainsKey(fileSha1) == false)
						fileManifestSHA1s.Add(fileSha1, manifestSha1);
					else
						if (fileManifestSHA1s[fileSha1] != manifestSha1)
							throw new ApplicationException($"Manifest SHA1 Missmatch fileSha1:{fileSha1}.");
				}
			}

			using (StreamWriter writer = new StreamWriter(hashLookupFilename, false))
				foreach (string key in fileManifestSHA1s.Keys)
					writer.WriteLine($"{key}\t{fileManifestSHA1s[key]}");

			Globals.Reports.SaveHtmlReport(report, "Machine manifests");
		}

		public static Dictionary<string, string> GetRomNameHashes(DataRow parentMachineRow, DataTable machineTable, DataTable romTable)
		{
			string parent_machine_name = (string)parentMachineRow["name"];

			Dictionary<string, string> nameHashes = new Dictionary<string, string>();

			GetRomNameHashes(nameHashes, parentMachineRow, romTable);

			foreach (DataRow childMachineRow in machineTable.Select($"cloneof = '{parent_machine_name}'"))
			{
				string child_machine_name = (string)childMachineRow["name"];

				GetRomNameHashes(nameHashes, childMachineRow, romTable);
			}

			return nameHashes;
		}

		private static Dictionary<string, string> GetRomNameHashes(Dictionary<string, string> nameHashes, DataRow machineRow, DataTable romTable)
		{
			long machine_id = (long)machineRow["machine_id"];
			string machine_name = (string)machineRow["name"];
			bool isParent = machineRow.IsNull("cloneof");

			foreach (DataRow romRow in romTable.Select($"machine_id = {machine_id}"))
			{
				if (romRow.IsNull("merge") == false)
					continue;

				string sha1 = (string)romRow["sha1"];
				string name = (string)romRow["name"];

				if (isParent == false)
					name = $"{machine_name}/{name}";

				if (nameHashes.ContainsKey(name) == true)
				{
					if (nameHashes[name] != sha1)
						throw new ApplicationException($"ROM name sha1 mismatch, machine_id:{machine_id}, rom name:{name}.");
					continue;
				}

				nameHashes.Add(name, sha1);
			}

			return nameHashes;
		}

		public static string InspectRomManifest(string zipFilename)
		{
			Dictionary<string, string> nameHashes = new Dictionary<string, string>();

			using (TempDirectory tempDir = new TempDirectory())
			{
				ZipFile.ExtractToDirectory(zipFilename, tempDir.Path);

				Tools.ClearAttributes(tempDir.Path);

				foreach (string filename in Directory.GetFiles(tempDir.Path, "*", SearchOption.AllDirectories))
				{
					string name = filename.Substring(tempDir.Path.Length + 1).Replace('\\', '/');
					string sha1 = Tools.SHA1HexFile(filename);

					nameHashes.Add(name, sha1);
				}
			}

			return CreateRomManifest(nameHashes);
		}

		public static string CreateRomManifest(Dictionary<string, string> nameHashes)
		{
			StringBuilder manifest = new StringBuilder();
			foreach (string name in nameHashes.Keys.OrderBy(i => i))
			{
				manifest.Append(name);
				manifest.Append("\t");
				manifest.Append(nameHashes[name]);
				manifest.Append("\r\n");
			}
			return manifest.ToString();
		}



		public static string UploadFile(string itemName, string filename)
		{
			string url = $"https://s3.us.archive.org/{Uri.EscapeUriString(itemName)}/{Uri.EscapeUriString(Path.GetFileName(filename))}";

			byte[] data = File.ReadAllBytes(filename);

			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = "PUT";
			request.Timeout = 3 * 60 * 60 * 1000;
			
			request.ContentType = "application/octet-stream";
			request.ContentLength = data.Length;

			request.Headers.Add("authorization", ApiAuth);

			request.Headers.Add("x-archive-size-hint", data.Length.ToString());
			request.Headers.Add("x-archive-queue-derive", "0");

			Console.Write($"Upload '{filename}' => '{url}' ...");

			using (Stream requestStream = request.GetRequestStream())
			{
				requestStream.Write(data, 0, data.Length);
			}

			using (WebResponse response = request.GetResponse())
			{
				HttpWebResponse httpResponse = (HttpWebResponse)response;

				int statusCode = (int)httpResponse.StatusCode;

				if (statusCode < 200 || statusCode >= 300)
					throw new ApplicationException($"Bad status code:{statusCode}, url:{url}");

				using (Stream responseStream = response.GetResponseStream())
				{
					Console.WriteLine("...done.");

					using (StreamReader reader = new StreamReader(responseStream))
						return reader.ReadToEnd();
				}
			}
		}

		public static string DeleteFile(string itemName, string filename)
		{
			string url = $"https://s3.us.archive.org/{Uri.EscapeUriString(itemName)}/{Uri.EscapeUriString(Path.GetFileName(filename))}";

			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = "DELETE";

			request.Headers.Add("authorization", ApiAuth);

			Console.Write($"Delete '{url}' ...");

			using (WebResponse response = request.GetResponse())
			{
				HttpWebResponse httpResponse = (HttpWebResponse)response;

				int statusCode = (int)httpResponse.StatusCode;

				if (statusCode < 200 || statusCode >= 300)
					throw new ApplicationException($"Bad status code:{statusCode}, url:{url}");

				using (Stream responseStream = response.GetResponseStream())
				{
					Console.WriteLine("...done.");

					using (StreamReader reader = new StreamReader(responseStream))
						return reader.ReadToEnd();
				}
			}
		}



	}
}
