$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
public class Clicker {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int d, IntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr e);
  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(300); mouse_event(0x0002,0,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(80); mouse_event(0x0004,0,0,0,IntPtr.Zero); }
  public static void Key(byte vk) { keybd_event(vk,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(60); keybd_event(vk,0,2,IntPtr.Zero); }
}
"@
[Clicker]::SetProcessDPIAware() | Out-Null

$OK_TEXT = [string]([char]0x786E) + [char]0x5B9A     # OK button caption
$exe = 'F:\miniMAxWork\VMixer\release\VMixer.exe'
$proc = Start-Process -FilePath $exe -ArgumentList '--settings' -PassThru
Start-Sleep -Seconds 4

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
if ($wins.Count -eq 0) { Write-Output 'dialog not found'; Stop-Process -Id $proc.Id -Force; exit }
$dlg = $wins[0]
Write-Output ("dialog @ " + $dlg.Current.BoundingRectangle)

$walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
function Get-Items($parent) {
  $list = @()
  $c = [System.Windows.Automation.TreeWalker]::RawViewWalker.GetFirstChild($parent)
  while ($c -ne $null) { $list += $c; $c = [System.Windows.Automation.TreeWalker]::RawViewWalker.GetNextSibling($c) }
  return $list
}

$items = Get-Items $dlg
$toneCombo = $null; $okBtn = $null; $maxX = -1
foreach ($it in $items) {
  if ($it.Current.ClassName -like '*COMBOBOX*') {
    $x = $it.Current.BoundingRectangle.X
    if ($x -gt $maxX) { $maxX = $x; $toneCombo = $it }
  }
  if ($it.Current.Name -eq $OK_TEXT) { $okBtn = $it }
}
if ($toneCombo -eq $null) { Write-Output 'tone combo not found'; Stop-Process -Id $proc.Id -Force; exit }

$tr = $toneCombo.Current.BoundingRectangle
Write-Output ("tone combo '" + $toneCombo.Current.Name + "' at " + $tr.X + "," + $tr.Y)
[Clicker]::Click([int]($tr.X + $tr.Width/2), [int]($tr.Y + $tr.Height/2))
Start-Sleep -Milliseconds 900
[Clicker]::Key(0x26)          # Up arrow -> previous item
Start-Sleep -Milliseconds 300
[Clicker]::Key(0x0D)          # Enter -> commit
Start-Sleep -Milliseconds 900

foreach ($it in (Get-Items $dlg)) {
  if ($it.Current.ClassName -like '*COMBOBOX*') { Write-Output ("combo now: '" + $it.Current.Name + "'") }
}

$or = $okBtn.Current.BoundingRectangle
Write-Output ("OK at " + $or.X + "," + $or.Y)
[Clicker]::Click([int]($or.X + $or.Width/2), [int]($or.Y + $or.Height/2))
Start-Sleep -Milliseconds 1500

Get-Content "$env:APPDATA\VMixer\config.ini" | Select-String 'ToneStyle|SoundFeedback'
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Output 'done'
