// -----------------------------------------------------------------------
//  <copyright file="ApplicationInformation.cs" company="Henric Jungheim">
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;

namespace SM.Media.Utility
{
    public static class ApplicationInformationFactory
    {
        static readonly Task<IApplicationInformation> CreateTask = Create();

        static async Task<IApplicationInformation> Create()
        {
            try
            {
                XDocument xmldoc;
                using (var manifestStream = await Package.Current.InstalledLocation.OpenStreamForReadAsync("AppxManifest.xml").ConfigureAwait(false))
                {
                    xmldoc = XDocument.Load(manifestStream);
                }

                var identity = XName.Get("Identity", "http://schemas.microsoft.com/appx/2010/manifest");

                var version = xmldoc.Descendants(identity).Select(i => i.Attribute("Version").Value).FirstOrDefault();

                var processor = xmldoc.Descendants(identity).Select(i => i.Attribute("ProcessorArchitecture").Value);


                var visualElements = XName.Get("VisualElements", "http://schemas.microsoft.com/appx/2013/manifest");

                var displayName = xmldoc.Descendants(visualElements)
                                                   .Select(ve => ve.Attribute("DisplayName").Value)
                                                   .FirstOrDefault();

                if (null == displayName)
                {

                    var displayNameElementName = XName.Get("DisplayName", "http://schemas.microsoft.com/appx/2010/manifest");

                    var displayNameElement = xmldoc.Descendants(displayNameElementName).FirstOrDefault();

                    displayName = null == displayNameElement ? "unknown" : displayNameElement.Value;
                }

                return new ApplicationInformation(displayName, version);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ApplicationInformatioNFactory.Create() failed: " + ex.Message);
            }

            return new ApplicationInformation(null, null);
        }

       static  public Task<IApplicationInformation> DefaultTask { get { return CreateTask; } }
    }
}
