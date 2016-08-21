@echo off

nuget push *.nupkg -s https://www.nuget.org/api/v2/package

echo.
set /P _ret=All packages pushed...
goto :eof