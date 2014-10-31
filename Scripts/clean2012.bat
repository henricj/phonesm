@if exist "%~dp0..\Source\playerframework\WP7.SL.Core\Microsoft.PlayerFramework.Core.csproj" goto pfexists

@type "%~dp0..\Source\playerframework\readme.wp7.txt"
@exit /b 1

:pfexists

@setlocal

@call "%VS110COMNTOOLS%vsvars32.bat"
@if %errorlevel% neq 0 goto errorexit

msbuild /m /consoleloggerparameters:Summary /verbosity:minimal /t:Clean %~dp0build2012.proj
@if %errorlevel% neq 0 goto errorexit

@endlocal
@exit /b 0

:errorexit
@endlocal
@exit /b 1
