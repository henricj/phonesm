// -----------------------------------------------------------------------
//  <copyright file="StreamSourceBase.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace SM.Media.Audio.Generator
{
    public abstract class StreamSourceBase
    {
#if DEBUG
        int _alloc;
        int _free;
#endif
        readonly CancellationTokenSource _closedCancellationTokenSource = new CancellationTokenSource();
        readonly AudioEncodingProperties _encodingProperties;
        readonly ConcurrentStack<IBuffer> _freeBuffers = new ConcurrentStack<IBuffer>();
        readonly TimeSpan _bufferDuration;
        readonly int _bufferSize;
        TimeSpan _time;
        ulong _position;
        readonly uint _samplesPerBuffer;

        protected StreamSourceBase(IAudioStreamSourceParameters parameters)
            : this(AudioEncodingProperties.CreatePcm(parameters.SampleRate, parameters.Channels, parameters.Is16Bit ? 16u : 8u))
        { }

        protected StreamSourceBase(AudioEncodingProperties encodingProperties)
        {
            if (null == encodingProperties)
                throw new ArgumentNullException("encodingProperties");

            _encodingProperties = encodingProperties;

            var bytesPerSecond = _encodingProperties.BitsPerSample / 8 * _encodingProperties.SampleRate * _encodingProperties.ChannelCount;

            var bufferSize = bytesPerSecond / 4;

            if (0 != (bufferSize & 1))
                bufferSize += 1;

            _bufferSize = (int)bufferSize;

            _bufferDuration = TimeSpan.FromTicks((bufferSize * (10L * 1000 * 1000) + bytesPerSecond / 2) / bytesPerSecond);

            _samplesPerBuffer = bufferSize / (_encodingProperties.ChannelCount * _encodingProperties.BitsPerSample / 8);
        }

        protected AudioEncodingProperties EncodingProperties
        {
            get { return _encodingProperties; }
        }

        protected bool IsCancellationRequested
        {
            get { return _closedCancellationTokenSource.IsCancellationRequested; }
        }

        public TimeSpan BufferDuration
        {
            get { return _bufferDuration; }
        }

        protected uint SamplesPerBuffer
        {
            get { return _samplesPerBuffer; }
        }

        protected int BufferSize
        {
            get { return _bufferSize; }
        }

        protected TimeSpan Time
        {
            get { return _time; }
            set { _time = value; }
        }

        protected ulong Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public virtual MediaStreamSource OpenSource()
        {
            var descriptor = new AudioStreamDescriptor(EncodingProperties);

            var mss = new MediaStreamSource(descriptor);

            mss.Starting += MssOnStarting;
            mss.SampleRequested += MssOnSampleRequested;
            mss.Closed += MssOnClosed;

            return mss;
        }

        public virtual void CloseSource()
        {
            _closedCancellationTokenSource.Cancel();
        }

        #region MSS Event Handlers

        protected virtual void MssOnStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            Debug.WriteLine("StreamSourceBase.MssOnStarting()");

            if (args.Request.StartPosition.HasValue)
            {
                Time = args.Request.StartPosition.Value;
                Position = (ulong)Math.Round(_encodingProperties.SampleRate * Time.TotalSeconds);
            }

            args.Request.SetActualStartPosition(Time);
        }

        protected abstract void MssOnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args);

        protected virtual void MssOnClosed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            Debug.WriteLine("StreamSourceBase.MssOnClosed()");

            _closedCancellationTokenSource.Cancel();

            sender.Starting -= MssOnStarting;
            sender.SampleRequested -= MssOnSampleRequested;
            sender.Closed -= MssOnClosed;
        }

        #endregion

        protected void SampleOnProcessed(MediaStreamSample mss, object obj)
        {
            _freeBuffers.Push(mss.Buffer);

#if DEBUG
            Interlocked.Increment(ref _free);
#endif

            mss.Processed -= SampleOnProcessed;
        }

        protected IBuffer GetBuffer()
        {
            IBuffer buffer;

            if (!_freeBuffers.TryPop(out buffer))
            {
                buffer = WindowsRuntimeBuffer.Create(BufferSize);
                buffer.Length = (uint)BufferSize;
            }

#if DEBUG
            Interlocked.Increment(ref _alloc);
#endif

            return buffer;
        }

        protected IBuffer CopyBuffer(byte[] buffer)
        {
            IBuffer rtBuffer;

            if (_freeBuffers.TryPop(out rtBuffer))
                buffer.CopyTo(rtBuffer);
            else
                rtBuffer = WindowsRuntimeBuffer.Create(buffer, 0, buffer.Length, buffer.Length);

#if DEBUG
            Interlocked.Increment(ref _alloc);
#endif

            return rtBuffer;
        }

        protected MediaStreamSample CreateSample(byte[] buffer)
        {
            var sample = MediaStreamSample.CreateFromBuffer(CopyBuffer(buffer), Time);

            sample.Processed += SampleOnProcessed;

            sample.Duration = _bufferDuration;

            _time += _bufferDuration;
            _position += _samplesPerBuffer;

            return sample;
        }
    }
}
