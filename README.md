# StorHUB

**Versão 0.1.0-alpha — Windows / C# WinForms**

O StorHUB é uma central independente de diagnóstico e manutenção do Windows. A meta é aproximar a **interface e as funções** do [HUB Pro, de Mestres da Informática](https://github.com/mestresdainformatica/HUB-Pro), mantendo a identidade StorHUB. Este projeto utiliza código próprio. **Não é uma versão 100% equivalente ao HUB Pro**, nem contém seu executável ou código fechado.

## Funções implementadas

| Área | Já existe e pode ser usado | O que ainda falta |
|---|---|---|
| Painel | Indicador simples, RAM, disco, Windows e navegação | Diagnóstico completo e fidelidade visual |
| Meu PC | CPU, GPU, RAM, disco e resumo copiável | Sensores avançados e informações adicionais |
| Programas | Inventário do Registro e pesquisa | Catálogo, instalação, atualização e desinstalação próprios |
| Inicialização | Itens do Registro e pastas Startup | Gestão e medição do tempo de inicialização |
| Drivers | Inventário real de dispositivos Plug and Play, códigos de erro, pesquisa e exportação CSV | Atualização e recuperação de drivers |
| Rede | Adaptadores, IP, gateway, DNS e ping sob demanda | Medição de velocidade, DNS comparativo e diagnóstico avançado |
| Windows 11 | Verificação parcial de RAM, disco, Secure Boot e TPM | Validação oficial completa de CPU e GPU |
| Pontos de restauração | Consulta e solicitação de criação mediante confirmação | Restauração guiada e gestão completa |
| Saúde dos discos | Partições, formato, espaço disponível | Temperatura, S.M.A.R.T. e desgaste |
| Bateria | Detecção e percentual fornecidos pelo Windows | Ciclos, histórico e relatórios |
| Limpeza | Pesquisa em TEMP do usuário e envio seletivo à Lixeira com confirmação | Categorias adicionais, testes de exclusão ponta a ponta |
| Arquivos duplicados | Busca por SHA-256, seleção e proteção de uma cópia por grupo | Escalabilidade, outros filtros e exclusão validada ponta a ponta |
| Mapa do disco | Análise por pasta, tamanhos e gráfico | Mapa recursivo interativo |
| Benchmark | Medições de CPU, cópia de memória e disco, histórico, cancelamento | Equivalência com a fórmula do aplicativo de referência |
| Periféricos | Testes interativos de teclado e mouse, teste de pixels e geração de tom estéreo | Microfone, webcam, controle e testes de precisão/compatibilidade |
| Reparar o Windows | Verificações DISM CheckHealth e SFC verifyonly acionadas manualmente, com relatório e cancelamento | Fluxo de reparo seguro, rollback e integração de resultados |
| Otimizações | Consulta de serviços e atalhos para ajustes oficiais do Windows | Otimizações reversíveis e recomendações verificadas |

As demais áreas (incluindo pós-formatação, desinstalador avançado, backup e central desfazer) **ainda são telas ou fluxos parciais**, não funções completas.

O indicador do Painel usa exclusivamente memória e espaço livre e **não equivale** a um diagnóstico profundo do PC. O benchmark produz métricas próprias, **não uma pontuação comparável ao HUB Pro**.

## Compilar no NEXUS

Código local: `E:\StorHUB\StorHUB-src\`.

~~~powershell
cd E:\StorHUB\StorHUB-src
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\dist\StorHUB.exe
~~~

O script utiliza o compilador C# do .NET Framework 4.8 já presente no Windows. O executável compilado é mantido em `dist/` e **não é versionado no GitHub**.

## Testes

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\smoke.ps1
~~~

Em **08/11/2026, 30 testes automatizados passaram no NEXUS**, incluindo abertura de 18 telas, consulta de dispositivos, tarefas de benchmark, cancelamento e integridade de comandos Windows. As verificações de SFC/DISM foram **inspecionadas, não executadas** durante a suíte. Os testes não comprovam qualidade visual pixel por pixel, limpeza real, instalação de programas ou equivalência integral com o aplicativo de referência.

Para medir diferenças reais entre imagens de **mesmo tamanho, DPI e estado**:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\compare-pixels.ps1 -Reference E:\StorHUB\referencias\painel.png -Actual C:\caminho\captura-storhub.png -Heatmap C:\caminho\diff.png
~~~

Veja o [plano de fidelidade visual](docs/fidelidade-visual.md).

## Ghidra + REA disponíveis globalmente no NEXUS

As ferramentas foram instaladas em `C:\Tools\ReverseEngineering` e adicionadas ao **PATH de todos os usuários**:

| Ferramenta | Versão | Comando |
|---|---|---|
| Ghidra | 12.1.4 | `ghidra` |
| Ghidra Headless | 12.1.4 | `ghidra-headless` |
| REA | 6.8.0 | `rea` |

Também há um atalho **Ghidra 12.1.4** no Menu Iniciar de todos os usuários. Use um novo terminal para carregar o PATH. Leia a [configuração global](docs/ferramentas-globais-nexus.md) e a [análise do executável original](docs/analise-binaria.md).

Os originais `MestresHub.exe`, `HUB-Pro-Setup-0.3.23.0.exe` e o ZIP de referência em `E:\StorHUB` não foram modificados.

## Segurança e autoria

As verificações de integridade, benchmarks e varreduras não iniciam automaticamente ao abrir o programa. Operações como envio de arquivos à Lixeira e criação de pontos de restauração exigem ação explícita e confirmação. **Não utilize os módulos ainda não validados com arquivos importantes.**

HUB Pro é de **Mestres da Informática**. StorHUB é uma implementação independente e não afiliada. As capturas e binários do projeto original não são redistribuídos aqui.
