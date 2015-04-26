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
using Windows.Media.Playback;
using Windows.System;
using Windows.UI.Xaml;
using SM.Media.Utility;

namespace SM.Media.BackgroundAudio
{
    public sealed class SmMediaBackgroundAudioTask : IBackgroundTask
    {
        BackgroundAudioRun _run;
        const uint RequiredMemory = 2 * 1024 * 1024;

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

            MemoryDiagnostics.DumpMemory();

            var usage = MemoryManager.AppMemoryUsage;
            var limit = MemoryManager.AppMemoryUsageLimit;

            if (usage + RequiredMemory > limit)
            {
                Debug.WriteLine("*** SmMediaBackgroundAudioTask.RunAsync() low memory");

                // We can't play anything because there isn't enough memory.  Force
                // the process to restart.

                try
                {
                    Application.Current.Exit();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SmMediaBackgroundAudioTask.RunAsync() exit failed " + ex.ExtendedMessage());
                }

                // Exit() didn't help, so we hog as much as we can of the remaining memory.

                MemoryHog.ConsumeAllMemory();

                BackgroundMediaPlayer.Shutdown();

                return;
            }

            var deferralId = Guid.NewGuid();

            try
            {
                Debug.WriteLine("SmMediaBackgroundAudioTask.RunAsync() getting deferral " + deferralId);

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
                {
                    deferral.Complete();
                    Debug.WriteLine("SmMediaBackgroundAudioTask.RunAsync() released deferral " + deferralId);
                }
            }
        }
    }
}
