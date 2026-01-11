@echo off
setlocal

REM Check for running from inside ZIP (Temp folder)
echo "%~dp0" | find /i "%TEMP%" >nul
if not errorlevel 1 (
    echo.
    echo  ==============================================================
    echo   ERROR: RUNNING FROM INSIDE ZIP FILE DETECTED
    echo  ==============================================================
    echo.
    echo   It looks like you are validly trying to run this script directly
    echo   from inside the ZIP file without extracting it first.
    echo.
    echo   Please EXTRACT the ZIP file to a real folder, and then run
    echo   install.cmd from there.
    echo.
    echo  ==============================================================
    pause
    exit /b 1
)

REM Check if EXE exists (common issue when running from ZIP)
if not exist "%~dp0GpgWindowsHello.exe" (
    echo.
    echo  ==============================================================
    echo   ERROR: GpgWindowsHello.exe NOT FOUND
    echo  ==============================================================
    echo.
    echo   Could not find the executable in the current folder.
    echo   This usually happens when running directly from a ZIP file.
    echo.
    echo   Please EXTRACT all files to a real folder, and then run
    echo   install.cmd from there.
    echo.
    echo  ==============================================================
    pause
    exit /b 1
)

set "TARGET_DIR=%LOCALAPPDATA%\Programs\GpgWindowsHello"
set "TARGET_EXE=%TARGET_DIR%\GpgWindowsHello.exe"

echo Installing GpgWindowsHello to: %TARGET_DIR%
echo.

if not exist "%TARGET_DIR%" (
    mkdir "%TARGET_DIR%"
)

copy /Y "%~dp0GpgWindowsHello.exe" "%TARGET_EXE%"

if errorlevel 1 (
    echo.
    echo Installation failed!
    pause
    exit /b 1
)

echo.
echo Installation successful.
echo Running setup...
echo.

"%TARGET_EXE%" --setup

pause
