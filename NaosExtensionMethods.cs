namespace BCUpdateUtilities;

using Naos;

using System.Linq;
using System.Threading.Tasks;

public static class NaosExtensionMethods
{
    public struct Result<T>
    {
        public Response response;
        public T value;
    }

    public static async Task<Result<string>> GetHostnameAsync(this MachineManager @this)
    {
        var response = await @this.RunScriptAsync("{ hostname }");
        return new()
        {
            response = response,
            value = response.output?.FirstOrDefault()?.ToString() as string
        };
    }
}
