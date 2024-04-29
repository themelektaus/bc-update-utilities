using System.Linq;
using System.Threading.Tasks;

namespace BCUpdateUtilities.Models;

public class Config_Configuration_Webserver : Config_Configuration
{
    public override async Task UpdateEntriesAsync()
    {
        entries.Clear();

        var navSession = await serverInstance.GetNavSessionAsync();

        if (navSession is null)
        {
            return;
        }

        var keys = new[]
        {
            "ClientServicesCredentialType",
            "DnsIdentity",
            "ServerHttps",
            "UnknownSpnHint"
        };

        foreach (var key in keys)
        {
            var result = await navSession.RunScriptAsync(
                $"Get-NAVWebServerInstanceConfiguration -WebServerInstance \"{serverInstance.name}\" -KeyName {key}"
            );

            if (!result.HasErrors)
            {
                var value = result.returnValue.Single();

                if (value is not null)
                {
                    entries.Add(new() { key = key, value = value.BaseObject.ToString() });
                }
            }
        }
    }

    public override async Task SetEntryAsync(Entry.Info entryInfo)
    {
        var navSession = await serverInstance.GetNavSessionAsync();

        if (navSession is null)
        {
            return;
        }

        var result = await navSession.RunScriptAsync(
            $"Set-NAVWebServerInstanceConfiguration -WebServerInstance \"{serverInstance.name}\""
            + $" -KeyName \"{entryInfo.entry.key}\" "
            + $" -KeyValue \"{entryInfo.newValue}\""
        );

        if (result.HasErrors)
        {
            return;
        }

        entryInfo.entry.value = entryInfo.newValue;
    }
}
