"%~dp0..\tools\NuGet\NuGet.exe" restore "%~dp0..\Distribution\Samples.WP8.sln"
"%~dp0..\tools\NuGet\NuGet.exe" restore "%~dp0..\Distribution\Sample.WinRT.sln"

msbuild /m "%~dp0buildDist2013.proj"

