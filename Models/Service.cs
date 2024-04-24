using System.Management.Automation;

namespace BCUpdateUtilities.Models;

public class Service
{
    public PSObject Object { get; set; }
    public string Name { get; private set; }
    public string DisplayName { get; private set; }
    public string StartType { get; private set; }
    public string Status { get; set; }

    public bool isStartable => StartType != "Disabled" && Status == "Stopped";
    public bool isStoppable => Status == "Running";

    public static Service From(PSObject x)
    {
        return new()
        {
            Object = x,
            Name = x.Get<string>(nameof(Name)),
            DisplayName = x.Get<string>(nameof(DisplayName)),
            StartType = x.Get<string>(nameof(StartType)),
            Status = x.Get<string>(nameof(Status))
        };
    }
}
