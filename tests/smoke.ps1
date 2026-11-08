# Execute no Windows com PowerShell 5.1.
# Testes locais e não destrutivos: não executa limpeza, reparos nem alterações do sistema.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $root 'dist\StorHUB.exe'
if (-not (Test-Path $exe)) { throw "Compile primeiro com build.ps1: $exe" }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Reflection.Assembly]::LoadFrom($exe) | Out-Null
$instance = New-Object StorHUB.StorHubForm
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$staticFlags = [Reflection.BindingFlags]'Static,NonPublic'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('StorHUB-QA-' + [Guid]::NewGuid().ToString('N'))
$passed = 0
try {
    New-Item -ItemType Directory -Path (Join-Path $temp 'A'), (Join-Path $temp 'B') -Force | Out-Null
    $instance.Show()
    [Windows.Forms.Application]::DoEvents()
    if ($instance.Text -notmatch 'StorHUB') { throw "Título incorreto." }
    Write-Host '[OK] Inicialização da janela' -ForegroundColor Green
    $passed++

    $show = $instance.GetType().GetMethod('ShowPage',$flags)
    $pages = @(
        'Painel', 'Meu PC', 'Programas', 'Inicialização', 'Rede', 'Windows 11',
        'Pontos de restauração', 'Limpeza', 'Bateria', 'Saúde dos discos',
        'Arquivos duplicados', 'Mapa do disco', 'Drivers', 'Periféricos', 'Benchmark', 'Sobre'
    )
    foreach($page in $pages) {
        $show.Invoke($instance,@($page)) | Out-Null
        [Windows.Forms.Application]::DoEvents()
        Write-Host "[OK] Tela: $page" -ForegroundColor Green
        $passed++
    }

    [IO.File]::WriteAllText((Join-Path $temp 'A\1.txt'), 'conteudo igual da fixture')
    [IO.File]::WriteAllText((Join-Path $temp 'B\2.txt'), 'conteudo igual da fixture')
    [IO.File]::WriteAllText((Join-Path $temp 'A\3.txt'), 'conteudo diferente')
    $find = $instance.GetType().GetMethod('FindDuplicates',$staticFlags)
    $duplicates = @($find.Invoke($null,@(($temp + '\'),[Threading.CancellationToken]::None)))
    if ($duplicates.Count -ne 2) { throw "Esperado 2 duplicados; encontrados $($duplicates.Count)." }
    Write-Host '[OK] Hash SHA-256 identifica duas cópias iguais' -ForegroundColor Green
    $passed++

    $map = $instance.GetType().GetMethod('GetDirectorySizes',$staticFlags)
    $buckets = @($map.Invoke($null,@(($temp + '\'),[Threading.CancellationToken]::None)))
    if ($buckets.Count -ne 2) { throw "Esperadas 2 pastas no mapa; encontrados $($buckets.Count)." }
    Write-Host '[OK] Mapa do disco soma pastas' -ForegroundColor Green
    $passed++

    [IO.File]::WriteAllText((Join-Path $temp 'A\old.tmp'), 'arquivo antigo')
    [IO.File]::SetLastWriteTime((Join-Path $temp 'A\old.tmp'),[DateTime]::Now.AddDays(-3))
    [IO.File]::WriteAllText((Join-Path $temp 'A\new.tmp'), 'arquivo recente')
    $scan = $instance.GetType().GetMethod('EnumerateOldTempFiles',$staticFlags)
    $old = @($scan.Invoke($null,@(($temp + '\A\'))))
    if ($old.Count -ne 1) { throw "Esperado 1 arquivo temporário antigo; encontrados $($old.Count)." }
    Write-Host '[OK] Analisador temporário ignora arquivos recentes' -ForegroundColor Green
    $passed++

    $score = $instance.GetType().GetMethod('CalculateScore',$flags)
    $value = [int]$score.Invoke($instance,@())
    if ($value -lt 0 -or $value -gt 100) { throw "Indicador inválido: $value" }
    Write-Host '[OK] Indicador de RAM e disco entre 0 e 100' -ForegroundColor Green
    $passed++

    # Consulta real, somente leitura, dos dispositivos Plug and Play.
    $devices = $instance.GetType().GetMethod('QueryDevices',$staticFlags).Invoke($null,@())
    if (@($devices).Count -lt 1) { throw "Nenhum dispositivo PnP foi detectado." }
    Write-Host "[OK] Inventário de $(@($devices).Count) dispositivos Plug and Play" -ForegroundColor Green
    $passed++

    # Evento local de teste de teclado, sem injeção de teclas no sistema.
    $show.Invoke($instance,@('Periféricos')) | Out-Null
    [Windows.Forms.Application]::DoEvents()
    $keyboardEvent = $instance.GetType().GetMethod('OnKeyDown',$flags)
    $keyboardEvent.Invoke($instance,@([Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::A))) | Out-Null
    $keys = $instance.GetType().GetField('testedKeys',$flags).GetValue($instance)
    if (-not $keys.Contains([Windows.Forms.Keys]::A)) { throw "O teste de teclado não registrou A." }
    Write-Host '[OK] Teste de teclado registra teclas reais' -ForegroundColor Green
    $passed++

    $bench = $instance.GetType().GetMethod('RunBenchmark',$staticFlags)
    $callback = [Action[string]]{ param($message) }
    $before = @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Filter 'StorHUB-benchmark-*.tmp' -File -ErrorAction SilentlyContinue).Count
    $results = @($bench.Invoke($null,@([Threading.CancellationToken]::None,$callback)))
    if ($results.Count -ne 5) { throw "Benchmark deveria retornar 5 medições, retornou $($results.Count)." }
    foreach ($metric in $results) {
        if ($metric.Value -le 0 -or [double]::IsNaN($metric.Value) -or [double]::IsInfinity($metric.Value)) {
            throw "Medição inválida: $($metric.Label) = $($metric.Value)."
        }
    }
    Write-Host '[OK] Benchmark CPU, RAM e disco: 5 medições reais' -ForegroundColor Green
    $passed++

    $after = @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Filter 'StorHUB-benchmark-*.tmp' -File -ErrorAction SilentlyContinue).Count
    if ($after -gt $before) { throw "O teste deixou arquivos temporários inesperados." }
    Write-Host '[OK] Arquivos temporários do benchmark removidos' -ForegroundColor Green
    $passed++

    $cancelSource = New-Object System.Threading.CancellationTokenSource
    $cancelSource.Cancel()
    $cancelled = $false
    try {
        $bench.Invoke($null,@($cancelSource.Token,$callback)) | Out-Null
    } catch {
        $inner = $_.Exception
        while ($inner -ne $null) {
            if ($inner -is [OperationCanceledException]) { $cancelled = $true }
            $inner = $inner.InnerException
        }
        if (-not $cancelled) { throw }
    } finally { $cancelSource.Dispose() }
    if (-not $cancelled) { throw "Benchmark ignorou pedido de cancelamento." }
    Write-Host '[OK] Cancelamento do benchmark respeitado' -ForegroundColor Green
    $passed++

    Write-Host "RESULTADO: $passed testes passaram. Nenhuma operação de reparo ou exclusão de arquivos pessoais foi executada." -ForegroundColor Green
} finally {
    $instance.Close()
    $instance.Dispose()
    # Exclusivamente a pasta única criada por este script.
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
