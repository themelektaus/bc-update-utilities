using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BCUpdateUtilities.Models;

public class Config_Configuration_Server : Config_Configuration
{
    public override async Task UpdateEntriesAsync()
    {
        entries.Clear();

        var navSession = await serverInstance.GetNavSessionAsync();

        if (navSession is null)
        {
            return;
        }

        var result = await navSession.RunScriptAsync(
            $"Get-NAVServerConfiguration -ServerInstance \"{serverInstance.name}\""
        );

        if (result.HasErrors)
        {
            return;
        }

        dynamic list = result.returnValue.Single().BaseObject;

        if (list is not null)
        {
            foreach (dynamic @object in list)
            {
                entries.Add(new() { key = @object.key as string, value = @object.value as string });
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
            $"Set-NAVServerConfiguration -ServerInstance \"{serverInstance.name}\""
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
