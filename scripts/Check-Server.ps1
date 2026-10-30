$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class TrayWindowCheck {
    delegate bool Callback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(Callback cb, IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    public static int Visible(int pid) {
        int count = 0;
        EnumWindows(delegate(IntPtr w, IntPtr p) { uint owner; GetWindowThreadProcessId(w, out owner); if (owner == pid && IsWindowVisible(w)) count++; return true; }, IntPtr.Zero);
        return count;
    }
}
'@
foreach ($name in 'CodingPassLocalServer','CodingPass.Judge') {
    foreach ($process in @(Get-Process -Name $name -ErrorAction Stop)) {
        Write-Output ($name + ' visible top-level windows: ' + [TrayWindowCheck]::Visible($process.Id))
    }
}
$response = Invoke-WebRequest -Uri 'http://localhost:24680/' -Method Options -UseBasicParsing -TimeoutSec 3
if ([int]$response.StatusCode -ne 204) { throw 'Unexpected bridge response' }
Write-Output 'Bridge HTTP 204 OK'
