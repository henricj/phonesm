"%~dp0..\tools\NuGet\NuGet.exe" restore "%~dp0..\Source\HlsPlayer.VS2013.sln"
@if %errorlevel% neq 0 exit /b %errorlevel%

@setlocal

call "%VS120COMNTOOLS%vsvars32.bat"
@if %errorlevel% neq 0 goto errorexit

msbuild /m /consoleloggerparameters:Summary /verbosity:minimal "%~dp0build2013.proj"
@if %errorlevel% neq 0 goto errorexit

@endlocal
@exit /b 0

:errorexit
@endlocal
@exit /b 1
