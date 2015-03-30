// -----------------------------------------------------------------------
//  <copyright file="GeneratorStreamSource.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2015.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2015 Henric Jungheim <software@henric.org>
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
using System.Threading.Tasks;
using Windows.Media.Core;
using SM.Media.Utility;

namespace SM.Media.Audio.Generator
{
    public class GeneratorStreamSource : StreamSourceBase
    {
        const int MaxSamples = 4;
        readonly byte[] _buffer;
        readonly Action<ulong, byte[]> _generator;
        readonly AsyncManualResetEvent _haveSamples = new AsyncManualResetEvent();
        readonly object _lock = new object();
        readonly AsyncManualResetEvent _readyToStart = new AsyncManualResetEvent();
        readonly Queue<MediaStreamSample> _samples = new Queue<MediaStreamSample>();
        readonly SignalTask _worker;
        bool _clear;

        public GeneratorStreamSource(Action<ulong, byte[]> generator, IAudioStreamSourceParameters parameters)
            : base(parameters)
        {
            if (generator == null)
                throw new ArgumentNullException("generator");

            _generator = generator;

            _buffer = new byte[BufferSize];

            _worker = new SignalTask(CreateSamplesAsync);
        }

        public override MediaStreamSource OpenSource()
        {
            _worker.Fire();

            return base.OpenSource();
        }

        protected override async void MssOnStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            base.MssOnStarting(sender, args);

            var deferral = args.Request.GetDeferral();

            try
            {
                _readyToStart.Reset();

                lock (_lock)
                {
                    _clear = true;
                    _samples.Clear();
                }

                _worker.Fire();

                await _readyToStart.WaitAsync().ConfigureAwait(false);
            }
            finally
            {
                deferral.Complete();
            }
        }

        protected override void MssOnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            Debug.WriteLine("GeneratorStreamSource.MssOnSampleRequested()");

            if (IsCancellationRequested)
            {
                Debug.WriteLine("GeneratorStreamSource.MssOnSampleRequested() eof");

                return;
            }

            var sample = GetSample();

            if (null != sample)
            {
                Debug.WriteLine("GeneratorStreamSource.MssOnSampleRequested() sample " + sample.Timestamp + " " + sample.Duration);
                args.Request.Sample = sample;
                return;
            }

            var task = OnSampleRequestedAsync(args);

            TaskCollector.Default.Add(task, "GeneratorStreamSource MssOnSampleRequested");
        }

        MediaStreamSample GetSample()
        {
            MediaStreamSample sample = null;
            int count;

            if (IsCancellationRequested)
                return null;

            lock (_lock)
            {
                count = _samples.Count;

                if (count > 0)
                    sample = _samples.Dequeue();
            }

            if (count < MaxSamples / 2)
            {
                if (0 == count)
                    _haveSamples.Reset();

                // There is a race here since the worker could have added
                // samples after we just reset "_haveSample".  Since we
                // call "Fire()" after we muck with "_haveSample", the worker
                // should sort things out.

                _worker.Fire();
            }

            return sample;
        }

        async Task OnSampleRequestedAsync(MediaStreamSourceSampleRequestedEventArgs args)
        {
            Debug.WriteLine("GeneratorStreamSourceFactory.OnSampleRequestedAsync()");

            var deferral = args.Request.GetDeferral();

            try
            {
                for (; ; )
                {
                    await _haveSamples.WaitAsync().ConfigureAwait(false);

                    var sample = GetSample();

                    if (null != sample)
                    {
                        Debug.WriteLine("GeneratorStreamSourceFactory.OnSampleRequestedAsync() sample " + sample.Timestamp + " " + sample.Duration);
                        args.Request.Sample = sample;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GeneratorStreamSourceFactory.OnSampleRequestedAsync() failed: " + ex.Message);
            }
            finally
            {
                deferral.Complete();
            }
        }

        Task CreateSamplesAsync()
        {
            try
            {
                MediaStreamSample sample = null;

                for (; ; )
                {
                    int count;

                    lock (_lock)
                    {
                        if (_clear)
                        {
                            _clear = false;
                            _samples.Clear();
                        }

                        if (null != sample)
                            _samples.Enqueue(sample);

                        count = _samples.Count;
                    }

                    if (IsCancellationRequested)
                    {
                        _haveSamples.Set();
                        _readyToStart.Set();

                        break;
                    }

                    if (0 != count)
                        _haveSamples.Set();
                    else
                        _haveSamples.Reset();

                    if (count >= MaxSamples)
                    {
                        _readyToStart.Set();
                        break;
                    }

                    _generator(Position, _buffer);

                    sample = CreateSample(_buffer);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GeneratorStreamSourceFactory.CreateSamplesAsync() failed: " + ex.Message);
            }

            return TplTaskExtensions.CompletedTask;
        }
    }

    public class GeneratorStreamSourceFactory : IGeneratorStreamSourceFactory<MediaStreamSource>
    {
        #region IGeneratorStreamSourceFactory<MediaStreamSource> Members

        public IStreamSourceFactory<MediaStreamSource> CreateFactory(Action<ulong, byte[]> generator)
        {
            return new StreamSourceFactory(generator);
        }

        #endregion

        #region Nested type: StreamSourceFactory

        class StreamSourceFactory : IStreamSourceFactory<MediaStreamSource>
        {
            readonly Action<ulong, byte[]> _generator;

            public StreamSourceFactory(Action<ulong, byte[]> generator)
            {
                if (null == generator)
                    throw new ArgumentNullException("generator");

                _generator = generator;
            }

            #region IStreamSourceFactory<MediaStreamSource> Members

            public MediaStreamSource CreateSource(IAudioStreamSourceParameters audioStreamSourceParameters)
            {
                var source = new GeneratorStreamSource(_generator, audioStreamSourceParameters);

                return source.OpenSource();
            }

            #endregion
        }

        #endregion
    }
}
