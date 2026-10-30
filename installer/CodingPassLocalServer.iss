; CodingPass Local Server installer
#define AppVersion "1.3.0"
#ifndef MinGWSource
  #define MinGWSource "C:\Program Files\CodeBlocks\MinGW"
#endif

[Setup]
AppId={{DE954893-76C9-476A-94BD-3257FD42C201}
AppName={cm:AppTitle}
AppVersion={#AppVersion}
VersionInfoProductName=CodingPass Local Server
VersionInfoDescription=CodingPass Local Server Setup (unofficial)
AppPublisher=Personal Tools
DefaultDirName={localappdata}\Programs\CodingPassLocalServer
DefaultGroupName={cm:AppTitle}
; Admin is needed because CodingPass only detects the compiler at C:\Program Files\CodeBlocks\MinGW.
PrivilegesRequired=admin
UsedUserAreasWarning=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
DisableDirPage=no
DisableProgramGroupPage=yes
WizardStyle=modern
; Always let the user pick 繁體中文 or English.
ShowLanguageDialog=yes
SetupIconFile=..\build\CodingPassLocalServer.ico
UninstallDisplayIcon={app}\CodingPassLocalServer.exe
OutputDir=..\dist
OutputBaseFilename=CodingPass-LocalServer-Setup-{#AppVersion}
Compression=lzma2/max
LZMANumBlockThreads=4
SolidCompression=yes
CloseApplications=no
RestartApplications=no
; Second name is the tray mutex used before the rename (PracticeBridgeTray).
AppMutex=Local\CodingPassLocalServerTray,Local\CodingPassPracticeBridgeTray

[Languages]
Name: "chinesetraditional"; MessagesFile: "compiler:Default.isl,ChineseTraditional.isl"; LicenseFile: "licenses\Disclaimer.zh-TW.txt"; InfoBeforeFile: "InstallNotes.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "licenses\Disclaimer.en.txt"; InfoBeforeFile: "InstallNotes.en.txt"

; Keep non-ASCII text here, not in [Code] literals, so it shows correctly on non-Chinese code pages.
[CustomMessages]
chinesetraditional.AppTitle=CodingPass 本機服務
chinesetraditional.DesktopIcon=建立桌面捷徑
chinesetraditional.StartupTask=登入 Windows 時自動啟動
chinesetraditional.LaunchApp=啟動 CodingPass 本機服務
chinesetraditional.NeedDotNet=需要先安裝 Microsoft .NET 10 Desktop Runtime (x64)，再執行此安裝程式。
chinesetraditional.StopFirst=請先結束考試或從通知區停止本機服務，再解除安裝。
english.AppTitle=CodingPass Local Server
english.DesktopIcon=Create a desktop shortcut
english.StartupTask=Start automatically when signing in to Windows
english.LaunchApp=Launch CodingPass Local Server
english.NeedDotNet=Microsoft .NET 10 Desktop Runtime (x64) is required. Please install it first, then run this setup again.
english.StopFirst=Please close the exam, or stop the local server from the notification area, before uninstalling.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartupTask}"

[Files]
Source: "..\build\payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Code::Blocks 25.03 MinGW (GCC 14.2.0), installed only when no compiler exists at the detected path.
Source: "{#MinGWSource}\*"; DestDir: "{commonpf64}\CodeBlocks\MinGW"; Check: NeedMinGW; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion

; Leftovers from versions before the rename ("CodingPass 練習助手" / PracticeBridgeTray).
[InstallDelete]
Type: files; Name: "{app}\PracticeBridgeTray.exe"
Type: files; Name: "{app}\practice-bridge.state.json"
Type: filesandordirs; Name: "{autoprograms}\CodingPass 練習助手"
Type: files; Name: "{autodesktop}\CodingPass 練習助手.lnk"

[Icons]
Name: "{group}\{cm:AppTitle}"; Filename: "{app}\CodingPassLocalServer.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{cm:AppTitle}"; Filename: "{app}\CodingPassLocalServer.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "CodingPassPracticeBridge"; Flags: deletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CodingPassLocalServer"; ValueData: """{app}\CodingPassLocalServer.exe"" --startup"; Tasks: startup

[Run]
Filename: "{app}\CodingPassLocalServer.exe"; Description: "{cm:LaunchApp}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
var
  MinGWMissing: Boolean;

function NeedMinGW: Boolean;
begin
  Result := MinGWMissing;
end;

function HasDesktopRuntime: Boolean;
var
  Versions: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Versions) or
     RegGetValueNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Versions) then
    for I := 0 to GetArrayLength(Versions) - 1 do
      if Pos('10.', Versions[I]) = 1 then begin Result := True; Exit; end;
end;

function InitializeSetup: Boolean;
begin
  MinGWMissing := not (FileExists(ExpandConstant('{commonpf64}\CodeBlocks\MinGW\bin\gcc.exe')) and
    FileExists(ExpandConstant('{commonpf64}\CodeBlocks\MinGW\bin\g++.exe')));
  Result := HasDesktopRuntime;
  if not Result then
    MsgBox(CustomMessage('NeedDotNet') + #13#10 +
      'https://dotnet.microsoft.com/download/dotnet/10.0', mbError, MB_OK);
end;

function InitializeUninstall: Boolean;
var
  ExitCode: Integer;
begin
  Result := True;
  if FileExists(ExpandConstant('{app}\CodingPassLocalServer.exe')) then begin
    Result := Exec(ExpandConstant('{app}\CodingPassLocalServer.exe'), '--shutdown', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ExitCode);
    if Result then Result := ExitCode = 0;
    if not Result then MsgBox(CustomMessage('StopFirst'), mbError, MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Value: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'CodingPassLocalServer', Value) then
      if CompareText(Value, '"' + ExpandConstant('{app}\CodingPassLocalServer.exe') + '" --startup') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'CodingPassLocalServer');
end;
