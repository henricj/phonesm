// -----------------------------------------------------------------------
//  <copyright file="PlsSegmentManagerPolicy.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;

namespace SM.Media.Pls
{
    public interface IPlsSegmentManagerPolicy
    {
        Task<Uri> GetTrackAsync(PlsParser pls, ContentType contentType, CancellationToken cancellationToken);
    }

    public class PlsSegmentManagerPolicy : IPlsSegmentManagerPolicy
    {
        public static Func<ICollection<PlsTrack>, PlsTrack> SelectTrack = tracks => tracks.FirstOrDefault();

        #region IPlsSegmentManagerPolicy Members

        public Task<Uri> GetTrackAsync(PlsParser pls, ContentType contentType, CancellationToken cancellationToken)
        {
            var tracks = pls.Tracks;

            var track = SelectTrack(tracks);

            if (null == track)
                return TaskEx.FromResult<Uri>(null);

            if (tracks.Count > 1)
                Debug.WriteLine("PlsSegmentManagerPolicy.GetTrackAsync() multiple tracks are not supported");

            if (null == track.File)
                Debug.WriteLine("PlsSegmentManagerPolicy.GetTrackAsync() track does not have a file");

            Uri trackUrl;
            if (!Uri.TryCreate(pls.BaseUrl, track.File, out trackUrl))
            {
                Debug.WriteLine("PlsSegmentManagerPolicy.GetTrackAsync() invalid track file: " + track.File);

                return TaskEx.FromResult<Uri>(null);
            }

            return TaskEx.FromResult(trackUrl);
        }

        #endregion
    }
}
