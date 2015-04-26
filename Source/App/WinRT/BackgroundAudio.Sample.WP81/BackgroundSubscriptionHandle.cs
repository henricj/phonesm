// -----------------------------------------------------------------------
//  <copyright file="BackgroundSubscriptionHandle.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using Windows.Media.Playback;

namespace BackgroundAudio.Sample
{
    sealed class BackgroundSubscriptionHandle : IDisposable
    {
        readonly EventHandler<MediaPlayerDataReceivedEventArgs> _eventHandler;
        readonly object _lock = new object();
        bool _isSubscribed;

        public BackgroundSubscriptionHandle(EventHandler<MediaPlayerDataReceivedEventArgs> eventHandler)
        {
            _eventHandler = eventHandler;
        }

        public bool IsSubscribed
        {
            get { lock (_lock) return _isSubscribed; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            Unsubscribe();
        }

        #endregion

        public bool Subscribe()
        {
            Debug.WriteLine("BackgroundSubscriptionHandle.Subscribe()");

            lock (_lock)
            {
                if (_isSubscribed)
                    return true;

                BackgroundMediaPlayer.MessageReceivedFromBackground += _eventHandler;

                _isSubscribed = true;

                return false;
            }
        }

        public bool Unsubscribe()
        {
            Debug.WriteLine("BackgroundSubscriptionHandle.Unsubscribe()");

            lock (_lock)
            {
                var wasSubscribed = _isSubscribed;

                _isSubscribed = false;

                BackgroundMediaPlayer.MessageReceivedFromBackground -= _eventHandler;

                return wasSubscribed;
            }
        }
    }
}
