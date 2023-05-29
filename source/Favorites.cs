using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;

namespace Spludlow.MameAO
{
	public class Favorites
	{
		private string _DataFilename;

		public HashSet<string> _Machines;

		public Favorites(string rootDirectory)
		{
			_DataFilename = Path.Combine(rootDirectory, "_FavoritesMachines.txt");

			if (File.Exists(_DataFilename) == false)
				File.WriteAllText(_DataFilename, "", Encoding.UTF8);

			_Machines = new HashSet<string>(File.ReadAllLines(_DataFilename, Encoding.UTF8));
		}

		public void AddMachine(string name)
		{
			_Machines.Add(name);
			Save();
		}

		public void RemoveMachine(string name)
		{
			_Machines.Remove(name);
			Save();
		}

		public void Save()
		{
			File.WriteAllLines(_DataFilename, _Machines.ToArray(), Encoding.UTF8);
		}

		public bool IsMachine(string name)
		{
			return _Machines.Contains(name);
		}

		public void AddColumn(DataTable table, string nameColumnName, string targetColumnName)
		{
			table.Columns.Add(targetColumnName, typeof(bool));

			foreach (DataRow row in table.Rows)
				row[targetColumnName] = IsMachine((string)row[nameColumnName]);
		}


	}
}
