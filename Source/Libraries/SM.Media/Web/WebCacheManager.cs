// -----------------------------------------------------------------------
//  <copyright file="WebCacheManager.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
// 
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Utility;

namespace SM.Media.Web
{
    public interface IWebCacheManager :IDisposable
    {
        Task FlushAsync();

        Task<TCached> ReadAsync<TCached>(Uri uri, Func<Uri, byte[], TCached> factory, ContentKind contentKind, ContentType contentType, CancellationToken cancellationToken)
            where TCached : class;
    }

    public class WebCacheManager : IWebCacheManager
    {
        readonly Dictionary<Uri, CacheEntry> _cache = new Dictionary<Uri, CacheEntry>();
        readonly IWebReader _webReader;
        CancellationTokenSource _flushCancellationTokenSource = new CancellationTokenSource();

        public WebCacheManager(IWebReader webReader)
        {
            if (null == webReader)
                throw new ArgumentNullException(nameof(webReader));

            _webReader = webReader;
        }

        #region IWebCacheManager Members

        public async Task FlushAsync()
        {
            _flushCancellationTokenSource.Cancel();

            CacheEntry[] cacheEntries;

            lock (_cache)
            {
                cacheEntries = _cache.Values.ToArray();
                _cache.Clear();
            }

            try
            {
                await Task.WhenAll(cacheEntries.Where(c => null != c.ReadTask).Select(c => c.ReadTask)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            { }
            catch (Exception ex)
            {
                Debug.WriteLine("WebCacheManager.FlushAsync() exception: " + ex.ExtendedMessage());
            }

            foreach (var cacheEntry in cacheEntries)
                cacheEntry.WebCache.WebReader.Dispose();

            var fcts = _flushCancellationTokenSource;

            _flushCancellationTokenSource = new CancellationTokenSource();

            fcts.Dispose();
        }

        public Task<TCached> ReadAsync<TCached>(Uri uri, Func<Uri, byte[], TCached> factory, ContentKind contentKind, ContentType contentType, CancellationToken cancellationToken)
            where TCached : class
        {
            CacheEntry cacheEntry;
            TaskCompletionSource<TCached> tcs = null;

            lock (_cache)
            {
                if (_cache.TryGetValue(uri, out cacheEntry))
                {
                    if (cacheEntry.ReadTask.IsCompleted && cacheEntry.Age > TimeSpan.FromSeconds(5))
                    {
                        tcs = new TaskCompletionSource<TCached>();

                        cacheEntry.ReadTask = tcs.Task;
                    }
                }
                else
                {
                    var wc = _webReader.CreateWebCache(uri, contentKind, contentType);

                    tcs = new TaskCompletionSource<TCached>();

                    cacheEntry = new CacheEntry
                    {
                        WebCache = wc,
                        ReadTask = tcs.Task
                    };

                    _cache[uri] = cacheEntry;
                }
            }

            if (null == tcs)
                return (Task<TCached>)cacheEntry.ReadTask;

            var task = cacheEntry.WebCache.ReadAsync(factory, cancellationToken);

            task.ContinueWith(t =>
            {
                cacheEntry.ResetTime();

                var ex = t.Exception;

                if (null != ex)
                    tcs.TrySetCanceled();
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(task.Result);
            }, cancellationToken);

            return tcs.Task;
        }

        #endregion

        #region Nested type: CacheEntry

        class CacheEntry
        {
            Stopwatch _lastUpdate;

            public TimeSpan Age
            {
                get { return _lastUpdate.Elapsed; }
            }

            public void ResetTime()
            {
                _lastUpdate = Stopwatch.StartNew();
            }

            public Task ReadTask;
            public IWebCache WebCache;
        }

        #endregion

        public void Dispose()
        {
            _webReader.Dispose();
        }
    }
}
