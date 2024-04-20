using Microsoft.AspNetCore.Components;

using System;
using System.Threading.Tasks;

namespace BCUpdateUtilities.Web;

public record SimpleAsyncCallback(Func<Task> Callback) : IHandleEvent
{
    public static Func<Task> Create(Func<Task> callback) => new SimpleAsyncCallback(callback).Invoke;
    public Task Invoke() => Callback();
    public Task HandleEventAsync(EventCallbackWorkItem item, object arg) => item.InvokeAsync(arg);
}
