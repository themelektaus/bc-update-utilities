namespace Naos;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public struct Response
{
    public ICollection<dynamic> output;
    public Exception exception;
}

/// <summary>
/// Manages various remote tasks on a machine using the WinRM protocol.
/// </summary>
public class MachineManager
{
    static readonly object syncTrustedHosts = new();

    readonly string hostname;
    readonly string username;
    readonly SecureString password;
    readonly bool autoManageTrustedHosts;
    readonly long fileChunkSizeThresholdByteCount;
    readonly long fileChunkSizePerSend;
    readonly long fileChunkSizePerRetrieve;

    /// <summary>
    /// Initializes a new instance of the <see cref="MachineManager"/> class.
    /// </summary>
    /// <param name="hostname">Hostname of machine to interact with.</param>
    /// <param name="username">Username to use to connect.</param>
    /// <param name="password">Password to use to connect.</param>
    /// <param name="autoManageTrustedHosts">Optionally specify whether to update the TrustedHost list prior to execution or assume it's handled elsewhere (default is FALSE).</param>
    /// <param name="fileChunkSizeThresholdByteCount">Optionally specify file size that will trigger chunking the file rather than sending as one file (150000 is default).</param>
    /// <param name="fileChunkSizePerSend">Optionally specify size of each chunk that is sent when a file is being chunked for send.</param>
    /// <param name="fileChunkSizePerRetrieve">Optionally specify size of each chunk that is received when a file is being chunked for fetch.</param>
    public MachineManager(
        string hostname,
        string username,
        string password,
        bool autoManageTrustedHosts = true,
        long fileChunkSizeThresholdByteCount = 150_000,
        long fileChunkSizePerSend = 100_000,
        long fileChunkSizePerRetrieve = 100_000
    )
    {
        this.hostname = hostname;
        this.username = username;

        var securePassword = new SecureString();
        foreach (var character in password.ToCharArray())
            securePassword.AppendChar(character);
        securePassword.MakeReadOnly();
        this.password = securePassword;

        this.autoManageTrustedHosts = autoManageTrustedHosts;
        this.fileChunkSizeThresholdByteCount = fileChunkSizeThresholdByteCount;
        this.fileChunkSizePerSend = fileChunkSizePerSend;
        this.fileChunkSizePerRetrieve = fileChunkSizePerRetrieve;
    }

    public static void AddTrustedHost(string hostname)
    {
        lock (syncTrustedHosts)
        {
            var trustedHosts = GetTrustedHosts().ToList();

            if (!trustedHosts.Contains(hostname) && !IsWildcard(trustedHosts))
            {
                trustedHosts.Add(hostname);
                var newValue = trustedHosts.Count == 0 ? hostname : string.Join(',', trustedHosts);

                using var runspace = RunspaceFactory.CreateRunspace();
                runspace.Open();

                var command = new Command("Set-Item");
                command.Parameters.Add("Path", @"WSMan:\localhost\Client\TrustedHosts");
                command.Parameters.Add("Value", newValue);
                command.Parameters.Add("Force", true);

                _ = RunLocalCommand(runspace, command);
            }
        }
    }

    public static void RemoveTrustedHost(string hostname)
    {
        lock (syncTrustedHosts)
        {
            var trustedHosts = GetTrustedHosts().ToList();

            if (trustedHosts.Contains(hostname))
            {
                trustedHosts.Remove(hostname);

                // can't pass null must be an empty string...
                var newValue = trustedHosts.Count == 0
                    ? string.Empty
                    : string.Join(',', trustedHosts);

                using var runspace = RunspaceFactory.CreateRunspace();
                runspace.Open();

                var command = new Command("Set-Item");
                command.Parameters.Add("Path", @"WSMan:\localhost\Client\TrustedHosts");
                command.Parameters.Add("Value", newValue);
                command.Parameters.Add("Force", true);

                _ = RunLocalCommand(runspace, command);
            }
        }
    }

    public static string[] GetTrustedHosts()
    {
        lock (syncTrustedHosts)
        {
            try
            {
                using var runspace = RunspaceFactory.CreateRunspace();
                runspace.Open();

                var command = new Command("Get-Item");
                command.Parameters.Add("Path", @"WSMan:\localhost\Client\TrustedHosts");

                var response = RunLocalCommand(runspace, command);

                return (
                    response
                        .Single().Properties
                        .Single(x => x.Name == "Value")
                        .Value.ToString() ?? string.Empty
                ).Split(',')
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
            }
            catch (Exception ex)
            {
                // if we don't have any trusted hosts then just ignore...
                if (ex.Message.Contains("Cannot find path 'WSMan:\\localhost\\Client\\TrustedHosts' because it does not exist."))
                {
                    return [];
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Sends a file to the remote machine at the provided file path on that target computer.
    /// </summary>
    /// <param name="filePathOnTargetMachine">File path to write the contents to on the remote machine.</param>
    /// <param name="fileContents">Payload to write to the file.</param>
    /// <param name="appended">Optionally writes the bytes in appended mode or not (default is NOT).</param>
    /// <param name="overwrite">Optionally will overwrite a file that is already there [can NOT be used with 'appended'] (default is NOT).</param>
    public void SendFile(string filePathOnTargetMachine, byte[] fileContents, bool appended = false, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(fileContents);

        if (appended && overwrite)
        {
            throw new ArgumentException("Cannot run with overwrite AND appended.");
        }

        using var runspace = RunspaceFactory.CreateRunspace();

        runspace.Open();

        var sessionObject = BeginSession(runspace);

        var verifyFileDoesntExistScriptBlock = @"
        {
            param($filePath)

            if (Test-Path $filePath)
            {
                throw ""File already exists at: $filePath""
            }
        }";

        if (!appended && !overwrite)
        {
            RunScriptUsingSession(
                scriptBlock: verifyFileDoesntExistScriptBlock,
                scriptBlockParameters: new[] { filePathOnTargetMachine },
                runspace,
                sessionObject
            );
        }

        var firstSendUsingSession = true;

        if (fileContents.Length <= fileChunkSizeThresholdByteCount)
        {
            SendFileUsingSession(
                filePathOnTargetMachine,
                fileContents,
                appended,
                overwrite,
                runspace,
                sessionObject
            );
        }
        else
        {
            // deconstruct and send pieces as appended...
            var nibble = new List<byte>();

            foreach (byte currentByte in fileContents)
            {
                nibble.Add(currentByte);

                if (nibble.Count < fileChunkSizePerSend)
                {
                    continue;
                }

                SendFileUsingSession(
                    filePathOnTargetMachine,
                    fileContents: [.. nibble],
                    !firstSendUsingSession,
                    overwrite && firstSendUsingSession,
                    runspace,
                    sessionObject
                );

                firstSendUsingSession = false;
                nibble.Clear();
            }

            // flush the "buffer"...
            if (nibble.Count > 0)
            {
                SendFileUsingSession(
                    filePathOnTargetMachine,
                    fileContents: [.. nibble],
                    appended: true,
                    overwrite: false,
                    runspace,
                    sessionObject
                );

                nibble.Clear();
            }
        }

        var expectedChecksum = ComputeSha256Hash(fileContents);
        var verifyChecksumScriptBlock = @"
        {
            param($filePath, $expectedChecksum)

            $fileToCheckFileInfo = New-Object System.IO.FileInfo($filePath)
            if (-not $fileToCheckFileInfo.Exists)
            {
                # If the file can't be found, try looking for it in the current directory.
                $fileToCheckFileInfo = New-Object System.IO.FileInfo($filePath)
                if (-not $fileToCheckFileInfo.Exists)
                {
                    throw ""Can't find the file specified to calculate a checksum on: $filePath""
                }
            }

            $fileToCheckFileStream = $fileToCheckFileInfo.OpenRead()
            $provider = New-Object System.Security.Cryptography.SHA256CryptoServiceProvider
            $hashBytes = $provider.ComputeHash($fileToCheckFileStream)
            $fileToCheckFileStream.Close()
            $fileToCheckFileStream.Dispose()
        
            $base64 = [System.Convert]::ToBase64String($hashBytes)
        
            $calculatedChecksum = [System.String]::Empty
            foreach ($byte in $hashBytes)
            {
                $calculatedChecksum = $calculatedChecksum + $byte.ToString(""X2"")
            }

            if($calculatedChecksum -ne $expectedChecksum)
            {
                Write-Error ""Checksums don't match on File: $filePath - Expected: $expectedChecksum - Actual: $calculatedChecksum""
            }
        }";

        RunScriptUsingSession(
            verifyChecksumScriptBlock,
            [filePathOnTargetMachine, expectedChecksum],
            runspace,
            sessionObject
        );

        EndSession(sessionObject, runspace);

        runspace.Close();
    }

    void SendFileUsingSession(
        string filePathOnTargetMachine,
        byte[] fileContents,
        bool appended,
        bool overwrite,
        Runspace runspace,
        object sessionObject
    )
    {
        if (appended && overwrite)
        {
            throw new ArgumentException("Cannot run with overwrite AND appended.");
        }

        var commandName = appended ? "Add-Content" : "Set-Content";
        var forceAddIn = overwrite ? " -Force" : string.Empty;
        var scriptBlock = @"
        {
            param($filePath, $fileContents)

            $parentDir = Split-Path $filePath
            if (-not (Test-Path $parentDir))
            {
                md $parentDir | Out-Null
            }

            " + commandName + @" -Path $filePath -Encoding Byte -Value $fileContents" + forceAddIn + @"
        }";

        _ = RunScriptUsingSession(
            scriptBlock,
            [filePathOnTargetMachine, fileContents],
            runspace,
            sessionObject
        );
    }

    /// <summary>
    /// Retrieves a file from the remote machines and returns a checksum verified byte array.
    /// </summary>
    /// <param name="filePathOnTargetMachine">File path to fetch the contents of on the remote machine.</param>
    /// <returns>Bytes of the specified files (throws if missing).</returns>
    public byte[] RetrieveFile(string filePathOnTargetMachine)
    {
        using var runspace = RunspaceFactory.CreateRunspace();

        runspace.Open();

        var sessionObject = BeginSession(runspace);

        var verifyFileExistsScriptBlock = @"
        {
            param($filePath)

            if (-not (Test-Path $filePath))
            {
                throw ""File doesn't exist at: $filePath""
            }

            $file = ls $filePath
            Write-Output $file.Length
        }";

        var fileSizeRaw = RunScriptUsingSession(
            verifyFileExistsScriptBlock,
            new[] { filePathOnTargetMachine },
            runspace,
            sessionObject
        );

        var fileSize = (long) long.Parse(fileSizeRaw.Single().ToString());

        var getChecksumScriptBlock = @"
        {
            param($filePath)

            $fileToCheckFileInfo = New-Object System.IO.FileInfo($filePath)
            if (-not $fileToCheckFileInfo.Exists)
            {
                # If the file can't be found, try looking for it in the current directory.
                $fileToCheckFileInfo = New-Object System.IO.FileInfo($filePath)
                if (-not $fileToCheckFileInfo.Exists)
                {
                    throw ""Can't find the file specified to calculate a checksum on: $filePath""
                }
            }

            $fileToCheckFileStream = $fileToCheckFileInfo.OpenRead()
            $provider = New-Object System.Security.Cryptography.SHA256CryptoServiceProvider
            $hashBytes = $provider.ComputeHash($fileToCheckFileStream)
            $fileToCheckFileStream.Close()
            $fileToCheckFileStream.Dispose()
        
            $base64 = [System.Convert]::ToBase64String($hashBytes)
        
            $calculatedChecksum = [System.String]::Empty
            foreach ($byte in $hashBytes)
            {
                $calculatedChecksum = $calculatedChecksum + $byte.ToString(""X2"")
            }
                        
            # trimming off leading and trailing curly braces '{ }'
            $trimmedChecksum = $calculatedChecksum.Substring(1, $calculatedChecksum.Length - 2)

            Write-Output $trimmedChecksum
        }";

        var remoteChecksumRaw = RunScriptUsingSession(
            getChecksumScriptBlock,
            new[] { filePathOnTargetMachine },
            runspace,
            sessionObject
        );

        var remoteChecksum = remoteChecksumRaw.Single();

        var bytes = new List<byte>();

        if (fileSize <= fileChunkSizeThresholdByteCount)
        {
            bytes.AddRange(
                RetrieveFileUsingSession(
                    filePathOnTargetMachine,
                    runspace,
                    sessionObject
                )
            );
        }
        else
        {
            // deconstruct and fetch pieces...
            var lastNibblePoint = 0;

            for (var nibblePoint = 0; nibblePoint < fileSize; nibblePoint++)
            {
                if ((nibblePoint - lastNibblePoint) >= fileChunkSizePerRetrieve)
                {
                    var remainingBytes = fileSize - nibblePoint;

                    var nibbleSize = remainingBytes < fileChunkSizePerRetrieve
                        ? remainingBytes
                        : fileChunkSizePerRetrieve;

                    bytes.AddRange(
                        RetrieveFileUsingSession(
                            filePathOnTargetMachine,
                            runspace,
                            sessionObject,
                            lastNibblePoint,
                            nibbleSize
                        )
                    );

                    lastNibblePoint = nibblePoint;
                }
            }
        }

        var byteArray = bytes.ToArray();
        var actualChecksum = ComputeSha256Hash(byteArray);

        if (string.Equals(remoteChecksum.ToString(), actualChecksum, StringComparison.CurrentCultureIgnoreCase))
        {
            throw new("Checksum didn't match after file was downloaded.");
        }

        EndSession(sessionObject, runspace);

        runspace.Close();

        return byteArray;
    }

    byte[] RetrieveFileUsingSession(
        string filePathOnTargetMachine,
        Runspace runspace,
        object sessionObject,
        long nibbleStart = 0,
        long nibbleSize = 0
    )
    {
        if (nibbleStart != 0 && nibbleSize == 0)
        {
            nibbleSize = fileChunkSizePerRetrieve;
        }

        var scriptBlock = @"
        {
            param($filePath, $nibbleStart, $nibbleSize)

            if (-not (Test-Path $filePath))
            {
                throw ""Expected file to fetch missing at: $filePath""
            }

            $allBytes = [System.IO.File]::ReadAllBytes($filePath)
            if (($nibbleStart -eq 0) -and ($nibbleSize -eq 0))
            {
                Write-Output $allBytes
            }
            else
            {
                $nibble = new-object byte[] $nibbleSize
                [Array]::Copy($allBytes, $nibbleStart, $nibble, 0, $nibbleSize)
                Write-Output $nibble
            }
        }";

        var scriptBlockParameters = new object[] {
            filePathOnTargetMachine,
            nibbleStart,
            nibbleSize
        };

        var bytesRaw = RunScriptUsingSession(
            scriptBlock,
            scriptBlockParameters,
            runspace,
            sessionObject
        );

        return [.. bytesRaw.Select(x => byte.Parse(x.ToString()))];
    }

    /// <summary>
    /// Runs an arbitrary script block.
    /// </summary>
    /// <param name="scriptBlock">Script block.</param>
    /// <param name="scriptBlockParameters">Parameters to be passed to the script block.</param>
    /// <returns>Collection of objects that were the output from the script block.</returns>
    public ICollection<dynamic> RunScript(
        string scriptBlock,
        ICollection<object> scriptBlockParameters = null
    )
    {
        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();

        var sessionObject = BeginSession(runspace);

        var result = RunScriptUsingSession(
            scriptBlock,
            scriptBlockParameters,
            runspace,
            sessionObject
        );

        EndSession(sessionObject, runspace);

        runspace.Close();

        return result;
    }

    public async Task<Response> RunScriptAsync(
        string scriptBlock,
        ICollection<object> scriptBlockParameters = null
    )
    {
        return await Task.Run(() =>
        {
            try
            {
                return new Response()
                {
                    output = RunScript(scriptBlock, scriptBlockParameters)
                };
            }
            catch (Exception ex)
            {
                return new Response() { exception = ex };
            }
        });
    }

    void EndSession(object sessionObject, Runspace runspace)
    {
        if (autoManageTrustedHosts)
        {
            RemoveTrustedHost(hostname);
        }

        var command = new Command("Remove-PSSession");
        command.Parameters.Add("Session", sessionObject);
        _ = RunLocalCommand(runspace, command);
    }

    object BeginSession(Runspace runspace)
    {
        if (autoManageTrustedHosts)
        {
            AddTrustedHost(hostname);
        }

        var trustedHosts = GetTrustedHosts();

        if (!trustedHosts.Contains(hostname) && !IsWildcard(trustedHosts))
        {
            throw new(
                "Cannot execute a remote command with out the hostname being added to the trusted hosts list. " +
                $"Please set MachineManager to handle this automatically or add the address manually: {hostname}"
            );
        }

        var powershellCredentials = new PSCredential(username, password);

        var sessionOptionsCommand = new Command("New-PSSessionOption");
        sessionOptionsCommand.Parameters.Add("OperationTimeout", 0);
        sessionOptionsCommand.Parameters.Add("IdleTimeout", TimeSpan.FromMinutes(20).TotalMilliseconds);
        var sessionOptionsObject = RunLocalCommand(runspace, sessionOptionsCommand).Single().BaseObject;

        var sessionCommand = new Command("New-PSSession");
        sessionCommand.Parameters.Add("ComputerName", hostname);
        sessionCommand.Parameters.Add("Credential", powershellCredentials);
        sessionCommand.Parameters.Add("SessionOption", sessionOptionsObject);
        return RunLocalCommand(runspace, sessionCommand).Single().BaseObject;
    }

    static List<dynamic> RunScriptUsingSession(
        string scriptBlock,
        ICollection<object> scriptBlockParameters,
        Runspace runspace,
        object sessionObject
    )
    {
        using var powershell = PowerShell.Create();

        powershell.Runspace = runspace;

        Collection<PSObject> output;

        // session will implicitly assume remote - if null then localhost...
        if (sessionObject != null)
        {
            var variableNameArgs = "scriptBlockArgs";
            var variableNameSession = "invokeCommandSession";
            powershell.Runspace.SessionStateProxy.SetVariable(variableNameSession, sessionObject);

            var argsAddIn = string.Empty;

            if (scriptBlockParameters != null && scriptBlockParameters.Count > 0)
            {
                powershell.Runspace.SessionStateProxy.SetVariable(
                    variableNameArgs,
                    scriptBlockParameters.ToArray()
                );

                argsAddIn = " -ArgumentList $" + variableNameArgs;
            }

            var fullScript = "$sc = " + scriptBlock + Environment.NewLine
                + "Invoke-Command -Session $" + variableNameSession
                + argsAddIn
                + " -ScriptBlock $sc";

            powershell.AddScript(fullScript);

            output = powershell.Invoke();
        }
        else
        {
            var fullScript = "$sc = " + scriptBlock + Environment.NewLine
                + "Invoke-Command -ScriptBlock $sc";

            powershell.AddScript(fullScript);

            foreach (var scriptBlockParameter in scriptBlockParameters ?? new List<object>())
            {
                powershell.AddArgument(scriptBlockParameter);
            }

            output = powershell.Invoke(scriptBlockParameters);
        }

        ThrowOnError(powershell);

        return output.Cast<dynamic>().ToList();
    }

    static bool IsWildcard(IReadOnlyCollection<string> trustedHosts)
    {
        return trustedHosts.FirstOrDefault() == "*";
    }

    static List<PSObject> RunLocalCommand(Runspace runspace, Command command)
    {
        using var powershell = PowerShell.Create();
        powershell.Runspace = runspace;
        powershell.Commands.AddCommand(command);

        var output = powershell.Invoke();
        ThrowOnError(powershell);
        return [.. output];
    }

    static void ThrowOnError(PowerShell powershell)
    {
        var error = powershell.Streams.Error;
        if (error.Count > 0)
        {
            throw new(
                string.Join(
                    Environment.NewLine,
                    error.Select(
                        x => (
                            x.ErrorDetails is null
                                ? null
                                : $"{x.ErrorDetails} at {x.ScriptStackTrace}"
                        )
                        ?? (
                            x.Exception is null
                                ? "Naos: No error message available"
                                : $"{x.Exception} at {x.ScriptStackTrace}"
                        )
                    )
                )
            );
        }
    }

    static string ComputeSha256Hash(byte[] bytes)
    {
        var checksum = new StringBuilder();
        foreach (byte x in SHA256.HashData(bytes))
            checksum.Append(string.Format(CultureInfo.InvariantCulture, "{0:x2}", x));
        return checksum.ToString();
    }
}
