using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media
{
    static class TaskEx
    {
        public static Task Run(Action action)
        {
            return Task.Run(action);
        }

        public static Task Run(Action action, CancellationToken cancellationToken)
        {
            return Task.Run(action, cancellationToken);
        }

        public static Task Run(Func<Task> function)
        {
            return Task.Run(function);
        }

        public static Task Run(Func<Task> function, CancellationToken cancellationToken)
        {
            return Task.Run(function, cancellationToken);
        }

        public static Task Delay(int millisecondDelay)
        {
            return Task.Delay(millisecondDelay);
        }

        public static Task Delay(int millisecondDelay, CancellationToken cancellationToken)
        {
            return Task.Delay(millisecondDelay, cancellationToken);
        }

        public static Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay, cancellationToken);
        }

        public static Task WhenAll(IEnumerable<Task> tasks)
        {
            return Task.WhenAll(tasks);
        }

        public static Task<TResult[]> WhenAll<TResult>(IEnumerable<Task<TResult>> tasks)
        {
            return Task.WhenAll(tasks);
        }

        public static Task<Task> WhenAny(params Task[] tasks)
        {
            return Task.WhenAny(tasks);
        }

        public static Task<TResult> FromResult<TResult>(TResult result)
        {
            return Task.FromResult(result);
        }
    }
}
