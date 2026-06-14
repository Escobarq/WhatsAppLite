; WhatsApp Lite NSIS Installer Script

!define APP_NAME "WhatsApp Lite"
!define APP_VERSION "1.0.0"
!define APP_PUBLISHER "WhatsApp Lite Contributors"
!define APP_EXE "WhatsAppLite.exe"
!define APP_ICON "whatsapp.ico"
!define INSTALL_DIR "$PROGRAMFILES64\WhatsApp Lite"
!define START_MENU_GROUP "WhatsApp Lite"

; Modern UI
!include "MUI2.nsh"

; General
Name "${APP_NAME} ${APP_VERSION}"
OutFile "WhatsAppLite-Setup.exe"
InstallDir "$PROGRAMFILES64\WhatsApp Lite"
RequestExecutionLevel admin

; Interface Settings
!define MUI_ABORTWARNING
!define MUI_ICON "WhatsAppLite\${APP_ICON}"
!define MUI_UNICON "WhatsAppLite\${APP_ICON}"

; Welcome Page
!insertmacro MUI_PAGE_WELCOME

; Directory Page
!insertmacro MUI_PAGE_DIRECTORY

; InstFiles Page
!insertmacro MUI_PAGE_INSTFILES

; Finish Page
!insertmacro MUI_PAGE_FINISH

; Languages
!insertmacro MUI_LANGUAGE "English"

Section "Install"
    SetOutPath $INSTDIR
    
    ; Copy application files
    File /r "WhatsAppLite\bin\Release\net10.0-windows\win-x64\publish\*.*"
    
    ; Create desktop shortcut
    CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_ICON}" 0
    
    ; Create start menu shortcuts
    CreateDirectory "$SMPROGRAMS\${START_MENU_GROUP}"
    CreateShortCut "$SMPROGRAMS\${START_MENU_GROUP}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_ICON}" 0
    CreateShortCut "$SMPROGRAMS\${START_MENU_GROUP}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\uninstall.exe" "" "$INSTDIR\${APP_ICON}" 0
    
    ; Write uninstall information
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" '"$INSTDIR\uninstall.exe"'
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon" "$INSTDIR\${APP_ICON}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${APP_PUBLISHER}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "HelpLink" "https://github.com/HiwarkhedePrasad/WhatsAppLite"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "URLInfoAbout" "https://github.com/HiwarkhedePrasad/WhatsAppLite"
    
    ; Write the uninstaller
    WriteUninstaller "$INSTDIR\uninstall.exe"
SectionEnd

Section "Uninstall"
    ; Remove shortcuts
    Delete "$DESKTOP\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${START_MENU_GROUP}\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${START_MENU_GROUP}\Uninstall ${APP_NAME}.lnk"
    RMDir "$SMPROGRAMS\${START_MENU_GROUP}"
    
    ; Remove registry entries
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
    
    ; Remove files
    RMDir /r "$INSTDIR"
SectionEnd
