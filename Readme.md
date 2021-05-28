# Archived

Since this project primarily targets Windows Phone and Windows 8.1, it is no longer supported either here or by Microsoft (extended support for Windows 8.1 ends in January 2023).  Some of the utilities and transport stream code could be useful for other purposes, but if that should become relevant, another repository will be created.

# Project Description

HTTP Live Streaming (HLS) and MPEG-2 Transport Stream support for Windows Phone 8, Windows 8.1, and Windows Phone 8.1.  The legacy branch has support for Windows Phone 7 and Silverlight.

A MediaStreamSource has been implemented that supports HTTP Live Streaming (HLS).  It supports playing MPEG-2 transport streams (.ts segments) containing H.264 video and MP3 or AAC audio as well as MP3 streams (.mp3 segments) and AAC streams (.aac segments) on Windows Phone 7, Windows Phone 8, Silverlight, and Windows 8.1/Windows Phone 8.1. Both live and prerecorded programs can be played, but seek only works for prerecorded programs.  Playback of "AES-128" mode encrypted streams is supported.

Raw MPEG-2 transport streams, MP3 streams, and AAC streams can be played.  Note that stream types already supported by the underlying platform should probably be left to the platform (e.g., MP3).

There is also limited support for PLS playlists.  Only the first entry is played, which is sufficient to play many internet radio stations.

In addition to the formats supported on the other platforms, Windows 8.1 also supports transport streams containing MPEG-2 video and/or AC-3 audio and .ac3 streams as long as the operating system has support for those media types.

There are sample application for all but Silverlight that use the Player Framework ([http://playerframework.codeplex.com/](http://playerframework.codeplex.com/)). Both WP7 and WP8 have background audio player sample applications.  All of the platforms have sample code that initializes and plays media in a MediaElement.

Visual Studio 2015 is required.

For the legacy branch, Visual Studio 2012 Professional or better (Express does not support Portable Class Libraries) and the Windows Phone 8 SDK is required.

See the [Documentation](Documentation) for more information.


## Limitations

* The Player Framework plugin is fleshed out enough to start a stream, pause it, and seek.  Dealing with many practical application concerns like cleanly shutting down playback, dealing with navigation, and tombstoning needs to be implemented.  The HlsView application handles some of these issues, but does so rather inelegantly (i.e., everything is in MainPage.xaml.cs). 
* There is no support for changing the bitrate during playback. Neither manual control nor automatic bitrate selection is available.
* Error reporting is primitive and consists mainly of Debug.WriteLine() calls.  The HlsView apps report exceptions that end playback, but the SamplePlayer apps do not.
