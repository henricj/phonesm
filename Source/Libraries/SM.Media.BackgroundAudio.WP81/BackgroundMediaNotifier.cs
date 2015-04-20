// -----------------------------------------------------------------------
//  <copyright file="BackgroundMediaNotifier.cs" company="Henric Jungheim">
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

namespace SM.Media.BackgroundAudio
{
    interface IBackgroundMediaNotifier
    {
        void Notify(ValueSet valueSet);
    }

    abstract class BackgroundMediaNotifier : IBackgroundMediaNotifier
    {
        readonly Guid _id;

        protected BackgroundMediaNotifier(Guid id)
        {
            _id = id;
        }

        #region IBackgroundMediaNotifier Members

        public void Notify(ValueSet valueSet)
        {
            valueSet.Add(BackgroundNotificationType.Id, _id);

            try
            {
                SendMessage(valueSet);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioNotifier.Notify() failed: " + ex.Message);
            }
        }

        #endregion

        protected abstract void SendMessage(ValueSet valueSet);
    }

    static class BackgroundAudioNotifierExtensions
    {
        public static void Notify(this IBackgroundMediaNotifier notifier, BackgroundNotificationType type, object value = null)
        {
            //Debug.WriteLine("NotifierExtensions.Notify() " + _id);

            var valueSet = new ValueSet { { type.ToString(), value } };

            notifier.Notify(valueSet);
        }

        public static void Add(this ValueSet valueSet, BackgroundNotificationType type, object value = null)
        {
            valueSet.Add(type.ToString(), value);
        }

        public static bool TryGetValue(this ValueSet valueSet, BackgroundNotificationType type, out object value)
        {
            return valueSet.TryGetValue(type.ToString(), out value);
        }
    }
}
