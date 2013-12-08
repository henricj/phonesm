// -----------------------------------------------------------------------
//  <copyright file="Settings.xaml.cs" company="Henric Jungheim">
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
using System.Windows.Controls;
using Microsoft.Phone.Controls;
using NasaTv;
using NasaTv8.ViewModels;

namespace NasaTv8.Views
{
    public partial class Settings : PhoneApplicationPage
    {
        readonly PersistentSettings _persistedSettings = new PersistentSettings();
        readonly NasaTvSettings _settings = new NasaTvSettings();

        public Settings()
        {
            InitializeComponent();

            _settings.Load(_persistedSettings);

            DataContext = _settings;
        }

        void doneButton_Click(object sender, EventArgs e)
        {
            ForceBinding();

            _settings.Save(_persistedSettings);
        }

        void ForceBinding()
        {
            // The appbar does not change focus, therefore the
            // url box could still have changed data that has not
            // yet fired PropertyChangedEventHandler.

            var binding = videoUrl.GetBindingExpression(TextBox.TextProperty);
            if (null != binding)
                binding.UpdateSource();
        }

        void cancelButton_Click(object sender, EventArgs e)
        {
            ForceBinding();

            _settings.Load(_persistedSettings);
        }

        void resetButton_Click(object sender, EventArgs e)
        {
            ForceBinding();

            _persistedSettings.ResetToDefaults();
            _settings.Load(_persistedSettings);
        }
    }
}
