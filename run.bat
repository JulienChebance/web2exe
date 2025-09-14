@echo off
rem Change to true if you want the C# console to appear when running the generated exe
set debug=false

set target=/target:winexe
if "%debug%" == "true" set target=

rem Get product name from AssemblyInfo.cs as the file name
set filename=
rem Normally FOR options are enclosed within quotes. If you want to use a quote as part of an option, then the enclosing quotes must be ditched.
rem That means all characters that the CMD interpreter uses as token delimiters must be escaped, including space and equal sign. Also the quote needs to be escaped.
for /F eol^=^/^ tokens^=1^,2^ delims^=^" %%i in (AssemblyInfo.cs) do (
	if "%%i" == "[assembly: AssemblyProduct(" set filename=%%j
)
rem Remove unauthorized characters
set filename=%filename::=%
set filename=%filename:/=%
set filename=%filename:\=%
set filename=%filename:?=%
if "%filename%" == "" set filename=web2exe

if exist "%~dp0%filename% data\" (
	rmdir /S /Q "%~dp0%filename% data"
)

rem Powershell script to list all files as a list of resources: "/res:[file1] /res:[file2] /res:[file3] ..."
rem *.exe, *.cs and *.bat files are excluded, as well as files and folders beginning by a dot in the same level as this .bat
rem Output redirected to variable %resources% via a temporary file
PowerShell -NoProfile -ExecutionPolicy Bypass -Command "'/res:' + ((Get-ChildItem -Path .\ -Recurse -Exclude '*.exe', '*.cs', '*.bat' -File -Name | Select-String -Pattern '^\.' -NotMatch | ForEach-Object { '\"' + $_ + '\"' -replace '\\', '/' }) -Join ' /res:')" > "%temp%\win2exe.resources"
rem set /P can't be used here, as it's limited to 1024 caracters. Instead we read the file and put its content into a variable using 'for'
for /f "delims=" %%x in (%temp%\win2exe.resources) do set resources=%%x
rem del "%temp%\win2exe.resources"
echo Loaded resources: %resources%
echo.

set icon=
if exist "%~dp0favicon.ico" set icon=/win32icon:favicon.ico

%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe %target% -out:"%~dp0%filename%.exe" "%~dp0*.cs" -optimize -nologo %icon% %resources%

if %ERRORLEVEL% equ 0 (
	echo The following file has been successfully created:
	echo %~dp0%filename%.exe
	echo.
)
pause