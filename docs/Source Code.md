# Overview

The solution is organized into a number of assemblies, but the most important is the Portable Class Library (PLC), SM.Media located in the “Libraries” solution folder.  The bulk of the code is here; only such things that have platform-specific dependencies are implemented in the platform-specific support assemblies (SM.Media, SM.Media.Platform.Desktop, SM.Media.Platform.WP7, and SM.Media.Platform.WP8).  The Desktop library is present to facilitate debugging with the SimulatedPlayer console application.  The only major component not implemented in SM.Media is the MPEG-2 Transport Stream demultiplexer; it is in the SM.TsParser project.

There are some console applications that can be useful for testing and debugging the system located in the “Console” solution folder.  TsDump parses a transport stream and dumps its structure as human readable text output.  The SimulatedPlayer application implements a simulated media stream source capable of exercising the rest of the system.  This can simplify debugging since a console application can be restart much faster than a phone application and allows one to take advantage of such things as edit/continue and the Parallel Task window. 

The “Phone” solution folder contains the phone applications.  HlsView and HlsView.WP8 are meant for exercising TsMediaStreamSource, not as a template for a well architected application.  The SamplePlayer.WP7 application uses Microsoft’s SMF player ([http://smf.codeplex.com/](http://smf.codeplex.com/)) to play an HLS stream and the SamplePlayer.WP8 application uses Microsoft’s Player Framework ([http://playerframework.codeplex.com/](http://playerframework.codeplex.com/)) to play an HLS stream.

## SM.Media

SM.Media implements a MediaStreamSource called TsMediaStreamSource capable of playing HTTP Live Streams (HLS).

The folders in this project include,
|| Folder || Notes ||
| AAC | Implements a CodecPrivateData generator and support for handling AAC audio streams demuxed from Transport Streams. |
| Configuration | Interfaces used between the media stream and the rest of the system. |
| H264 | Implements a CodecPrivateData generator and support for handling H.264 video streams demuxed from Transport Streams. (Including NAL and RBSP decoding.) |
| M3U8  | Contains an M3U8/M3U parser and generator (it may not round-trip everything exactly). |
| Mmreg | Contains helper classes for dealing with the multimedia structures defined in Microsoft’s “mmreg.h” header file. |
| MP3 | Implements a CodecPrivateData generator and support for handling MP3 audio streams demuxed from Transport Streams as well as stand-alone MP3 streams (e.g., those with an “.mp3” file extension). |
| Pes | Implements classes for dealing with MPEG PES streams. |
| Playlists | Contains the classes that represent the actual playlists (typically read from M3U8 files). |
| Segments | Provides interfaces and classes for representing individual media stream segments. |


## Notes

The primary eyesore is the state of TsMediaManager/TsMediaStreamSource/TsMediaParser. I went through an iterative, "huh, that doesn't work, I'll have to try something else," process while trying to figure out how to implement a MediaStreamSource without causing MediaElement to fall over. Until I figured out that WP7 sometimes goes into a mode where keeps returning error 3100 whenever one tries to play media until one reboots (I think it is only when one stops the debugger while MediaElement is playing something), some of the, "something else," tries got creatively desperate.

The main functionality that is missing is some sane way to select the bitrate.

An AAC frame splitter would be useful to complement the MP3 frame splitter.

Here are some [Random To Do Notes](Random-To-Do-Notes). 

Some notes regarding the developing with the [Phone and MediaElement](Phone-and-MediaElement) may be helpful.

