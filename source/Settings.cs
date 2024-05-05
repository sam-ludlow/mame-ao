using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Spludlow.MameAO
{
	public class Settings
	{
		private string Filename;

		public Dictionary<string, List<string>> AvailableOptions = new Dictionary<string, List<string>>()
		{
			//{ "Artwork", new List<string>(new string[] { "No", "ArtworkPreview", "Artworks", "ArtworksAll", "ArtworksAlt", "ArtworksWideScreen" }) },
			{ "Artwork", new List<string>(new string[] { "No", "Artworks" }) },
		};

		public Dictionary<string, string> OptionDescriptions = new Dictionary<string, string>() {
			{ "Artwork", "Place Artwork Files. Note that switching this off will not clear existing placed artwork files." },
		};

		public Dictionary<string, string> Options = new Dictionary<string, string>();

		public Settings()
		{
			Filename = Path.Combine(Globals.RootDirectory, "_Settings.txt");

			Options = ReadDictionary();

			WriteDictionary();
		}

		public void Set(string key, string value)
		{
			if (AvailableOptions.ContainsKey(key) == false || AvailableOptions[key].Contains(value) == false)
				throw new ApplicationException($"Bad setting key:{key} value:{value}");

			Options[key] = value;

			WriteDictionary();
		}

		private Dictionary<string, string> ReadDictionary()
		{
			Dictionary<string, string> result = new Dictionary<string, string>();

			if (File.Exists(Filename) == true)
			{
				using (StreamReader reader = new StreamReader(Filename, Encoding.UTF8))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						string[] parts = line.Split('\t');
						if (parts.Length != 2)
							continue;

						string key = parts[0];
						string value = parts[1];

						if (AvailableOptions.ContainsKey(key) == false)
							continue;

						if (AvailableOptions[key].Contains(value) == false)
							continue;

						result.Add(key, value);
					}
				}
			}

			foreach (string key in AvailableOptions.Keys)
			{
				if (result.ContainsKey(key) == false)
					result.Add(key, AvailableOptions[key][0]);
			}

			return result;
		}

		private void WriteDictionary()
		{
			StringBuilder result = new StringBuilder();
			foreach (string key in Options.Keys)
				result.AppendLine($"{key}\t{Options[key]}");
			File.WriteAllText(Filename, result.ToString(), Encoding.UTF8);
		}
	}
}
