// -----------------------------------------------------------------------
//  <copyright file="NoiseSourceFactory.cs" company="Henric Jungheim">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using SM.Media.Utility;
using SM.Media.Utility.RandomGenerators;

namespace SM.Media.Audio.Generator
{
    public class NoiseStreamSource
    {
        public const float PhaseToRadian = (float)(-Math.PI / int.MinValue);
        const int MaxSamples = 4;

        static readonly float[] FilterCoefficients =
        {
            0.036184894812462634f,
            0.08439978131974415f,
            -0.030587228370937136f,
            -0.10882632146270712f,
            0.28584137527602943f,
            0.6317533613378057f,
            0.28584137527602943f,
            -0.10882632146270712f,
            -0.030587228370937136f,
            0.08439978131974415f,
            0.036184894812462634f
        };

        readonly byte[] _buffer;
        readonly TimeSpan _bufferDuration;
        readonly CancellationTokenSource _closedCancellationTokenSource = new CancellationTokenSource();
        readonly AudioEncodingProperties _encodingProperties;
        readonly ConcurrentStack<IBuffer> _freeBuffers = new ConcurrentStack<IBuffer>();
        readonly int _frequency;
        readonly NormalDistribution _gaussianNoise;
        readonly AsyncManualResetEvent _haveSamples = new AsyncManualResetEvent();
        readonly ImpulseNoise _impulseNoise;
        readonly object _lock = new object();
        readonly FirFilter _lp12kHzFilter;
        readonly PinkNoise _pinkNoise;
        readonly Queue<MediaStreamSample> _samples = new Queue<MediaStreamSample>();
        readonly SignalTask _worker;
        int _alloc;
        int _free;
        int _phase;
        ulong _position;
        TimeSpan _time;

        public NoiseStreamSource(IRandomGenerator randomGenerator)
        {
            if (null == randomGenerator)
                throw new ArgumentNullException("randomGenerator");

            var randomGenerator1 = randomGenerator;
            _gaussianNoise = new NormalDistribution(randomGenerator1, 0f, 0.1f);

            _encodingProperties = AudioEncodingProperties.CreatePcm(44100, 1, 16);

            var bytesPerSecond = _encodingProperties.BitsPerSample / 8 * _encodingProperties.SampleRate * _encodingProperties.ChannelCount;

            _frequency = GetFrequency(50);

            //var stepsPerCycle = ((double)(1L << 32)) / _frequency;

            //var cyclesPerSecond = (double)_encodingProperties.SampleRate * _frequency / (1L << 32);

            var bufferSize = bytesPerSecond / 4;

            if (0 != (bufferSize & 1))
                bufferSize += 1;

            if (null == _buffer || _buffer.Length != bufferSize)
                _buffer = new byte[bufferSize];

            _bufferDuration = TimeSpan.FromTicks((_buffer.Length * (10L * 1000 * 1000) + bytesPerSecond / 2) / bytesPerSecond);

            _impulseNoise = new ImpulseNoise(randomGenerator1, 425.0);
            _pinkNoise = new PinkNoise(randomGenerator1, 0.2f);

            _worker = new SignalTask(CreateSamplesAsync);
            _lp12kHzFilter = new FirFilter(FilterCoefficients);
        }

        int GetFrequency(float hz)
        {
            return (int)Math.Round((hz * (double)(1L << 32)) / _encodingProperties.SampleRate);
        }

        public MediaStreamSource CreateSource()
        {
            _worker.Fire();

            var descriptor = new AudioStreamDescriptor(_encodingProperties);

            var mss = new MediaStreamSource(descriptor);

            mss.Starting += MssOnStarting;
            mss.SampleRequested += MssOnSampleRequested;
            mss.Closed += MssOnClosed;

            return mss;
        }

        void MssOnClosed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            Debug.WriteLine("NoiseSource.MssOnClosed()");

            _closedCancellationTokenSource.Cancel();

            sender.Starting -= MssOnStarting;
            sender.SampleRequested -= MssOnSampleRequested;
            sender.Closed -= MssOnClosed;
        }

        void MssOnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            Debug.WriteLine("NoiseSource.MssOnSampleRequested()");

            if (_closedCancellationTokenSource.IsCancellationRequested)
                return;

            var sample = GetSample();

            if (null != sample)
            {
                Debug.WriteLine("NoiseSource.MssOnSampleRequested() sample " + sample.Timestamp + " " + sample.Duration);
                args.Request.Sample = sample;
                return;
            }

            var task = OnSampleRequestedAsync(args);

            TaskCollector.Default.Add(task, "NoiseSource MssOnSampleRequested");
        }

        MediaStreamSample GetSample()
        {
            MediaStreamSample sample = null;
            int count;

            if (_closedCancellationTokenSource.IsCancellationRequested)
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
            Debug.WriteLine("NoiseSource.OnSampleRequestedAsync()");

            var deferral = args.Request.GetDeferral();

            try
            {
                for (; ; )
                {
                    await _haveSamples.WaitAsync().ConfigureAwait(false);

                    var sample = GetSample();

                    if (null != sample)
                    {
                        Debug.WriteLine("NoiseSource.OnSampleRequestedAsync() sample " + sample.Timestamp + " " + sample.Duration);
                        args.Request.Sample = sample;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NoiseSource.OnSampleRequestedAsync() failed: " + ex.Message);
            }
            finally
            {
                deferral.Complete();
            }
        }

        void MssOnStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            Debug.WriteLine("NoiseSource.MssOnStarting()");

            args.Request.SetActualStartPosition(TimeSpan.Zero);
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
                        if (null != sample)
                            _samples.Enqueue(sample);

                        count = _samples.Count;
                    }

                    if (_closedCancellationTokenSource.IsCancellationRequested)
                    {
                        _haveSamples.Set();
                        break;
                    }

                    if (0 != count)
                        _haveSamples.Set();
                    else
                        _haveSamples.Reset();

                    if (count >= MaxSamples)
                        break;

                    //_randomGenerator.GetBytes(_buffer);

                    for (var i = 0; i < _buffer.Length; i += 2, ++_position)
                    {
                        unchecked
                        {
                            var radians = PhaseToRadian * _phase;

                            var degrees = radians * (180 / Math.PI);

                            var f = 0f; //0.15f * ((float)Math.Sin(radians));

                            //f += _gaussianNoise.Next();

                            //f = 0f;

                            f += 0.02f * _impulseNoise.Next(_position);

                            f = _lp12kHzFilter.Filter(f);

                            f += 1.1f * _pinkNoise.Next();

                            f += 0.05f * ((float)Math.Sin(radians));

                            var ff = short.MinValue * f;

                            short v;

                            if (ff >= short.MaxValue - 2)
                                v = short.MaxValue - 2;
                            else if (ff <= short.MinValue + 2)
                                v = short.MinValue + 2;
                            else
                                v = (short)ff;

                            _phase += _frequency;

#if true
                            _buffer[i] = (byte)v;
                            _buffer[i + 1] = (byte)(v >> 8);
#else
#if true
                            v += 128;

                            if (v > 255)
                                v = 255;
                            else if (v < 0)
                                v = 0;
#else
                            if (v > 127)
                                v = 127;
                            else if (v < -128)
                                v = 128;
#endif

                            _buffer[i] = (byte)v;
#endif
                        }
                    }

                    //for (var i = 0; i < _buffer.Length; ++i)
                    //{
                    //    unchecked
                    //    {
                    //        var v = (short)Math.Round(_normalDistribution.Next());

                    //        if (v > 127)
                    //            v = 127;
                    //        else if (v < -128)
                    //            v = 128;

                    //        _buffer[i] = (byte)v;
                    //    }
                    //}


                    IBuffer buffer;
                    if (_freeBuffers.TryPop(out buffer))
                        _buffer.CopyTo(buffer);
                    else
                        buffer = WindowsRuntimeBuffer.Create(_buffer, 0, _buffer.Length, _buffer.Length);

                    sample = MediaStreamSample.CreateFromBuffer(buffer, _time);

                    Interlocked.Increment(ref _alloc);

                    sample.Processed += SampleOnProcessed;

                    //_randomGenerator.GetBytes(_buffer);

                    sample.Duration = _bufferDuration;

                    _time += _bufferDuration;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NoiseSource.CreateSamplesAsync() failed: " + ex.Message);
            }

            return TplTaskExtensions.CompletedTask;
        }

        void SampleOnProcessed(MediaStreamSample mss, object obj)
        {
            _freeBuffers.Push(mss.Buffer);

            Interlocked.Increment(ref _free);

            mss.Processed -= SampleOnProcessed;
        }
    }
}
