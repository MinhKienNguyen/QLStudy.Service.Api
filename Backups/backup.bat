@echo off
chcp 65001 >nul
echo ===================================================
echo     HỆ THỐNG BACKUP DATABASE QLSTUDY (POSTGRESQL)
echo ===================================================

set PGPASSWORD=postgres
set PG_BIN=D:\Work\WorkHome\Tools\PostgreSQL\17\bin
set BACKUP_DIR=D:\Work\WorkHome\Backups

for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value') do set datetime=%%I
set TIMESTAMP=%datetime:~0,8%_%datetime:~8,6%

echo.
echo [1/3] Dang backup database qlstudy (.dump - nén tối ưu)...
"%PG_BIN%\pg_dump.exe" -U postgres -h localhost -p 5432 -d qlstudy -F c -b -f "%BACKUP_DIR%\qlstudy_%TIMESTAMP%.dump"

echo.
echo [2/3] Dang backup database qlstudy (.sql - script text SQL)...
"%PG_BIN%\pg_dump.exe" -U postgres -h localhost -p 5432 -d qlstudy --encoding=UTF8 -f "%BACKUP_DIR%\qlstudy_%TIMESTAMP%.sql"

echo.
echo [3/3] Dang backup TOÀN BỘ database trong Postgres (all_databases)...
"%PG_BIN%\pg_dumpall.exe" -U postgres -h localhost -p 5432 --encoding=UTF8 -f "%BACKUP_DIR%\all_databases_%TIMESTAMP%.sql"

echo.
echo ===================================================
echo     BACKUP HOÀN TẤT THÀNH CÔNG!
echo     Thư mục lưu trữ: %BACKUP_DIR%
echo ===================================================
echo.
pause
