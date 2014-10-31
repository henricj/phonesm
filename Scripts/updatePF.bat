@echo off
"%~dp0..\tools\NuGet\nuget.exe" update "%~dp0..\Source\playerframework\WP7.SL.Core\Microsoft.PlayerFramework.Core.csproj"
if %errorlevel% neq 0 exit /b %errorlevel%
