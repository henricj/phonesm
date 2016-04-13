call "%~dp0dirs.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%

call "%~dp0clean2015.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%

call "%~dp0build2015.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%

call "%~dp0copyFiles.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%

call "%~dp0buildDist2015.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%
