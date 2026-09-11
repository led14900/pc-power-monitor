; PC Power Monitor - Inno Setup script
; Build:  ISCC.exe /DAppVersion=1.0.0 build\installer.iss
; Driven by build\build-installer.ps1 (which publishes first and locates ISCC.exe).
;
; Security note: the app is ALWAYS installed under Program Files ({autopf}). The
; optional auto-start scheduled task runs /RL HIGHEST without a UAC prompt, so the
; executable must live somewhere only administrators can write - otherwise a local
; attacker could swap the exe and escalate. Do not change DefaultDirName.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "PC Power Monitor"
#define AppExe "PcPowerMonitor.App.exe"
#define AutoStartTask "PcPowerMonitorAutoStart"

[Setup]
AppId={{9C4B7F2A-1D63-4E58-9A2C-7F1E6B0D8A34}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\PcPowerMonitor
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=admin
; "x64compatible" (not "x64"): newer Inno Setup deprecates the bare "x64"
; identifier. x64compatible covers x64 + Arm64 running x64 code.
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=PcPowerMonitorSetup-{#AppVersion}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\src\PcPowerMonitor.App\Assets\app-icon.ico
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; Inno checks this mutex (session + global namespace) on both install and
; uninstall and asks the user to close the running app. The app owns
; "Global\PcPowerMonitor" via SingleInstanceGuard.
AppMutex=Global\PcPowerMonitor,PcPowerMonitor

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Tạo lối tắt trên màn hình nền (Desktop)"; Flags: unchecked
Name: "autostart"; Description: "Khởi động cùng Windows (tạo tác vụ chạy với quyền cao nhất, không hỏi UAC)"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Hướng dẫn người dùng"; Filename: "{app}\README-nguoi-dung.md"
Name: "{group}\Gỡ cài đặt {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; Optional: register the logon scheduled task. Absolute, fully-quoted path guards
; against argument injection through the install directory.
Filename: "{sys}\schtasks.exe"; \
  Parameters: "/Create /SC ONLOGON /TN ""{#AutoStartTask}"" /RL HIGHEST /IT /F /TR ""\""{app}\{#AppExe}\"" --autostart"""; \
  Flags: runhidden; Tasks: autostart; StatusMsg: "Đang tạo tác vụ khởi động cùng Windows..."
Filename: "{app}\{#AppExe}"; Description: "Chạy {#AppName} ngay"; Flags: postinstall shellexec skipifsilent

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""{#AutoStartTask}"" /F"; \
  Flags: runhidden; RunOnceId: "DelAutoStartTask"

[Code]
{ On uninstall, offer to delete the electricity-history data. Default = keep it
  (the data is the user's property). AppMutex above already handles the
  "app still running" prompt for both install and uninstall. }
procedure CurUninstallStepChanged(CurStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\PcPowerMonitor');
    if DirExists(DataDir) then
    begin
      if MsgBox('Xóa toàn bộ dữ liệu lịch sử điện năng trong:' + #13#10 +
                DataDir + ' ?' + #13#10 + #13#10 +
                'Chọn No để giữ lại dữ liệu (khuyến nghị).',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
