"%~dp0..\tools\NuGet\NuGet.exe" restore "%~dp0..\Source\HlsPlayer.sln"

msbuild /m "%~dp0build2012.proj"
