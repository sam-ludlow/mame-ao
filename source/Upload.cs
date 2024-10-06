using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;

namespace Spludlow.MameAO
{
	public class Upload
	{
		public static void Machine()
		{
			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name, cloneof, description FROM machine ORDER BY machine.name");
			DataTable romTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");

			DataTable report = Tools.MakeDataTable("Machine manifests",
				"name	sha1",
				"String	String");

			string targetDirectory = @"D:\TMP";

			DateTime modifiedDate = new DateTime(1996, 12, 24, 23, 32, 0, DateTimeKind.Utc);

			using (TempDirectory tempDir = new TempDirectory())
			{
				foreach (DataRow parentMachineRow in machineTable.Select("cloneof IS NULL"))
				{
					string parent_machine_name = (string)parentMachineRow["name"];

					// test
					//if (parent_machine_name.StartsWith("b") == true)
					//	break;

					Dictionary<string, string> nameHashes = new Dictionary<string, string>();

					GetRomNameHashes(nameHashes, parentMachineRow, romTable);

					foreach (DataRow childMachineRow in machineTable.Select($"cloneof = '{parent_machine_name}'"))
					{
						string child_machine_name = (string)childMachineRow["name"];

						GetRomNameHashes(nameHashes, childMachineRow, romTable);
					}

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

					StringBuilder manifest = new StringBuilder();

					foreach (string name in nameHashes.Keys.OrderBy(i => i))
					{
						manifest.Append(name);
						manifest.Append("\t");
						manifest.Append(nameHashes[name]);
						manifest.Append("\r\n");
					}

					string manifestText = manifest.ToString();

					string manifestSha1 = Tools.SHA1HexText(manifestText, Encoding.ASCII);

					report.Rows.Add(parent_machine_name, manifestSha1);

					File.WriteAllText(Path.Combine(targetDirectory, parent_machine_name + ".txt"), manifestText, Encoding.ASCII);

					string tempDirectory = Path.Combine(tempDir.Path, parent_machine_name);

					using (StringReader reader = new StringReader(manifestText))
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

						foreach (string tempFilename in Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories))
							File.SetLastWriteTimeUtc(tempFilename, modifiedDate);

						string targetFilename = Path.Combine(targetDirectory, parent_machine_name + ".zip");

						ZipFile.CreateFromDirectory(tempDirectory, targetFilename);
					}

					Directory.Delete(tempDirectory, true);
				}
			}

			Globals.Reports.SaveHtmlReport(report, "Machine manifests");
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



	}
}
