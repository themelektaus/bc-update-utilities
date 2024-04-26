using Microsoft.AspNetCore.Components;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using System.Threading.Tasks;

namespace BCUpdateUtilities;

public class PowerShellSession : IDisposable
{
    static readonly object @lock = new();

    const string WSMAN_PATH = @"WSMan:\localhost\Client\TrustedHosts";

    public struct Result
    {
        public List<ErrorRecord> errors;
        public List<PSObject> returnValue;
        public Exception exception;

        public bool HasErrors => errors.Count > 0 || exception is not null;

        public static Result Invoke(PowerShell powershell)
        {
            Collection<PSObject> returnValue = default;
            Exception exception;

            try
            {
                returnValue = powershell.Invoke();
                exception = null;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            var result = new Result
            {
                returnValue = [.. returnValue ?? []],
                errors = [.. powershell.Streams.Error],
                exception = exception,
            };

            result.LogResult();
            result.LogErrors();

            return result;
        }

        void LogResult()
        {
            var stringTypeName = "System.String";

            foreach (var value in returnValue.Where(x => x.TypeNames.Contains(stringTypeName)))
            {
                Logger.Result(value.ToString());
            }
        }

        void LogErrors()
        {
            foreach (var error in errors)
            {
                if (error.ErrorDetails?.Message is not null)
                {
                    Logger.Error(error.ErrorDetails.Message);
                }
                else if (error.Exception is not null)
                {
                    Logger.Exception(error.Exception);
                }
            }

            if (exception is not null)
            {
                Logger.Exception(exception);
            }
        }
    }

    readonly Runspace runspace;

    public string Hostname { get; private set; }
    public string Username { get; private set; }

    public string NavAdminTool { get; private set; }

    object session;

    public bool HasSession => session is not null;

    public PowerShellSession()
    {
        runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();
    }

    public void Dispose()
    {
        if (HasSession)
        {
            EndSession();
        }

        runspace.Close();
    }

    public MarkupString ToMarkupString()
    {
        if (session is null)
        {
            return new("-");
        }

        return new($"<span style=\"opacity: .75\">{Username ?? "guest"}@</span>{Hostname}");
    }

    public bool BeginSession(string hostname, string username, string password, string navAdminTool)
    {
        Logger.Pending("Starting Session");

        if (HasSession)
        {
            Logger.Warning("There is already an active session");
            return false;
        }

        Hostname = hostname;
        Username = username;

        NavAdminTool = navAdminTool;

        AddTrustedHost();

        try
        {
            Command command;
            CommandParameterCollection parameters;

            command = new("New-PSSessionOption");
            parameters = command.Parameters;
            parameters.Add("OperationTimeout", 0);
            parameters.Add("IdleTimeout", 1200000);

            var sessionOption = RunCommandInternal(command).returnValue.Single().BaseObject;

            var securePassword = new SecureString();
            foreach (var character in password.ToCharArray())
                securePassword.AppendChar(character);
            securePassword.MakeReadOnly();

            command = new("New-PSSession");
            parameters = command.Parameters;
            parameters.Add("ComputerName", Hostname);
            parameters.Add("Credential", new PSCredential(username, securePassword));
            parameters.Add("SessionOption", sessionOption);

            session = RunCommandInternal(command).returnValue.Single().BaseObject;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex);
            return false;
        }

        if (session is null)
        {
            return false;
        }

        if (navAdminTool != string.Empty)
        {
            RunScript($@". ""{navAdminTool}""");
        }

        Logger.Success("Session started");

        return true;
    }

    public void EndSession()
    {
        if (!HasSession)
        {
            return;
        }

        RemoveTrustedHost();

        var sessionCommand = new Command("Remove-PSSession");
        sessionCommand.Parameters.Add("Session", session);

        RunCommandInternal(sessionCommand);

        Hostname = null;
        Username = null;

        NavAdminTool = null;

        session = null;

        Logger.Info("Session closed");
    }

    bool AddTrustedHost()
    {
        var trustedHosts = GetTrustedHosts();

        if (trustedHosts.Contains(Hostname))
        {
            return true;
        }

        if (trustedHosts.FirstOrDefault() == "*")
        {
            return true;
        }

        var value = trustedHosts.Count == 0
            ? Hostname
            : string.Join(',', trustedHosts);

        var command = new Command("Set-Item");
        command.Parameters.Add("Path", WSMAN_PATH);
        command.Parameters.Add("Value", value);
        command.Parameters.Add("Force", true);

        var result = RunCommandInternal(command);
        if (result.HasErrors)
        {
            return false;
        }

        trustedHosts.Add(Hostname);
        return true;
    }

    bool RemoveTrustedHost()
    {
        var trustedHosts = GetTrustedHosts();

        if (!trustedHosts.Contains(Hostname))
        {
            return false;
        }

        var newValue = trustedHosts.Count == 0
            ? string.Empty
            : string.Join(',', trustedHosts);

        var command = new Command("Set-Item");
        command.Parameters.Add("Path", WSMAN_PATH);
        command.Parameters.Add("Value", newValue);
        command.Parameters.Add("Force", true);

        var result = RunCommandInternal(command);
        if (result.HasErrors)
        {
            return false;
        }

        trustedHosts.Remove(Hostname);
        return true;
    }

    List<string> GetTrustedHosts()
    {
        var command = new Command("Get-Item");
        command.Parameters.Add("Path", WSMAN_PATH);
        return RunCommandInternal(command)
            .returnValue
            .FirstOrDefault()?
            .Properties
            .Single(x => x.Name == "Value")?
            .Value
            .ToString()?
            .Split(',')
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList() ?? [];
    }

    public struct Script
    {
        public static implicit operator Script(string @this)
        {
            return new() { text = @this };
        }

        public string text;

        public Options options;

        public struct Options
        {
            public object[] sensitiveArgs;
            public object[] argumentList;
            public bool convertToJson;
        }
    }

    public Task<Result> RunScriptAsync(
        string scriptText,
        Script.Options scriptOptions
    )
    {
        return RunScriptAsync(new()
        {
            text = scriptText,
            options = scriptOptions
        });
    }

    public async Task<Result> RunScriptAsync(Script script)
    {
        return await Task.Run(() => RunScript(script));
    }

    public async Task<List<PSObject>> GetObjectListAsync(Script script)
    {
        return (await RunScriptAsync(script)).returnValue;
    }

    public async Task<PSObject> GetObjectAsync(Script script)
    {
        return (await GetObjectListAsync(script)).FirstOrDefault();
    }

    Result RunScript(Script script)
    {
        var scriptText = script.text;

        Logger.Script(scriptText);

        if (script.options.sensitiveArgs?.Length > 0)
        {
            scriptText = string.Format(scriptText, script.options.sensitiveArgs);
        }

        lock (@lock)
        {
            using var powershell = CreateShell();

            var scriptBlock = $"{{ {scriptText} }}";

            var args =
                $" -Session ${nameof(session)}" +
                $" -ScriptBlock ${nameof(scriptBlock)}";

            var proxy = powershell.Runspace.SessionStateProxy;
            proxy.SetVariable(nameof(session), session);

            var argumentList = script.options.argumentList;
            if (argumentList?.Length > 0)
            {
                args += $" -ArgumentList ${nameof(argumentList)}";
                proxy.SetVariable(nameof(argumentList), argumentList);
            }

            var suffix = script.options.convertToJson ? "| ConvertTo-Json -Compress" : "";

            powershell.AddScript(
                $"${nameof(scriptBlock)} = {scriptBlock}\r\n" +
                $"Invoke-Command {args} {suffix}"
            );

            return Result.Invoke(powershell);
        }
    }

    PowerShell CreateShell()
    {
        var powershell = PowerShell.Create();
        powershell.Runspace = runspace;
        return powershell;
    }

    Result RunCommandInternal(Command command)
    {
        using var powershell = CreateShell();

        powershell.Commands.AddCommand(command);

        return Result.Invoke(powershell);
    }

}
