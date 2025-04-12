@echo off
echo [Employee Assignments Revived] Building project...

REM Ensure we're in the correct directory
cd /d "%~dp0"

REM Clean old build
if exist bin\Release (
    echo Cleaning previous build...
    rmdir /s /q bin\Release
)

REM Run MSBuild on the solution
msbuild EmployeeAssignmentsRevived.sln /p:Configuration=Release

echo Done.
pause
