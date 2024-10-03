using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spludlow.MameAO
{
	public class Upload
	{
		public static void HashMachineDatabase()
		{
			DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name, description FROM machine ORDER BY machine.name");
			DataTable romTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");

			DataRow[] parentMachineRows = machineTable.Select("cloneof IS NULL");
			foreach (DataRow parentMachineRow in parentMachineRows)
			{
				HashSet<string> zipHashes = new HashSet<string>();

				string parent_machine_name = (string)parentMachineRow["name"];
				int parent_machine_id = (int)parentMachineRow["machine_id"];
				Dictionary<string, string> parentNameHashes = GetRomNameHashes(parent_machine_id, romTable, zipHashes);

				if (parentNameHashes.Count == 0)
					continue;

				//string archiveFilename = targetDirectory + @"\" + parent_machine_name + ".zip";

				//string archiveDirectory = tempDir.Path + @"\" + parent_machine_name;
				//Directory.CreateDirectory(archiveDirectory);

				//CopyRomFiles(archiveDirectory, parentNameHashes);

				DataRow[] childMachineRows = machineTable.Select("cloneof = '" + parent_machine_name + "'");
				foreach (DataRow childMachineRow in childMachineRows)
				{
					string child_machine_name = (string)childMachineRow["name"];
					int child_machine_Id = (int)childMachineRow["machine_Id"];
					Dictionary<string, string> childNameHashes = GetRomNameHashes(child_machine_Id, romTable, zipHashes);

					if (childNameHashes.Count == 0)
						continue;

					//string childDirectory = tempDir.Path + @"\" + parent_machine_name + @"\" + child_machine_name;
					//Directory.CreateDirectory(childDirectory);

					//CopyRomFiles(childDirectory, childNameHashes);
				}

				//Spludlow.Archive.Create(archiveFilename, archiveDirectory + @"\*", false);

				//Directory.Delete(archiveDirectory, true);
			}

		}

		private static Dictionary<string, string> GetRomNameHashes(int machine_id, DataTable romTable, HashSet<string> zipHashes)
		{
			Dictionary<string, string> nameHashes = new Dictionary<string, string>();

			foreach (DataRow romRow in romTable.Select("machine_id = " + machine_id))
			{
				if (romRow.IsNull("sha1") == true)
					continue;

				if (romRow.IsNull("merge") == false)
					continue;

				string sha1 = ((string)romRow["sha1"]).ToUpper();
				string rom_name = (string)romRow["name"];

				if (nameHashes.ContainsKey(rom_name) == true)
				{
					if (nameHashes[rom_name] != sha1)
						throw new ApplicationException("ROM name sha1 mismatch, machine_id:" + machine_id + "," + rom_name);
					continue;
				}

				if (zipHashes.Contains(sha1) == true)
					continue;

				zipHashes.Add(sha1);

				nameHashes.Add(rom_name, sha1);
			}

			return nameHashes;
		}



	}
}
