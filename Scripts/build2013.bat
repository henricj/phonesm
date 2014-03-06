.nuget\NuGet.exe restore HlsPlayer.VS2013.sln

msbuild /m HlsPlayer.VS2013.sln /t:Build "/p:Configuration=Debug;Platform=ARM"
msbuild /m HlsPlayer.VS2013.sln /t:Build "/p:Configuration=Debug;Platform=x86"
msbuild /m HlsPlayer.VS2013.sln /t:Build "/p:Configuration=Debug;Platform=x64"

msbuild /m HlsPlayer.VS2013.sln /t:Build "/p:Configuration=Release;Platform=ARM"
msbuild /m HlsPlayer.VS2013.sln /t:Build "/p:Configuration=Release;Platform=x86"
msbuild /m HlsPlayer.VS2013.sln /t:Build "/p:Configuration=Release;Platform=x64"
