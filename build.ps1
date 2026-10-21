param([switch]$Desktop)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskBuild = Join-Path $taskRoot 'build'
$taskPayload = Join-Path $taskBuild 'payload'
$taskDist = Join-Path $taskRoot 'dist'
New-Item -ItemType Directory -Force -Path $taskPayload,$taskDist | Out-Null
Add-Type -AssemblyName System.Drawing

# Draw the app's simple vector icon at each Windows icon size.
$taskFrames = @()
foreach ($taskSize in @(16, 24, 32, 48, 64, 128, 256)) {
    $taskBitmap = New-Object System.Drawing.Bitmap($taskSize, $taskSize)
    $taskGraphics = [System.Drawing.Graphics]::FromImage($taskBitmap)
    $taskGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $taskGraphics.ScaleTransform(($taskSize / 256.0), ($taskSize / 256.0))
    $taskBrushes = @{}
    foreach ($taskColor in @('#F7EBD8','#C29B6C','#E8D6B0','#D8B399','#FFF8EC','#40362E','#E3B0A2')) {
        $taskBrushes[$taskColor] = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($taskColor))
    }
    $taskGraphics.FillEllipse($taskBrushes['#F7EBD8'], 4, 4, 248, 248)
    $taskGraphics.FillEllipse($taskBrushes['#C29B6C'], 38, 28, 64, 76)
    $taskGraphics.FillEllipse($taskBrushes['#C29B6C'], 154, 28, 64, 76)
    $taskGraphics.FillEllipse($taskBrushes['#D8B399'], 49, 38, 42, 52)
    $taskGraphics.FillEllipse($taskBrushes['#D8B399'], 165, 38, 42, 52)
    $taskGraphics.FillEllipse($taskBrushes['#E8D6B0'], 25, 58, 206, 168)
    $taskGraphics.FillEllipse($taskBrushes['#FFF8EC'], 35, 131, 112, 83)
    $taskGraphics.FillEllipse($taskBrushes['#FFF8EC'], 109, 131, 112, 83)
    $taskGraphics.FillEllipse($taskBrushes['#40362E'], 72, 111, 20, 25)
    $taskGraphics.FillEllipse($taskBrushes['#40362E'], 164, 111, 20, 25)
    $taskGraphics.FillEllipse($taskBrushes['#FFF8EC'], 76, 114, 6, 7)
    $taskGraphics.FillEllipse($taskBrushes['#FFF8EC'], 168, 114, 6, 7)
    $taskGraphics.FillEllipse($taskBrushes['#E3B0A2'], 114, 151, 28, 18)
    $taskPen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml('#8C7053'), 3)
    $taskGraphics.DrawLine($taskPen, 128, 167, 128, 178)
    $taskGraphics.DrawArc($taskPen, 113, 168, 15, 16, 0, 150)
    $taskGraphics.DrawArc($taskPen, 128, 168, 15, 16, 30, 150)
    $taskGraphics.DrawLine($taskPen, 80, 164, 43, 157)
    $taskGraphics.DrawLine($taskPen, 79, 176, 41, 181)
    $taskGraphics.DrawLine($taskPen, 176, 164, 213, 157)
    $taskGraphics.DrawLine($taskPen, 177, 176, 215, 181)
    $taskPng = New-Object System.IO.MemoryStream
    $taskBitmap.Save($taskPng, [System.Drawing.Imaging.ImageFormat]::Png)
    $taskFrames += ,@{ Size = $taskSize; Bytes = $taskPng.ToArray() }
    $taskPng.Dispose(); $taskPen.Dispose(); $taskGraphics.Dispose(); $taskBitmap.Dispose()
    foreach ($taskBrush in $taskBrushes.Values) { $taskBrush.Dispose() }
}
$taskIconFile = Join-Path $taskPayload 'hamster.ico'
$taskIconStream = [System.IO.File]::Create($taskIconFile)
$taskWriter = New-Object System.IO.BinaryWriter($taskIconStream)
$taskWriter.Write([UInt16]0); $taskWriter.Write([UInt16]1); $taskWriter.Write([UInt16]$taskFrames.Count)
$taskOffset = 6 + 16 * $taskFrames.Count
foreach ($taskFrame in $taskFrames) {
    $taskDimension = if ($taskFrame.Size -eq 256) { 0 } else { $taskFrame.Size }
    $taskWriter.Write([byte]$taskDimension); $taskWriter.Write([byte]$taskDimension)
    $taskWriter.Write([byte]0); $taskWriter.Write([byte]0)
    $taskWriter.Write([UInt16]1); $taskWriter.Write([UInt16]32)
    $taskWriter.Write([UInt32]$taskFrame.Bytes.Length); $taskWriter.Write([UInt32]$taskOffset)
    $taskOffset += $taskFrame.Bytes.Length
}
foreach ($taskFrame in $taskFrames) { $taskWriter.Write([byte[]]$taskFrame.Bytes) }
$taskWriter.Dispose(); $taskIconStream.Dispose()

Copy-Item -LiteralPath (Join-Path $taskRoot 'assets') -Destination $taskPayload -Recurse -Force
$taskSdk = Join-Path $taskRoot 'vendor\webview2'
Copy-Item -LiteralPath (Join-Path $taskSdk 'lib\net462\Microsoft.Web.WebView2.Core.dll') -Destination $taskPayload -Force
Copy-Item -LiteralPath (Join-Path $taskSdk 'lib\net462\Microsoft.Web.WebView2.WinForms.dll') -Destination $taskPayload -Force
Copy-Item -LiteralPath (Join-Path $taskSdk 'runtimes\win-x64\native\WebView2Loader.dll') -Destination $taskPayload -Force
Copy-Item -LiteralPath (Join-Path $taskSdk 'LICENSE.txt') -Destination (Join-Path $taskPayload 'WebView2-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $taskSdk 'NOTICE.txt') -Destination (Join-Path $taskPayload 'WebView2-NOTICE.txt') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'THIRD-PARTY-NOTICES.txt') -Destination $taskPayload -Force

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskBundle = Join-Path $taskBuild 'bundle.zip'
if (Test-Path -LiteralPath $taskBundle) { Remove-Item -LiteralPath $taskBundle }
[System.IO.Compression.ZipFile]::CreateFromDirectory($taskPayload, $taskBundle, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskExe = Join-Path $taskDist '仓鼠小挂件.exe'
& $taskCompiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/out:$taskExe" `
    "/win32manifest:$taskRoot\src\app.manifest" "/win32icon:$taskIconFile" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Web.Extensions.dll `
    "/r:$taskPayload\Microsoft.Web.WebView2.Core.dll" "/r:$taskPayload\Microsoft.Web.WebView2.WinForms.dll" `
    "/resource:$taskBundle,hamster.bundle.zip" (Join-Path $taskRoot 'src\HamsterDesktop.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
if ($Desktop) {
    $taskDesktopExe = Join-Path ([Environment]::GetFolderPath('Desktop')) '仓鼠小挂件.exe'
    if (Test-Path -LiteralPath $taskDesktopExe) { throw "桌面已存在同名文件，请先确认：$taskDesktopExe" }
    Copy-Item -LiteralPath $taskExe -Destination $taskDesktopExe
}
Get-Item -LiteralPath $taskExe | Select-Object FullName,Length
