# CodingPass Local Server

非官方工具，與 CodingPass 官方沒有關係。它在 Windows 通知區管理 CodingPass 的本機 Judge 服務（`http://localhost:24680/`），讓網頁版 CodingPass 的 Client Judge 能用本機編譯器。安裝檔另外附帶 Code::Blocks 25.03 的 MinGW（GCC 14.2.0）。

## 目錄

| 路徑 | 內容 |
| --- | --- |
| `src/` | 通知區程式（C#，.NET Framework 4 WinForms） |
| `installer/` | Inno Setup 腳本、繁中語系檔、安裝說明、免責聲明與第三方授權 |
| `scripts/` | 編譯、打包、檢查腳本 |
| `tools/` | Inno Setup 6 可攜版（不進版控） |
| `build/`、`dist/` | 編譯產物（不進版控） |

本專案**不含** CodingPass 原廠檔案，打包時才從原本的 CodingPass 資料夾複製 Judge 等元件。不含考試模組。

## 編譯

只編譯通知區程式（使用 Windows 內建 csc，不需安裝其他東西）：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Build-Tray.ps1
```

產生安裝檔（輸出到 `dist\`，約需 2 分鐘）：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Build-Installer.ps1 -CodingPassDir "C:\Users\LINAGJIE-WANG\Downloads\CodingPass"
```

- `-MinGWDir`：要打包的 MinGW，預設是 `C:\Program Files\CodeBlocks\MinGW`
- `-Iscc`：ISCC.exe 位置，預設依序找 `tools\InnoSetup`、系統安裝的 Inno Setup 6

## 重點行為

- CodingPass 只在 `C:\Program Files\CodeBlocks\MinGW\bin` 偵測 CodeBlocks(gcc/g++)，所以安裝需要系統管理員權限。那裡已有編譯器就不覆蓋。
- 偵測到 `CodingPass.Exam` 執行中時，不啟動也不停止服務。
- 安裝介面有繁體中文和 English，需同意 `installer/licenses/Disclaimer.*.txt` 才能安裝。
- 從舊名「CodingPass 練習助手」（PracticeBridgeTray）升級時，會清掉舊的 EXE、捷徑及自動啟動設定。
- 需要 .NET 10 Desktop Runtime x64（Judge 使用）。
