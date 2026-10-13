; Glide's installer: per-user (no administrator rights), built by build.ps1.
; iscc /DArch=x64|arm64 /DVersion=0.1.0 Glide.iss, after publishing to ..\artifacts\publish-<arch>.

#ifndef Arch
  #define Arch "x64"
#endif
#ifndef Version
  #define Version "0.1.0"
#endif
#define SourceDir "..\artifacts\publish-" + Arch

[Setup]
AppId={{8C0E5E2A-6A55-4E2B-9B0B-1C9B0D6F1A11}
AppName=Glide
AppVersion={#Version}
AppVerName=Glide {#Version}
AppPublisher=QuackByte
AppPublisherURL=https://github.com/QuackByte/glide-windows
DefaultDirName={localappdata}\Programs\Glide
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.19041
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
OutputDir=..\artifacts
OutputBaseFilename=Glide-{#Version}-{#Arch}-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Glide.exe
UninstallDisplayName=Glide
; Glide is asked to quit (and restore its windows) below, not closed by Restart Manager.
CloseApplications=no

[Tasks]
Name: startup; Description: "Start Glide when I sign in"; Flags: unchecked

[InstallDelete]
; Files from the previous version, so an upgrade doesn't mix DLLs.
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Glide"; Filename: "{app}\Glide.exe"; Comment: "Scrolling window manager"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Glide"; ValueData: """{app}\Glide.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\Glide.exe"; Description: "Start Glide now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
const
  GlideWindowClass = 'GlideMessageWindow';
  QuitMessage = $8002; { WM_APP + 2, see MessageWindow.QuitMessage }

{ Asks a running Glide to put every window back on screen and quit, and waits for it. }
procedure QuitGlide();
var
  Wnd: HWND;
  Waited: Integer;
begin
  Wnd := FindWindowByClassName(GlideWindowClass);
  if Wnd = 0 then Exit;
  PostMessage(Wnd, QuitMessage, 0, 0);
  Waited := 0;
  while (FindWindowByClassName(GlideWindowClass) <> 0) and (Waited < 10000) do
  begin
    Sleep(200);
    Waited := Waited + 200;
  end;
  { The window goes first; give the process a moment to exit. }
  Sleep(500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  QuitGlide();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  QuitGlide();
  Result := True;
end;
