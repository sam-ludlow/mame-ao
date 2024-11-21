using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections;
using Newtonsoft.Json.Linq;

namespace mame_ao.source.Torrent
{
    public class MainClass
    {
        //public EngineSettingsBuilder settingBuilder { get; private set; }
        //public ClientEngine Engine { get; private set; }

        public MainClass(EngineSettingsBuilder settingBuilder, ClientEngine engine, Top10Listener listener)
        {
            SettingBuilder = settingBuilder;
            Engine = engine;
            Listener = listener;
        }

        public EngineSettingsBuilder SettingBuilder { get; }
        public ClientEngine Engine { get; }
        public Top10Listener Listener { get; }

        public async Task RunMainTask(List<MagnetItem> magnets, List<string> StartsWith, List<string> ContainsStrings)
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();

            await Main(magnets, StartsWith, ContainsStrings).ConfigureAwait(false);

            // We need to cleanup correctly when the user closes the window by using ctrl-c
            // or an unhandled exception happens
            //Console.CancelKeyPress += delegate { cancellation.Cancel(); task.Wait(); };
            //AppDomain.CurrentDomain.ProcessExit += delegate { cancellation.Cancel(); task.Wait(); };
            //AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); cancellation.Cancel(); task.Wait(); };
            //Thread.GetDomain().UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); cancellation.Cancel(); task.Wait(); };

            //task.Wait();
        }
        public async Task FilterFiles(List<string> startsWithStrings, List<string> containsStrings, CancellationToken token)
        {
            foreach (TorrentManager manager in Engine.Torrents)
            {

                MonoTorrent.Torrent torrent = manager.Torrent;
                var n1 = manager.Files.Count;
                var n2 = n1;
                Console.WriteLine($"{n1} - {n2}");

                var fileArray = manager.Files.ToArray();
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
                IProgress<string> progress = new Progress<string>(message => Console.WriteLine(message));

                await Task.Run(() =>
                {
                    Parallel.ForEach(fileArray, parallelOptions, async file =>
                    {
                        if (!startsWithStrings.Any(prefix => file.Path.StartsWith(prefix)) ||
                            !containsStrings.Any(prefix => file.Path.Contains(prefix)))
                        {
                            await manager.SetFilePriorityAsync(file, Priority.DoNotDownload).ConfigureAwait(false);
                            Interlocked.Decrement(ref n2);
                            progress.Report($"File {file.Path} set to DoNotDownload");

                        }
                        else await manager.SetFilePriorityAsync(file, Priority.Normal).ConfigureAwait(false);


                    });
                }).ConfigureAwait(false);

                manager.Files
                .Where(files => files.Priority == Priority.Normal)
                .ToList()
                .ForEach(files => Console.WriteLine(files.Path));

                if (n1 == n2)
                    Console.WriteLine($"{n1} - {n2} No files found");
                else
                    Console.WriteLine($"{n1} - {n1 - n2}");

                Console.WriteLine(manager.InfoHashes.V1OrV2.ToHex());
            }
        }

        public async Task Main(List<MagnetItem> magnets, List<string> StartsWith, List<string> ContainsStrings)
        {
            //List<string> StartsWith = new List<string> { "a", "b", "c" };
            //StartsWith = (await MameAOProcessor.EnterNewList(StartsWith, "Enter List of Filename Starts Withs : ").ConfigureAwait(false)).Result;

            //List<string> ContainsStrings = new List<string> { "ami", "out", "com" };
            //ContainsStrings = MameAOProcessor.EnterNewList(ContainsStrings, "Enter List of Filename Contains : ").Result;


            //settingBuilder = new EngineSettingsBuilder
            //{
            //    // Allow the engine to automatically forward ports using upnp/nat-pmp (if a compatible router is available)
            //    AllowPortForwarding = true,

            //    // Automatically save a cache of the DHT table when all torrents are stopped.
            //    AutoSaveLoadDhtCache = true,

            //    // Automatically save 'FastResume' data when TorrentManager.StopAsync is invoked, automatically load it
            //    // before hash checking the torrent. Fast Resume data will be loaded as part of 'engine.AddAsync' if
            //    // torrent metadata is available. Otherwise, if a magnetlink is used to download a torrent, fast resume
            //    // data will be loaded after the metadata has been downloaded. 
            //    AutoSaveLoadFastResume = true,

            //    // If a MagnetLink is used to download a torrent, the engine will try to load a copy of the metadata
            //    // it's cache directory. Otherwise the metadata will be downloaded and stored in the cache directory
            //    // so it can be reloaded later.
            //    AutoSaveLoadMagnetLinkMetadata = true,

            //    // Use a fixed port to accept incoming connections from other peers for testing purposes. Production usages should use a random port, 0, if possible.
            //    ListenEndPoints = new Dictionary<string, IPEndPoint> {
            //        { "ipv4", new IPEndPoint (IPAddress.Any, 55123) },
            //        { "ipv6", new IPEndPoint (IPAddress.IPv6Any, 55123) }
            //    },

            //    // Use a fixed port for DHT communications for testing purposes. Production usages should use a random port, 0, if possible.
            //    DhtEndPoint = new IPEndPoint(IPAddress.Any, 55123),


            //    // Wildcards such as these are supported as long as the underlying .NET framework version, and the operating system, supports them:
            //    //HttpStreamingPrefix = $"http://+:{httpListeningPort}/"
            //    //HttpStreamingPrefix = $"http://*.mydomain.com:{httpListeningPort}/"

            //    // For now just bind to localhost.
            //    HttpStreamingPrefix = $"http://127.0.0.1:{httpListeningPort}/"
            //};
            //Engine = new ClientEngine(settingBuilder.ToSettings());
            //using ClientEngine engine = clientEngine;

            if (Engine.Settings.AllowPortForwarding)
                Console.WriteLine("uPnP or NAT-PMP port mappings will be created for any ports needed by MonoTorrent");

            try
            {
                var sd = new StandardDownloader(Engine);

                foreach (var magnet in magnets)
                {
                    await sd.DownloadAsync(startsWithStrings: StartsWith, ContainsStrings, magnet).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {

            }

            //await StopEngine();

            if (Engine.Settings.AutoSaveLoadDhtCache)
                Console.WriteLine($"DHT cache has been written to disk.");

            if (Engine.Settings.AllowPortForwarding)
                Console.WriteLine("uPnP and NAT-PMP port mappings have been removed");
        }
        public async Task StartEngine(CancellationToken token)
        {
            // For each torrent manager we loaded and stored in our list, hook into the events
            // in the torrent manager and start the engine.
            foreach (TorrentManager manager in Engine.Torrents)
            {
                manager.PeersFound += (o, e) =>
                {
                    Listener.WriteLine(string.Format($"{e.GetType().Name}: {e.NewPeers} peers for {e.TorrentManager.Name}"));
                };
                manager.PeerConnected += (o, e) =>
                {
                    lock (Listener)
                        Listener.WriteLine($"Connection succeeded: {e.Peer.Uri}");
                };
                manager.ConnectionAttemptFailed += (o, e) =>
                {
                    lock (Listener)
                        Listener.WriteLine(
                            $"Connection failed: {e.Peer.ConnectionUri} - {e.Reason}");
                };
                // Every time a piece is hashed, this is fired.
                manager.PieceHashed += delegate (object o, PieceHashedEventArgs e)
                {
                    lock (Listener)
                        Listener.WriteLine($"Piece Hashed: {e.PieceIndex} - {(e.HashPassed ? "Pass" : "Fail")}");
                };

                // Every time the state changes (Stopped -> Seeding -> Downloading -> Hashing) this is fired
                manager.TorrentStateChanged += delegate (object o, TorrentStateChangedEventArgs e)
                {
                    lock (Listener)
                        Listener.WriteLine($"OldState: {e.OldState} NewState: {e.NewState}");
                };

                // Every time the tracker's state changes, this is fired
                manager.TrackerManager.AnnounceComplete += (sender, e) =>
                {
                    Listener.WriteLine($"{e.Successful}: {e.Tracker}");
                };

                // Start the torrentmanager. The file will then hash (if required) and begin downloading/seeding.
                // As EngineSettings.AutoSaveLoadDhtCache is enabled, any cached data will be loaded into the
                // Dht engine when the first torrent is started, enabling it to bootstrap more rapidly.
                await manager.StartAsync().ConfigureAwait(false);
            }

            // While the torrents are still running, print out some stats to the screen.
            // Details for all the loaded torrent managers are shown.
            StringBuilder sb = new StringBuilder(1024);
            while (Engine.IsRunning)
            {
                sb.Remove(0, sb.Length);

                AppendFormat(sb, $"Transfer Rate:      {Engine.TotalDownloadRate / 1024.0:0.00}kB/sec ↓ / {Engine.TotalUploadRate / 1024.0:0.00}kB/sec ↑");
                AppendFormat(sb, $"Memory Cache:       {Engine.DiskManager.CacheBytesUsed / 1024.0:0.00}/{Engine.Settings.DiskCacheBytes / 1024.0:0.00} kB");
                AppendFormat(sb, $"Disk IO Rate:       {Engine.DiskManager.ReadRate / 1024.0:0.00} kB/s read / {Engine.DiskManager.WriteRate / 1024.0:0.00} kB/s write");
                AppendFormat(sb, $"Disk IO Total:      {Engine.DiskManager.TotalBytesRead / 1024.0:0.00} kB read / {Engine.DiskManager.TotalBytesWritten / 1024.0:0.00} kB written");
                AppendFormat(sb, $"Open Files:         {Engine.DiskManager.OpenFiles} / {Engine.DiskManager.MaximumOpenFiles}");
                AppendFormat(sb, $"Open Connections:   {Engine.ConnectionManager.OpenConnections}");
                AppendFormat(sb, $"DHT State:          {Engine.Dht.State}");

                // Print out the port mappings
                foreach (var mapping in Engine.PortMappings.Created)
                    AppendFormat(sb, $"Successful Mapping    {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");
                foreach (var mapping in Engine.PortMappings.Failed)
                    AppendFormat(sb, $"Failed mapping:       {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");
                foreach (var mapping in Engine.PortMappings.Pending)
                    AppendFormat(sb, $"Pending mapping:      {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");

                foreach (TorrentManager manager in Engine.Torrents)
                {
                    AppendSeparator(sb);
                    AppendFormat(sb, $"State:              {manager.State}");
                    AppendFormat(sb, $"Name:               {(manager.Torrent == null ? "MetaDataMode" : manager.Torrent.Name)}");
                    AppendFormat(sb, $"Progress:           {manager.Progress:0.00}");
                    AppendFormat(sb, $"Transferred:        {manager.Monitor.DataBytesReceived / 1024.0 / 1024.0:0.00} MB ↓ / {manager.Monitor.DataBytesSent / 1024.0 / 1024.0:0.00} MB ↑");
                    AppendFormat(sb, $"Tracker Status");
                    foreach (var tier in manager.TrackerManager.Tiers)
                        AppendFormat(sb, $"\t{tier.ActiveTracker} : Announce Succeeded: {tier.LastAnnounceSucceeded}. Scrape Succeeded: {tier.LastScrapeSucceeded}.");

                    if (manager.PieceManager != null)
                        AppendFormat(sb, "Current Requests:   {0}", await manager.PieceManager.CurrentRequestCountAsync().ConfigureAwait(false));

                    var peers = await manager.GetPeersAsync().ConfigureAwait(false);
                    AppendFormat(sb, "Outgoing:");
                    foreach (PeerId p in peers.Where(t => t.ConnectionDirection == Direction.Outgoing))
                    {
                        AppendFormat(sb, $"\t{p.AmRequestingPiecesCount} - {(p.Monitor.DownloadRate / 1024.0):0.00}/{(p.Monitor.UploadRate / 1024.0):0.00}kB/sec - {p.Uri} - {p.EncryptionType}");
                    }
                    AppendFormat(sb, "");
                    AppendFormat(sb, "Incoming:");
                    foreach (PeerId p in peers.Where(t => t.ConnectionDirection == Direction.Incoming))
                    {
                        AppendFormat(sb, $"\t{p.AmRequestingPiecesCount} - {(p.Monitor.DownloadRate / 1024.0):0.00}/{(p.Monitor.UploadRate / 1024.0):0.00}kB/sec - {p.Uri} - {p.EncryptionType}");
                    }

                    AppendFormat(sb, "", null);
                    if (manager.Torrent != null)
                        foreach (var file in manager.Files)
                            if (file.BitField.PercentComplete > 0)
                            {
                                AppendFormat(sb, "{1:0.00}% - {0}", file.Path, file.BitField.PercentComplete);
                            }
                }
                Console.Clear();
                Console.WriteLine(sb.ToString());
                Listener.ExportTo(Console.Out);

                await Task.Delay(5000, token).ConfigureAwait(false);
            }
        }
        //Top10Listener Listener = new Top10Listener(10);

        //public MainClass(EngineSettingsBuilder settingBuilder, ClientEngine engine, Top10Listener listener)
        //{
        //    this.settingBuilder = settingBuilder;
        //    Engine = engine;
        //    Listener = listener;
        //}

        void AppendFormat(StringBuilder sb, string str, params object[] formatting)
        {
            if (formatting != null && formatting.Length > 0)
                sb.AppendFormat(str, formatting);
            else
                sb.Append(str);
            sb.AppendLine();
        }

        void AppendSeparator(StringBuilder sb)
        {
            AppendFormat(sb, "");
            AppendFormat(sb, "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -");
            AppendFormat(sb, "");
        }

        public async Task StopEngine()
        {
            foreach (var manager in Engine.Torrents)
            {
                var stoppingTask = manager.StopAsync();
                while (manager.State != TorrentState.Stopped)
                {
                    Console.WriteLine("{0} is {1}", manager.Torrent.Name, manager.State);
                    await Task.WhenAll(stoppingTask, Task.Delay(250));
                }
                await stoppingTask;
                if (Engine.Settings.AutoSaveLoadFastResume)
                    
                    Console.WriteLine($"FastResume data for {manager.Torrent?.Name ?? manager.InfoHashes.V1?.ToHex() ?? manager.InfoHashes.V2?.ToHex()} has been written to disk.");
            }
        }
    }
}