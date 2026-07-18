; WeatherWizard — Inno Setup script (unpackaged, self-contained, no code-signing cert required)
;
; Build steps:
;   1. powershell -ExecutionPolicy Bypass -File scripts\Publish-Release.ps1 -Mode Portable
;   2. Open this file in Inno Setup Compiler and click Build, OR run:
;      powershell -ExecutionPolicy Bypass -File scripts\Build-InnoSetup.ps1
;
; Output: dist\WeatherWizard-Setup-win-x64.exe

#define MyAppName "WeatherWizard"
#define MyAppPublisher "WeatherWizard"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.21"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\WeatherWizard-win-x64-portable"
#endif

[Setup]
AppId={{A7C3E9F2-4B18-4D6E-9F21-8C5D0A1B2E34}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppVerName={#MyAppName} {#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=WeatherWizard-Setup-win-x64
SetupIconFile=..\WeatherWizard\Assets\WeatherWizard.ico
UninstallDisplayIcon={app}\WeatherWizard.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0.17763
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start WeatherWizard when I sign in to Windows"; GroupDescription: "Options:"; Flags: unchecked
Name: "cleardata"; Description: "Start fresh (remove previous locations and settings)"; GroupDescription: "Options:"; Flags: unchecked

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WeatherWizard"; ValueData: """{app}\WeatherWizard.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\WeatherWizard.exe"; IconFilename: "{app}\WeatherWizard.exe"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\WeatherWizard.exe"; IconFilename: "{app}\WeatherWizard.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\WeatherWizard.exe"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssInstall) and WizardIsTaskSelected('cleardata') then
  begin
    DelTree(ExpandConstant('{localappdata}\WeatherWizard'), True, True, True);
  end;
end;
