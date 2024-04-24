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

    public class BC
    {
        public class RemoteMachine
        {
            public string hostname = "localhost";
            public string username = string.Empty;
            public string password = string.Empty;
        }
        public RemoteMachine remoteMachine = new();

        public string oldInstanceName = "BC190";
        public string newInstanceName = "BC220";
    }
    public BC bc = new();

    public class MSSQL
    {
        public string hostname = "localhost";
        public int port = 1433;
        public bool integratedSecurity = true;
        public string username = "sa";
        public string password = "";
        public string database = "";

        public string CreateScriptBlock(
            string command,
            bool selectName = false,
            bool useCredentialArg = false,
            bool addDatabaseArg = false
        )
        {
            var scriptBlock = new System.Text.StringBuilder();

            if (integratedSecurity)
            {
                scriptBlock
                    .Append(command);
            }
            else
            {
                if (useCredentialArg)
                {
                    scriptBlock
                        .AppendLine($"$password = ConvertTo-SecureString \"{password}\" -AsPlainText -Force")
                        .AppendLine($"$credential = New-Object System.Management.Automation.PSCredential(\"{username}\", $password)")
                        .Append(command)
                        .Append($" -Credential $credential");
                }
                else
                {
                    scriptBlock
                        .Append(command)
                        .Append($" -Username \"{username}\"")
                        .Append($" -Password \"{password}\"");
                }
            }

            scriptBlock.Append($" -ServerInstance \"{hostname},{port}\"");

            if (addDatabaseArg)
                scriptBlock.Append($" -Database \"{database}\"");

            if (selectName)
                scriptBlock.Append(" | Select-Object Name");

            return $"{{ {scriptBlock} }}";
        }
    }
    public MSSQL mssql = new();

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
