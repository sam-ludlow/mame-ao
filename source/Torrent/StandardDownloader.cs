using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.Client;

namespace mame_ao.source.Torrent
{
    public class FileType
    {
        public string Path { get; set; }
        public Priority Priority { get; set; }
    }
    public class DatfileItem
    {
        public string Datname { get; set; }

        //public string MagnetLink { get; set; }
    }

    public class MagnetItem
    {
        public string torrentName { get; set; }

        public string MagnetLink { get; set; }
    }

    class StandardDownloader
    {
        private static List<string> StartsWithStrings;
        private static List<string> ContainsStrings;

        ClientEngine Engine { get; }
        Top10Listener Listener { get; }         // This is a subclass of TraceListener which remembers the last 20 statements sent to it
        public static TorrentManager manager { get; private set; }
        //public static int n2 { get; private set; }

        public StandardDownloader(ClientEngine engine)
        {
            Engine = engine;
            Listener = new Top10Listener(10);
        }

        //private static async Task ProcessFileAsync(FileType file, List<string> StartsWithStrings, List<string> ContainsStrings, IProgress<string> progress, ref int n2)
        //{
        //    if (!StartsWithStrings.Any(prefix => file.Path.StartsWith(prefix)) ||
        //        !ContainsStrings.Any(prefix => file.Path.Contains(prefix)))
        //    {
        //        await manager.SetFilePriorityAsync((ITorrentManagerFile)file, Priority.DoNotDownload);
        //        Interlocked.Decrement(ref n2);
        //        progress.Report($"File {file.Path} set to DoNotDownload");
        //    }
        //    else
        //    {
        //        await manager.SetFilePriorityAsync((ITorrentManagerFile)file, Priority.Normal);
        //        progress.Report($"File {file.Path} set to Normal");
        //    }
        //}

        public async Task DownloadAsync(List<string> startsWithStrings, List<string> containsStrings, MagnetItem magnet)
        {
            // Torrents will be downloaded to this directory
            var downloadsPath = Path.Combine(Environment.CurrentDirectory, "Downloads");

            // .torrent files will be loaded from this directory (if any exist)
            var torrentsPath = Path.Combine(Environment.CurrentDirectory, "Torrents");

#if DEBUG
            //LoggerFactory.Register(new TextWriterLogger(Console.Out));
#endif
            var settingsBuilder = new TorrentSettingsBuilder
            {
                MaximumConnections = 60,
            };

            // If the torrentsPath does not exist, we want to create it
            if (!Directory.Exists(torrentsPath))
                Directory.CreateDirectory(torrentsPath);
            //try
            //{

            // For each file in the torrents path that is a .torrent file, load it into the engine.
            // foreach (string file in Directory.GetFiles(torrentsPath))
            //foreach (MagnetItem magnet in magnets)
            //{
            //if (file.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            //{
            // EngineSettings.AutoSaveLoadFastResume is enabled, so any cached fast resume
            // data will be implicitly loaded. If fast resume data is found, the 'hash check'
            // phase of starting a torrent can be skipped.
            // 
            // TorrentSettingsBuilder can be used to modify the settings for this
            // torrent.
            MagnetLink.TryParse(magnet.MagnetLink, out MagnetLink magnetLink);
            await Engine.AddAsync(magnetLink, downloadsPath, settingsBuilder.ToSettings()).ConfigureAwait(false);
            //}

            //await NewMethod(startsWithStrings, containsStrings).ConfigureAwait(false);

            //catch (Exception e)
            //{
            //    Console.Write("Couldn't decode {0}: ", magnet.MagnetLink);
            //    Console.WriteLine(e.Message);
            //}

            // If we loaded no torrents, just exist. The user can put files in the torrents directory and start
            // the client again
            if (Engine.Torrents.Count == 0)
            {
                Console.WriteLine($"No torrents found in '{torrentsPath}'");
                Console.WriteLine("Exiting...");
                return;
            }

            bool start_engine = true;
        }



        private async Task ProcessFileAsync(ITorrentManagerFile file, Progress<string> progress)
        {
            throw new NotImplementedException();
        }

        void Manager_PeersFound(object sender, PeersAddedEventArgs e)
        {
            lock (Listener)
                Listener.WriteLine($"Found {e.NewPeers} new peers and {e.ExistingPeers} existing peers");//throw new Exception("The method or operation is not implemented.");
        }

        void AppendSeparator(StringBuilder sb)
        {
            AppendFormat(sb, "");
            AppendFormat(sb, "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -");
            AppendFormat(sb, "");
        }

        void AppendFormat(StringBuilder sb, string str, params object[] formatting)
        {
            if (formatting != null && formatting.Length > 0)
                sb.AppendFormat(str, formatting);
            else
                sb.Append(str);
            sb.AppendLine();
        }
    }
}