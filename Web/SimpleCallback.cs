using Microsoft.AspNetCore.Components;

using System;
using System.Threading.Tasks;

namespace BCUpdateUtilities.Web;

public record SimpleCallback(Action Callback) : IHandleEvent
{
    public static Action Create(Action callback) => new SimpleCallback(callback).Invoke;
    public void Invoke() => Callback();
    public Task HandleEventAsync(EventCallbackWorkItem item, object arg) => item.InvokeAsync(arg);
}
