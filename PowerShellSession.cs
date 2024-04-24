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

            foreach (var error in result.errors)
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

            return result;
        }
    }

    Runspace runspace;

    string hostname;
    string username;
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

    public MarkupString ToMarkup()
    {
        if (session is null)
        {
            return new("-");
        }

        return new($"<span style=\"opacity: .75\">{username ?? "guest"}@</span>{hostname}");
    }

    public bool BeginSession(string hostname, string username, string password)
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

        var session = RunCommand(sessionCommand).returnValue.FirstOrDefault()?.BaseObject;

        if (session is null)
        {
            return false;
        }

        this.hostname = hostname;
        this.username = username;
        this.session = session;

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
        session = null;

        Logger.Info("Disconnected");
    }

    List<string> GetTrustedHosts()
    {
        var command = new Command("Get-Item");
        command.Parameters.Add("Path", @"WSMan:\localhost\Client\TrustedHosts");

        return (
            RunCommand(command).returnValue
                .Single().Properties
                .Single(x => x.Name == "Value")
                .Value.ToString() ?? string.Empty
            )
            .Split(',')
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();
    }

    Result RunCommand(Command command)
    {
        using var powershell = PowerShell.Create();

        powershell.Runspace = runspace;
        powershell.Commands.AddCommand(command);

        return Result.Invoke(powershell);
    }

    public Result RunScript(string scriptBlock, object[] argumentList = null)
    {
        using var powershell = PowerShell.Create();
        powershell.Runspace = runspace;

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

        var result = Result.Invoke(powershell);

        if (result.returnValue.Count > 0)
        {
            Logger.Result(string.Join("\r\n", result.returnValue));
        }

        return result;
    }

    public async Task<Result> RunScriptAsync(string scriptBlock, object[] argumentList = null)
    {
        return await Task.Run(() => RunScript(scriptBlock, argumentList));
    }

    public async Task<PSObject> GetObjectAsync(string scriptBlock, object[] argumentList = null)
    {
        var result = await RunScriptAsync(scriptBlock, argumentList);
        return result.returnValue.FirstOrDefault();
    }

    public async Task<string> GetStringAsync(string scriptBlock, object[] argumentList = null)
    {
        return (await GetObjectAsync(scriptBlock, argumentList))?.ToString();
    }

    public async Task<List<string>> GetStringListAsync(string scriptBlock, object[] argumentList = null)
    {
        var result = await RunScriptAsync(scriptBlock, argumentList);
        return result.returnValue.Select(x => x?.ToString()).ToList();
    }

}
