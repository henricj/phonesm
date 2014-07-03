// -----------------------------------------------------------------------
//  <copyright file="App.xaml.cs" company="Henric Jungheim">
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
using System.Net;
using System.Net.Browser;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Browser;
using SM.Media.AAC;

namespace HlsView.Silverlight
{
    public partial class App : Application
    {
        public App()
        {
            Startup += Application_Startup;
            Exit += Application_Exit;
            UnhandledException += Application_UnhandledException;

            //TaskScheduler.UnobservedTaskException += Application_UnobservedException;

            AacDecoderSettings.Parameters.UseRawAac = true;
            AacDecoderSettings.Parameters.ConfigurationFormat = AacDecoderParameters.WaveFormatEx.RawAac;

            var httpResult = WebRequest.RegisterPrefix("http://", WebRequestCreator.ClientHttp);
            var httpsResult = WebRequest.RegisterPrefix("https://", WebRequestCreator.ClientHttp);

            Debug.WriteLine("Defaulting to client HTTP handling. http {0} https {1}", httpResult, httpsResult);

            InitializeComponent();
        }

        void Application_Startup(object sender, StartupEventArgs e)
        {
            RootVisual = new MainPage();
        }

        void Application_Exit(object sender, EventArgs e)
        { }

        void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            Debug.WriteLine("*** Unhandled exception: " + e.ExceptionObject.Message);

            // If the app is running outside of the debugger then report the exception using
            // the browser's exception mechanism. On IE this will display it a yellow alert 
            // icon in the status bar and Firefox will display a script error.
            if (!Debugger.IsAttached)
            {
                Debugger.Break();

                // NOTE: This will allow the application to continue running after an exception has been thrown
                // but not handled. 
                // For production applications this error handling should be replaced with something that will 
                // report the error to the website and stop the application.
                //e.Handled = true;
                Deployment.Current.Dispatcher.BeginInvoke(() => ReportErrorToDOM(e.ExceptionObject));
            }
        }

        void Application_UnobservedException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine("*** Unobserved task exception {0}", e.Exception.Message);

            if (Debugger.IsAttached)
                Debugger.Break();

            Deployment.Current.Dispatcher.BeginInvoke(() => ReportErrorToDOM(e.Exception));
        }

        void ReportErrorToDOM(Exception ex)
        {
            try
            {
                var errorMsg = ex.Message + ex.StackTrace;
                errorMsg = errorMsg.Replace('"', '\'').Replace("\r\n", @"\n");

                HtmlPage.Window.Eval("throw new Error(\"Unhandled Error in Silverlight Application " + errorMsg + "\");");
            }
            catch (Exception)
            { }
        }
    }
}
