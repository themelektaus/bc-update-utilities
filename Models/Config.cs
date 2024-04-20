using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BCUpdateUtilities.Models;

public class Config
{
    static readonly string PATH = Path.Combine("data", "config.json");

    static Config instance;
    public static Config Instance => instance ??= Load();

    public Rectangle? bounds;
    public bool maximized;

    public string downloadUrl = "https://steinalt.online/download/bc-update-utilities";

    public class BC
    {
        public class RemoteMachine
        {
            public string hostname = "localhost";
            public string username = string.Empty;
            public string password = string.Empty;

            public Naos.MachineManager CreateManager()
            {
                return new(hostname, username, password);
            }
        }
        public RemoteMachine remoteMachine = new();

        public string oldInstanceName = "BC190";
        public string newInstanceName = "BC220";
    }
    public BC bc = new();

    Config() { }

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
}
