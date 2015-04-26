// -----------------------------------------------------------------------
//  <copyright file="BackgroundSettings.cs" company="Henric Jungheim">
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
using Windows.Storage;
using SM.Media.Utility;

namespace SM.Media.BackgroundAudio
{
    static class BackgroundSettings
    {
        const string ForegroundId = "ForegroundId";
        const string BackgroundId = "BackgroundId";

        public static Guid? GetBackgroundId()
        {
            return ApplicationData.Current.LocalSettings.Values[BackgroundId] as Guid?;
        }

        public static Guid? GetForegroundId()
        {
            return ApplicationData.Current.LocalSettings.Values[ForegroundId] as Guid?;
        }

        public static void SetBackgroundId(Guid id)
        {
            ApplicationData.Current.LocalSettings.Values[BackgroundId] = id;
        }

        public static void SetForegroundId(Guid id)
        {
            ApplicationData.Current.LocalSettings.Values[ForegroundId] = id;
        }

        public static void RemoveForegroundId()
        {
            RemoveSafe(ForegroundId);
        }

        public static void RemoveForegroundId(Guid id)
        {
            RemoveSafe(ForegroundId, id);
        }

        public static void RemoveBackgroundId()
        {
            RemoveSafe(BackgroundId);
        }

        public static void RemoveBackgroundId(Guid id)
        {
            RemoveSafe(BackgroundId, id);
        }

        static void RemoveSafe(string key)
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values.Remove(key);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundSettings.RemoveSafe() removing \"" + key + "\" failed: " + ex.ExtendedMessage());
            }
        }

        static void RemoveSafe(string key, Guid value)
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values.Remove(new KeyValuePair<string, object>(key, value));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerHandle.RemoveSafe() removing ( " + key + ", " + value + ") failed: " + ex.ExtendedMessage());
            }
        }
    }
}
