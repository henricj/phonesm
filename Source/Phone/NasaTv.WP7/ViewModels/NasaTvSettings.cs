// -----------------------------------------------------------------------
//  <copyright file="NasaTvSettings.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NasaTv.Annotations;

namespace NasaTv.ViewModels
{
    public class NasaTvSettings : INotifyPropertyChanged
    {
        Uri _videoUrl;

        public string VideoUrl
        {
            get
            {
                if (null == _videoUrl)
                    return string.Empty;

                return _videoUrl.ToString();
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (null == _videoUrl)
                        return;

                    _videoUrl = null;
                }
                else
                {
                    var url = new Uri(value);

                    if (_videoUrl == url)
                        return;

                    _videoUrl = url;
                }

                OnPropertyChanged();
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        public void Load(PersistentSettings settings)
        {
            VideoUrl = settings.PlaylistUrl.ToString();
        }

        public void Save(PersistentSettings settings)
        {
            settings.PlaylistUrl = new Uri(VideoUrl);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "<invalid>")
        {
            var handler = PropertyChanged;

            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
