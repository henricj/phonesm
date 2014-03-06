msbuild /m HlsPlayer.sln /t:Clean "/p:Configuration=Debug;Platform=ARM"
msbuild /m HlsPlayer.sln /t:Clean "/p:Configuration=Debug;Platform=Mixed Platforms"

msbuild /m HlsPlayer.sln /t:Clean "/p:Configuration=Release;Platform=ARM"
msbuild /m HlsPlayer.sln /t:Clean "/p:Configuration=Release;Platform=Mixed Platforms"
