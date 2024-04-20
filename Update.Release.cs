#if RELEASE
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BCUpdateUtilities;

public partial class Update
{
    public static async Task Prepare()
    {
        RecreateDirectory("temp");

        using (var httpClient = new HttpClient())
        {
            using var stream = await Config.Instance.DownloadFileStream("data.zip");

            await Task.Run(() =>
            {
                using var zip = new ZipArchive(stream);
                zip.ExtractToDirectory("temp");
            });
        }

        RecreateDirectory("backup");

        await Task.Run(() =>
        {
            var s = Path.DirectorySeparatorChar;

            Copy(
                sourceDirectory: ".",
                destinationDirectory: "backup",
                exclusions: [
                    $"{s}backup",
                    $"{s}temp"
                ]
            );
        });
    }

    public static void Publish(string publicFolder, string version)
    {
        var buildPath = Path.Combine(Environment.CurrentDirectory, "Build");

        if (!Directory.Exists(buildPath))
        {
            return;
        }

        Environment.CurrentDirectory = buildPath;

        foreach (var file in Directory.EnumerateFiles("wwwroot", "*.scss"))
        {
            DeleteFile(file);
        }

        if (publicFolder is null)
        {
            return;
        }

        if (File.Exists("data.zip"))
        {
            DeleteFile("data.zip");
        }

        using (var dataZip = ZipFile.Open("data.zip", ZipArchiveMode.Create))
        {
            var files = Directory.EnumerateFiles(".", "*.*", SearchOption.AllDirectories);

            foreach (var file in files.Select(x => x[2..]))
            {
                if (file == "data.zip")
                    continue;

                if (file.StartsWith("backup"))
                    continue;

                if (file.StartsWith("data"))
                    continue;

                if (file.StartsWith("logs"))
                    continue;

                dataZip.CreateEntryFromFile(file, file);
            }
        }

        if (version is not null)
        {
            MoveFile("data.zip", Path.Combine(publicFolder, "data.zip"));
            File.WriteAllText(Path.Combine(publicFolder, "version.txt"), version);
        }
    }

    static void RecreateDirectory(string name)
    {
        if (Directory.Exists(name))
            DeleteDirectory(name);

        CreateDirectory(name);
    }

    static void DeleteEmptyDirectories(string path)
    {
        var info = new DirectoryInfo(path);

        foreach (var sub in info.EnumerateDirectories())
            DeleteEmptyDirectories(sub.FullName);

        if (info.GetDirectories().Length == 0 && info.GetFiles().Length == 0)
            info.Delete();
    }

    static void Copy(string sourceDirectory, string destinationDirectory, string[] exclusions)
    {
        var searchOption = SearchOption.AllDirectories;

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", searchOption))
        {
            var _directory = directory[sourceDirectory.Length..];
            if (exclusions.Any(_directory.StartsWith))
            {
                Logger.Info($"Skipping: {directory}");
                continue;
            }

            _directory = destinationDirectory + _directory;
            if (!Directory.Exists(_directory))
                CreateDirectory(_directory);
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*.*", searchOption))
        {
            var _file = file[sourceDirectory.Length..];
            if (exclusions.Contains(_file))
            {
                Logger.Info($"Skipping: {file}");
                continue;
            }

            var _directory = Path.GetDirectoryName(_file);
            if (exclusions.Any(_directory.StartsWith))
            {
                Logger.Info($"Skipping: {file}");
                continue;
            }

            CopyFile(file, destinationDirectory + _file);
        }
    }

    static void CreateDirectory(string name)
    {
        Logger.Info($"Create Directory: {name}");
        Directory.CreateDirectory(name);
    }

    static void DeleteDirectory(string name)
    {
        Logger.Info($"Delete Directory: {name}");
        Directory.Delete(name, true);
    }

    static void CopyFile(string sourceFile, string destinationFile)
    {
        Logger.Info($"Copy File: {sourceFile} => {destinationFile}");
        File.Copy(sourceFile, destinationFile, true);
    }

    static void MoveFile(string sourceFile, string destinationFile)
    {
        var destinationDirectory = string.Join('\\', destinationFile.Split('\\').SkipLast(1));
        if (!Directory.Exists(destinationDirectory))
            CreateDirectory(destinationDirectory);

        Logger.Info($"Move File: {sourceFile} => {destinationFile}");
        File.Move(sourceFile, destinationFile, true);
    }

    static void DeleteFile(string name)
    {
        Logger.Info($"Delete File: {name}");
        File.Delete(name);
    }
}
#endif
