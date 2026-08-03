# Chạy dưới quyền Administrator để Build
Write-Host "Đang chuẩn bị đóng gói SELECT.bundle..." -ForegroundColor Cyan

$CurrentPath = Get-Location
$SolutionFile = "$CurrentPath\..\SELECT.sln"
$CsprojFile = "$CurrentPath\..\SELECT.csproj"

# 1. Tìm MSBuild
$msbuildPaths = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
)

$msbuild = $msbuildPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $msbuild) {
    Write-Host "Lỗi: Không tìm thấy MSBuild. Hãy build solution từ Visual Studio bằng chế độ Release trước." -ForegroundColor Red
} else {
    Write-Host "Tìm thấy MSBuild: $msbuild"
    Write-Host "Bắt đầu biên dịch dự án (Release)..."
    & $msbuild $CsprojFile /p:Configuration=Release /t:Rebuild
}

$dllPath = "$CurrentPath\..\bin\Release\SELECT.dll"
if (Test-Path $dllPath) {
    Write-Host "`nĐã tìm thấy file DLL. Môi trường cài đặt đã sẵn sàng!" -ForegroundColor Green
    Write-Host "Vui lòng mở file SELECT_Installer.iss bằng phần mềm Inno Setup để xuất ra file EXE." -ForegroundColor Yellow
} else {
    Write-Host "`nLỗi: Không tìm thấy file SELECT.dll ở dạng Release. Vui lòng kiểm tra lại quá trình Build ở Visual Studio." -ForegroundColor Red
}

Pause
