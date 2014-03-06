msbuild /m HlsPlayer.VS2013.sln /t:Clean "/p:Configuration=Debug;Platform=ARM"
msbuild /m HlsPlayer.VS2013.sln /t:Clean "/p:Configuration=Debug;Platform=x86"
msbuild /m HlsPlayer.VS2013.sln /t:Clean "/p:Configuration=Debug;Platform=x64"

msbuild /m HlsPlayer.VS2013.sln /t:Clean "/p:Configuration=Release;Platform=ARM"
msbuild /m HlsPlayer.VS2013.sln /t:Clean "/p:Configuration=Release;Platform=x86"
msbuild /m HlsPlayer.VS2013.sln /t:Clean "/p:Configuration=Release;Platform=x64"
