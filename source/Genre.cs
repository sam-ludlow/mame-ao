using System;
using System.Collections.Generic;
using System.Net.Http;
using System.IO;
using System.IO.Compression;
using System.Data;

namespace Spludlow.MameAO
{
	public class Genre
	{
		public readonly HttpClient HttpClient;
		public readonly string RootDirectory;


		//Genre genre = new Genre(_HttpClient, _RootDirectory);

		//genre.Initialize();
		//	return;


		public Genre(HttpClient httpClient, string rootDirectory)
		{
			HttpClient = httpClient;

			RootDirectory = Path.Combine(rootDirectory, "_METADATA", "Genre");

			Directory.CreateDirectory(RootDirectory);
		}

		public bool Initialize()
		{
			string url = "https://www.progettosnaps.net/catver/";

			string html = Tools.Query(HttpClient, url);

			int index;

			index = html.IndexOf("href=\"/download/?tipo=catver");

			if (index == -1)
				throw new ApplicationException("Did not find download link");

			html = html.Substring(index + 6);

			index = html.IndexOf("\"");

			if (index == -1)
				throw new ApplicationException("Did not find closing quote");

			html = html.Substring(0, index);

			string zipUrl = new Uri(new Uri(url), html).AbsoluteUri;

			index = html.LastIndexOf("_");

			if (index == -1)
				throw new ApplicationException("Did not find last underscore");

			string version = html.Substring(index + 1);

			index = version.LastIndexOf(".");

			if (index == -1)
				throw new ApplicationException("Did not find last dot");

			version = version.Substring(0, index);


			//
			// Download & Extract
			//

			string zipFilename = Path.Combine(RootDirectory, version + ".zip");

			if (File.Exists(zipFilename) == false)
				Tools.Download(zipUrl, zipFilename, 0, 3);

			string versionDirectory = Path.Combine(RootDirectory, version);

			if (Directory.Exists(versionDirectory) == false)
			{
				Directory.CreateDirectory(versionDirectory);
				ZipFile.ExtractToDirectory(zipFilename, versionDirectory);
			}

			string filename = Path.Combine(versionDirectory, "catver.ini");

			if (File.Exists(filename) == false)
				throw new ApplicationException("ProgettoSnaps catver.ini not found: " + filename);

			//
			// Load
			//

			DataTable table = Tools.MakeDataTable(
				"Machine	Group	Genre",
				"String		String	String"
			);

			List<string> groups = new List<string>();
			List<string> genres = new List<string>();

			bool inData = false;

			foreach (string rawLine in File.ReadAllLines(filename))
			{
				string line = rawLine.Trim();
				if (line.Length == 0)
					continue;

				if (line == "[Category]")
				{
					inData = true;
					continue;
				}

				if (line == "[VerAdded]")
					break;

				if (inData == false)
					continue;

				string[] parts;

				parts = line.Split(new char[] { '=' });

				if (parts.Length != 2)
					throw new ApplicationException("Not 2 parts on line");

				string machine = parts[0];
				string genre = parts[1];

				if (genres.Contains(genre) == false)
					genres.Add(genre);

				parts = parts[1].Split(new char[] { '/' });

				string group = parts[0].Trim();

				if (groups.Contains(group) == false)
					groups.Add(group);

				table.Rows.Add(machine, group, genre);
			}

			groups.Sort();
			genres.Sort();

			Tools.PopText(table);


			return true;
		}
	}
}
