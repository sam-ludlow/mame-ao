using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using System.Xml.Linq;

namespace Spludlow.MameAO
{
	public class Favorites
	{
		private string _MachinesFilename;
		private string _SoftwareFilename;

		public Dictionary<string, HashSet<string>> _Machines;
		public Dictionary<string, HashSet<string>> _Software;

		//	Machines	machine	[softwareLists...]
		//	Software	list	[software...]

		public Favorites(string rootDirectory)
		{
			_MachinesFilename = Path.Combine(rootDirectory, "_FavoritesMachines.txt");
			_SoftwareFilename = Path.Combine(rootDirectory, "_FavoritesSoftware.txt");

			_Machines = Load(_MachinesFilename);
			_Software = Load(_SoftwareFilename);
		}

		private Dictionary<string, HashSet<string>> Load(string filename)
		{
			if (File.Exists(filename) == false)
				File.WriteAllText(filename, "", Encoding.UTF8);

			Dictionary<string, HashSet<string>> data = new Dictionary<string, HashSet<string>>();

			using (StreamReader reader = new StreamReader(filename, Encoding.UTF8))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					string[] parts = line.Split(new char[] { '\t' });

					if (parts.Length == 0)
						throw new ApplicationException($"Bad favorites file: {filename}");

					data.Add(parts[0], new HashSet<string>());

					if (parts.Length > 1)
					{
						for (int index = 1; index < parts.Length; index++)
							data[parts[0]].Add(parts[index]);
					}
				}
			}

			return data;
		}

		private void Save()
		{
			Save(_Machines, _MachinesFilename);
			Save(_Software, _SoftwareFilename);
		}

		private void Save(Dictionary<string, HashSet<string>> data, string filename)
		{
			using (StreamWriter writer = new StreamWriter(filename, false, Encoding.UTF8))
			{
				foreach (string key in data.Keys)
				{
					writer.Write(key);

					foreach (string value in data[key])
					{
						writer.Write('\t');
						writer.Write(value);
					}

					writer.WriteLine();
				}
			}
		}

		public void AddCommandLine(string line)
		{
			string[] parts = line.Split(new char[] { ' ' });

			if (parts.Length != 2 && parts.Length != 4 || (parts[0].StartsWith(".favs") == true && parts.Length != 4))
				throw new ApplicationException("Bad Favorites AddCommandLine: " + line);

			switch (parts[0])
			{
				case ".favm":
					AddMachine(parts[1]);
					break;
				case ".favmx":
					RemoveMachine(parts[1]);
					break;
				case ".favs":
					AddSoftware(parts[1], parts[2], parts[3]);
					break;
				case ".favsx":
					RemoveSoftware(parts[1], parts[2], parts[3]);
					break;
				default:
					throw new ApplicationException("Bad Favorites AddCommandLine: " + line);
			}
		}

		public void AddMachine(string name)
		{
			if (_Machines.ContainsKey(name) == true)
				return;

			_Machines.Add(name, new HashSet<string>());
			Save();
		}

		public void RemoveMachine(string name)
		{
			if (_Machines.ContainsKey(name) == false)
				return;

			_Machines.Remove(name);

			// Not removing orphaned software !!!

			Save();
		}

		public bool IsMachine(string name)
		{
			return _Machines.ContainsKey(name);
		}

		public void AddColumnMachines(DataTable table, string nameColumnName, string targetColumnName)
		{
			table.Columns.Add(targetColumnName, typeof(bool));

			foreach (DataRow row in table.Rows)
				row[targetColumnName] = IsMachine((string)row[nameColumnName]);
		}


		public void AddSoftware(string machineName, string listName, string softwareName)
		{
			if (_Machines.ContainsKey(machineName) == false)
				_Machines.Add(machineName, new HashSet<string>());

			_Machines[machineName].Add(listName);

			if (_Software.ContainsKey(listName) == false)
				_Software.Add(listName, new HashSet<string>());

			_Software[listName].Add(softwareName);

			Save();
		}

		public void RemoveSoftware(string machineName, string listName, string softwareName)
		{
			if (_Machines.ContainsKey(machineName) == false)
				return;

			if (_Software.ContainsKey(listName) == false)
				return;

			if (_Software[listName].Contains(softwareName) == false)
				return;

			_Software[listName].Remove(softwareName);

			if (_Software[listName].Count == 0)
			{
				_Software.Remove(listName);
				_Machines[machineName].Remove(listName);
			}

			Save();
		}

		public bool IsSoftware(string machineName, string listName, string softwareName)
		{
			if (_Machines.ContainsKey(machineName) == false)
				return false;

			if (_Machines[machineName].Contains(listName) == false)
				return false;

			if (_Software.ContainsKey(listName) == false)
				return false;

			if (_Software[listName].Contains(softwareName) == false)
				return false;

			return true;
		}

		public void AddColumnSoftware(DataTable table, string machineName, string listName, string nameColumnName, string targetColumnName)
		{
			table.Columns.Add(targetColumnName, typeof(bool));

			foreach (DataRow row in table.Rows)
				row[targetColumnName] = IsSoftware(machineName, listName == "@fav" ? (string)row["softwarelist_name"] : listName, (string)row[nameColumnName]);
		}

		public string[][] ListSoftwareUsedByMachine(string machineName)
		{
			List<string[]> result = new List<string[]>();

			if (_Machines.ContainsKey(machineName) == true)
			{
				foreach (string listName in _Machines[machineName])
				{
					if (_Software.ContainsKey(listName) == true)
					{
						foreach (string softwareName in _Software[listName])
						{
							result.Add(new string[] { listName, softwareName });
						}
					}
				}
			}

			return result.ToArray();
		}

	}
}
