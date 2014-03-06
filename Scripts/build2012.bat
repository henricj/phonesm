msbuild /m HlsPlayer.sln /t:Build "/p:Configuration=Release;Platform=Mixed Platforms"
msbuild /m HlsPlayer.sln /t:Build "/p:Configuration=Release;Platform=ARM"
msbuild /m HlsPlayer.sln /t:Build "/p:Configuration=Debug;Platform=Mixed Platforms"
msbuild /m HlsPlayer.sln /t:Build "/p:Configuration=Debug;Platform=ARM"



