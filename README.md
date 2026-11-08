# StorHUB

**Versão 0.1.0-alpha · Aplicativo nativo para Windows**

O StorHUB é um aplicativo independente de diagnóstico e manutenção de PCs, com interface escura, navegação em português e ferramentas do Windows. O objetivo do projeto é atingir **equivalência visual e funcional** com a referência [HUB Pro — Mestres da Informática](https://github.com/mestresdainformatica/HUB-Pro), preservando o nome e a identidade **StorHUB**.

> **Estado atual: implementação parcial; ainda NÃO é equivalente 100% ao HUB Pro.** O repositório de referência oferece executáveis, documentação e imagens, mas não disponibiliza o código-fonte integral. O código deste repositório está sendo escrito de forma independente.

## Implementado

| Módulo | Funcionalidade implementada | Limitações conhecidas |
|---|---|---|
| Painel | Indicador básico e leitura de RAM, disco, Windows, rede e unidades | Não corresponde à análise profunda original; indicador usa RAM e espaço livre |
| Meu PC | Consulta real a Windows, processador, GPU, RAM, disco; copiar resumo | Relatório completo e sensores pendentes |
| Programas | Inventário real de aplicativos instalados, pesquisa e abertura das configurações do Windows | Catálogo winget, instalação e atualização ainda pendentes |
| Inicialização | Itens do Registro e pastas de inicialização; abrir Gerenciador de Tarefas | Desabilitar, reabilitar e medir tempo de boot pendentes |
| Rede | Interfaces ativas, IPv4, gateway, DNS e teste de ping sob demanda | Speedtest, histórico, DNS comparativo e otimização pendentes |
| Windows 11 | RAM, espaço, registro Secure Boot e consulta do TPM | Compatibilidade de CPU e GPU não validada; resultado não é certificação |
| Pontos de restauração | Consulta WMI e solicitação manual de criação com confirmação | Criação depende de permissões/proteção do Windows; restauração real não implementada |
| Limpeza | Análise dos temporários do usuário com mais de 48h; seleção e envio à Lixeira com confirmação | Não cobre demais caches do Windows; exclusão não validada ponta a ponta |
| Saúde dos discos | Unidades detectadas, formato, tamanho total e espaço livre | Não mede S.M.A.R.T., temperatura ou desgaste |
| Bateria | Inventário e percentual via Win32_Battery | Gráficos, ciclos e histórico pendentes |
| Arquivos duplicados | Varredura por tamanho e SHA-256, marcação de cópias extras, confirmação e envio à Lixeira | Até 12 mil arquivos e 1 GiB/arquivo na versão inicial; exclusão não testada ponta a ponta |
| Mapa do disco | Soma de tamanhos por pasta, gráfico de blocos e tabela | Até 30 mil arquivos; navegação detalhada e treemap recursivo pendentes |

Os demais módulos da barra lateral são **estruturas de interface**, sem as funções completas. Não estão prontos: pós-formatação, benchmark, testes de periféricos, reparos, otimizações, desinstalador avançado, drivers, duplicações do catálogo e central desfazer.

## Compilação (Windows)

Requer Windows com **.NET Framework 4.8**, PowerShell 5.1 e o compilador C# incluído no Framework (o script já utiliza o compilador de 64 bits). Um SDK .NET separado não é necessário.

~~~powershell
cd E:\StorHUB\StorHUB-src
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\dist\StorHUB.exe
~~~

## Testes não destrutivos

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\smoke.ps1
~~~

No NEXUS, a versão foi compilada e a suíte retornou **18 testes aprovados** em 08/11/2026. Os testes cobrem criação da janela, abertura de 13 telas e lógica de diagnóstico em dados de amostra (SHA-256, ocupação por pastas, temporários antigos e indicador). **Não** testam, nem executam, limpeza real, criação de pontos de restauração, alterações do Windows ou equivalência total com a referência.

## Arquivos no NEXUS

- Fontes: \`E:\StorHUB\StorHUB-src\src\`
- Script de compilação: \`E:\StorHUB\StorHUB-src\build.ps1\`
- Executável gerado: \`E:\StorHUB\StorHUB-src\dist\StorHUB.exe\`
- Capturas de referência: \`E:\StorHUB\referencias\` (23 telas, 1280 × 820 pixels)
- Originais preservados: \`E:\StorHUB\HUB-Pro-Setup-0.3.23.0.exe\`, \`E:\StorHUB\MestresHub.exe\`, \`E:\StorHUB\HUB-Pro-0.3.23.zip\`

As imagens originais de comparação são somente material local do usuário no NEXUS e **não são distribuídas** neste repositório.

## Fidelidade visual

Veja [docs/fidelidade-visual.md](docs/fidelidade-visual.md). A tela inicial usa navegação, cores e cartões inspirados na referência, mas ainda é uma aproximação: **a equivalência pixel a pixel não foi atingida**.

## Segurança e créditos

O projeto não executa reparos, limpeza ou instalações em segundo plano. As operações destrutivas já implementadas só ocorrem após seleção explícita e confirmação; recomenda-se validá-las em ambiente de testes antes de usar com arquivos importantes.

O HUB Pro original pertence a **Mestres da Informática**. O StorHUB é independente, sem afiliação declarada. Os binários originais e código-fonte não publicado **não são redistribuídos**.
