@echo off
rem Remove any "Mark of the Web" flag from a downloaded/copied copy so that
rem Windows SmartScreen does not treat it as an untrusted internet file.
>nul 2>&1 powershell -NoProfile -Command "Unblock-File -LiteralPath '%~dp0dist\PomoRest.exe' -ErrorAction SilentlyContinue"
start "" "%~dp0dist\PomoRest.exe"
