using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BCUpdateUtilities.Models;

public class Config
{
    static readonly Size defaultWindowSize = new(1120, 720);

    static readonly string PATH = Path.Combine("data", "config.json");

    public string downloadUrl = "https://steinalt.online/download/bc-update-utilities";

    public BC bc = new();

    public class BC
    {
        public const string NAV_PATH = @"C:\Program Files\Microsoft Dynamics 365 Business Central";

        public Config_RemoteMachine remoteMachine = new();

        public static void OverwriteBy(
            List<Config_Configuration.Entry.Info> entryInfos,
            List<Config_Configuration.Entry> otherEntries
        )
        {
            foreach (var entryInfo in entryInfos)
            {
                var oldItem = otherEntries.FirstOrDefault(x => x.key == entryInfo.entry.key);

                if (oldItem is null)
                {
                    continue;
                }

                var ignoreCase = StringComparison.InvariantCultureIgnoreCase;

                if (oldItem.value.Equals("false", ignoreCase))
                {
                    if (entryInfo.entry.value.Equals("false", ignoreCase))
                    {
                        continue;
                    }
                }

                if (oldItem.value.Equals("true", ignoreCase))
                {
                    if (entryInfo.entry.value.Equals("true", ignoreCase))
                    {
                        continue;
                    }
                }

                entryInfo.newValue = oldItem.value;
            }
        }
    }

    Config() { }

    static Config instance;
    public static Config Instance => instance ??= Load();

    static Config Load()
    {
        if (!File.Exists(PATH))
            return new();

        var json = File.ReadAllText(PATH);
        return json.FromJson<Config>();
    }

    public void Save()
    {
        var json = this.ToJson();
        Directory.CreateDirectory("data");
        File.WriteAllText(PATH, json);
    }

    string GetDownloadFileUrl(string file)
    {
        return $"{downloadUrl.TrimEnd('/')}/{file}";
    }

    public async Task<string> DownloadFileContent(string file)
    {
        var url = GetDownloadFileUrl(file);
        using var httpClient = new HttpClient();
        return await httpClient.GetStringAsync(url);
    }

    public async Task<Stream> DownloadFileStream(string file)
    {
        var url = GetDownloadFileUrl(file);
        using var httpClient = new HttpClient();
        return await httpClient.GetStreamAsync(url);
    }

    public async Task<bool> DownloadFile(string file)
    {
        var url = GetDownloadFileUrl(file);
        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return false;

        Directory.CreateDirectory("files");
        var path = Path.Combine("files", file);

        using var fileStream = new FileStream(path, FileMode.Create);

        await response.Content.CopyToAsync(fileStream);

        return true;
    }

    public Rectangle bounds;
    public bool maximized;

    public void ResetBounds()
    {
        bounds = default;
        maximized = false;

        SetupBounds();
    }

    public void SetupBounds()
    {
        if (bounds.Size.IsEmpty)
            bounds.Size = defaultWindowSize;
    }
}
