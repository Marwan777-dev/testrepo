$zip = Join-Path (Get-Location) 'SRS-M16-Customer-Journey-Mapping-v1_1.docx'
$tmp = Join-Path $env:TEMP 'docx_extract_spec'
Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $tmp | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $tmp)
$xmlPath = Join-Path $tmp 'word/document.xml'
if (-Not (Test-Path $xmlPath)) { Write-Error 'Missing document.xml'; exit 1 }
$xml = Get-Content $xmlPath -Raw
$text = $xml -replace '</w:p>', "`n" -replace '<[^>]+>', ''
$out = Join-Path (Get-Location) 'docx_full_output.txt'
Set-Content -Path $out -Value $text -NoNewline
Write-Output $out
