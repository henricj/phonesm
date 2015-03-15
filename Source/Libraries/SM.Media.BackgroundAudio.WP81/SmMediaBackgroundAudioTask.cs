// -----------------------------------------------------------------------
//  <copyright file="SmMediaBackgroundAudioTask.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using SM.Media.Utility;

namespace SM.Media.BackgroundAudio
{
    public sealed class SmMediaBackgroundAudioTask : IBackgroundTask
    {
        BackgroundAudioRun _run;

#if DEBUG
        readonly MemoryDiagnostics _memoryDiagnostics = new MemoryDiagnostics();
#endif

        #region IBackgroundTask Members

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            Debug.WriteLine("SmMediaBackgroundAudioTask.Run() " + taskInstance.Task.Name + " instance " + taskInstance.InstanceId);

            var task = RunAsync(taskInstance);

            TaskCollector.Default.Add(task, "Play");
        }

        #endregion

        async Task RunAsync(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral deferral = null;
            BackgroundAudioRun run = null;

            try
            {
                deferral = taskInstance.GetDeferral();

                try
                {
#if DEBUG
                    _memoryDiagnostics.StartPoll();
#endif

                    run = new BackgroundAudioRun(taskInstance.InstanceId);

                    var oldRun = Interlocked.Exchange(ref _run, run);

                    if (null != oldRun)
                    {
                        Debug.WriteLine("SmMediaBackgroundAudioTask.Run() run already exists");
                        oldRun.Dispose();
                    }

                    taskInstance.Canceled += run.OnCanceled;
                    taskInstance.Task.Completed += run.OnTaskCompleted;

                    await run.ExecuteAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SmMediaBackgroundAudioTask.RunAsync() failed " + ex.ExtendedMessage());
                }

                try
                {
#if DEBUG
                    _memoryDiagnostics.StopPoll();
#endif

                    var currentRun = Interlocked.CompareExchange(ref _run, null, run);

                    if (!ReferenceEquals(currentRun, run))
                        Debug.WriteLine("SmMediaBackgroundAudioTask.RunAsync() mismatching run");

                    if (null != run)
                    {
                        taskInstance.Canceled -= run.OnCanceled;
                        taskInstance.Task.Completed -= run.OnTaskCompleted;
                    }

                    if (null != run)
                        run.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SmMediaBackgroundAudioTask.RunAsync() cleanup failed: " + ex.ExtendedMessage());
                }
            }
            finally
            {
                if (null != deferral)
                    deferral.Complete();
            }
        }
    }
}
