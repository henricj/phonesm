@setlocal

call "%VS140COMNTOOLS%vsvars32.bat"
@if %errorlevel% neq 0 goto errorexit

msbuild /m /consoleloggerparameters:Summary /verbosity:minimal /t:Clean %~dp0build2015.proj
@if %errorlevel% neq 0 goto errorexit

@endlocal
@exit /b 0

:errorexit
@endlocal
@exit /b 1
