using System;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media
{
    public static class TplTaskExtensions
    {
        public static readonly Task CompletedTask;
        public static readonly Task<bool> TrueTask;
        public static readonly Task<bool> FalseTask;

        static TplTaskExtensions()
        {
            var tcs = new TaskCompletionSource<bool>();

            tcs.SetResult(true);

            CompletedTask = tcs.Task;
            TrueTask = tcs.Task;


            tcs = new TaskCompletionSource<bool>();

            tcs.SetResult(false);
            
            FalseTask = tcs.Task;
        }

#if WINDOWS_PHONE8
    // http://blogs.msdn.com/b/pfxteam/archive/2012/10/05/how-do-i-cancel-non-cancelable-async-operations.aspx
        public static async Task<T> WithCancellation<T>(
            this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            using (cancellationToken.Register(
                s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);

            return await task;
        }

        public static async Task WithCancellation(
            this Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            using (cancellationToken.Register(
                s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);

            await task;
        }
#else
        // The only difference is the TaskEx.WhenAny (instead of Task.WhenAny).
        public static async Task<T> WithCancellation<T>(
            this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            using (cancellationToken.Register(
                s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                if (task != await TaskEx.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);

            return await task;
        }

        public static async Task WithCancellation(
            this Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            using (cancellationToken.Register(
                s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                if (task != await TaskEx.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);

            await task;
        }
#endif
    }
}
