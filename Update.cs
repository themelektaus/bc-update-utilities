using System;
using System.Threading.Tasks;

namespace BCUpdateUtilities;

public partial class Update
{
    public class Version
    {
        public enum Status { Null, Empty, ParseError, Zero, Ok }
        public readonly string text;
        public readonly Status status;
        public readonly int value;

        public Version(string text)
        {
            this.text = text;

            if (text is null)
            {
                status = Status.Null;
                return;
            }

            if (text == string.Empty)
            {
                status = Status.Empty;
                return;
            }

            if (!int.TryParse(text.Replace(".", string.Empty), out int _value))
            {
                status = Status.ParseError;
                return;
            }

            if (_value == 0)
            {
                status = Status.Zero;
                return;
            }

            value = _value;
            status = Status.Ok;
        }

        public string GetStatusMessage(string name)
        {
            return status switch
            {
                Status.Null => $"Error: {name} is null",
                Status.Empty => $"Error: {name} is null",
                Status.ParseError => "ParseError",
                Status.Zero => $"Error: Can not parse {name} \"{text}\"",
                Status.Ok => $"Error: Parsed {name} is 0",
                _ => $"Error: Unknown status of {name}",
            };
        }

        public override string ToString() => text;

        public override int GetHashCode() => HashCode.Combine(status, value);
        public override bool Equals(object obj)
            => obj is not null
            && obj is Version other
            && GetHashCode() == other.GetHashCode();
        public static bool operator ==(Version a, Version b) => a?.Equals(b) ?? false;
        public static bool operator !=(Version a, Version b) => !(a == b);
    }

    public readonly Version localVersion;
    public readonly Version remoteVersion;
    public readonly bool available;

    Update(string localVersion, string remoteVersion)
    {
        this.localVersion = new(localVersion);
        this.remoteVersion = new(remoteVersion);

        if (this.localVersion.status != Version.Status.Ok)
            return;

        if (this.remoteVersion.status != Version.Status.Ok)
            return;

        if (this.localVersion == this.remoteVersion)
            return;

        available = true;
    }

    public async static Task<Update> Check() => new(
        localVersion: AppInfo.version,
        remoteVersion: await Config.Instance.DownloadFileContent("version.txt")
    );

#if DEBUG
    public static async Task Prepare() => await Task.CompletedTask;
#endif
}
