# Ferramentas de engenharia reversa do NEXUS

**Configuração verificada no NEXUSLITE-PC (Windows 11 Pro), em 08/11/2026.**

As ferramentas estão disponíveis para **todos os usuários locais** do Windows por meio do \`PATH\` da máquina, não apenas pela conta \`Administrator\`. Os executáveis originais do HUB-Pro não foram modificados.

## Instalação compartilhada

| Ferramenta | Versão | Local |
|---|---|---|
| Ghidra | 12.1.4 | \`C:\Tools\ReverseEngineering\ghidra\ghidra_12.1.4_PUBLIC\` |
| REA (rea-agents) | 6.8.0 | \`C:\Tools\ReverseEngineering\rea-global\` |
| JDK Temurin | 21.0.13.9 | \`C:\Program Files\Eclipse Adoptium\jdk-21.0.13.9-hotspot\` |
| Comandos globais | — | \`C:\Tools\ReverseEngineering\bin\` |

A pasta \`bin\` possui os arquivos \`ghidra.cmd\`, \`ghidra-headless.cmd\` e \`rea.cmd\`. Eles configuram as variáveis de ambiente necessárias e iniciam os programas sem depender do perfil de quem estiver logado.

Variáveis de ambiente no escopo **Machine**:
- \`GHIDRA_INSTALL_DIR=C:\Tools\ReverseEngineering\ghidra\ghidra_12.1.4_PUBLIC\`
- \`JAVA_HOME=C:\Program Files\Eclipse Adoptium\jdk-21.0.13.9-hotspot\`
- O caminho \`C:\Tools\ReverseEngineering\bin\` foi adicionado ao \`PATH\` da máquina.

**Segurança:** a pasta compartilhada tem permissões NTFS para \`Administrators\` e \`SYSTEM\` (controle total) e \`Users\` (leitura e execução). A permissão de escrita herdada pelo grupo \`Authenticated Users\` foi removida para impedir que usuários comuns substituam os executáveis ou scripts globais.

## Usando em um novo terminal

~~~powershell
ghidra
ghidra-headless -help
rea --version
rea doctor --provider ghidra
~~~

- \`ghidra\` abre a interface gráfica; utilize-o em uma sessão Windows interativa.
- \`ghidra-headless\` executa análises por linha de comando.
- \`rea\` oferece CLI de análise e utiliza o Ghidra como provedor quando configurado.

**Verificações realizadas:** as três entradas \`ghidra\`, \`ghidra-headless\` e \`rea\` foram localizadas pelo Windows; o REA 6.8.0 reconheceu o provedor Ghidra 12.1.4; e \`ghidra-headless -help\` retornou a ajuda do analisador. O comando de ajuda retorna código 1 por falta de argumentos de projeto, mas sua inicialização foi confirmada.

Ajustes ao \`PATH\` normalmente só são reconhecidos por terminais **abertos após** a mudança. Não é necessário reiniciar todo o Windows.

## Integridade e backups

O ZIP oficial do Ghidra teve seu SHA-256 conferido antes da instalação: \`ddac49f903da9d5bac833e5cc79395098b9c33cfd3279be5f31bd00387d2d4db\`.

A instalação inicial sob \`E:\StorHUB\tools\ghidra_12.1.4_PUBLIC\` e a instalação anterior do REA no perfil do administrador foram preservadas. Elas não são necessárias para os comandos globais, mas não foram removidas.

Este documento descreve a **máquina NEXUS**; não faz parte de um instalador que altere outros servidores ou computadores.
