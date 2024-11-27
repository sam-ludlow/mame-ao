using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace mame_ao.source
{
    public static class Globals
    {
        public class TaskInfo
        {
            public string Command = "";
            public long BytesCurrent = 0;
            public long BytesTotal = 0;
        }

        static Globals()
        {
            Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            AssemblyVersion = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";

            HttpClient = new HttpClient(new HttpClientHandler { UseCookies = false });
            HttpClient.DefaultRequestHeaders.Add("User-Agent", $"mame-ao/{Globals.AssemblyVersion} (https://github.com/sam-ludlow/mame-ao)");
            HttpClient.Timeout = TimeSpan.FromSeconds(180);     // metdata 3 minutes
        }

        public static readonly int AssetDownloadTimeoutMilliseconds = 6 * 60 * 60 * 1000;   // assets 6 hours

        public static string ListenAddress = "http://localhost:12380/";

        public static long DownloadDotSize = 1024 * 1024;

        public static string AssemblyVersion;

        public static HttpClient HttpClient;
        public static string AuthCookie = null;

        public static Dictionary<string, string> Arguments = new Dictionary<string, string>();

        public static string RootDirectory;
        public static string TempDirectory;
        public static string CacheDirectory;
        public static string ReportDirectory;

        public static string MameDirectory;

        public static string MameVersion;

        public static bool LinkingEnabled = false;

        public static Dictionary<ItemType, ArchiveOrgItem[]> ArchiveOrgItems = new Dictionary<ItemType, ArchiveOrgItem[]>();
        public static Dictionary<string, GitHubRepo> GitHubRepos = new Dictionary<string, GitHubRepo>();

        public static HashStore RomHashStore;
        public static HashStore DiskHashStore;

        public static MameAOProcessor AO;

        public static Artwork Artwork;
        public static BadSources BadSources;
        public static Database Database;
        public static Favorites Favorites;
        public static Genre Genre;
        public static MameChdMan MameChdMan;
        public static Reports Reports;
        public static Samples Samples;
        public static Settings Settings;
        public static WebServer WebServer = null;

        public static Task WorkerTask = null;
        public static TaskInfo WorkerTaskInfo = new TaskInfo();
        public static DataSet WorkerTaskReport;

        public static PhoneHome PhoneHome;
    }
}
