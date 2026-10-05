<#
.SYNOPSIS
    Yeni sürümü derler, test eder, Velopack paketi üretir ve güncelleme klasörüne kopyalar.
.DESCRIPTION
    1. Sürüm numarası Directory.Build.props'a yazılır (tek kaynak)
    2. Testler Release yapılandırmasında çalışır; başarısızsa yayın durur
    3. Self-contained win-x64 publish
    4. vpk pack: tam paket + önceki sürüme göre delta paket + Setup.exe
       (önceki paketler yerel Releases\ klasöründe tutulur, delta için gereklidir)
    5. Releases\ içeriği yayın klasörüne kopyalanır; kurulu uygulamalar oradan günceller

    Gerçek sunucu yolu repoda tutulmaz: -YayinKlasoru ile ya da PDKS_YAYIN_KLASORU
    ortam değişkeniyle verilir. Verilmezse yalnız paket üretilir.
.EXAMPLE
    .\araclar\yayinla.ps1 -Surum 0.2.0 -SurumNotu "Ekibim sekmesine arama eklendi"
#>
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Surum,
    [string]$SurumNotu = '',
    [string]$YayinKlasoru = $env:PDKS_YAYIN_KLASORU
)

$ErrorActionPreference = 'Stop'
$kok = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $kok
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

function Adim($metin) { Write-Host "`n▶ $metin" -ForegroundColor Cyan }
function Calistir([scriptblock]$komut) { & $komut; if ($LASTEXITCODE -ne 0) { throw "Komut başarısız (çıkış kodu $LASTEXITCODE)" } }

$uygulama = 'src\PdksMasaustu'
if (-not (Test-Path "$uygulama\appsettings.json")) {
    throw "$uygulama\appsettings.json yok. appsettings.example.json dosyasını kopyalayıp gerçek adresleri girin."
}
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    throw "vpk bulunamadı. Kurulum: dotnet tool install -g vpk"
}

Adim 'Marka (ad ve logodan ikon)'
# Ad ve adresler gitignore'daki appsettings.json'dan okunur; repo firma bilgisi içermez.
$ayar = Get-Content "$uygulama\appsettings.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$uygulamaAdi = if ($ayar.Marka.UygulamaAdi) { $ayar.Marka.UygulamaAdi } else { 'PDKS Masaüstü' }
$markaIkonu = "$uygulama\Varliklar\marka.ico"
try {
    $taban = $ayar.Api.TabanAdres.TrimEnd('/') + '/'
    $logoUrl = (Invoke-RestMethod "${taban}ayarlar" -TimeoutSec 20).logoUrl
    if (-not $logoUrl) { throw 'Portalda logo tanımlı değil' }
    $logoAdres = [Uri]::new([Uri]$taban, $logoUrl).AbsoluteUri
    $logoDosyasi = Join-Path $env:TEMP ('pdks-logo' + [IO.Path]::GetExtension(([Uri]$logoAdres).AbsolutePath))
    Invoke-WebRequest $logoAdres -OutFile $logoDosyasi -UseBasicParsing
    # WPF çizimi Windows PowerShell 5.1 ile yapılır
    Calistir { powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'ikon-uret.ps1') -Kaynak $logoDosyasi -Cikti $markaIkonu }
} catch {
    Write-Host "  Logo alınamadı ($($_.Exception.Message)); genel ikon kullanılacak." -ForegroundColor Yellow
    if (Test-Path $markaIkonu) { Remove-Item -LiteralPath $markaIkonu }
}
$ikon = if (Test-Path $markaIkonu) { $markaIkonu } else { "$uygulama\Varliklar\uygulama.ico" }
Write-Host "  Ad: $uygulamaAdi · İkon: $ikon"

Adim "Sürüm $Surum yazılıyor"
$props = Join-Path $kok 'Directory.Build.props'
$icerik = [IO.File]::ReadAllText($props)
$icerik = [regex]::Replace($icerik, '<Version>[^<]+</Version>', "<Version>$Surum</Version>")
[IO.File]::WriteAllText($props, $icerik, [Text.UTF8Encoding]::new($false))

Adim 'Testler'
Calistir { dotnet test -c Release -nologo -v q }

Adim 'Publish (self-contained win-x64)'
if (Test-Path publish) { Remove-Item publish -Recurse -Force }
Calistir { dotnet publish $uygulama -c Release -r win-x64 --self-contained -o publish -nologo -v q "-p:Product=$uygulamaAdi" }

Adim 'Velopack paketi'
$vpkArgs = @(
    'pack', '--packId', 'PdksMasaustu', '--packVersion', $Surum,
    '--packDir', 'publish', '--mainExe', 'PdksMasaustu.exe',
    '--packTitle', $uygulamaAdi, '--packAuthors', 'Batuhan KAHRAMAN',
    '--icon', $ikon, '--outputDir', 'Releases',
    '--runtime', 'win-x64'
)
if ($SurumNotu) {
    $notDosyasi = Join-Path $env:TEMP "pdks-surum-notu-$Surum.md"
    [IO.File]::WriteAllText($notDosyasi, "## v$Surum`n`n$SurumNotu`n", [Text.UTF8Encoding]::new($false))
    $vpkArgs += @('--releaseNotes', $notDosyasi)
}
Calistir { vpk @vpkArgs }

if ($YayinKlasoru) {
    Adim "Yayın klasörüne kopyalanıyor: $YayinKlasoru"
    New-Item -ItemType Directory -Force $YayinKlasoru | Out-Null
    # Önce paketler, en son sürüm listesi: istemci listeyi gördüğünde paket hazır olsun.
    Get-ChildItem Releases -File | Where-Object Name -notlike 'releases.*.json' | Copy-Item -Destination $YayinKlasoru -Force
    Get-ChildItem Releases -File -Filter 'releases.*.json' | Copy-Item -Destination $YayinKlasoru -Force
} else {
    Write-Host "`nYayın klasörü verilmedi; paketler yalnız Releases\ içinde." -ForegroundColor Yellow
}

Adim 'Tamamlandı'
Get-ChildItem Releases -File | Where-Object Name -like "*$Surum*" | ForEach-Object { Write-Host ('  {0,-45} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB)) }
