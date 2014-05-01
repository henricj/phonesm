"%~dp0..\tools\NuGet\NuGet.exe" restore "%~dp0..\Distribution\Samples.WP7.sln"
"%~dp0..\tools\NuGet\NuGet.exe" restore "%~dp0..\Distribution\Samples.WP8.sln"
"%~dp0..\tools\NuGet\NuGet.exe" restore "%~dp0..\Distribution\Sample.Silverlight.sln"

copy "%~dp0..\Source\smf\*.dll" "%~dp0..\Distribution\smf\"
copy "%~dp0..\Source\smf\*.pdb" "%~dp0..\Distribution\smf\"
copy "%~dp0..\Source\smf\*.xml" "%~dp0..\Distribution\smf\"

msbuild /m "%~dp0buildDist2012.proj"
