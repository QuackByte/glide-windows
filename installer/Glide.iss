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
SetupIconFile=..\src\Glide\Assets\Glide.ico
UninstallDisplayIcon={app}\Glide.exe
UninstallDisplayName=Glide
; Glide is asked to quit (and restore its windows) below, not closed by Restart Manager.
CloseApplications=no
ShowLanguageDialog=no

[Languages]
; Picked from Windows' display language. Glide contacts GitHub for updates, so the installer
; shows its privacy policy, in each language, and offers to turn that off (the "updates" task below).
Name: "en"; MessagesFile: "compiler:Default.isl,Languages\Glide.en.isl"; InfoBeforeFile: "Privacy.txt"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl,Languages\Glide.es.isl"; InfoBeforeFile: "Privacy.es.txt"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl,Languages\Glide.fr.isl"; InfoBeforeFile: "Privacy.fr.txt"
Name: "de"; MessagesFile: "compiler:Languages\German.isl,Languages\Glide.de.isl"; InfoBeforeFile: "Privacy.de.txt"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl,Languages\Glide.ja.isl"; InfoBeforeFile: "Privacy.ja.txt"
; Simplified Chinese isn't an official translation; ChineseSimplified.isl is vendored (see its first lines).
Name: "zh"; MessagesFile: "Languages\ChineseSimplified.isl,Languages\Glide.zh.isl"; InfoBeforeFile: "Privacy.zh.txt"
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl,Languages\Glide.ko.isl"; InfoBeforeFile: "Privacy.ko.txt"
Name: "pt"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl,Languages\Glide.pt.isl"; InfoBeforeFile: "Privacy.pt.txt"

[Tasks]
Name: startup; Description: "{cm:StartAtSignIn}"; Flags: unchecked
Name: updates; Description: "{cm:CheckForUpdates}"

[InstallDelete]
; Files from the previous version, so an upgrade doesn't mix DLLs.
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Glide"; Filename: "{app}\Glide.exe"; Comment: "{cm:ScrollingWindowManager}"

[Registry]
; Glide reads the updates choice on its next start and removes the value (Settings.ApplyInstallerChoices).
; Updates keep whatever was chosen in Settings since.
Root: HKCU; Subkey: "Software\Glide"; ValueType: dword; ValueName: "CheckForUpdates"; ValueData: 1; Flags: uninsdeletekey; Tasks: updates; Check: IsFreshInstall
Root: HKCU; Subkey: "Software\Glide"; ValueType: dword; ValueName: "CheckForUpdates"; ValueData: 0; Flags: uninsdeletekey; Tasks: not updates; Check: IsFreshInstall
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Glide"; ValueData: """{app}\Glide.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\Glide.exe"; Description: "{cm:StartGlideNow}"; Flags: nowait postinstall skipifsilent
; Updates run silently with /UPDATE=1: start the new Glide when they're done.
Filename: "{app}\Glide.exe"; Flags: nowait; Check: IsUpdate

[UninstallRun]
; The sign-in task Glide creates when it runs as administrator. Removing it needs the same rights,
; so this may fail quietly for a task made elevated.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN Glide /F"; Flags: runhidden; RunOnceId: "RemoveSignInTask"

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

function IsUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:update|0}') = '1';
end;

function IsFreshInstall(): Boolean;
begin
  Result := not IsUpdate();
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
