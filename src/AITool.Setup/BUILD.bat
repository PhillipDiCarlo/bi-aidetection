@echo off
:: Builds the AITool installer with Inno Setup 6 (https://jrsoftware.org/isinfo.php).
:: Usage: BUILD.bat [Debug|Release]   (defaults to Debug)
setlocal
set CONFIG=%~1
if "%CONFIG%"=="" set CONFIG=Debug

set ISCC=
for %%I in (ISCC.exe) do if not "%%~$PATH:I"=="" set ISCC=%%~$PATH:I
if "%ISCC%"=="" if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe
if "%ISCC%"=="" if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe
if "%ISCC%"=="" (
    echo Inno Setup 6 not found. Install it from https://jrsoftware.org/isdl.php or add ISCC.exe to PATH.
    exit /b 1
)

"%ISCC%" /DConfiguration=%CONFIG% "%~dp0Script.iss"
