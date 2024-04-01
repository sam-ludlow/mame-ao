using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Spludlow.MameAO
{
	public class Samples
	{
		public readonly HttpClient _HttpClient;
		public Samples(HttpClient httpClient)
		{
			_HttpClient = httpClient;
		}

		public void Initialize()
		{
			DateTime version = GetLastProgettoSnapsUpdate();

			Console.WriteLine(version);
		}

		public DateTime GetLastProgettoSnapsUpdate()
		{
			string html = Tools.Query(_HttpClient, "https://www.progettosnaps.net/samples/");

			string find = "<span class=\"last-updated\">";
			int index = html.IndexOf(find);
	
			if (index == -1)
				throw new ApplicationException("GetLastProgettoSnapsUpdate did not find start span");
			
			html = html.Substring(index + find.Length);

			find = "</span>";

			index = html.IndexOf(find);

			if (index == 1)
				throw new ApplicationException("GetLastProgettoSnapsUpdate did not find end span");

			html = html.Substring(0, index);

			find = "Last updated on";

			if (html.StartsWith(find) == false)
				throw new ApplicationException("GetLastProgettoSnapsUpdate bad start");

			html = html.Substring(find.Length).Trim();

			return DateTime.ParseExact(html, "MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture);
		}
	}
}
