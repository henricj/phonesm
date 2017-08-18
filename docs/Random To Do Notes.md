## Things that should be looked at...

* The media play start and stop dance needs cleanup. It is far too complicated.

* Places that await need to be reviewed for missing “.ConfigureAwait()” calls.

* The retry code would probably be cleaner with a static method that takes a parameter argument instead of the way it is now.

* The seek handling should probably return the nearest time that it found rather than waiting for the MediaElement to discard samples until it reaches the requested time.  It is also much too slow (why?).

* Why is stop and seek so slow?  (Are cancellation tokens getting to where they need to be?)

* What should be buffering strategy be?

* The MP3 frame splitter may have issues since I sometimes hear distortion while playing streams (it could be the stream?).  Perhaps the duration is wrong, leading to incorrect timestamps?  Should we be splitting frames for the transport stream case as well? 

* I'm not sure the server fallback works properly as it is, and it does not keep state so it will always try the servers in the order listed in the playlist.

* The low-level network retry code needs to interact with higher level retry code (that way, the playlist load is aware of which segment servers may not be working). 

* Why are so many people having trouble with NuGet’s package restore?

* A number of System.OutOfMemoryException problems inspired the buffer pool management code.  However, it is suspected that the MediaElement may cause memory exhaustion problems when faced with timestamp discontinuities.  The code should handle PTS discontinuities that may arise in real life streams.  It also needs code to do something with legitimate “#EXT-X-DISCONTINUITY” discontinuities.

* Could one twist the ISmoothStreamingCache (or some other Smooth Streaming seam) into something that transforms the M3U8/TS files into Smooth Streaming MP4 fragments…?   Could one think of this as some degenerate form of custom decryption?

* The playlist code should use "#EXT-X-MEDIA-SEQUENCE" when available.