using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;

namespace BCUpdateUtilities;

public class PowerShellSession : IDisposable
{
    public struct Result
    {
        public PSDataStreams streams;
        public List<PSObject> returnValue;
    }

    readonly string hostname;
    readonly string username;
    readonly SecureString password = new();

    Runspace runspace;

    public PowerShellSession(string hostname, string username, string password)
    {
        this.hostname = hostname;
        this.username = username;

        foreach (var character in password.ToCharArray())
            this.password.AppendChar(character);
        this.password.MakeReadOnly();

        runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();
    }

    public void Dispose()
    {
        runspace.Close();
    }

    public Result RunScript(string scriptBlock, object[] argumentList = null)
    {
        return RunSession(session =>
        {
            return RunScript(session, scriptBlock, argumentList);
        });
    }

    public async Task<Result> RunScriptAsync(string scriptBlock, object[] argumentList = null)
    {
        return await Task.Run(() => RunScript(scriptBlock, argumentList));
    }

    Result RunSession(Func<object, Result> callback)
    {
        var result = BeginSession();
        var session = result.returnValue.FirstOrDefault()?.BaseObject;
        if (session is not null)
        {
            result = callback(session);
            EndSession(session);
        }
        return result;
    }

    Result BeginSession()
    {
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
            throw new(
                "Cannot execute a remote command with out the hostname being added to the trusted hosts list. " +
                $"Please set MachineManager to handle this automatically or add the address manually: {hostname}"
            );
        }

        var sessionOptionCommand = new Command("New-PSSessionOption");
        sessionOptionCommand.Parameters.Add("OperationTimeout", 0);
        sessionOptionCommand.Parameters.Add("IdleTimeout", 1200000);
        var sessionOption = RunCommand(sessionOptionCommand).returnValue.Single().BaseObject;

        var sessionCommand = new Command("New-PSSession");
        sessionCommand.Parameters.Add("ComputerName", hostname);
        sessionCommand.Parameters.Add("Credential", new PSCredential(username, password));
        sessionCommand.Parameters.Add("SessionOption", sessionOption);

        return RunCommand(sessionCommand);
    }

    void EndSession(object session)
    {
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

        var returnValue = powershell.Invoke();

        foreach (var error in powershell.Streams.Error)
        {
            Logger.Error(error.ErrorDetails.Message);
        }

        return new()
        {
            streams = powershell.Streams,
            returnValue = [.. returnValue]
        };
    }

    Result RunScript(object session, string scriptBlock, object[] argumentList = null)
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

        Logger.Script(scriptBlock.Trim(['{', '}']));

        scriptBlock = $"${nameof(scriptBlock)} = {scriptBlock}";
        powershell.AddScript($"{scriptBlock}\r\nInvoke-Command {args}");

        var result = new Result
        {
            streams = powershell.Streams,
            returnValue = [.. powershell.Invoke()]
        };

        Logger.Result(string.Join("\r\n", result.returnValue));

        return result;
    }
}
