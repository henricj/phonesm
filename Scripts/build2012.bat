%~dp0..\Source\.nuget\NuGet.exe restore %~dp0..\Source\HlsPlayer.sln

msbuild /m %~dp0build2012.proj
