@echo off

set vers=0.3.0
set libs=CIL.NET
set tools=
set files=%tools% %libs%

:: clear whole folder
::for %%i in (%files%) do rd /S /Q "%%i"

:: generate packages
del *.nupkg
for %%i in (%files%) do sed -e "s/[$]vers[$]/%vers%/" %%i.nuspec.xml > %%i.nuspec
for %%i in (%files%) do nuget pack %%i.nuspec
