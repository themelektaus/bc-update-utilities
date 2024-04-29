using Newtonsoft.Json;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BCUpdateUtilities.Models;

public class Config_SqlServer
{
    [JsonIgnore] public Config_RemoteMachine remoteMachine;

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

    public async Task FetchDatabaseNamesAsync()
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
