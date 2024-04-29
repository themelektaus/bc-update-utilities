using Newtonsoft.Json;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BCUpdateUtilities.Models;

public class Config_ServerInstance
{
    [JsonIgnore] public Config_RemoteMachine remoteMachine;

    public string name = string.Empty;
    public string navAdminTool = string.Empty;

    public Config_Configuration_Server serverConfig = new();

    public Config_Configuration_Webserver webServerConfig = new();
    
    public Config_ServerInstance()
    {
        serverConfig.serverInstance = this;
        webServerConfig.serverInstance = this;
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
