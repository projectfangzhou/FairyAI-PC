@echo off
setlocal enabledelayedexpansion
set "ADDON=%~dp0fairyai_image_to_3d.py"
if not exist "%ADDON%" exit /b 0

for %%v in (4.5 4.2 5.2 3.6 4.0 4.1) do (
    set "DEST=%APPDATA%\Blender Foundation\Blender\%%v\scripts\addons"
    if exist "%APPDATA%\Blender Foundation\Blender\%%v" (
        mkdir "!DEST!" 2>nul
        copy /y "%ADDON%" "!DEST!\fairyai_image_to_3d.py" >nul 2>&1
    )
)
exit /b 0
