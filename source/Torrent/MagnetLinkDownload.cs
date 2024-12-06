using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.Client;

namespace mame_ao.source.Torrent
{
    class MagnetLinkStreaming
    {
        ClientEngine Engine { get; }

        public MagnetLinkStreaming(ClientEngine engine)
        {
            Engine = engine;
        }

        public async Task DownloadAsync(MagnetLink link, CancellationToken token)
        {
            var times = new List<(string message, TimeSpan time)>();
            var manager = await Engine.AddAsync(link, "downloads");
            //var settingsBuilder = new TorrentSettingsBuilder
            //{
            //    MaximumConnections = 60,
            //};
            //var manager = await Engine.AddAsync(file, downloadsPath, settingsBuilder.ToSettings());

            var overall = Stopwatch.StartNew();
            var firstPeerFound = Stopwatch.StartNew();
            var firstPeerConnected = Stopwatch.StartNew();
            //manager.PeerConnected += (o, e) => {
            //    if (!firstPeerConnected.IsRunning)
            //        return;

            //    firstPeerConnected.Stop();
            //    lock (times)
            //        times.Add(("First peer connected. Time since torrent started: ", firstPeerConnected.Elapsed));
            //};
            //manager.PeersFound += (o, e) => {
            //    if (!firstPeerFound.IsRunning)
            //        return;

            //    firstPeerFound.Stop();
            //    lock (times)
            //        times.Add(($"First peers found via {e.GetType().Name}. Time since torrent started: ", firstPeerFound.Elapsed));
            //};
            //manager.PieceHashed += (o, e) => {
            //    if (manager.State != TorrentState.Downloading)
            //        return;

            //    lock (times)
            //        times.Add(($"Piece {e.PieceIndex} hashed. Time since torrent started: ", overall.Elapsed));
            //};

            await manager.StartAsync();
            await manager.WaitForMetadataAsync(token);

            var largestFile = manager.Files.OrderByDescending(t => t.Length).First();

            // If we loaded no torrents, just exist. The user can put files in the torrents directory and start
            // the client again
            if (Engine.Torrents.Count == 0)
            {
                //Console.WriteLine($"No torrents found in '{torrentsPath}'");
                Console.WriteLine("Exiting...");
                return;
            }

            // For each torrent manager we loaded and stored in our list, hook into the events
            // in the torrent manager and start the engine.
            //foreach (TorrentManager manager in Engine.Torrents)
            //{
            //    manager.PeersFound += (o, e) => {
            //        Listener.WriteLine(string.Format($"{e.GetType().Name}: {e.NewPeers} peers for {e.TorrentManager.Name}"));
            //    };
            //    manager.PeerConnected += (o, e) => {
            //        lock (Listener)
            //            Listener.WriteLine($"Connection succeeded: {e.Peer.Uri}");
            //    };
            //    manager.ConnectionAttemptFailed += (o, e) => {
            //        lock (Listener)
            //            Listener.WriteLine(
            //                $"Connection failed: {e.Peer.ConnectionUri} - {e.Reason}");
            //    };
            //    // Every time a piece is hashed, this is fired.
            //    manager.PieceHashed += delegate (object o, PieceHashedEventArgs e) {
            //        lock (Listener)
            //            Listener.WriteLine($"Piece Hashed: {e.PieceIndex} - {(e.HashPassed ? "Pass" : "Fail")}");
            //    };

            //    // Every time the state changes (Stopped -> Seeding -> Downloading -> Hashing) this is fired
            //    manager.TorrentStateChanged += delegate (object o, TorrentStateChangedEventArgs e) {
            //        lock (Listener)
            //            Listener.WriteLine($"OldState: {e.OldState} NewState: {e.NewState}");
            //    };

            //    // Every time the tracker's state changes, this is fired
            //    manager.TrackerManager.AnnounceComplete += (sender, e) => {
            //        Listener.WriteLine($"{e.Successful}: {e.Tracker}");
            //    };

            //    // Start the torrentmanager. The file will then hash (if required) and begin downloading/seeding.
            //    // As EngineSettings.AutoSaveLoadDhtCache is enabled, any cached data will be loaded into the
            //    // Dht engine when the first torrent is started, enabling it to bootstrap more rapidly.
            //    await manager.StartAsync();
            //}

            //var stream = await manager.StreamProvider.CreateStreamAsync(largestFile, false);

            //// Read the middle
            //await TimedRead(manager, stream, stream.Length / 2, times, token);
            //// Then the start
            //await TimedRead(manager, stream, 0, times, token);
            //// Then the last piece
            //await TimedRead(manager, stream, stream.Length - 2, times, token);
            //// Then the 3rd last piece
            //await TimedRead(manager, stream, stream.Length - manager.Torrent.PieceLength * 3, times, token);
            //// Then the 5th piece
            //await TimedRead(manager, stream, manager.Torrent.PieceLength * 5, times, token);
            //// Then 1/3 of the way in
            //await TimedRead(manager, stream, stream.Length / 3, times, token);
            //// Then 2/3 of the way in
            //await TimedRead(manager, stream, stream.Length / 3 * 2, times, token);
            //// Then 1/5 of the way in
            //await TimedRead(manager, stream, stream.Length / 5, times, token);
            //// Then 4/5 of the way in
            //await TimedRead(manager, stream, stream.Length / 5 * 4, times, token);

            lock (times)
            {
                foreach (var (message, time) in times)
                    Console.WriteLine($"{message} {time.TotalSeconds:0.00} seconds");
            }

            //await manager.StopAsync();
        }

        async Task TimedRead(TorrentManager manager, Stream stream, long position, List<(string, TimeSpan)> times, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            stream.Seek(position, SeekOrigin.Begin);
            await stream.ReadExactlyAsync((new byte[1]).AsMemory(0, 1), token);
            lock (times)
                times.Add(($"Read piece: {manager.Torrent.ByteOffsetToPieceIndex(stream.Position - 1)}. Time since seeking: ", stopwatch.Elapsed));
        }
    }
}