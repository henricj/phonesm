// -----------------------------------------------------------------------
//  <copyright file="ForegroundNotifier.cs" company="Henric Jungheim">
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
using Windows.Foundation.Collections;
using Windows.Media.Playback;

namespace SM.Media.BackgroundAudio
{
    sealed class ForegroundNotifier
    {
        readonly Guid _id;

        public ForegroundNotifier(Guid id)
        {
            _id = id;
        }

        public void Notify(string key, object value = null)
        {
            //Debug.WriteLine("ForegroundNotifier.Notify() " + _id);

            var valueSet = new ValueSet { { key, value } };

            Notify(valueSet);
        }

        public void Notify(ValueSet valueSet)
        {
            valueSet["Id"] = _id;

            try
            {
                BackgroundMediaPlayer.SendMessageToForeground(valueSet);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ForegroundNotifier.Notify() failed: " + ex.Message);
            }
        }
    }
}
