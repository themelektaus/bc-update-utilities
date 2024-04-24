using Microsoft.AspNetCore.Components;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Flags = System.Reflection.BindingFlags;

namespace BCUpdateUtilities;

public static class ExtensionMethods
{
    const Flags PRIVATE_FLAGS = Flags.Instance | Flags.NonPublic;

    public static void RenderLater(this IEnumerable<ComponentBase> @this)
    {
        foreach (var x in @this)
            x.RenderLater();
    }

    public static void RenderLater(this ComponentBase @this)
    {
        var method = @this.GetPrivateMethod("StateHasChanged");
        var action = new Action(() => method.Invoke(@this, null));
        var invokeMethod = @this.GetPrivateMethod("InvokeAsync", typeof(Action));
        invokeMethod.Invoke(@this, new object[] { action });
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
        Formatting = Formatting.Indented
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
}
