<#
.SYNOPSIS
    Uygulama ikonunu çok boyutlu .ico olarak üretir.
.DESCRIPTION
    İki kip:
    - Varsayılan: yeşil daire üzerinde saat (repodaki genel ikon, Varliklar\uygulama.ico)
    - -Kaynak <görsel>: firma logosundan ikon. Logo çevresindeki çerçeve çizgileri ve boşluk
      atılır, içerik beyaz yuvarlatılmış kare zemine ortalanır (yayın betiği Varliklar\marka.ico
      için kullanır; firma logosu repoya girmez).

    Her boyut ayrı çizilir (küçük boyutta bulanıklık olmasın), PNG kodlanıp tek ICO'da birleştirilir.
    Windows PowerShell 5.1 ile çalıştırın:  powershell -File araclar\ikon-uret.ps1 [-Kaynak logo.jpg -Cikti marka.ico]
#>
param(
    [string]$Kaynak,
    [string]$Cikti = (Join-Path $PSScriptRoot '..\src\PdksMasaustu\Varliklar\uygulama.ico')
)

Add-Type -AssemblyName PresentationCore, WindowsBase
$ErrorActionPreference = 'Stop'
$boyutlar = 16, 24, 32, 48, 64, 128, 256

function Cizim([int]$b, [scriptblock]$ciz) {
    $gorsel = New-Object Windows.Media.DrawingVisual
    $dc = $gorsel.RenderOpen()
    & $ciz $dc $b
    $dc.Close()
    $bmp = New-Object Windows.Media.Imaging.RenderTargetBitmap $b, $b, 96, 96, ([Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($gorsel)
    $kod = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $kod.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $akis = New-Object IO.MemoryStream
    $kod.Save($akis)
    , $akis.ToArray()
}

<# Logodaki içeriğin sınırını bulur: beyaz olmayan pikseller, ancak bir satırın/sütunun
   %60'ından fazlasını kaplayan çizgiler (çerçeve) sayılmaz. #>
function IcerikSiniri($kaynak) {
    $bgra = New-Object Windows.Media.Imaging.FormatConvertedBitmap($kaynak, [Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $w = $bgra.PixelWidth; $h = $bgra.PixelHeight; $adim = $w * 4
    $p = New-Object byte[] ($adim * $h)
    $bgra.CopyPixels($p, $adim, 0)

    $dolu = New-Object 'bool[,]' $w, $h
    $satir = New-Object int[] $h; $sutun = New-Object int[] $w
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $i = $y * $adim + $x * 4
            if ($p[$i + 3] -gt 32 -and ($p[$i] -lt 225 -or $p[$i + 1] -lt 225 -or $p[$i + 2] -lt 225)) {
                $dolu[$x, $y] = $true; $satir[$y]++; $sutun[$x]++
            }
        }
    }
    $x1 = $w; $y1 = $h; $x2 = -1; $y2 = -1
    for ($y = 0; $y -lt $h; $y++) {
        if ($satir[$y] -gt $w * 0.6) { continue }
        for ($x = 0; $x -lt $w; $x++) {
            if ($sutun[$x] -gt $h * 0.6 -or -not $dolu[$x, $y]) { continue }
            if ($x -lt $x1) { $x1 = $x }; if ($x -gt $x2) { $x2 = $x }
            if ($y -lt $y1) { $y1 = $y }; if ($y -gt $y2) { $y2 = $y }
        }
    }
    if ($x2 -lt 0) { return New-Object Windows.Int32Rect 0, 0, $w, $h }
    New-Object Windows.Int32Rect $x1, $y1, ($x2 - $x1 + 1), ($y2 - $y1 + 1)
}

if ($Kaynak) {
    $kaynakGorsel = New-Object Windows.Media.Imaging.BitmapImage
    $kaynakGorsel.BeginInit()
    $kaynakGorsel.CacheOption = [Windows.Media.Imaging.BitmapCacheOption]::OnLoad
    $kaynakGorsel.UriSource = New-Object Uri (Resolve-Path $Kaynak).Path
    $kaynakGorsel.EndInit()
    $sinir = IcerikSiniri $kaynakGorsel
    $icerik = New-Object Windows.Media.Imaging.CroppedBitmap($kaynakGorsel, $sinir)

    $pngler = foreach ($b in $boyutlar) {
        Cizim $b {
            param($dc, $b)
            $kenar = [math]::Max(1, $b * 0.06)
            $kose = $b * 0.18
            $dc.DrawRoundedRectangle([Windows.Media.Brushes]::White, $null, (New-Object Windows.Rect $kenar, $kenar, ($b - 2 * $kenar), ($b - 2 * $kenar)), $kose, $kose)
            # İçerik zeminin %80'ine en-boy oranı korunarak sığdırılır
            $alan = $b * 0.80
            $olcek = [math]::Min($alan / $sinir.Width, $alan / $sinir.Height)
            $gen = $sinir.Width * $olcek; $yuk = $sinir.Height * $olcek
            $dc.DrawImage($icerik, (New-Object Windows.Rect (($b - $gen) / 2), (($b - $yuk) / 2), $gen, $yuk))
        }
    }
} else {
    $yesil = New-Object Windows.Media.SolidColorBrush ([Windows.Media.Color]::FromRgb(0x2E, 0x9E, 0x5B))
    $font = New-Object Windows.Media.FontFamily('Segoe Fluent Icons, Segoe MDL2 Assets')
    $pngler = foreach ($b in $boyutlar) {
        Cizim $b {
            param($dc, $b)
            $dc.DrawEllipse($yesil, $null, (New-Object Windows.Point ($b / 2), ($b / 2)), ($b / 2 - $b * 0.02), ($b / 2 - $b * 0.02))
            $yazi = New-Object Windows.Media.FormattedText(([string][char]0xE823), [Globalization.CultureInfo]::InvariantCulture,
                [Windows.FlowDirection]::LeftToRight,
                (New-Object Windows.Media.Typeface($font, [Windows.FontStyles]::Normal, [Windows.FontWeights]::Normal, [Windows.FontStretches]::Normal)),
                ($b * 0.52), [Windows.Media.Brushes]::White, 1.0)
            $dc.DrawText($yazi, (New-Object Windows.Point (($b - $yazi.Width) / 2), (($b - $yazi.Height) / 2)))
        }
    }
}

# ICO: 6 bayt başlık + girdi başına 16 bayt dizin + PNG verileri
$cikis = New-Object IO.MemoryStream
$yaz = New-Object IO.BinaryWriter $cikis
$yaz.Write([uint16]0); $yaz.Write([uint16]1); $yaz.Write([uint16]$boyutlar.Count)
$konum = 6 + 16 * $boyutlar.Count
for ($i = 0; $i -lt $boyutlar.Count; $i++) {
    $b = $boyutlar[$i]
    $kenar = if ($b -ge 256) { 0 } else { $b }   # 256 piksel 0 olarak yazılır
    $yaz.Write([byte]$kenar); $yaz.Write([byte]$kenar); $yaz.Write([byte]0); $yaz.Write([byte]0)
    $yaz.Write([uint16]1); $yaz.Write([uint16]32)
    $yaz.Write([uint32]$pngler[$i].Length); $yaz.Write([uint32]$konum)
    $konum += $pngler[$i].Length
}
foreach ($p in $pngler) { $yaz.Write($p) }
$yaz.Flush()

$klasor = Split-Path $Cikti
New-Item -ItemType Directory -Force $klasor | Out-Null
[IO.File]::WriteAllBytes((Join-Path (Resolve-Path $klasor).Path (Split-Path $Cikti -Leaf)), $cikis.ToArray())
"İkon üretildi: $Cikti ($($boyutlar -join ', ') px)"
