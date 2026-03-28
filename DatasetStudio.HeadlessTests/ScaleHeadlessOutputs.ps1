param(
    [Parameter(Mandatory = $true)]
    [string]$SourceFolder,

    [Parameter(Mandatory = $true)]
    [string]$TargetFolder,

    [int]$Scale = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (Test-Path $TargetFolder) {
    Remove-Item $TargetFolder -Recurse -Force
}

New-Item -ItemType Directory -Path $TargetFolder | Out-Null

Add-Type -AssemblyName System.Drawing

Get-ChildItem $SourceFolder -Filter '*.png' | ForEach-Object {
    [System.Drawing.Image]$sourceImage = [System.Drawing.Image]::FromFile($_.FullName)
    try {
        [int]$targetWidth = $sourceImage.Width * $Scale
        [int]$targetHeight = $sourceImage.Height * $Scale
        [System.Drawing.Bitmap]$scaledBitmap = New-Object System.Drawing.Bitmap $targetWidth, $targetHeight
        try {
            [System.Drawing.Graphics]$graphics = [System.Drawing.Graphics]::FromImage($scaledBitmap)
            try {
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                $graphics.DrawImage($sourceImage, 0, 0, $targetWidth, $targetHeight)

                [string]$targetPath = Join-Path $TargetFolder ($_.BaseName + "-${Scale}x.png")
                $scaledBitmap.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally {
                $graphics.Dispose()
            }
        }
        finally {
            $scaledBitmap.Dispose()
        }
    }
    finally {
        $sourceImage.Dispose()
    }
}
