@echo off
setlocal
set "SKILLS_DIR=%~dp0skills"
set "TARGET=%APPDATA%\FairyAI\skills"
mkdir "%TARGET%" 2>nul
if exist "%SKILLS_DIR%" (
    copy /y "%SKILLS_DIR%\*.md" "%TARGET%\" >nul 2>&1
)
exit /b 0
