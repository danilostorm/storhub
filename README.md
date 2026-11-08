# StorHUB

**Protótipo independente para Windows — versão 0.1.0-alpha.**

O StorHUB é uma central de diagnóstico e manutenção de PCs com interface escura e navegação em português. O desenvolvimento é independente, usando como **referência visual e funcional** o [HUB Pro — Mestres da Informática](https://github.com/mestresdainformatica/HUB-Pro).

## Estado atual

**Já implementado e compilado no NEXUS:**
- Interface Windows Forms com painel principal, navegação por categorias, cartões e indicador de análise.
- Consulta real e **somente leitura** de Windows, CPU, GPU, memória RAM e disco.
- Listagem de unidades, interfaces de rede e detecção de bateria.
- Resumo copiável das informações do computador.
- Indicador inicial baseado **somente** em RAM e espaço livre: não equivale ao diagnóstico de saúde completo do programa de referência.
- Atalhos explícitos para páginas de configurações do Windows.

**Ainda não implementado:** os módulos de manutenção, limpeza, benchmark, drivers, reparos, desinstalação, pós-formatação e otimização. Eles estão representados na navegação, mas não executam ações.

## Onde estão os arquivos no NEXUS

- Fontes: \`E:\StorHUB\StorHUB-src\src\StorHUB.cs\`
- Compilação: \`E:\StorHUB\StorHUB-src\build.ps1\`
- Binário de desenvolvimento: \`E:\StorHUB\StorHUB-src\dist\StorHUB.exe\`
- Referência visual extraída: \`E:\StorHUB\referencias\painel.png\`

Os executáveis originais permanecem em \`E:\StorHUB\` e não foram alterados.

## Compilar no Windows

Requer Windows com .NET Framework 4.8 e PowerShell 5.1. O script utiliza o compilador C# do próprio .NET Framework, sem SDK adicional.

~~~powershell
cd E:\StorHUB\StorHUB-src
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\dist\StorHUB.exe
~~~

## Direção visual

O objetivo é aproximar progressivamente o layout do material de referência, ajustando cores, medidas, tipografia, cartões, ícones e espaçamento por comparação de capturas. **Ainda não há equivalência pixel a pixel**, e as imagens do aplicativo original não estão incluídas no código.

## Autoria e créditos

A implementação foi escrita do zero. **Não copiamos o código-fonte nem os executáveis** do HUB Pro, porque seu repositório público fornece documentação, imagens e binários de distribuição, mas não disponibiliza o código-fonte do aplicativo. O documento \`LICENCAS-TERCEIROS.txt\` do projeto original diz respeito a componentes de terceiros e não autoriza, por si só, reprodução ou redistribuição de todo o aplicativo.

**HUB Pro original:** Mestres da Informática. **StorHUB:** projeto independente, sem afiliação declarada.
