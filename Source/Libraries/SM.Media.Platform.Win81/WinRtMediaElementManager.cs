// -----------------------------------------------------------------------
//  <copyright file="WinRtMediaElementManager.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2014.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2014 Henric Jungheim <software@henric.org>
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
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using SM.Media.MediaParser;
using SM.Media.Utility;

namespace SM.Media
{
    public class WinRtMediaElementManager : IMediaElementManager
    {
        readonly Func<MediaElement> _createMediaElement;
        readonly Action<MediaElement> _destroyMediaElement;
        readonly CoreDispatcher _dispatcher;
        MediaElement _mediaElement;

        public WinRtMediaElementManager(CoreDispatcher dispatcher, Func<MediaElement> createMediaElement, Action<MediaElement> destroyMediaElement)
        {
            _dispatcher = dispatcher;
            _createMediaElement = createMediaElement;
            _destroyMediaElement = destroyMediaElement;
        }

        #region IMediaElementManager Members

        public Task SetSourceAsync(IMediaStreamSource source)
        {
            Debug.WriteLine("WinRtMediaElementManager.SetSourceAsync()");

            var mss = source as WinRtMediaStreamSource;

            if (null == mss)
                throw new ArgumentException("source type is invalid", "source");

            return Dispatch(() =>
                            {
                                Debug.WriteLine("WinRtMediaElementManager.SetSourceAsync() handler");

                                source.ValidateEvent(MediaStreamFsm.MediaEvent.MediaStreamSourceAssigned);

                                if (null != _mediaElement)
                                {
                                    var mediaElement = _mediaElement;
                                    _mediaElement = null;

                                    _destroyMediaElement(mediaElement);
                                }

                                _mediaElement = _createMediaElement();

                                mss.RegisterMediaStreamSourceHandler(ms => Dispatch(() =>
                                                                                    {
                                                                                        Debug.WriteLine("WinRtMediaElementManager.SetSourceAsync() ME state {0} MM state {1} HasThreadAccess {2}",
                                                                                            _mediaElement.CurrentState,
                                                                                            null == mss.MediaManager ? "<Unknown>" :  mss.MediaManager.State.ToString(), _dispatcher.HasThreadAccess);
                                                                                        
                                                                                        _mediaElement.SetMediaStreamSource(ms);

                                                                                        Debug.WriteLine("WinRtMediaElementManager.SetSourceAsync() post set ME state {0} MM state {1} HasThreadAccess {2}",
                                                                                            _mediaElement.CurrentState,
                                                                                            null == mss.MediaManager ? "<Unknown>" : mss.MediaManager.State.ToString(), _dispatcher.HasThreadAccess);
                                                                                    }));
                            });
        }

        public async Task CloseAsync()
        {
            await Dispatch(() =>
                           {
                               var mediaElement = _mediaElement;
                               _mediaElement = null;

                               if (null != mediaElement)
                                   _destroyMediaElement(mediaElement);
                           })
                .ConfigureAwait(false);
        }

        #endregion

        Task Dispatch(Action action)
        {
            if (_dispatcher.HasThreadAccess)
            {
                action();

                return TplTaskExtensions.CompletedTask;
            }

            return _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action()).AsTask();
        }
    }
}
