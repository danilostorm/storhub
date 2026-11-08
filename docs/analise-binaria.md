# Análise estática do HUB-Pro para orientar o StorHUB

## Escopo

O arquivo original foi consultado em \`E:\StorHUB\MestresHub.exe\`, sem modificá-lo e sem republicá-lo.

- Tamanho do executável: **73.382.879 bytes**
- SHA-256: \`049b4d811ec3f3a3401c4db180a9190737f25f5bf3045ad360e254c64def1ac1\`
- Formato: **PE32+ / AMD64 (x86-64)**.
- Segundo o REA, não contém diretório **.NET CLI**: portanto, não é diretamente recuperável como um projeto C# gerenciado.
- O Ghidra carregou a imagem na base **0x140000000**, com código executável na seção \`.text\` e recursos \`.rsrc\`.
- A tabela de importação estática contém 17 bibliotecas, entre elas KERNEL32, ADVAPI32, OLE32, OLEAUT32, USER32, SHELL32 e bibliotecas CRT.
- O REA conseguiu inspecionar recursos PE, inclusive ícones e itens RCDATA. **Isso não equivale a extrair todo o programa ou conhecer todas as funções da interface.**

## Ferramentas

- **REA 6.8.0**: identificação de formato, metadados PE e recursos.
- **Ghidra 12.1.4**: importação da imagem e leitura das seções e bibliotecas.
- **Temurin JDK 21**: execução do Ghidra.

O arquivo de projeto de análise local está em \`E:\StorHUB\tools\ghidra_projects\StorHUB-Original\`. O relatório inicial de seções está em \`E:\StorHUB\tools\ghidra-findings.txt\`.

## Implicações para o StorHUB

1. As **23 capturas oficiais de 1280 × 820** disponíveis no NEXUS fornecem uma referência para comparar a interface.
2. Os módulos reimplementados precisam de **testes de entrada, saída, cancelamento, falhas e efeitos no Windows**, não só de comparação visual.
3. O comparador \`tests/compare-pixels.ps1\` mede a correspondência exata de pixels entre capturas de **mesmo tamanho e DPI**. Um relatório de similaridade só é válido com estado, escala e conteúdo equivalentes.
4. A análise estática ajuda a investigar recursos e lógica de baixo nível, mas **não demonstra por si só equivalência de 100%**.

Créditos: o HUB Pro original é de **Mestres da Informática**. O StorHUB é uma implementação independente; não é uma redistribuição dos binários originais.
