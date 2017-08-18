## Phone and Media Element

* If you start getting Error 3100s, then reboot the phone.  The MediaElement has died.

* MediaElement doesn't seem to like being on the stack more than once.  Hence, be careful of doing anything that might call back into MediaElement.  It doesn't always blow up...

* MediaElement doesn't seem to like being attached to the visual tree when the view changes (hence the OnNavigatedFrom code to detach it).  I took it a step further by getting rid of it entirely; that may not be required.
  
* Do not submit data to MediaElement until after seeks are done (including the first seek triggered by the open media).

* The phone has a web cache.  I'm not sure exactly what the rules are, but it can make reloading live playlists fun.  (A proxy server, fiddler, or Wireshark can be helpful here.)

* If experiencing corrupted playback, compare the stream contents to the [Supported media codecs for Windows Phone](http://msdn.microsoft.com/en-us/library/windowsphone/develop/ff462087(v=vs.105).aspx).

* Here are some details regarding [Implementing MediaStream Sources](http://msdn.microsoft.com/en-us/library/hh180779(v=vs.95).aspx) and [Troubleshooting Media Issues](http://msdn.microsoft.com/en-us/library/hh180774(v=vs.95).aspx).