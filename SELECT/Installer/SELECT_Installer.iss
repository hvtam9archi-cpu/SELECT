[Setup]
; Thông tin cấu hình bộ cài đặt
AppName=SELECT AutoCAD Plugin
AppVersion=1.0.0
Publisher=TamHoang Tools
DefaultDirName={code:GetAutoCADAppDir}\SELECT.bundle
DefaultGroupName=SELECT Plugin
OutputDir=.\Output
OutputBaseFilename=SELECT_Plugin_Setup
Compression=lzma2/ultra
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=highest
SetupIconFile=compiler:SetupClassicIcon.ico
UninstallDisplayIcon={app}\Contents\SELECT.dll

[Code]
// Hàm tìm thư mục ApplicationPlugins của Autodesk để cài tự động
function GetAutoCADAppDir(Param: String): String;
begin
  Result := ExpandConstant('{commonappdata}\Autodesk\ApplicationPlugins');
end;

[Files]
; Copy file định nghĩa cấu trúc Bundle của AutoCAD
Source: "PackageContents.xml"; DestDir: "{app}"; Flags: ignoreversion
; Copy file DLL (Plugin đã compile)
Source: "..\bin\Release\SELECT.dll"; DestDir: "{app}\Contents"; Flags: ignoreversion createallsubdirs

[Messages]
FinishedHeadingLabel=Hoàn tất cài đặt SELECT Plugin
FinishedLabel=Plugin SELECT đã được cài đặt thành công vào hệ thống.%nKhởi động lại AutoCAD để nạp tính năng mới.
