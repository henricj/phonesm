// -----------------------------------------------------------------------
//  <copyright file="MediaElementManager.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012 Henric Jungheim <software@henric.org>
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SM.Media.Utility;

namespace SM.Media
{
    public class MediaElementManager : IMediaElementManager
    {
        //#if MEDIA_STREAM_STATE_VALIDATION
        //#endif
        readonly Func<MediaElement> _createMediaElement;
        readonly Action<MediaElement> _destroyMediaElement;
        readonly Dispatcher _dispatcher;
        MediaElement _mediaElement;
        MediaStreamFsm _mediaStreamFsm = new MediaStreamFsm();
        int _sourceIsSet;

        public MediaElementManager(Dispatcher dispatcher, Func<MediaElement> createMediaElement, Action<MediaElement> destroyMediaElement)
        {
            _dispatcher = dispatcher;
            _createMediaElement = createMediaElement;
            _destroyMediaElement = destroyMediaElement;

            _mediaStreamFsm.Reset();
        }

        #region IMediaElementManager Members

        public Task SetSource(IMediaStreamSource source)
        {
            return Dispatch(() =>
                            {
                                ValidateEvent(MediaStreamFsm.MediaEvent.MediaStreamSourceAssigned);

                                var wasSet = Interlocked.Exchange(ref _sourceIsSet, 1);

                                Debug.Assert(0 == wasSet);

                                if (null != _mediaElement)
                                {
                                    UiThreadCleanup();

                                    var mediaElement = _mediaElement;
                                    _mediaElement = null;

                                    _destroyMediaElement(mediaElement);
                                }

                                _mediaElement = _createMediaElement();

                                _mediaElement.SetSource((MediaStreamSource)source);
                            });
        }

        public async Task Close()
        {
            var wasSet = Interlocked.CompareExchange(ref _sourceIsSet, 2, 1);

            if (0 != wasSet)
                await Dispatch(() =>
                               {
                                   UiThreadCleanup();

                                   var mediaElement = _mediaElement;
                                   _mediaElement = null;

                                   _destroyMediaElement(mediaElement);
                               });
        }

        public Task Dispatch(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();

                return TplTaskExtensions.CompletedTask;
            }

            return _dispatcher.InvokeAsync(action);
        }

        public void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent)
        {
            _mediaStreamFsm.ValidateEvent(mediaEvent);
        }

        #endregion

        void UiThreadCleanup()
        {
            var was2 = Interlocked.CompareExchange(ref _sourceIsSet, 3, 2);

            if (2 != was2 && 3 != was2)
                return;

            var state = _mediaElement.CurrentState;

            if (MediaElementState.Closed != state && MediaElementState.Stopped != state)
                _mediaElement.Stop();

            state = _mediaElement.CurrentState;

            //if (MediaElementState.Closed == state || MediaElementState.Stopped == state)
            _mediaElement.Source = null;

            state = _mediaElement.CurrentState;

            if (MediaElementState.Closed == state || MediaElementState.Stopped == state)
            {
                var was3 = Interlocked.Exchange(ref _sourceIsSet, 0);

                Debug.Assert(3 == was3);
            }
        }
    }
}