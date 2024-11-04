[Setup]
AppName=Aru Screen Summary
AppVersion=1.0.0
WizardStyle=modern
DefaultDirName={autopf}\Aru Screen Summary
DefaultGroupName=Aru Screen Summary
UninstallDisplayIcon={app}\bot.ico
OutputDir=output
OutputBaseFilename=AruScreenSummary_Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin

[Files]
Source: "bin\Release\net7.0-windows\win-x64\publish\AruScreenSummary.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net7.0-windows\win-x64\publish\bot.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net7.0-windows\win-x64\publish\bot.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Aru Screen Summary"; Filename: "{app}\AruScreenSummary.exe"; IconFilename: "{app}\bot.ico"
Name: "{commondesktop}\Aru Screen Summary"; Filename: "{app}\AruScreenSummary.exe"; IconFilename: "{app}\bot.ico"

[Run]
Filename: "{app}\AruScreenSummary.exe"; Description: "启动 Aru Screen Summary"; Flags: postinstall nowait
