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
    public struct Result
    {
        public List<ErrorRecord> errors;
        public List<PSObject> returnValue;
        public Exception exception;

        public bool HasErrors() => errors.Count > 0 || exception is not null;

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

            //result.LogResult();
            result.LogErrors();

            return result;
        }

        public void LogErrors()
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

        public void LogResult()
        {
            if (returnValue.Count > 0)
            {
                var stringResult = new System.Text.StringBuilder();

                foreach (var value in returnValue)
                {
                    foreach (var property in value.Properties)
                    {
                        stringResult
                            .Append(property.Name)
                            .Append(": ")
                            .Append(property.Value?.ToString());

                        if (property != value.Properties.LastOrDefault())
                        {
                            stringResult.Append(", ");
                        }
                    }
                    stringResult.AppendLine();
                }

                Logger.Result(stringResult.ToString());
            }
        }
    }

    readonly Runspace runspace;

    public string hostname { get; private set; }
    public string username { get; private set; }

    public string navAdminTool { get; private set; }

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

        return new($"<span style=\"opacity: .75\">{username ?? "guest"}@</span>{hostname}");
    }

    public bool BeginSession(string hostname, string username, string password, string navAdminTool)
    {
        Logger.Pending("Connecting");

        if (HasSession)
        {
            Logger.Warning("There is already an active session");
            return false;
        }

        var trustedHosts = GetTrustedHosts();

        if (!trustedHosts.Contains(hostname) && trustedHosts.FirstOrDefault() != "*")
        {
            trustedHosts.Add(hostname);

            var newValue = trustedHosts.Count == 0 ? hostname : string.Join(',', trustedHosts);

            var command = new Command("Set-Item");
            command.Parameters.Add("Path", @"WSMan:\localhost\Client\TrustedHosts");
            command.Parameters.Add("Value", newValue);
            command.Parameters.Add("Force", true);

            RunCommand(command);
        }

        if (!trustedHosts.Contains(hostname) && trustedHosts.FirstOrDefault() != "*")
        {
            Logger.Error(
                "Cannot execute a remote command with out the hostname being added to the trusted hosts list. " +
                $"Please set MachineManager to handle this automatically or add the address manually: {hostname}"
            );
            return false;
        }

        var securePassword = new SecureString();
        foreach (var character in password.ToCharArray())
            securePassword.AppendChar(character);
        securePassword.MakeReadOnly();

        PSCredential credential;

        try
        {
            credential = new(username, securePassword);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex);
            return false;
        }

        var sessionOptionCommand = new Command("New-PSSessionOption");
        sessionOptionCommand.Parameters.Add("OperationTimeout", 0);
        sessionOptionCommand.Parameters.Add("IdleTimeout", 1200000);
        var sessionOption = RunCommand(sessionOptionCommand).returnValue.Single().BaseObject;

        var sessionCommand = new Command("New-PSSession");
        sessionCommand.Parameters.Add("ComputerName", hostname);
        sessionCommand.Parameters.Add("Credential", credential);
        sessionCommand.Parameters.Add("SessionOption", sessionOption);

        session = RunCommand(sessionCommand).returnValue.FirstOrDefault()?.BaseObject;

        if (session is null)
        {
            return false;
        }

        if (navAdminTool != string.Empty)
        {
            RunScript($@"{{ . ""{navAdminTool}"" }}");
        }

        this.hostname = hostname;
        this.username = username;

        this.navAdminTool = navAdminTool;

        Logger.Success("Connected");

        return true;
    }

    public void EndSession()
    {
        if (!HasSession)
        {
            return;
        }

        var trustedHosts = GetTrustedHosts();

        if (trustedHosts.Contains(hostname))
        {
            trustedHosts.Remove(hostname);

            var newValue = trustedHosts.Count == 0
                ? string.Empty
                : string.Join(',', trustedHosts);

            var command = new Command("Set-Item");
            command.Parameters.Add("Path", @"WSMan:\localhost\Client\TrustedHosts");
            command.Parameters.Add("Value", newValue);
            command.Parameters.Add("Force", true);

            RunCommand(command);
        }

        var sessionCommand = new Command("Remove-PSSession");
        sessionCommand.Parameters.Add("Session", session);

        RunCommand(sessionCommand);

        hostname = null;
        username = null;

        navAdminTool = null;

        session = null;

        Logger.Info("Disconnected");
    }

    List<string> GetTrustedHosts()
    {
        var command = new Command("Get-Item");
        command.Parameters.Add("Path", @"WSMan:\localhost\Client\TrustedHosts");

        var result = RunCommand(command);

        if (result.returnValue is null || result.returnValue.Count == 0)
        {
            return [];
        }

        return (
            result.returnValue
                .Single().Properties
                .Single(x => x.Name == "Value")
                .Value.ToString() ?? string.Empty
            )
            .Split(',')
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();
    }

    PowerShell CreateShell()
    {
        var powershell = PowerShell.Create();
        powershell.Runspace = runspace;
        return powershell;
    }

    Result RunCommand(Command command)
    {
        using var powershell = CreateShell();

        powershell.Commands.AddCommand(command);

        return Result.Invoke(powershell);
    }

    public Result RunScript(string scriptBlock, object[] argumentList = null)
    {
        using var powershell = CreateShell();

        var proxy = powershell.Runspace.SessionStateProxy;
        proxy.SetVariable(nameof(session), session);
        proxy.SetVariable(nameof(argumentList), argumentList ?? []);

        var args = "" +
            $" -Session ${nameof(session)}" +
            $" -ScriptBlock ${nameof(scriptBlock)}" +
            $" -ArgumentList ${nameof(argumentList)}" +
            "";

        Logger.Script(scriptBlock);

        var script = $"${nameof(scriptBlock)} = {scriptBlock}\r\nInvoke-Command {args}";

        powershell.AddScript(script);

        return Result.Invoke(powershell);
    }

    public async Task<Result> RunScriptAsync(string scriptBlock, object[] argumentList = null)
    {
        return await Task.Run(() => RunScript(scriptBlock, argumentList));
    }

    public async Task<List<PSObject>> GetObjectListAsync(string scriptBlock, object[] argumentList = null)
    {
        return (await RunScriptAsync(scriptBlock, argumentList)).returnValue;
    }

    public async Task<PSObject> GetObjectAsync(string scriptBlock, object[] argumentList = null)
    {
        return (await GetObjectListAsync(scriptBlock, argumentList)).FirstOrDefault();
    }

}
