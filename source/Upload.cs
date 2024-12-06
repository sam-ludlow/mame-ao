using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace mame_ao.source
{
    public class Upload
    {
        public static readonly DateTime MagicModifiedDate = new DateTime(1996, 12, 24, 23, 32, 0, DateTimeKind.Utc);

        private static string ApiAuth;

        private static readonly string MANIFEST_NAME = "_manifest-sha1.txt";
        private static readonly string MANIFEST_KEY = Path.GetFileNameWithoutExtension(MANIFEST_NAME);

        static Upload()
        {
            string authFilename = Path.Combine(Globals.RootDirectory, "_api-auth.txt");

            if (File.Exists(authFilename) == false)
                throw new ApplicationException($"API Auth file missing:'{authFilename}'. Format: 'LOW <Your_S3_access_key>:<Your_S3_secret_key>'");

            ApiAuth = File.ReadAllText(authFilename);
        }

        public static async Task MachineRomAsync(string itemName, int batchSize)
        {
            ArchiveOrgItem item = new ArchiveOrgItem(itemName, null, null);
            item.DontCache = true;
            item.GetFile(null);

            dynamic metadata = JsonConvert.DeserializeObject<dynamic>(Tools.Query(item.UrlMetadata));
            if (metadata.conflict != null)
                throw new ApplicationException("Archive.org item status is 'conflict' try again later.");

            DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name, cloneof, description FROM machine ORDER BY machine.name");
            DataTable romTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");

            machineTable.PrimaryKey = new DataColumn[] { machineTable.Columns["name"] };

            DataTable report = Tools.MakeDataTable("Parent Machine ZIP Files",
                "status	action	name	description	sha1",
                "String	String	String	String		String");

            Dictionary<string, string> fileManifestSHA1s = new Dictionary<string, string>();

            ArchiveOrgFile manifestFile = item.GetFile(MANIFEST_KEY);
            if (manifestFile != null)
            {
                using (StringReader reader = new StringReader(Tools.Query(item.DownloadLink(manifestFile))))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split('\t');
                        fileManifestSHA1s.Add(parts[0], parts[1]);
                    }
                }
            }

            List<string> parentNames = new List<string>();

            foreach (DataRow parentMachineRow in machineTable.Select("cloneof IS NULL"))
            {
                Dictionary<string, string> nameHashes = GetRomNameHashes(parentMachineRow, machineTable, romTable);

                if (nameHashes.Count == 0)
                    continue;

                string name = (string)parentMachineRow["name"];
                string description = (string)parentMachineRow["description"];

                parentNames.Add(name);

                string manifest = CreateRomManifest(nameHashes);
                string manifestSha1 = Tools.SHA1HexText(manifest, Encoding.ASCII);

                DataRow reportRow = report.Rows.Add("", "", name, description, manifestSha1);

                bool noHave = false;
                foreach (string key in nameHashes.Keys)
                    if (Globals.RomHashStore.Exists(nameHashes[key]) == false)
                    {
                        noHave = true;
                        break;
                    }

                if (noHave == true)
                {
                    reportRow["Status"] = "NOHAVE";
                    continue;
                }

                if (item.Files.ContainsKey(name) == false)
                {
                    reportRow["Status"] = "CREATE";
                    continue;
                }

                ArchiveOrgFile file = item.Files[name];

                if (fileManifestSHA1s.ContainsKey(file.sha1) == false)
                {
                    reportRow["Status"] = "MISSING";
                    continue;
                }

                string remoteManifestSha1 = fileManifestSHA1s[file.sha1];

                if (manifestSha1 != remoteManifestSha1)
                {
                    reportRow["Status"] = "REPLACE";
                    continue;
                }

                reportRow["Status"] = "OK";
            }

            foreach (string name in item.Files.Keys)
            {
                if (name != MANIFEST_KEY && parentNames.Contains(name) == false)
                    report.Rows.Add("DELETE", "", name);
            }

            string[] headings = new string[] { "CREATE", "REPLACE", "DELETE", "MISSING", "OK", "NOHAVE" };

            List<DataView> views = new List<DataView>();
            foreach (string heading in headings)
                views.Add(new DataView(report, $"Status = '{heading}'", null, DataViewRowState.CurrentRows));

            Globals.Reports.SaveHtmlReport(views.ToArray(), headings, report.TableName);

            if (batchSize > 0)
            {
                try
                {
                    int count = 0;

                    foreach (string action in new string[] { "CREATE", "UPDATE" })
                    {
                        foreach (DataRow reportRow in report.Select($"Status = '{action}'"))
                        {
                            string machneName = (string)reportRow["name"];
                            DataRow machineRow = machineTable.Rows.Find(machneName);

                            Dictionary<string, string> nameHashes = GetRomNameHashes(machineRow, machineTable, romTable);

                            using (TempDirectory tempDir = new TempDirectory())
                            {
                                string targetFilename = Path.Combine(tempDir.Path, machneName + ".zip");
                                string tempDirectory = Path.Combine(tempDir.Path, machneName);

                                CreateRomArchive(nameHashes, Globals.RomHashStore, tempDirectory, targetFilename);

                                string fileSha1 = Tools.SHA1HexFile(targetFilename);

                                string manifest = CreateRomManifest(nameHashes);
                                string manifestSha1 = Tools.SHA1HexText(manifest, Encoding.ASCII);

                                await UploadFile(itemName, targetFilename);

                                if (fileManifestSHA1s.ContainsKey(fileSha1) == false)
                                    fileManifestSHA1s.Add(fileSha1, manifestSha1);
                                else
                                    if (fileManifestSHA1s[fileSha1] != manifestSha1)
                                    throw new ApplicationException($"Manifest SHA1 Missmatch fileSha1:{fileSha1}.");
                            }

                            reportRow["action"] = "PUT";

                            if (++count >= batchSize)
                                break;
                        }
                    }

                    foreach (string action in new string[] { "DELETE", "MISSING" })
                    {
                        foreach (DataRow reportRow in report.Select($"Status = '{action}'"))
                        {
                            string machneName = (string)reportRow["name"];
                            DeleteFile(itemName, $"{machneName}.zip");

                            reportRow["action"] = "DELETE";
                        }
                    }
                }
                finally
                {
                    using (TempDirectory tempDir = new TempDirectory())
                    {
                        string filename = Path.Combine(tempDir.Path, MANIFEST_NAME);

                        using (StreamWriter writer = new StreamWriter(filename, false))
                            foreach (string key in fileManifestSHA1s.Keys)
                                writer.WriteLine($"{key}\t{fileManifestSHA1s[key]}");

                        UploadFile(itemName, filename);
                    }

                    Globals.Reports.SaveHtmlReport(views.ToArray(), headings, report.TableName + " - Actions");
                }
            }
        }


        public static void MachineRomExport(string targetDirectory)
        {
            DataTable machineTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, name, cloneof, description FROM machine ORDER BY machine.name");
            DataTable romTable = Database.ExecuteFill(Globals.Database._MachineConnection, "SELECT machine_id, sha1, name, merge FROM rom WHERE sha1 IS NOT NULL");

            DataTable report = Tools.MakeDataTable("Machine manifests",
                "name	sha1",
                "String	String");

            string hashLookupFilename = Path.Combine(targetDirectory, MANIFEST_NAME);

            Dictionary<string, string> fileManifestSHA1s = new Dictionary<string, string>();

            using (TempDirectory tempDir = new TempDirectory())
            {
                foreach (DataRow parentMachineRow in machineTable.Select("cloneof IS NULL"))
                {
                    string parent_machine_name = (string)parentMachineRow["name"];

                    Dictionary<string, string> nameHashes = GetRomNameHashes(parentMachineRow, machineTable, romTable);

                    if (nameHashes.Count == 0)
                        continue;

                    Console.WriteLine(parent_machine_name);

                    bool missing = false;
                    foreach (string name in nameHashes.Keys)
                        if (Globals.RomHashStore.Exists(nameHashes[name]) == false)
                        {
                            missing = true;
                            break;
                        }
                    if (missing == true)
                        continue;

                    string manifest = CreateRomManifest(nameHashes);
                    string manifestSha1 = Tools.SHA1HexText(manifest, Encoding.ASCII);

                    report.Rows.Add(parent_machine_name, manifestSha1);

                    string tempDirectory = Path.Combine(tempDir.Path, parent_machine_name);
                    string targetFilename = Path.Combine(targetDirectory, parent_machine_name + ".zip");

                    CreateRomArchive(nameHashes, Globals.RomHashStore, tempDirectory, targetFilename);

                    File.WriteAllText(Path.Combine(targetDirectory, parent_machine_name + ".txt"), manifest, Encoding.ASCII);

                    string fileSha1 = Tools.SHA1HexFile(targetFilename);

                    string inspectManifest = InspectRomManifest(targetFilename);
                    string inspectManifestSha1 = Tools.SHA1HexText(inspectManifest, Encoding.ASCII);

                    if (manifestSha1 != inspectManifestSha1)
                        throw new ApplicationException($"Bad Minifest Inspection: ${targetFilename}");

                    if (fileManifestSHA1s.ContainsKey(fileSha1) == false)
                        fileManifestSHA1s.Add(fileSha1, manifestSha1);
                    else
                        if (fileManifestSHA1s[fileSha1] != manifestSha1)
                        throw new ApplicationException($"Manifest SHA1 Missmatch fileSha1:{fileSha1}.");
                }
            }

            using (StreamWriter writer = new StreamWriter(hashLookupFilename, false))
                foreach (string key in fileManifestSHA1s.Keys)
                    writer.WriteLine($"{key}\t{fileManifestSHA1s[key]}");

            Globals.Reports.SaveHtmlReport(report, "Machine manifests");
        }

        public static void SoftwareDisk(string itemName, int batchSize, string softwareListName)
        {
            //string targetDirectory = @"D:\TMP";

            DataTable softwareTable = Database.ExecuteFill(Globals.Database._SoftwareConnection,
                "SELECT softwarelist.name AS softwarelist_name, software.name, software.cloneof, software.description, software.software_id " +
                "FROM softwarelist INNER JOIN software ON softwarelist.softwarelist_id = software.softwarelist_id " +
                $"WHERE (softwarelist.name = '{softwareListName}') ORDER BY softwarelist.name, software.name");

            DataTable diskTable = Database.ExecuteFill(Globals.Database._SoftwareConnection,
                "SELECT part.software_id, disk.name, disk.sha1 " +
                "FROM (part INNER JOIN diskarea ON part.part_id = diskarea.part_id) INNER JOIN disk ON diskarea.diskarea_id = disk.diskarea_id " +
                "WHERE (disk.sha1 IS NOT NULL)");

            DataTable report = Tools.MakeDataTable("Software Disk Export",
                "name	cloneof	description	rom_name	sha1	in_parent	have",
                "String	String	String		String		String	Boolean		Boolean");

            HashSet<string> hashes = new HashSet<string>();

            for (int pass = 0; pass < 2; ++pass)
            {
                foreach (DataRow softwareRow in softwareTable.Select(pass == 0 ? "cloneof IS NULL" : "cloneof IS NOT NULL"))
                {
                    long software_id = (long)softwareRow["software_id"];
                    string software_name = (string)softwareRow["name"];
                    string cloneof = pass == 0 ? "" : (string)softwareRow["cloneof"];
                    string description = (string)softwareRow["description"];

                    foreach (DataRow diskRow in diskTable.Select($"software_id = {software_id}"))
                    {
                        string name = (string)diskRow["name"];
                        string sha1 = (string)diskRow["sha1"];

                        bool inParent = hashes.Add(sha1) == false;
                        bool have = Globals.DiskHashStore.Exists(sha1);

                        report.Rows.Add(software_name, cloneof, description, name, sha1, inParent, have);
                    }

                }
            }

            Globals.Reports.SaveHtmlReport(report, report.TableName);
        }

        public static void CreateRomArchive(Dictionary<string, string> nameHashes, HashStore hashStore, string tempDirectory, string targetFilename)
        {
            HashSet<string> directories = new HashSet<string>();

            for (int pass = 0; pass < 2; ++pass)
            {
                foreach (string name in nameHashes.Keys)
                {
                    List<string> pathParts = new List<string>(name.Split('/'));
                    pathParts.Insert(0, tempDirectory);
                    string tempFilename = Path.Combine(pathParts.ToArray());

                    if (pass == 0)
                    {
                        string directory = Path.GetDirectoryName(tempFilename);
                        if (directories.Add(directory) == true)
                            Directory.CreateDirectory(directory);
                    }
                    else
                    {
                        File.Copy(hashStore.Filename(nameHashes[name]), tempFilename);
                    }
                }
            }

            foreach (string tempFilename in Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories))
                File.SetLastWriteTimeUtc(tempFilename, MagicModifiedDate);

            ZipFile.CreateFromDirectory(tempDirectory, targetFilename);
            Directory.Delete(tempDirectory, true);
        }

        public static Dictionary<string, string> GetRomNameHashes(DataRow parentMachineRow, DataTable machineTable, DataTable romTable)
        {
            string parent_machine_name = (string)parentMachineRow["name"];

            Dictionary<string, string> nameHashes = new Dictionary<string, string>();

            GetRomNameHashes(nameHashes, parentMachineRow, romTable);

            foreach (DataRow childMachineRow in machineTable.Select($"cloneof = '{parent_machine_name}'"))
            {
                string child_machine_name = (string)childMachineRow["name"];

                GetRomNameHashes(nameHashes, childMachineRow, romTable);
            }

            return nameHashes;
        }

        private static Dictionary<string, string> GetRomNameHashes(Dictionary<string, string> nameHashes, DataRow machineRow, DataTable romTable)
        {
            long machine_id = (long)machineRow["machine_id"];
            string machine_name = (string)machineRow["name"];
            bool isParent = machineRow.IsNull("cloneof");

            foreach (DataRow romRow in romTable.Select($"machine_id = {machine_id}"))
            {
                if (romRow.IsNull("merge") == false)
                    continue;

                string sha1 = (string)romRow["sha1"];
                string name = (string)romRow["name"];

                if (isParent == false)
                    name = $"{machine_name}/{name}";

                if (nameHashes.ContainsKey(name) == true)
                {
                    if (nameHashes[name] != sha1)
                        throw new ApplicationException($"ROM name sha1 mismatch, machine_id:{machine_id}, rom name:{name}.");
                    continue;
                }

                nameHashes.Add(name, sha1);
            }

            return nameHashes;
        }

        public static string InspectRomManifest(string zipFilename)
        {
            Dictionary<string, string> nameHashes = new Dictionary<string, string>();

            using (TempDirectory tempDir = new TempDirectory())
            {
                ZipFile.ExtractToDirectory(zipFilename, tempDir.Path);

                Tools.ClearAttributes(tempDir.Path);

                foreach (string filename in Directory.GetFiles(tempDir.Path, "*", SearchOption.AllDirectories))
                {
                    string name = filename.Substring(tempDir.Path.Length + 1).Replace('\\', '/');
                    string sha1 = Tools.SHA1HexFile(filename);

                    nameHashes.Add(name, sha1);
                }
            }

            return CreateRomManifest(nameHashes);
        }

        public static string CreateRomManifest(Dictionary<string, string> nameHashes)
        {
            StringBuilder manifest = new StringBuilder();
            foreach (string name in nameHashes.Keys.OrderBy(i => i))
            {
                manifest.Append(name);
                manifest.Append("\t");
                manifest.Append(nameHashes[name]);
                manifest.Append("\r\n");
            }
            return manifest.ToString();
        }

        public static async Task<string> UploadFile(string itemName, string filePath)
        {
            string url = $"https://s3.us.archive.org/{Uri.EscapeUriString(itemName)}/{Uri.EscapeUriString(Path.GetFileName(filePath))}";
            //string filePath = @"C:\path\to\file"; // Replace with your file path
            FileInfo fileInfo = new FileInfo(filePath);

            lock (Globals.WorkerTaskInfo)
                Globals.WorkerTaskInfo.BytesTotal = fileInfo.Length;

            long total = 0;
            byte[] buffer = new byte[64 * 1024];

            using (HttpClient client = new())
            {
                client.Timeout = TimeSpan.FromMilliseconds(3 * 60 * 60 * 1000);
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Put, url);
                //requestMessage.AllowWriteStreamBuffering = true;   //	TEST BOTH
                requestMessage.Headers.Add("authorization", ApiAuth);
                requestMessage.Headers.Add("x-archive-size-hint", new FileInfo(filePath).Length.ToString());
                requestMessage.Headers.Add("x-archive-queue-derive", "0");
                //HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    requestMessage.Content = new StreamContent(fileStream);
                    requestMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    requestMessage.Content.Headers.ContentLength = fileStream.Length; // Send the request
                    HttpResponseMessage responseMessage = await client.SendAsync(requestMessage);
                    responseMessage.EnsureSuccessStatusCode();
                    Console.WriteLine("File uploaded successfully.");
                    using (Stream targetStream = await client.GetStreamAsync(filePath))
                    {
                        {
                            using (FileStream sourceStream = new FileStream(filePath, FileMode.Open))
                            {
                                int bytesRead;
                                long progress = 0;
                                while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    total += bytesRead;
                                    targetStream.Write(buffer, 0, bytesRead);

                                    progress += bytesRead;
                                    if (progress >= Globals.DownloadDotSize)
                                    {
                                        Console.Write(".");
                                        progress = 0;
                                    }

                                    lock (Globals.WorkerTaskInfo)
                                        Globals.WorkerTaskInfo.BytesCurrent = total;
                                }
                            }
                        }
                    }
                }


                Console.Write($"Uploading size:{Tools.DataSize(fileInfo.Length)} {url} ...");


                Console.WriteLine("...done.");

                using (var response = await client.GetAsync(filePath))
                {
                    {
                        int statusCode = (int)response.StatusCode;

                        if (statusCode < 200 || statusCode >= 300)
                            throw new ApplicationException($"Bad status code:{statusCode}, url:{url}");
                        string responseBody = await response.Content.ReadAsStringAsync();
                        return responseBody;
                    }
                }
            }
        }


        public static string DeleteFile(string itemName, string filename)
        {
            string url = $"https://s3.us.archive.org/{Uri.EscapeUriString(itemName)}/{Uri.EscapeUriString(Path.GetFileName(filename))}";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "DELETE";

            request.Headers.Add("authorization", ApiAuth);

            Console.Write($"Delete '{url}' ...");

            using (WebResponse response = request.GetResponse())
            {
                HttpWebResponse httpResponse = (HttpWebResponse)response;

                int statusCode = (int)httpResponse.StatusCode;

                if (statusCode < 200 || statusCode >= 300)
                    throw new ApplicationException($"Bad status code:{statusCode}, url:{url}");

                using (Stream responseStream = response.GetResponseStream())
                {
                    Console.WriteLine("...done.");

                    using (StreamReader reader = new StreamReader(responseStream))
                        return reader.ReadToEnd();
                }
            }
        }

    }
}
