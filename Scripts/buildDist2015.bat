"%~dp0..\tools\NuGet\NuGet.exe" restore "%~dp0..\Distribution\Samples.WP8.sln"
@if %errorlevel% neq 0 exit /b %errorlevel%

"%~dp0..\tools\NuGet\NuGet.exe" restore "%~dp0..\Distribution\Sample.WinRT.sln"
@if %errorlevel% neq 0 exit /b %errorlevel%

@setlocal

call "%VS140COMNTOOLS%vsvars32.bat"
@if %errorlevel% neq 0 goto errorexit

msbuild /m /consoleLoggerParameters:Summary /verbosity:minimal /fileLogger /fileLoggerParameters:Summary;Verbosity=normal;LogFile=buildDist2015.log "%~dp0buildDist2015.proj"
@if %errorlevel% neq 0 goto errorexit

@endlocal
@exit /b 0

:errorexit
@endlocal
@exit /b 1
