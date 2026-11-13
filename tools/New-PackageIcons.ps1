param(
    [string]$OutDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'src\WidBar.DailyVerse.ExtensionApp\Package\Images')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Render-Icon([string]$filePath, [int]$width, [int]$height, [string]$variant) {
    $bmp = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $cx = $width / 2.0
    $cy = $height / 2.0
    $s = [Math]::Min($width, $height)

    $drawPlate = $variant -in @('plated', 'tile', 'splash')
    $isLight = $variant -eq 'lightunplated'

    if ($drawPlate) {
        $bgGrad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            [System.Drawing.PointF]::new(0, 0),
            [System.Drawing.PointF]::new($width, $height),
            [System.Drawing.Color]::FromArgb(255, 32, 28, 24),
            [System.Drawing.Color]::FromArgb(255, 18, 16, 15)
        )
        if ($variant -eq 'plated') {
            $pad = [float]($s * 0.06)
            $platePath = New-RoundedRectPath $pad $pad ($s - 2 * $pad) ($s - 2 * $pad) ([float]($s * 0.22))
            $g.FillPath($bgGrad, $platePath)
            $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, 245, 200, 92), [Math]::Max(1.0, $s * 0.025))
            $g.DrawPath($borderPen, $platePath)
            $borderPen.Dispose()
            $platePath.Dispose()
        }
        else {
            $g.FillRectangle($bgGrad, 0, 0, $width, $height)
        }
        $bgGrad.Dispose()
    }

    $iconScale = if ($variant -eq 'tile') { $s * 0.56 } elseif ($variant -eq 'splash') { $s * 0.42 } else { $s * 0.72 }
    $unit = $iconScale / 24.0
    $ox = $cx - 12.0 * $unit
    $oy = $cy - 12.0 * $unit

    # Brilho suave atrás do livro
    $haloRadius = [float](9.5 * $unit)
    $haloPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $haloPath.AddEllipse([float]($cx - $haloRadius), [float]($oy + 2.0 * $unit), [float]($haloRadius * 2), [float]($haloRadius * 2))
    $haloBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($haloPath)
    $haloBrush.CenterColor = if ($isLight) { [System.Drawing.Color]::FromArgb(75, 217, 119, 6) } else { [System.Drawing.Color]::FromArgb(90, 245, 200, 92) }
    $haloBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 245, 200, 92))
    $g.FillPath($haloBrush, $haloPath)
    $haloBrush.Dispose()
    $haloPath.Dispose()

    $strokeWidth = [float]([Math]::Max(1.25, 1.75 * $unit))
    $goldStroke = if ($isLight) { [System.Drawing.Color]::FromArgb(255, 154, 91, 19) } else { [System.Drawing.Color]::FromArgb(255, 245, 200, 92) }
    $pageFill = if ($isLight) { [System.Drawing.Color]::FromArgb(50, 217, 119, 6) } else { [System.Drawing.Color]::FromArgb(55, 245, 200, 92) }
    $lineColor = if ($isLight) { [System.Drawing.Color]::FromArgb(180, 154, 91, 19) } else { [System.Drawing.Color]::FromArgb(185, 253, 230, 138) }

    # Páginas abertas (esquerda + direita)
    $bookPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bookPath.AddBezier(
        [float]($ox + 2.8 * $unit), [float]($oy + 5.6 * $unit),
        [float]($ox + 7.2 * $unit), [float]($oy + 4.8 * $unit),
        [float]($ox + 10.8 * $unit), [float]($oy + 5.2 * $unit),
        [float]($ox + 12.0 * $unit), [float]($oy + 7.0 * $unit))
    $bookPath.AddBezier(
        [float]($ox + 12.0 * $unit), [float]($oy + 7.0 * $unit),
        [float]($ox + 13.2 * $unit), [float]($oy + 5.2 * $unit),
        [float]($ox + 16.8 * $unit), [float]($oy + 4.8 * $unit),
        [float]($ox + 21.2 * $unit), [float]($oy + 5.6 * $unit))
    $bookPath.AddLine([float]($ox + 21.2 * $unit), [float]($oy + 5.6 * $unit), [float]($ox + 21.2 * $unit), [float]($oy + 17.6 * $unit))
    $bookPath.AddBezier(
        [float]($ox + 21.2 * $unit), [float]($oy + 17.6 * $unit),
        [float]($ox + 16.8 * $unit), [float]($oy + 16.8 * $unit),
        [float]($ox + 13.4 * $unit), [float]($oy + 17.4 * $unit),
        [float]($ox + 12.0 * $unit), [float]($oy + 19.2 * $unit))
    $bookPath.AddBezier(
        [float]($ox + 12.0 * $unit), [float]($oy + 19.2 * $unit),
        [float]($ox + 10.6 * $unit), [float]($oy + 17.4 * $unit),
        [float]($ox + 7.2 * $unit), [float]($oy + 16.8 * $unit),
        [float]($ox + 2.8 * $unit), [float]($oy + 17.6 * $unit))
    $bookPath.CloseFigure()

    $fillBrush = New-Object System.Drawing.SolidBrush($pageFill)
    $g.FillPath($fillBrush, $bookPath)
    $fillBrush.Dispose()

    $bookPen = New-Object System.Drawing.Pen($goldStroke, $strokeWidth)
    $bookPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($bookPen, $bookPath)
    $g.DrawLine($bookPen, [float]($ox + 12.0 * $unit), [float]($oy + 7.0 * $unit), [float]($ox + 12.0 * $unit), [float]($oy + 19.2 * $unit))
    $bookPen.Dispose()
    $bookPath.Dispose()

    $linePen = New-Object System.Drawing.Pen($lineColor, [float]([Math]::Max(1.0, 1.3 * $unit)))
    $linePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $linePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    foreach ($yOff in @(9.2, 12.0, 14.8)) {
        $g.DrawLine($linePen, [float]($ox + 5.2 * $unit), [float]($oy + $yOff * $unit), [float]($ox + 9.4 * $unit), [float]($oy + ($yOff + 0.2) * $unit))
        $g.DrawLine($linePen, [float]($ox + 14.6 * $unit), [float]($oy + ($yOff + 0.2) * $unit), [float]($ox + 18.8 * $unit), [float]($oy + $yOff * $unit))
    }
    $linePen.Dispose()

    $g.Dispose()
    $bmp.Save($filePath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

foreach ($scale in @(100, 125, 150, 200)) {
    $f = $scale / 100.0
    Render-Icon (Join-Path $OutDir "AppIcon.scale-$scale.png") ([int][Math]::Round(44 * $f)) ([int][Math]::Round(44 * $f)) 'plated'
    Render-Icon (Join-Path $OutDir "StoreLogo.scale-$scale.png") ([int][Math]::Round(50 * $f)) ([int][Math]::Round(50 * $f)) 'plated'
    Render-Icon (Join-Path $OutDir "SmallTile.scale-$scale.png") ([int][Math]::Round(71 * $f)) ([int][Math]::Round(71 * $f)) 'tile'
    Render-Icon (Join-Path $OutDir "MediumTile.scale-$scale.png") ([int][Math]::Round(150 * $f)) ([int][Math]::Round(150 * $f)) 'tile'
    Render-Icon (Join-Path $OutDir "WideTile.scale-$scale.png") ([int][Math]::Round(310 * $f)) ([int][Math]::Round(150 * $f)) 'tile'
    Render-Icon (Join-Path $OutDir "LargeTile.scale-$scale.png") ([int][Math]::Round(310 * $f)) ([int][Math]::Round(310 * $f)) 'tile'
    Render-Icon (Join-Path $OutDir "SplashScreen.scale-$scale.png") ([int][Math]::Round(620 * $f)) ([int][Math]::Round(300 * $f)) 'splash'
}

foreach ($sz in @(16, 24, 32, 48, 256)) {
    Render-Icon (Join-Path $OutDir "AppIcon.targetsize-$sz.png") $sz $sz 'plated'
    Render-Icon (Join-Path $OutDir "AppIcon.altform-unplated_targetsize-$sz.png") $sz $sz 'unplated'
    Render-Icon (Join-Path $OutDir "AppIcon.altform-lightunplated_targetsize-$sz.png") $sz $sz 'lightunplated'
}

Write-Host "Ícones gerados com sucesso em $OutDir"
