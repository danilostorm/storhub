param(
    [Parameter(Mandatory=$true)][string]$Reference,
    [Parameter(Mandatory=$true)][string]$Actual,
    [string]$Heatmap = ""
)
# Verificacao pixel a pixel: imagens devem ter EXATAMENTE a mesma resolucao e escala.
# Não modifica as imagens de origem.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not (Test-Path -LiteralPath $Reference)) { throw "Referencia nao encontrada: $Reference" }
if (-not (Test-Path -LiteralPath $Actual)) { throw "Captura nao encontrada: $Actual" }
if ($Heatmap.Length -gt 0) {
    $parent = Split-Path -Parent $Heatmap
    if ($parent -and -not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
}
$cs = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class StorHubPixelCompare {
    public sealed class Result {
        public int Width, Height;
        public long Same, Changed, ChannelTotal, HighDifference;
        public double MeanAbsoluteChannelError, IdenticalPercentage, ChangedPercentage;
    }
    private static byte[] GetBytes(Bitmap bmp) {
        Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try {
            if (data.Stride < 0) throw new InvalidOperationException("Stride negativo nao suportado.");
            byte[] bytes = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return bytes;
        } finally { bmp.UnlockBits(data); }
    }
    private static void PutBytes(Bitmap bmp, byte[] bytes) {
        Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        BitmapData data = bmp.LockBits(rect, ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try { Marshal.Copy(bytes, 0, data.Scan0, bytes.Length); }
        finally { bmp.UnlockBits(data); }
    }
    public static Result Compare(string reference, string actual, string heatmap) {
        using (Bitmap a0 = new Bitmap(reference))
        using (Bitmap b0 = new Bitmap(actual)) {
            if (a0.Width != b0.Width || a0.Height != b0.Height)
                throw new ArgumentException("Resolucao diferente: referencia " +
                    a0.Width + "x" + a0.Height + ", captura " + b0.Width + "x" + b0.Height +
                    ". Compare em mesmo tamanho, zoom, escala e DPI.");
            int width = a0.Width, height = a0.Height;
            using (Bitmap a = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (Bitmap b = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (Graphics ga = Graphics.FromImage(a))
            using (Graphics gb = Graphics.FromImage(b)) {
                ga.DrawImageUnscaled(a0, 0, 0);
                gb.DrawImageUnscaled(b0, 0, 0);
                byte[] pxA = GetBytes(a), pxB = GetBytes(b);
                byte[] diff = heatmap.Length > 0 ? new byte[pxA.Length] : null;
                long changed = 0, same = 0, totalDifference = 0, severe = 0;
                int stride = width * 4;
                for (int y = 0; y < height; y++) {
                    for (int x = 0; x < width; x++) {
                        int i = y * stride + x * 4;
                        int db = Math.Abs((int)pxA[i] - pxB[i]);
                        int dg = Math.Abs((int)pxA[i + 1] - pxB[i + 1]);
                        int dr = Math.Abs((int)pxA[i + 2] - pxB[i + 2]);
                        int error = db + dg + dr;
                        if (error == 0) same++; else changed++;
                        if (error > 96) severe++;
                        totalDifference += error;
                        if (diff != null) {
                            int intensity = Math.Min(255, error * 2 / 3);
                            diff[i] = 0;
                            diff[i + 1] = 0;
                            diff[i + 2] = (byte)intensity;
                            diff[i + 3] = 255;
                        }
                    }
                }
                long pixels = (long)width * height;
                if (diff != null) {
                    using (Bitmap outBmp = new Bitmap(width,height,
                        PixelFormat.Format32bppArgb)) {
                        PutBytes(outBmp,diff);
                        outBmp.Save(heatmap,ImageFormat.Png);
                    }
                }
                return new Result {
                    Width=width, Height=height, Same=same, Changed=changed,
                    ChannelTotal=totalDifference,HighDifference=severe,
                    MeanAbsoluteChannelError=totalDifference/(double)(pixels*3),
                    IdenticalPercentage=100.0*same/pixels,
                    ChangedPercentage=100.0*changed/pixels
                };
            }
        }
    }
}
'@
Add-Type -TypeDefinition $cs -ReferencedAssemblies 'System.Drawing'
$result = [StorHubPixelCompare]::Compare(
    (Resolve-Path -LiteralPath $Reference).Path,
    (Resolve-Path -LiteralPath $Actual).Path,
    $Heatmap)
Write-Host ('Dimensoes: {0} x {1}' -f $result.Width,$result.Height)
Write-Host ('Pixels identicos: {0:N0} ({1:N3}%)' -f $result.Same,$result.IdenticalPercentage)
Write-Host ('Pixels diferentes: {0:N0} ({1:N3}%)' -f $result.Changed,$result.ChangedPercentage)
Write-Host ('Pixels com diferenca de cor alta: {0:N0}' -f $result.HighDifference)
Write-Host ('Erro absoluto medio por canal RGB: {0:N2}/255' -f $result.MeanAbsoluteChannelError)
if ($Heatmap.Length -gt 0) { Write-Host ('Mapa de diferencas: '+$Heatmap) }
# Esta comparacao nao substitui testes de comportamento, acessibilidade ou recursos.
