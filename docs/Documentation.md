# Windows Phone Streaming Media

More information about how [Phone and MediaElement](Phone-and-MediaElement) work and some [Random To Do Notes](Random-To-Do-Notes) regarding this project may be helpful

## Build Requirements

Building the WP7 and Silverlight applications is only supported in Visual Studio 2012.

Building the Windows 8.1/Windows Phone 8.1 applications requires [Visual Studio 2013](https://www.visualstudio.com/products/visual-studio-community-vs) or later.

The WP8 and console applications can be built in either VS2012 or VS2013.

[Player Framework v2.0](https://playerframework.codeplex.com/releases) should be installed in order to build the SamplePlayer applications.  The HlsView applications do not require Player Framework.

[Visual Studio Community 2013](https://www.visualstudio.com/products/visual-studio-community-vs) is free for most non-Enterprise developers and works for both source and binary builds.

## For VS2012
For targeting WP7 (for emulation or device), the WP8 emulator, and for the console applications, use the "Mixed Platforms" solution platform.  The ARM platform is only for targeting WP8 devices.  (You can find it as the "Active solution platform:" box in the "Build" -> "Configuration Manager..." window.)  If odd things are going on with the build, remember to double-check the platform.

The [Windows Phone SDK 8.0](http://www.microsoft.com/en-us/download/details.aspx?id=35471) is required when building for WP8.  The [Silverlight 5 SDK](https://www.microsoft.com/en-us/download/details.aspx?id=28359) is required when building for Silverlight. The [SDK update for Windows Phone 7.8](http://www.microsoft.com/en-us/download/details.aspx?id=36474) should be used when building for WP7.

[Visual Studio 2012 Update 4](https://www.visualstudio.com/news/2013-nov-13-vs) is recommend (and may be necessary).

## For VS2013
The solution platform should match what is being targeted (x86, x64, or ARM).  As with VS2012, the emulators will need x86.

[Visual Studio 2013 Update 4](https://www.microsoft.com/en-us/download/details.aspx?id=44921) or later is recommended.

Building for WP7 and Silverlight is not supported in Visual Studio 2013.

## For VS2015 RC
The Community edition of [Visual Studio 2015 RC](https://www.visualstudio.com/en-us/downloads/visual-studio-2015-downloads-vs.aspx) appears to work with the VS2013 solution, but it has not been tested extensively.

# From Source

If VS2012 Professional or higher is available, building from the full [source code](https://phonesm.codeplex.com/SourceControl/BrowseLatest) is suggested.

Open the solution (HlsPlayer.sln or HlsPlayer.VS2013.sln) and it should be ready to go.  If enabled, the required NuGet packages will be fetched by the package manager as part of the build.  (The Package Manager's options can be found under "Tools" -> "Options" -> "Package Manager" -> "General".)

The SamplePlayer applications are likely the best starting point for an application, but for debugging streaming issues or for working with this code base generally, the HlsView applications may be simpler since they do not involve interacting with a full-featured player.  In those cases where the issue is not directly related to the decoded audio or video, the SimulatedPlayer console application may be a better choice.   Nice as the phone tools are, debugging a desktop application is even better (where the "Parallel Tasks" window, Edit & Continue, and the like are available).

For more details, see [Source Code](Source-Code).

# From Binary

The zip file contains sample WP7, WP8, WP8.1, Silverlight and Windows 8.1 applications and binaries for SM.Media, SM.Builder (for DI configuration), and SM.Media.Platform.*.

Error reporting beyond Debug.WriteLine and other customizations for SamplePlayer can be done in the SM.Media.MediaPlayer.* projects, which is why they are supplied in source form.

## Selecting the Stream

When playing HLS Master Playlists, the default behavior selects the first media playlist in the file.  To change this, SM.Media.Hls.HlsPlaylistSegmentManagerPolicy.SelectSubProgram can be set before playback starts.  For example, to play the highest available bandwidth:
{code:c#}
HlsPlaylistSegmentManagerPolicy.SelectSubProgram =
    programs => programs.OrderByDescending(p => p.Bandwidth)
                        .FirstOrDefault();
{code:c#}
or, equivalently:
{code:c#}
HlsPlaylistSegmentManagerPolicy.SelectSubProgram =
    programs => (from program in programs
                 orderby program.Bandwidth descending
                 select program)
        .FirstOrDefault();
{code:c#}
