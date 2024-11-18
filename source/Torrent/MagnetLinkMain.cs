using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent.Client;

namespace mame_ao.source.Torrent
{
    public class MainClass
    {
        public static async Task RunMainTask(MagnetItem magnet, List<string> StartsWith, List<string> ContainsStrings)
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();

            var task = MainAsync(magnet, StartsWith, ContainsStrings, cancellation.Token);

            // We need to cleanup correctly when the user closes the window by using ctrl-c
            // or an unhandled exception happens
            Console.CancelKeyPress += delegate { cancellation.Cancel(); task.Wait(); };
            AppDomain.CurrentDomain.ProcessExit += delegate { cancellation.Cancel(); task.Wait(); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); cancellation.Cancel(); task.Wait(); };
            Thread.GetDomain().UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); cancellation.Cancel(); task.Wait(); };

            task.Wait();
        }

        static async Task MainAsync(MagnetItem magnet, List<string> StartsWith, List<string> ContainsStrings, CancellationToken token)
        {
            //List<string> StartsWith = new List<string> { "a", "b", "c" };
            //StartsWith = (await MameAOProcessor.EnterNewList(StartsWith, "Enter List of Filename Starts Withs : ").ConfigureAwait(false)).Result;

            //List<string> ContainsStrings = new List<string> { "ami", "out", "com" };
            //ContainsStrings = MameAOProcessor.EnterNewList(ContainsStrings, "Enter List of Filename Contains : ").Result;

            const int httpListeningPort = 55125;

            var settingBuilder = new EngineSettingsBuilder
            {
                // Allow the engine to automatically forward ports using upnp/nat-pmp (if a compatible router is available)
                AllowPortForwarding = true,

                // Automatically save a cache of the DHT table when all torrents are stopped.
                AutoSaveLoadDhtCache = true,

                // Automatically save 'FastResume' data when TorrentManager.StopAsync is invoked, automatically load it
                // before hash checking the torrent. Fast Resume data will be loaded as part of 'engine.AddAsync' if
                // torrent metadata is available. Otherwise, if a magnetlink is used to download a torrent, fast resume
                // data will be loaded after the metadata has been downloaded. 
                AutoSaveLoadFastResume = true,

                // If a MagnetLink is used to download a torrent, the engine will try to load a copy of the metadata
                // it's cache directory. Otherwise the metadata will be downloaded and stored in the cache directory
                // so it can be reloaded later.
                AutoSaveLoadMagnetLinkMetadata = true,

                // Use a fixed port to accept incoming connections from other peers for testing purposes. Production usages should use a random port, 0, if possible.
                ListenEndPoints = new Dictionary<string, IPEndPoint> {
                    { "ipv4", new IPEndPoint (IPAddress.Any, 55123) },
                    { "ipv6", new IPEndPoint (IPAddress.IPv6Any, 55123) }
                },

                // Use a fixed port for DHT communications for testing purposes. Production usages should use a random port, 0, if possible.
                DhtEndPoint = new IPEndPoint(IPAddress.Any, 55123),


                // Wildcards such as these are supported as long as the underlying .NET framework version, and the operating system, supports them:
                //HttpStreamingPrefix = $"http://+:{httpListeningPort}/"
                //HttpStreamingPrefix = $"http://*.mydomain.com:{httpListeningPort}/"

                // For now just bind to localhost.
                HttpStreamingPrefix = $"http://127.0.0.1:{httpListeningPort}/"
            };
            ClientEngine clientEngine = new ClientEngine(settingBuilder.ToSettings());
            //using ClientEngine engine = clientEngine;

            if (clientEngine.Settings.AllowPortForwarding)
                Console.WriteLine("uPnP or NAT-PMP port mappings will be created for any ports needed by MonoTorrent");

            try
            {
                Task task = new StandardDownloader(clientEngine).DownloadAsync(token, startsWithStrings: StartsWith, ContainsStrings, magnet);
                await task;
                //task.Wait();
            }
            catch (OperationCanceledException)
            {

            }

            foreach (var manager in clientEngine.Torrents)
            {
                var stoppingTask = manager.StopAsync();
                while (manager.State != TorrentState.Stopped)
                {
                    Console.WriteLine("{0} is {1}", manager.Torrent.Name, manager.State);
                    await Task.WhenAll(stoppingTask, Task.Delay(250));
                }
                await stoppingTask;
                if (clientEngine.Settings.AutoSaveLoadFastResume)
                    Console.WriteLine($"FastResume data for {manager.Torrent?.Name ?? manager.InfoHashes.V1?.ToHex() ?? manager.InfoHashes.V2?.ToHex()} has been written to disk.");
            }

            if (clientEngine.Settings.AutoSaveLoadDhtCache)
                Console.WriteLine($"DHT cache has been written to disk.");

            if (clientEngine.Settings.AllowPortForwarding)
                Console.WriteLine("uPnP and NAT-PMP port mappings have been removed");
        }
    }
}