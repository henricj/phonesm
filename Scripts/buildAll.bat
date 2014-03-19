setlocal
call "%VS110COMNTOOLS%vsvars32.bat"
call "%~dp0clean2012.bat"
endlocal
setlocal
call "%VS120COMNTOOLS%vsvars32.bat"
call "%~dp0clean2013.bat"
endlocal
setlocal
call "%VS110COMNTOOLS%vsvars32.bat"
call "%~dp0build2012.bat"
endlocal
setlocal
call "%VS120COMNTOOLS%vsvars32.bat"
call "%~dp0build2013.bat"
endlocal
setlocal
call "%~dp0dirs.bat"
call "%~dp0copyFiles.bat"
setlocal
call "%VS110COMNTOOLS%vsvars32.bat"
call "%~dp0buildDist2012.bat"
endlocal
setlocal
call "%VS120COMNTOOLS%vsvars32.bat"
call "%~dp0buildDist2013.bat"
endlocal
