"%~dp0..\tools\NuGet\NuGet.exe" restore "%~dp0..\Distribution\Samples.WP8.sln"
"%~dp0..\tools\NuGet\NuGet.exe" restore "%~dp0..\Distribution\Sample.Win81.sln"

msbuild /m "%~dp0buildDist2013.proj"

