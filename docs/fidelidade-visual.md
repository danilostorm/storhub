# Plano de fidelidade visual — StorHUB

## Meta

Recriar a interface Windows do projeto de referência, tela a tela, em componentes próprios, mantendo o nome **StorHUB**, sem redistribuir os executáveis ou o código fechado do aplicativo de referência.

## Referências verificadas no NEXUS

No computador \`NEXUSLITE-PC\`, a pasta \`E:\StorHUB\` contém o arquivo \`HUB-Pro-0.3.23.zip\`, o instalador e o executável portátil da versão 0.3.23. Os três arquivos foram preservados sem alterações.

O ZIP contém **23 capturas de tela**, todas em **1280 × 820 pixels**. Foram extraídas para \`E:\StorHUB\referencias\` e permanecem apenas como material local para comparação (não são distribuídas neste repositório).

As telas são:

- Painel, pós-formatação, programas, meu PC.
- Saúde dos discos, bateria, Windows 11, drivers e rede.
- Benchmark e periféricos.
- Limpeza, mapa do disco, arquivos duplicados, otimizações, desinstalador e inicialização.
- Reparar Windows, pontos de restauração, central desfazer e backup para formatar.
- Sobre e teste de pendrive.

## Medidas e cores iniciais

A partir de amostras da imagem original de \`painel.png\`:

| Posição da amostra | Cor da imagem de referência |
|---|---|
| Barra lateral (15, 0) | \`#0E1422\` |
| Fundo do conteúdo (260, 260) | \`#0B0F19\` |
| Cartão principal (270, 60) | \`#172139\` |
| Cartão principal (500, 370) | \`#151E31\` |
| Cartão principal (1210, 510) | \`#1C1838\` |
| Cartões secundários (310, 540) | \`#141B2B\` |

A interface tem uma região lateral fixa de cerca de 225–230 px, painel principal começando por volta de x=260 px e componentes com degradê escuro. Essas são **medições de amostras**, não uma especificação pronta nem garantia de correspondência pixel a pixel.

## Critérios para cada tela

1. Criar seus controles e estados com código próprio.
2. Comparar a captura nova com a original em **1280×820**, com o mesmo DPI (100%), idioma e fontes.
3. Ajustar geometria, cores, gradientes, ícones, tipografia, textos, bordas, animações, foco e estados de clique.
4. Registrar visualmente o resultado após cada rodada, com comparação das diferenças por pixel.
5. Exercitar as ações reais e distinguir claramente funções implementadas de telas ainda ilustrativas.

## Ferramentas

Ghidra e REA podem ser adicionados ao NEXUS caso se mostre necessária a análise técnica **do comportamento** ou dos recursos do executável. A **reprodução visual** começa pelas capturas: não precisa de descompilação. Descompiladores não produzem o código-fonte original de forma garantida.

## Situação

O primeiro painel em WinForms e os dados básicos do computador já compilam e abrem no NEXUS. **Não há ainda equivalência pixel a pixel**, nem implementação funcional de todos os módulos.
