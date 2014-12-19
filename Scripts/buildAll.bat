call "%~dp0dirs.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%

call "%~dp0clean2012.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%

call "%~dp0clean2013.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%

call "%~dp0build2012.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%

call "%~dp0build2013.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%

call "%~dp0copyFiles.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%

call "%~dp0buildDist2012.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%

call "%~dp0buildDist2013.bat"
@if %errorlevel% neq 0 exit /b %errorlevel%
