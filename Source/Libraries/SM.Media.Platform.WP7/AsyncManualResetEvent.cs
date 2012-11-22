using System.Threading;
using System.Threading.Tasks;

namespace SM.Media
{
    /// <summary>
    ///     http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx
    /// </summary>
    public sealed class AsyncManualResetEvent
    {
        volatile TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public AsyncManualResetEvent(bool initialState = false)
        {
            if (!initialState)
                return;

            Set();
        }

        public Task WaitAsync()
        {
            return _tcs.Task;
        }

        //public void Set() { _mTcs.TrySetResult(true); }
        public void Set()
        {
            var tcs = _tcs;

            if (tcs.Task.IsCompleted)
                return;

            Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
                                  tcs, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);

            //tcs.Task.Wait();
        }

        public void Reset()
        {
            var newTcs = new TaskCompletionSource<bool>();

            while (true)
            {
                var tcs = _tcs;
                if (!tcs.Task.IsCompleted ||
#pragma warning disable 0420
                    Interlocked.CompareExchange(ref _tcs, newTcs, tcs) == tcs)
#pragma warning restore 0420
                    return;
            }
        }
    }
}