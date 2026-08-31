<#
.SYNOPSIS
    Downloads the Tesseract OCR trained-data files the app needs (English, Spanish,
    Simplified Chinese) into src/ScreenTranslator.App/tessdata.

    These files are intentionally NOT committed to git (they are binary, several MB each,
    and Tesseract's own distribution model is "download the language packs you need").
    Run this script once after cloning the repo, before running or building ScreenTranslator.App.
#>

$ErrorActionPreference = 'Stop'

$targetDir = Join-Path $PSScriptRoot '..\src\ScreenTranslator.App\tessdata'
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$languages = @('eng', 'spa', 'chi_sim')
$baseUrl = 'https://github.com/tesseract-ocr/tessdata_fast/raw/main'

foreach ($lang in $languages) {
    $destination = Join-Path $targetDir "$lang.traineddata"
    if (Test-Path $destination) {
        Write-Host "[skip] $lang.traineddata already present"
        continue
    }

    $url = "$baseUrl/$lang.traineddata"
    Write-Host "[download] $url"
    Invoke-WebRequest -Uri $url -OutFile $destination
}

Write-Host "Done. Trained data available at $targetDir"
