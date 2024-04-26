using Microsoft.AspNetCore.Components;

using Newtonsoft.Json;

using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using Flags = System.Reflection.BindingFlags;

namespace BCUpdateUtilities;

public static class ExtensionMethods
{
    const Flags PRIVATE_FLAGS = Flags.Instance | Flags.NonPublic;

    public static void RenderLater(this ComponentBase @this)
    {
        var method = @this.GetPrivateMethod("StateHasChanged");
        var action = new Action(() => method.Invoke(@this, null));
        var invokeMethod = @this.GetPrivateMethod("InvokeAsync", typeof(Action));
        invokeMethod.Invoke(@this, [action]);
    }

    public static async Task RenderLaterAsync(this ComponentBase @this)
    {
        @this.RenderLater();
        await Task.Delay(1);
    }

    public static MethodInfo GetPrivateMethod(this object @this, string name, params Type[] argTypes)
    {
        return @this.AsType().GetMethod(name, PRIVATE_FLAGS, argTypes);
    }

    static Type AsType(this object @object)
    {
        return @object is Type type ? type : @object.GetType();
    }

    static readonly JsonSerializerSettings jsonSerializerSettings = new()
    {
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    public static string ToJson<T>(this T @this)
    {
        return JsonConvert.SerializeObject(@this, jsonSerializerSettings);
    }

    public static T FromJson<T>(this string @this)
    {
        return JsonConvert.DeserializeObject<T>(@this, jsonSerializerSettings);
    }

    public static async Task<T> InvokeAsync<T>(this Control @this, Func<T> method)
    {
        var result = @this.BeginInvoke(method);
        await Task.Run(result.AsyncWaitHandle.WaitOne);
        return (T) @this.EndInvoke(result);
    }

    public static T Get<T>(this System.Management.Automation.PSObject @this, string propertyName)
    {
        return (T) @this.Properties[propertyName].Value;
    }

    public static bool IsValidPattern(this string @this, out bool ignoreCase)
    {
        ignoreCase = !@this.Any(char.IsUpper);
        return string.Empty.TrySearchMatch(@this, true, out _);
    }

    public static bool SearchMatch(this string @this, string pattern, bool ignoreCase)
    {
        @this.TrySearchMatch(pattern, ignoreCase, out var result);

        return result;
    }

    static bool TrySearchMatch(this string @this, string pattern, bool ignoreCase, out bool result)
    {
        if (pattern.Length >= 3)
        {
            if (pattern.StartsWith('/') && pattern.EndsWith('/'))
            {
                try
                {
                    result = Regex.IsMatch(@this, pattern.Trim('/'));
                    return true;
                }
                catch
                {
                    result = false;
                    return false;
                }
            }
        }

        result = @this.Contains(
            pattern,
            ignoreCase
                ? StringComparison.InvariantCultureIgnoreCase
                : StringComparison.InvariantCulture
        );

        return true;
    }
}
