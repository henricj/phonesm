// -----------------------------------------------------------------------
//  <copyright file="ApplicationInformationFactory.cs" company="Henric Jungheim">
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
using System.Xml;

namespace SM.Media.Utility
{
    public static class ApplicationInformationFactory
    {
        static readonly IApplicationInformation ApplicationInformation = Initialize();

        static IApplicationInformation Initialize()
        {
            try
            {
                using (var rdr = XmlReader.Create("WMAppManifest.xml",
                    new XmlReaderSettings
                    {
                        XmlResolver = new XmlXapResolver()
                    }))
                {
                    if (!rdr.ReadToDescendant("App") || !rdr.IsStartElement())
                        Debug.WriteLine("Cannot find <App> in WMAppManifest.xml");
                    else
                    {
                        var title = rdr.GetAttribute("Title");
                        var version = rdr.GetAttribute("Version");

                        return new ApplicationInformation(title, version);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ApplicationInformationFactory.Initialize() Unable to create application information: " + ex.Message);
            }

            return new ApplicationInformation(null, null);
        }

        public static IApplicationInformation Default
        {
            get { return ApplicationInformation; }
        }
    }
}
