using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BCUpdateUtilities;

public static class PowerShellSessionManager
{
    static readonly List<PowerShellSession> sessions = [];

    public static async Task<PowerShellSession> GetSessionAsync(Config.BC.RemoteMachine rm, string navAdminTool)
    {
        var session = sessions.FirstOrDefault(x
            => x.Hostname == rm.Hostname
            && x.Username == rm.Username
            && x.NavAdminTool == navAdminTool
        );

        if (session is null)
        {
            session = new();

            var sessionStarted = await Task.Run(() => session.BeginSession(rm.Hostname, rm.Username, rm.Password, navAdminTool));
            if (sessionStarted)
            {
                sessions.Add(session);
            }
        }

        return session;

    }

    public static void Dispose()
    {
        foreach (var session in sessions)
        {
            session.EndSession();
        }

        sessions.Clear();
    }
}
