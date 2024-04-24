using System.Collections.Generic;
using System.Linq;

namespace BCUpdateUtilities;

public static class PowerShellSessionManager
{
    static readonly List<PowerShellSession> sessions = [];

    public static PowerShellSession DefaultSession => GetSession(string.Empty);

    public static PowerShellSession GetSession(string navAdminTool)
        => GetSession(Config.Instance.bc.remoteMachine, navAdminTool);

    static PowerShellSession GetSession(Config.BC.RemoteMachine rm, string navAdminTool)
    {
        var session = sessions.FirstOrDefault(x
            => x.Hostname == rm.Hostname
            && x.Username == rm.Username
            && x.NavAdminTool == navAdminTool
        );

        if (session is null)
        {
            session = new();

            if (session.BeginSession(rm.Hostname, rm.Username, rm.Password, navAdminTool))
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
