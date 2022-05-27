using System.Runtime.CompilerServices;

namespace Faucet.Helpers;

/// <summary>
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
public class AsyncLazy<T> : Lazy<Task<T>>
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="valueFactory"></param>
    public AsyncLazy(Func<T> valueFactory) : base(() => Task.Run(valueFactory))
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="taskFactory"></param>
    public AsyncLazy(Func<Task<T>> taskFactory) : base(() => Task.Run(taskFactory))
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public TaskAwaiter<T> GetAwaiter()
    {
        return Value.GetAwaiter();
    }
}