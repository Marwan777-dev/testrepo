$zip = Join-Path (Get-Location) 'SRS-M16-Customer-Journey-Mapping-v1_1.docx'
$tmp = Join-Path $env:TEMP 'docx_extract'
Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $tmp | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $tmp)
$xmlPath = Join-Path $tmp 'word/document.xml'
if (-Not (Test-Path $xmlPath)) { Write-Error "Missing $xmlPath"; exit 1 }
$xml = Get-Content $xmlPath -Raw
$text = $xml -replace '</w:p>', "`n" -replace '<[^>]+>', ''
$text.Split("`n") | Select-Object -First 300 | ForEach-Object { if ($_.Trim()) { Write-Output $_ } }
