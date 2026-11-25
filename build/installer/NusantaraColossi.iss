; Nusantara Colossi - Windows installer (Inno Setup 6).
; Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.
;
; Built by build/package-windows.ps1, which passes:
;   /DAppVersion=<x.y.z> /DSourceDir=<staged publish folder> /DOutputDir=<packages folder> /DRepoRoot=<repo>

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{6B0E4C1A-9A57-4C7E-9C2B-1D2A45C04545}
AppName=Nusantara Colossi
AppVersion={#AppVersion}
AppVerName=Nusantara Colossi {#AppVersion}
AppPublisher=Syubadubin Studios
AppPublisherURL=https://github.com/SyubadubinStudios/Nusantara.Colossi
AppSupportURL=https://github.com/SyubadubinStudios/Nusantara.Colossi/issues
AppCopyright=Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios
VersionInfoDescription=Nusantara Colossi installer
DefaultDirName={autopf}\Syubadubin Studios\Nusantara Colossi
DefaultGroupName=Nusantara Colossi
DisableProgramGroupPage=yes
; Per-user install by default (no admin prompt); the user may choose all users.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=NusantaraColossi-{#AppVersion}-win-x64-setup
SetupIconFile={#RepoRoot}\assets\art\icon.ico
UninstallDisplayIcon={app}\NusantaraColossi.exe
LicenseFile={#RepoRoot}\LICENSE
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Nusantara Colossi"; Filename: "{app}\NusantaraColossi.exe"
Name: "{group}\{cm:UninstallProgram,Nusantara Colossi}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Nusantara Colossi"; Filename: "{app}\NusantaraColossi.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NusantaraColossi.exe"; Description: "{cm:LaunchProgram,Nusantara Colossi}"; Flags: nowait postinstall skipifsilent
