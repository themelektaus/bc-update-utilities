using Newtonsoft.Json;
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

        public RemoteMachine remoteMachine = new();

        public class RemoteMachine
        {
            public string Hostname { get; set; } = "localhost";
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;

            public List<string> navAdminTools = [];

            public ServerInstance oldServerInstance = new();
            public ServerInstance newServerInstance = new();

            public SqlServer sqlServer = new();

            public RemoteMachine()
            {
                oldServerInstance.remoteMachine = this;
                newServerInstance.remoteMachine = this;

                sqlServer.remoteMachine = this;
            }

            public async Task<PowerShellSession> GetSessionAsync()
            {
                return await PowerShellSessionManager.GetSessionAsync(this, string.Empty);
            }

            public async Task FetchNavAdminToolsAsync()
            {
                navAdminTools.Clear();

                var session = await GetSessionAsync();

                var items = await session.GetObjectListAsync(
                    $@"Get-ChildItem -Path ""{NAV_PATH}"" -Recurse -Filter ""NavAdminTool.ps1"""
                );

                foreach (dynamic item in items)
                {
                    navAdminTools.Add(item.FullName);
                }
            }

            public class ServerInstance
            {
                [JsonIgnore] public RemoteMachine remoteMachine;

                public string name = string.Empty;
                public string navAdminTool = string.Empty;

                public List<ConfigEntry> configuration = [];

                [JsonConverter(typeof(ConfigEntryJsonConverter))]
                public class ConfigEntry
                {
                    public string key;
                    public string value;

                    class ConfigEntryJsonConverter : JsonConverter<ConfigEntry>
                    {
                        public override ConfigEntry ReadJson(JsonReader reader, Type objectType, ConfigEntry existingValue, bool hasExistingValue, JsonSerializer _)
                        {
                            var x = (reader.Value as string).Split('=', 2);
                            return new() { key = x[0], value = x[1] };
                        }

                        public override void WriteJson(JsonWriter writer, ConfigEntry value, JsonSerializer _)
                            => writer.WriteValue($"{value.key}={value.value}");
                    }
                }

                public List<string> allNames = [];
                public List<string> names = [];

                public async Task<PowerShellSession> GetNavSessionAsync()
                {
                    if (navAdminTool == string.Empty)
                    {
                        return null;
                    }

                    return await PowerShellSessionManager.GetSessionAsync(remoteMachine, navAdminTool);
                }

                public async Task FetchServerInstanceNamesAsync()
                {
                    allNames.Clear();
                    names.Clear();

                    var navSession = await GetNavSessionAsync();

                    if (navSession is not null)
                    {
                        var result = await navSession.RunScriptAsync("Get-NAVServerInstance");

                        allNames = result.returnValue
                            .Select(x => (x as dynamic).ServerInstance as string)
                            .Select(x => x.Split('$', 2).LastOrDefault())
                            .ToList();

                        foreach (var name in allNames)
                        {
                            result = await navSession.RunScriptAsync($"Get-NAVApplication -ServerInstance \"{name}\"");

                            if (result.HasErrors)
                            {
                                var message = result.errors.FirstOrDefault().Exception?.Message ?? string.Empty;
                                if (!message.Contains("is not running"))
                                {
                                    continue;
                                }
                            }

                            names.Add(name);
                        }
                    }
                }
            }

            public class SqlServer
            {
                [JsonIgnore] public RemoteMachine remoteMachine;

                public string hostname = "localhost";
                public int port = 1433;
                public bool integratedSecurity = true;
                public string username = "sa";
                public string password = "";
                public string oldDatabase = "";
                public string newDatabase = "";

                public List<string> databaseNames = [];
                public string dataFolder;
                public List<string> backupFileNames = [];

                public PowerShellSession.Script GenerateScript(
                    string text,
                    string suffix = "",
                    bool useCredentialArg = false
                )
                {
                    var scriptTextBuilder = new System.Text.StringBuilder();
                    object[] sensitiveArgs;

                    if (integratedSecurity)
                    {
                        scriptTextBuilder.Append(text);
                        sensitiveArgs = null;
                    }
                    else
                    {
                        sensitiveArgs = [password];

                        if (useCredentialArg)
                        {
                            scriptTextBuilder
                                .AppendLine("$password = ConvertTo-SecureString \"{0}\" -AsPlainText -Force")
                                .AppendLine("$password.MakeReadOnly()")
                                .AppendLine($"$credential = New-Object System.Management.Automation.PSCredential(\"{username}\", $password)")
                                .Append(text)
                                .Append($" -Credential $credential");
                        }
                        else
                        {
                            scriptTextBuilder
                                .Append(text)
                                .Append($" -Username \"{username}\"")
                                .Append(" -Password \"{0}\"");
                        }
                    }

                    scriptTextBuilder.Append($" -ServerInstance \"{hostname},{port}\"");
                    scriptTextBuilder.Append($" -TrustServerCertificate");
                    scriptTextBuilder.Append(suffix);

                    return new()
                    {
                        text = scriptTextBuilder.ToString(),
                        options = new() { sensitiveArgs = sensitiveArgs }
                    };
                }

                public async Task FetchDatabaseNames()
                {
                    databaseNames.Clear();

                    var session = await remoteMachine.GetSessionAsync();

                    var script = GenerateScript(
                        text: "Get-SqlDatabase",
                        suffix: " | Select Name",
                        useCredentialArg: true
                    );

                    var result = await session.RunScriptAsync(script);

                    if (!result.HasErrors)
                    {
                        databaseNames.AddRange(
                            result.returnValue.Select(x => (x as dynamic).Name as string)
                        );

                        if (!databaseNames.Contains(oldDatabase))
                        {
                            oldDatabase = string.Empty;
                        }
                    }
                }

                public async Task FetchBackupFilesAsync()
                {
                    this.dataFolder = null;
                    backupFileNames.Clear();

                    var session = await remoteMachine.GetSessionAsync();

                    var script = GenerateScript(
                        "Invoke-Sqlcmd -Query "
                            + "\""
                            + "SELECT [filename] FROM [master].[sys].[sysfiles] "
                            + "WHERE [name] = 'master'"
                            + "\""
                    );

                    var result = await session.RunScriptAsync(script);

                    var firstValue = result.returnValue?.FirstOrDefault();

                    string dataFolder = null;

                    if (firstValue is not null)
                    {
                        string filename = (firstValue as dynamic).filename;

                        if (filename is not null)
                        {
                            dataFolder = new FileInfo(filename).DirectoryName;

                            if (oldDatabase != string.Empty)
                            {
                                script = GenerateScript(
                                    $"Invoke-Sqlcmd -Query \"EXEC xp_dirtree '{dataFolder}', 2, 1\""
                                );

                                result = await session.RunScriptAsync(script);

                                foreach (var @object in result.returnValue)
                                {
                                    var x = @object as dynamic;
                                    if (x.file == 1)
                                    {
                                        string fileName = x.subdirectory;
                                        if (fileName.StartsWith(oldDatabase) && fileName.EndsWith(".bak"))
                                        {
                                            backupFileNames.Add(fileName);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    this.dataFolder = dataFolder;
                }
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
