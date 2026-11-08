using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;

namespace StorHUB {
    public partial class StorHubForm {
        private class AppEntry {
            public string Name, Version, Publisher;
        }
        private class StartEntry {
            public string Name, Location, Command;
        }
        private class TempEntry {
            public string Path;
            public long Length;
            public DateTime Modified;
        }
        private List<TempEntry> tempEntries = new List<TempEntry>();
        private string tempRoot = "";

        private bool TryShowFunctionalModule(string section) {
            switch (section) {
                case "Programas": ShowPrograms(); return true;
                case "Drivers": ShowDrivers(); return true;
                case "Periféricos": ShowPeripherals(); return true;
                case "Benchmark": ShowBenchmark(); return true;
                case "Arquivos duplicados": ShowDuplicates(); return true;
                case "Mapa do disco": ShowDiskMap(); return true;
                case "Inicialização": ShowStartup(); return true;
                case "Rede": ShowNetwork(); return true;
                case "Windows 11": ShowWindows11(); return true;
                case "Pontos de restauração": ShowRestorePoints(); return true;
                case "Limpeza": ShowCleanup(); return true;
                case "Bateria": ShowBattery(); return true;
                case "Saúde dos discos": ShowDriveDetails(); return true;
                default: return false;
            }
        }

        private Panel ModuleCard(string title, string subtitle, int height = 595) {
            int width = ViewWidth;
            AddText(workspace, title, 20, 18, width - 30, 54, 21, FontStyle.Bold);
            AddText(workspace, subtitle, 22, 70, width - 35, 40, 9, FontStyle.Regular, Muted);
            return MakeCard(20, 124, width, height);
        }

        private ListView NewTable(Control panel, int top, params string[] columns) {
            ListView lv = new ListView {
                Left = 18, Top = top, Width = Math.Max(380, panel.Width - 36),
                Height = Math.Max(170, panel.Height - top - 20),
                View = View.Details, FullRowSelect = true, HideSelection = false,
                MultiSelect = false, GridLines = true, BackColor = Color.FromArgb(19, 25, 43),
                ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle, Font = bodyFont
            };
            int available = Math.Max(300, lv.Width - 30);
            for (int i = 0; i < columns.Length; i++) {
                int colWidth = available / columns.Length;
                if (i == 0 && columns.Length > 2) colWidth = (int)(available * .42);
                else if (columns.Length > 2) colWidth = (int)(available * .58 / (columns.Length - 1));
                lv.Columns.Add(columns[i], colWidth);
            }
            panel.Controls.Add(lv);
            return lv;
        }

        private void PutRow(ListView lv, object tag, params string[] columns) {
            if (columns.Length == 0) return;
            ListViewItem row = new ListViewItem(columns[0]);
            for (int i = 1; i < columns.Length; i++) row.SubItems.Add(columns[i]);
            row.Tag = tag;
            lv.Items.Add(row);
        }

        private void ShowPrograms() {
            Panel pane = ModuleCard("Programas", "Programas realmente instalados no Windows • leitura do registro, sem alterações", 605);
            AddText(pane, "Instalados", 17, 12, 175, 37, 14, FontStyle.Bold);
            TextBox filter = new TextBox {
                Bounds = new Rectangle(190, 17, Math.Max(210, pane.Width - 370), 28),
                BackColor = Bg, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle,
                Font = semibold
            };
            pane.Controls.Add(filter);
            AddText(pane, "Buscar", 190, 46, 160, 21, 8, FontStyle.Regular, Muted);
            ListView table = NewTable(pane, 125, "Nome", "Versão", "Fabricante");
            Label status = AddText(pane, "Consultando programas...", 18, 79, pane.Width - 45, 31, 9, FontStyle.Regular, Muted);
            List<AppEntry> entries = GetInstalledPrograms();
            Action update = delegate {
                table.BeginUpdate();
                table.Items.Clear();
                string needle = filter.Text.Trim();
                int count = 0;
                foreach (AppEntry app in entries) {
                    if (needle.Length > 0 && app.Name.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) < 0 &&
                        (app.Publisher ?? "").IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                    PutRow(table, app, app.Name, app.Version, app.Publisher);
                    count++;
                }
                table.EndUpdate();
                status.Text = count + " programa(s) exibido(s) de " + entries.Count + " registrado(s).";
            };
            filter.TextChanged += delegate { update(); };
            update();
            AddButton(pane, "Aplicativos do Windows", Math.Max(10, pane.Width - 200), 16, 180,
                delegate { OpenWindowsSettings("ms-settings:appsfeatures"); }, false);
        }

        private List<AppEntry> GetInstalledPrograms() {
            List<AppEntry> apps = new List<AppEntry>();
            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string sub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser }) {
                foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 }) {
                    try {
                        using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                        using (RegistryKey list = root.OpenSubKey(sub)) {
                            if (list == null) continue;
                            foreach (string id in list.GetSubKeyNames()) {
                                try {
                                    using (RegistryKey item = list.OpenSubKey(id)) {
                                        if (item == null) continue;
                                        string name = Convert.ToString(item.GetValue("DisplayName", "")).Trim();
                                        if (name.Length == 0 || Convert.ToString(item.GetValue("SystemComponent", "0")) == "1") continue;
                                        string version = Convert.ToString(item.GetValue("DisplayVersion", ""));
                                        string publisher = Convert.ToString(item.GetValue("Publisher", ""));
                                        if (unique.Add(name + "|" + version + "|" + publisher)) apps.Add(new AppEntry {
                                            Name = name, Version = version, Publisher = publisher
                                        });
                                    }
                                } catch (Exception) { /* key access denied; skip only this item */ }
                            }
                        }
                    } catch (Exception) { /* some registry hives may not be accessible */ }
                }
            }
            apps.Sort((a,b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return apps;
        }

        private void ShowStartup() {
            Panel pane = ModuleCard("Inicialização", "Programas iniciados com o Windows • inventário real, somente leitura", 605);
            AddText(pane, "Entradas de inicialização", 17, 14, pane.Width - 30, 41, 14, FontStyle.Bold);
            List<StartEntry> entries = ReadStartup();
            AddText(pane, entries.Count + " entrada(s) detectada(s) no Registro e nas pastas de Inicialização.", 18, 61,
                pane.Width - 35, 30, 9, FontStyle.Regular, Muted);
            AddButton(pane, "Abrir Gerenciador de Tarefas", 18, 94, 265,
                delegate {
                    try { Process.Start("taskmgr.exe"); }
                    catch (Exception ex) { ShowError(ex.Message); }
                }, false);
            ListView table = NewTable(pane, 152, "Programa", "Origem", "Comando / atalho");
            foreach (StartEntry e in entries) PutRow(table, e, e.Name, e.Location, e.Command);
            table.DoubleClick += delegate {
                if (table.SelectedItems.Count == 0) return;
                StartEntry e = table.SelectedItems[0].Tag as StartEntry;
                if (e != null) MessageBox.Show(e.Command, e.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        private List<StartEntry> ReadStartup() {
            List<StartEntry> data = new List<StartEntry>();
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine }) {
                foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 }) {
                    foreach (string path in new [] {
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
                    }) {
                        try {
                            using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                            using (RegistryKey key = root.OpenSubKey(path)) {
                                if (key == null) continue;
                                foreach (string name in key.GetValueNames()) {
                                    string value = Convert.ToString(key.GetValue(name, ""));
                                    if (data.Any(x => x.Name == name && x.Command == value && x.Location.StartsWith(hive.ToString()))) continue;
                                    data.Add(new StartEntry {
                                        Name = name, Location = hive + "\\" + (path.EndsWith("RunOnce") ? "RunOnce" : "Run"),
                                        Command = value
                                    });
                                }
                            }
                        } catch (Exception) { }
                    }
                }
            }
            foreach (Environment.SpecialFolder folder in new[] {
                Environment.SpecialFolder.Startup, Environment.SpecialFolder.CommonStartup
            }) {
                try {
                    string path = Environment.GetFolderPath(folder);
                    if (string.IsNullOrEmpty(path)) continue;
                    foreach (string file in Directory.GetFiles(path)) {
                        data.Add(new StartEntry {
                            Name = Path.GetFileNameWithoutExtension(file),
                            Location = folder.ToString(), Command = file
                        });
                    }
                } catch (Exception) { }
            }
            return data.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private void ShowNetwork() {
            Panel pane = ModuleCard("Rede", "Adaptadores, endereços e teste de conectividade • dados atuais do Windows", 605);
            AddText(pane, "Interfaces de rede ativas", 18, 12, pane.Width - 30, 40, 14, FontStyle.Bold);
            Label result = AddText(pane, "Teste de ping sob demanda: nenhuma conexão será testada automaticamente.",
                19, 54, pane.Width - 42, 39, 9, FontStyle.Regular, Muted);
            AddButton(pane, "Testar internet (ping)", 18, 92, 190,
                async delegate {
                    result.Text = "Testando 1.1.1.1...";
                    try {
                        PingReply reply = await Task.Run(() => {
                            using (Ping ping = new Ping()) return ping.Send("1.1.1.1", 2400);
                        });
                        if (!IsDisposed && !pane.IsDisposed)
                            result.Text = reply.Status == IPStatus.Success
                                ? "Internet respondeu em " + reply.RoundtripTime + " ms (1.1.1.1)."
                                : "Ping sem resposta: " + reply.Status + ". Redes podem bloquear ICMP.";
                    } catch (Exception ex) { if (!IsDisposed && !pane.IsDisposed) result.Text = "Teste indisponível: " + ex.Message; }
                });
            ListView table = NewTable(pane, 155, "Adaptador", "IP (IPv4)", "Gateway / DNS");
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces()) {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                try {
                    IPInterfaceProperties p = nic.GetIPProperties();
                    string ip = string.Join(", ", p.UnicastAddresses
                        .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(x => x.Address.ToString()).ToArray());
                    string gateways = string.Join(", ", p.GatewayAddresses.Select(x => x.Address.ToString()).ToArray());
                    string dns = string.Join(", ", p.DnsAddresses.Select(x => x.ToString()).ToArray());
                    PutRow(table, nic, nic.Name, ip, "Gateway: " + gateways + " | DNS: " + dns);
                } catch (Exception) { PutRow(table, nic, nic.Name, "Indisponível", "-"); }
            }
        }

        private void ShowWindows11() {
            Panel pane = ModuleCard("Windows 11", "Verificações locais de requisitos • compatibilidade completa exige validação da Microsoft", 535);
            AddText(pane, "Requisitos de hardware", 17, 14, pane.Width - 32, 45, 15, FontStyle.Bold);
            bool ramOk = snapshot.TotalRam >= 4UL * 1024 * 1024 * 1024;
            bool driveOk = snapshot.DiskTotal >= 64L * 1024 * 1024 * 1024;
            AddCheckLine(pane, 0, "Memória RAM mínima de 4 GB",
                snapshot.TotalRam == 0 ? "Não foi possível verificar" : FormatBytes(snapshot.TotalRam),
                snapshot.TotalRam == 0 ? (bool?)null : ramOk);
            AddCheckLine(pane, 1, "Armazenamento mínimo de 64 GB",
                snapshot.DiskTotal == 0 ? "Não foi possível verificar" : FormatBytes(snapshot.DiskTotal),
                snapshot.DiskTotal == 0 ? (bool?)null : driveOk);
            object secureBoot = null;
            try { secureBoot = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot\State",
                                                "UEFISecureBootEnabled", null); } catch { }
            bool? boot = secureBoot == null ? (bool?)null : Convert.ToInt32(secureBoot) == 1;
            AddCheckLine(pane, 2, "Inicialização Segura (Secure Boot)",
                boot == null ? "Status não disponível" : (boot.Value ? "Ativada" : "Desativada"), boot);
            bool? tpm = null; string tpmMessage = "Não foi possível verificar";
            try {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    @"root\CIMV2\Security\MicrosoftTpm", "SELECT * FROM Win32_Tpm")) {
                    foreach (ManagementObject item in searcher.Get()) {
                        string specs = Convert.ToString(item["SpecVersion"]);
                        tpm = specs.TrimStart().StartsWith("2.0", StringComparison.Ordinal);
                        tpmMessage = "Versão informada: " + specs;
                        break;
                    }
                }
            } catch { }
            AddCheckLine(pane, 3, "TPM (versão 2.0 exigida)",
                tpmMessage, tpm);
            AddCheckLine(pane, 4, "CPU compatível com a lista oficial",
                "Não implementado: exige consulta à lista oficial de processadores", null);
            AddCheckLine(pane, 5, "DirectX 12 e driver WDDM 2.0",
                "Não implementado: requer avaliação específica da GPU", null);
            AddText(pane, "As verificações acima são parciais. A presença do TPM não comprova TPM 2.0.",
                21, 432, pane.Width - 50, 43, 9, FontStyle.Regular, Muted);
            AddButton(pane, "Abrir informações do Windows", 20, 483, 270,
                delegate { OpenWindowsSettings("ms-settings:about"); }, false);
        }

        private void AddCheckLine(Control pane, int index, string label, string detail, bool? ok) {
            int y = 67 + 61 * index;
            string prefix = ok == null ? "?" : (ok.Value ? "✓" : "!");
            Color status = ok == null ? Muted : (ok.Value ? Success : Danger);
            AddText(pane, prefix, 20, y, 30, 39, 18, FontStyle.Bold, status);
            AddText(pane, label, 53, y, Math.Max(100, pane.Width - 75), 22, 10, FontStyle.Bold);
            AddText(pane, detail, 54, y + 22, Math.Max(100, pane.Width - 75), 27,
                8, FontStyle.Regular, Muted);
        }

        private void ShowRestorePoints() {
            Panel pane = ModuleCard("Pontos de restauração",
                "Histórico do Windows • criar um ponto exige Proteção do Sistema habilitada", 605);
            AddText(pane, "Pontos disponíveis", 17, 12, pane.Width - 30, 41, 14, FontStyle.Bold);
            ListView table = NewTable(pane, 137, "Descrição", "Criado em", "Identificador");
            Label status = AddText(pane, "Consultando histórico...", 17, 54, pane.Width - 30, 30, 9, FontStyle.Regular, Muted);
            Action refresh = delegate {
                table.Items.Clear();
                int count = 0;
                try {
                    using (ManagementObjectSearcher query = new ManagementObjectSearcher(
                        @"root\default", "SELECT * FROM SystemRestore")) {
                        foreach (ManagementObject item in query.Get()) {
                            string name = Convert.ToString(item["Description"]);
                            string date = Convert.ToString(item["CreationTime"]);
                            try { date = ManagementDateTimeConverter.ToDateTime(date).ToString("dd/MM/yyyy HH:mm"); } catch { }
                            PutRow(table, item, name, date, Convert.ToString(item["SequenceNumber"]));
                            count++;
                        }
                    }
                    status.Text = count + " ponto(s) encontrado(s).";
                } catch (Exception ex) { status.Text = "Não foi possível ler: " + ex.Message; }
            };
            AddButton(pane, "Atualizar", 18, 89, 110, delegate { refresh(); }, false);
            AddButton(pane, "Criar ponto de restauração", 141, 89, 235, delegate {
                if (MessageBox.Show("Criar um ponto de restauração do Windows agora?\n\n" +
                    "O recurso precisa estar habilitado e pode exigir elevação.",
                    "StorHUB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try {
                    using (ManagementClass mc = new ManagementClass(@"root\default", "SystemRestore", null)) {
                        ManagementBaseObject args = mc.GetMethodParameters("CreateRestorePoint");
                        args["Description"] = "StorHUB - ponto manual";
                        args["RestorePointType"] = 0;
                        args["EventType"] = 100;
                        ManagementBaseObject ret = mc.InvokeMethod("CreateRestorePoint", args, null);
                        uint code = Convert.ToUInt32(ret["ReturnValue"]);
                        MessageBox.Show(code == 0 ? "Solicitação de ponto de restauração concluída."
                            : "Windows retornou o código " + code + ". Verifique a Proteção do Sistema.",
                            "StorHUB");
                        refresh();
                    }
                } catch (Exception ex) { ShowError("Falha ao criar ponto: " + ex.Message); }
            });
            refresh();
        }

        private void ShowBattery() {
            Panel pane = ModuleCard("Bateria", "Informações fornecidas pelo Windows • somente leitura", 490);
            AddText(pane, "Baterias detectadas", 18, 18, pane.Width - 45, 47, 14, FontStyle.Bold);
            int count = 0;
            try {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT * FROM Win32_Battery")) {
                    foreach (ManagementObject o in s.Get()) {
                        AddCheckLine(pane, count, Convert.ToString(o["Name"]),
                            "Carga restante: " + Convert.ToString(o["EstimatedChargeRemaining"]) + "%" +
                            " • Estado: " + Convert.ToString(o["BatteryStatus"]), null);
                        count++;
                    }
                }
            } catch (Exception ex) {
                AddText(pane, "Não foi possível consultar: " + ex.Message,
                    20, 84, pane.Width - 40, 45, 9, FontStyle.Regular, Muted);
            }
            if (count == 0) AddText(pane, "Nenhuma bateria identificada (normal em desktops).",
                20, 96, pane.Width - 40, 43, 10, FontStyle.Regular, Muted);
            AddButton(pane, "Configurações de energia", 20, 395, 235,
                delegate { OpenWindowsSettings("ms-settings:powersleep"); }, false);
        }

        private void ShowDriveDetails() {
            Panel pane = ModuleCard("Saúde dos discos",
                "Volumes e espaço disponível • estado S.M.A.R.T. ainda não foi validado", 605);
            AddText(pane, "Discos e volumes", 17, 12, pane.Width - 30, 44, 14, FontStyle.Bold);
            ListView table = NewTable(pane, 126, "Unidade", "Formato", "Disponível / Total");
            foreach (DriveInfo drive in DriveInfo.GetDrives()) {
                try {
                    if (!drive.IsReady) continue;
                    PutRow(table, drive, drive.Name, drive.DriveFormat,
                        FormatBytes(drive.AvailableFreeSpace) + " / " + FormatBytes(drive.TotalSize));
                } catch { }
            }
            AddText(pane, "Não confunda espaço livre com desgaste, temperatura ou condição S.M.A.R.T.",
                18, 65, pane.Width - 30, 40, 9, FontStyle.Regular, Muted);
        }

        private void ShowCleanup() {
            Panel pane = ModuleCard("Limpeza",
                "Somente pasta TEMP do usuário • arquivos antigos (>48h) • envio para a Lixeira com confirmação", 605);
            Label status = AddText(pane,
                "A análise não remove nada. Confira os arquivos antes de selecionar e enviar à Lixeira.",
                18, 15, pane.Width - 36, 37, 9, FontStyle.Regular, Muted);
            ListView table = NewTable(pane, 135, "Arquivo temporário", "Tamanho", "Modificado");
            table.CheckBoxes = true;
            Button analyze = AddButton(pane, "Analisar TEMP", 18, 70, 145, null);
            Button select = AddButton(pane, "Marcar candidatos", 176, 70, 173, delegate {
                foreach (ListViewItem item in table.Items) item.Checked = true;
            }, false);
            Button recycle = AddButton(pane, "Enviar seleção à Lixeira", 362, 70, 240, null, false);
            select.Enabled = false; recycle.Enabled = false;
            analyze.Click += async delegate {
                analyze.Enabled = false;
                status.Text = "Analisando arquivos temporários...";
                tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                           + Path.DirectorySeparatorChar;
                try {
                    List<TempEntry> found = await Task.Run(() => EnumerateOldTempFiles(tempRoot));
                    if (pane.IsDisposed || IsDisposed) return;
                    tempEntries = found;
                    table.BeginUpdate();
                    table.Items.Clear();
                    foreach (TempEntry item in found)
                        PutRow(table, item, item.Path.Substring(tempRoot.Length),
                            FormatBytes(item.Length), item.Modified.ToString("dd/MM/yyyy HH:mm"));
                    table.EndUpdate();
                    status.Text = found.Count + " arquivo(s) temporário(s) candidatos — " +
                                  FormatBytes(found.Sum(x => (double)x.Length)) +
                                  ". Limite de 600 arquivos por análise.";
                    select.Enabled = found.Count > 0;
                    recycle.Enabled = found.Count > 0;
                } catch (Exception ex) { if (!pane.IsDisposed) status.Text = "Erro na análise: " + ex.Message; }
                finally { if (!pane.IsDisposed) analyze.Enabled = true; }
            };
            recycle.Click += delegate {
                List<TempEntry> selected = table.CheckedItems.Cast<ListViewItem>()
                    .Select(x => x.Tag as TempEntry).Where(x => x != null).ToList();
                if (selected.Count == 0) { MessageBox.Show("Marque pelo menos um arquivo.", "StorHUB"); return; }
                if (MessageBox.Show("Enviar " + selected.Count +
                    " arquivo(s) da pasta TEMP para a Lixeira?\nArquivos em uso ou protegidos serão ignorados.",
                    "Confirmar limpeza", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                int ok = 0, skipped = 0;
                foreach (TempEntry entry in selected) {
                    try {
                        string full = Path.GetFullPath(entry.Path);
                        if (!full.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                        FileInfo fi = new FileInfo(full);
                        if (!fi.Exists || (fi.Attributes & FileAttributes.ReparsePoint) != 0 ||
                            fi.LastWriteTime >= DateTime.Now.AddHours(-48)) { skipped++; continue; }
                        FileSystem.DeleteFile(full, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        ok++;
                    } catch (Exception) { skipped++; }
                }
                status.Text = ok + " enviado(s) à Lixeira; " + skipped + " ignorado(s). Analise novamente para atualizar.";
                table.Items.Clear(); select.Enabled = false; recycle.Enabled = false;
            };
        }

        private static List<TempEntry> EnumerateOldTempFiles(string root) {
            List<TempEntry> files = new List<TempEntry>();
            if (!Directory.Exists(root)) return files;
            Stack<string> stack = new Stack<string>();
            stack.Push(root);
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DateTime cutoff = DateTime.Now.AddHours(-48);
            while (stack.Count > 0 && files.Count < 600 && visited.Count < 1500) {
                string dir = stack.Pop();
                if (!visited.Add(dir)) continue;
                try {
                    if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                    foreach (string file in Directory.EnumerateFiles(dir)) {
                        if (files.Count >= 600) break;
                        try {
                            FileInfo f = new FileInfo(file);
                            if (!f.Exists || (f.Attributes & FileAttributes.ReparsePoint) != 0 ||
                                f.LastWriteTime >= cutoff) continue;
                            files.Add(new TempEntry { Path = f.FullName, Length = f.Length, Modified = f.LastWriteTime });
                        } catch { }
                    }
                    foreach (string sub in Directory.EnumerateDirectories(dir)) {
                        try {
                            if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) stack.Push(sub);
                        } catch { }
                    }
                } catch (Exception) { }
            }
            return files.OrderByDescending(x => x.Length).ToList();
        }
    }
}
