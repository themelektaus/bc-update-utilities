using Microsoft.AspNetCore.Components;

using Newtonsoft.Json;

using System;
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

    public static async Task<DialogResult> ShowDialogAsync(this CommonDialog @this)
    {
        var result = DialogResult.None;
        var thread = new Thread(() => result = @this.ShowDialog());
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await Task.Run(thread.Join);
        return result;
    }

    public static async Task<DialogResult> ShowDialogAsync(this CommonDialog @this, Control threadHandle)
    {
        return await threadHandle.InvokeAsync(@this.ShowDialog);
    }

}
