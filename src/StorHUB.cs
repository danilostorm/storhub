using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows.Forms;

namespace StorHUB {
    internal static class Program {
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new StorHubForm());
        }
    }

    public class StorHubForm : Form {
        private static readonly Color Bg = Color.FromArgb(15, 20, 34);
        private static readonly Color Side = Color.FromArgb(12, 17, 29);
        private static readonly Color Card = Color.FromArgb(25, 31, 51);
        private static readonly Color Border = Color.FromArgb(39, 48, 71);
        private static readonly Color Primary = Color.FromArgb(58, 121, 247);
        private static readonly Color TextMain = Color.FromArgb(236, 242, 255);
        private static readonly Color Muted = Color.FromArgb(155, 168, 194);
        private static readonly Color Danger = Color.FromArgb(242, 113, 128);
        private static readonly Color Success = Color.FromArgb(89, 194, 155);

        private readonly Panel sidebar = new Panel();
        private readonly Panel workspace = new Panel();
        private readonly Dictionary<string, Button> navigation = new Dictionary<string, Button>();
        private string active = "Painel";
        private bool hasAnalysis;
        private int healthScore;
        private readonly Font titleFont = new Font("Segoe UI", 19, FontStyle.Bold);
        private readonly Font bodyFont = new Font("Segoe UI", 9);
        private readonly Font smallFont = new Font("Segoe UI", 8);
        private readonly Font semibold = new Font("Segoe UI Semibold", 10, FontStyle.Regular);

        private class SystemSnapshot {
            public string Os = "Indisponível";
            public string Cpu = "Indisponível";
            public string Gpu = "Indisponível";
            public string Computer = Environment.MachineName;
            public ulong TotalRam;
            public ulong FreeRam;
            public long DiskTotal;
            public long DiskFree;
            public string DiskName = "C:";
            public int DiskCount;
            public int NetworkCount;
            public int BatteryCount;
        }

        private SystemSnapshot snapshot = new SystemSnapshot();

        public StorHubForm() {
            Text = "StorHUB — Central do PC";
            Width = 1280;
            Height = 820;
            MinimumSize = new Size(980, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = TextMain;
            Font = bodyFont;
            DoubleBuffered = true;
            InitializeLayout();
            Shown += delegate { RefreshSnapshot(); ShowPage("Painel"); };
            Resize += delegate {
                if (WindowState != FormWindowState.Minimized) ShowPage(active);
            };
        }

        private void InitializeLayout() {
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = 225;
            sidebar.BackColor = Side;
            Controls.Add(sidebar);

            Panel brand = new Panel { Dock = DockStyle.Top, Height = 85, BackColor = Side };
            sidebar.Controls.Add(brand);
            Label emblem = MakeLabel("S", 19, FontStyle.Bold, Color.FromArgb(254, 205, 91));
            emblem.Location = new Point(15, 15);
            emblem.Size = new Size(32, 35);
            brand.Controls.Add(emblem);
            Label logo = MakeLabel("StorHUB", 17, FontStyle.Bold, TextMain);
            logo.Location = new Point(48, 14);
            logo.AutoSize = true;
            brand.Controls.Add(logo);
            Label sub = MakeLabel("Sua central de manutenção do PC", 8, FontStyle.Regular, Muted);
            sub.Location = new Point(13, 53);
            sub.AutoSize = true;
            brand.Controls.Add(sub);

            Label foot = MakeLabel("StorHUB 0.1.0-alpha  •  Português (Brasil)", 8, FontStyle.Regular, Muted);
            foot.Dock = DockStyle.Bottom;
            foot.Height = 35;
            foot.Padding = new Padding(12, 6, 2, 2);
            sidebar.Controls.Add(foot);

            FlowLayoutPanel items = new FlowLayoutPanel();
            items.Dock = DockStyle.None;
            items.FlowDirection = FlowDirection.TopDown;
            items.Location = new Point(0, 85);
            items.Size = new Size(218, Math.Max(150, sidebar.ClientSize.Height - 120));
            sidebar.Resize += delegate { items.Height = Math.Max(150, sidebar.ClientSize.Height - 120); };
            items.WrapContents = false;
            items.AutoScroll = true;
            items.Padding = new Padding(7, 3, 5, 15);
            sidebar.Controls.Add(items);
            items.BringToFront();
            sidebar.Controls.SetChildIndex(brand, 0);
            sidebar.Controls.SetChildIndex(foot, 1);

            AddGroup(items, null, new string[] { "Painel", "Pós-formatação", "Programas" });
            AddGroup(items, "MEU PC", new string[] { "Meu PC", "Saúde dos discos", "Bateria", "Windows 11", "Drivers", "Rede" });
            AddGroup(items, "TESTES", new string[] { "Benchmark", "Periféricos" });
            AddGroup(items, "OTIMIZAR", new string[] { "Limpeza", "Mapa do disco", "Arquivos duplicados", "Otimizações", "Desinstalador", "Inicialização" });
            AddGroup(items, "SEGURANÇA", new string[] { "Reparar o Windows", "Pontos de restauração", "Central Desfazer", "Backup para formatar" });
            AddGroup(items, "INFORMAÇÕES", new string[] { "Sobre" });

            workspace.Dock = DockStyle.Fill;
            workspace.BackColor = Bg;
            workspace.AutoScroll = true;
            Controls.Add(workspace);
            workspace.BringToFront();
        }

        private void AddGroup(FlowLayoutPanel items, string group, IEnumerable<string> names) {
            if (group != null) {
                Label name = MakeLabel(group, 8, FontStyle.Regular, Muted);
                name.Size = new Size(187, 23);
                name.Margin = new Padding(8, 12, 0, 0);
                items.Controls.Add(name);
            }
            foreach (string label in names) {
                Button b = new Button {
                    Text = "  " + label, Width = 198, Height = 31,
                    TextAlign = ContentAlignment.MiddleLeft,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Side, ForeColor = Muted,
                    Font = bodyFont, Margin = new Padding(1, 1, 0, 1),
                    Cursor = Cursors.Hand
                };
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = Card;
                b.Click += delegate { ShowPage(label); };
                navigation[label] = b;
                items.Controls.Add(b);
            }
        }

        private Label MakeLabel(string value, float size, FontStyle style, Color color) {
            return new Label {
                Text = value, ForeColor = color,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", size, style),
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private Panel MakeCard(int x, int y, int w, int h) {
            Panel p = new Panel {
                Left = x, Top = y, Width = Math.Max(100, w), Height = h,
                BackColor = Card
            };
            p.Paint += delegate(object o, PaintEventArgs e) {
                using (Pen pen = new Pen(Border)) {
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
                }
            };
            workspace.Controls.Add(p);
            return p;
        }

        private Label AddText(Control parent, string txt, int x, int y, int w, int h,
                              float size = 10, FontStyle style = FontStyle.Regular,
                              Color? color = null) {
            Label l = MakeLabel(txt, size, style, color ?? TextMain);
            l.SetBounds(x, y, w, h);
            parent.Controls.Add(l);
            return l;
        }

        private Button AddButton(Control parent, string text, int x, int y, int width,
                                 EventHandler action, bool filled = true) {
            Button b = new Button {
                Text = text, Width = width, Height = 38, Left = x, Top = y,
                ForeColor = TextMain, BackColor = filled ? Primary : Border,
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                Font = semibold
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += action;
            parent.Controls.Add(b);
            return b;
        }

        private int ViewWidth { get { return Math.Max(760, workspace.ClientSize.Width - 46); } }

        private void ShowPage(string section) {
            if (IsDisposed) return;
            active = section;
            workspace.SuspendLayout();
            workspace.Controls.Clear();
            foreach (KeyValuePair<string, Button> b in navigation) {
                b.Value.BackColor = b.Key == section ? Color.FromArgb(25, 42, 76) : Side;
                b.Value.ForeColor = b.Key == section ? TextMain : Muted;
            }
            if (section == "Painel") ShowDashboard();
            else ShowModule(section);
            workspace.ResumeLayout();
        }

        private void ShowDashboard() {
            int width = ViewWidth;
            Panel hero = MakeCard(18, 18, width, 342);
            Ring ring = new Ring();
            ring.SetBounds(25, 27, 225, 225);
            ring.Score = hasAnalysis ? healthScore : -1;
            hero.Controls.Add(ring);

            int left = 275;
            AddText(hero, hasAnalysis
                ? (healthScore >= 80 ? "Seu PC está em boas condições" : "Seu PC precisa de atenção")
                : "Bem-vindo ao StorHUB", left, 22, width - left - 18, 45, 18, FontStyle.Bold);
            AddText(hero, hasAnalysis
                ? "Verificação rápida somente leitura. Nenhuma alteração foi realizada."
                : "Central de diagnóstico e ferramentas do Windows. Comece com uma análise segura.",
                left, 65, width - left - 18, 43, 9, FontStyle.Regular, Muted);

            List<string> findings = new List<string>();
            if (hasAnalysis) {
                double diskLeft = snapshot.DiskTotal > 0 ? (double)snapshot.DiskFree / snapshot.DiskTotal : 1;
                double ramUsed = snapshot.TotalRam > 0 ? (double)(snapshot.TotalRam - snapshot.FreeRam) / snapshot.TotalRam : 0;
                findings.Add((diskLeft < .15 ? "●  Atenção: pouco espaço na unidade do sistema" : "✓  Espaço do disco do sistema verificado"));
                findings.Add((ramUsed > .85 ? "●  Atenção: uso elevado de memória RAM" : "✓  Memória RAM verificada"));
                findings.Add("✓  Sistema operacional identificado");
                findings.Add("✓  " + snapshot.DiskCount + " unidade(s) de armazenamento detectada(s)");
                findings.Add("✓  " + snapshot.NetworkCount + " interface(s) de rede em atividade");
            } else {
                findings.Add("○  Analisar espaço disponível");
                findings.Add("○  Verificar utilização da memória");
                findings.Add("○  Identificar hardware e Windows");
                findings.Add("○  Listar unidades de armazenamento");
                findings.Add("○  Detectar interfaces de rede");
            }
            for (int i = 0; i < findings.Count; i++) {
                Color c = hasAnalysis && findings[i].StartsWith("●") ? Danger
                        : hasAnalysis ? Success : Muted;
                AddText(hero, findings[i], left, 117 + 31 * i, width - left - 15, 27,
                        9, FontStyle.Regular, c);
            }
            AddButton(hero, hasAnalysis ? "Analisar novamente" : "Iniciar análise", left, 290, 177,
                delegate { RefreshSnapshot(); hasAnalysis = true; healthScore = CalculateScore(); ShowPage("Painel"); });
            AddButton(hero, "Meu PC", left + 190, 290, 104,
                delegate { ShowPage("Meu PC"); }, false);

            int gap = 13;
            int third = (width - 2 * gap) / 3;
            Panel ram = MakeCard(18, 375, third, 89);
            Panel disk = MakeCard(18 + third + gap, 375, third, 89);
            Panel os = MakeCard(18 + 2 * (third + gap), 375, third, 89);
            AddText(ram, "MEMÓRIA RAM", 17, 10, third - 20, 24, 8, FontStyle.Bold, Muted);
            AddText(ram, RamSummary(), 17, 38, third - 23, 36, 11, FontStyle.Bold);
            AddText(disk, "DISCO DO SISTEMA", 17, 10, third - 20, 24, 8, FontStyle.Bold, Muted);
            AddText(disk, DiskSummary(), 17, 38, third - 23, 36, 11, FontStyle.Bold);
            AddText(os, "WINDOWS", 17, 10, third - 20, 24, 8, FontStyle.Bold, Muted);
            AddText(os, snapshot.Os, 17, 38, third - 22, 36, 9, FontStyle.Regular);

            AddText(workspace, "Ferramentas", 19, 479, width - 20, 30, 12, FontStyle.Bold);
            string[] quick = { "Pós-formatação", "Benchmark", "Meu PC" };
            string[] details = { "Preparar o Windows com segurança", "Testes de desempenho (em preparação)", "Informações do seu computador" };
            for (int i = 0; i < 3; i++) {
                int pos = i;
                Panel tile = MakeCard(18 + i * (third + gap), 513, third, 114);
                AddText(tile, quick[i], 18, 19, third - 30, 34, 12, FontStyle.Bold);
                AddText(tile, details[i], 18, 52, third - 30, 48, 9, FontStyle.Regular, Muted);
                tile.Cursor = Cursors.Hand;
                tile.Click += delegate { ShowPage(quick[pos]); };
            }
            AddText(workspace,
                "Versão inicial independente • Diagnóstico somente leitura • Sem execução automática de reparos",
                19, 637, width, 30, 8, FontStyle.Regular, Muted);
        }

        private void ShowModule(string section) {
            int width = ViewWidth;
            AddText(workspace, section, 20, 20, width - 30, 52, 22, FontStyle.Bold);
            string intro = "Módulo em desenvolvimento. Nenhuma alteração no Windows será feita automaticamente.";
            if (section == "Meu PC") intro = "Informações reais do hardware e do Windows (somente leitura).";
            if (section == "Saúde dos discos") intro = "Capacidade e espaço disponível. Diagnóstico S.M.A.R.T. ainda não implementado.";
            if (section == "Rede") intro = "Interfaces de rede ativas. Testes de velocidade ainda não implementados.";
            if (section == "Bateria") intro = "Detecção de bateria pelo Windows.";
            if (section == "Sobre") intro = "StorHUB é uma implementação independente, em estágio inicial.";
            AddText(workspace, intro, 22, 72, width - 40, 35, 10, FontStyle.Regular, Muted);

            Panel info = MakeCard(20, 122, width, 380);
            if (section == "Meu PC") {
                AddInfoRow(info, 0, "Computador", snapshot.Computer);
                AddInfoRow(info, 1, "Sistema", snapshot.Os);
                AddInfoRow(info, 2, "Processador", snapshot.Cpu);
                AddInfoRow(info, 3, "Placa de vídeo", snapshot.Gpu);
                AddInfoRow(info, 4, "Memória RAM", RamSummary());
                AddInfoRow(info, 5, "Unidade do Windows", DiskSummary());
                AddButton(info, "Copiar resumo", 18, 324, 150, delegate {
                    try {
                        Clipboard.SetText("StorHUB - Resumo do PC\nSistema: " + snapshot.Os +
                            "\nCPU: " + snapshot.Cpu + "\nGPU: " + snapshot.Gpu +
                            "\nRAM: " + RamSummary() + "\nDisco: " + DiskSummary());
                        MessageBox.Show("Resumo copiado.", "StorHUB");
                    } catch (Exception ex) { ShowError(ex.Message); }
                });
                AddButton(info, "Atualizar", 180, 324, 115, delegate { RefreshSnapshot(); ShowPage(section); }, false);
            } else if (section == "Saúde dos discos") {
                int index = 0;
                foreach (DriveInfo drive in DriveInfo.GetDrives()) {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                    string value = FormatBytes(drive.AvailableFreeSpace) + " livres de " + FormatBytes(drive.TotalSize);
                    AddInfoRow(info, index++, drive.Name, value);
                    if (index > 5) break;
                }
                AddButton(info, "Atualizar discos", 18, 324, 166,
                    delegate { RefreshSnapshot(); ShowPage(section); }, false);
            } else if (section == "Rede") {
                int index = 0;
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces()) {
                    if (nic.OperationalStatus != OperationalStatus.Up ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    AddInfoRow(info, index++, nic.Name, nic.NetworkInterfaceType.ToString());
                    if (index > 5) break;
                }
                AddButton(info, "Atualizar rede", 18, 324, 145,
                    delegate { RefreshSnapshot(); ShowPage(section); }, false);
            } else if (section == "Bateria") {
                AddInfoRow(info, 0, "Baterias detectadas", snapshot.BatteryCount.ToString());
                AddButton(info, "Configurações de energia", 18, 324, 220,
                    delegate { OpenWindowsSettings("ms-settings:powersleep"); });
            } else if (section == "Sobre") {
                AddInfoRow(info, 0, "Nome", "StorHUB");
                AddInfoRow(info, 1, "Versão", "0.1.0-alpha");
                AddInfoRow(info, 2, "Plataforma", "Windows / .NET Framework");
                AddInfoRow(info, 3, "Origem", "Implementação independente");
                AddText(info, "Não inclui código-fonte, executáveis nem recursos do HUB Pro original.",
                        20, 280, width - 45, 45, 10, FontStyle.Regular, Muted);
            } else {
                AddText(info, "Funcionalidade em preparação", 22, 28, width - 46, 43, 17, FontStyle.Bold);
                AddText(info,
                    "Esta tela existe para estruturar a navegação, mas ainda não executa as funções do programa de referência. " +
                    "O objetivo é implementar cada módulo com verificações, confirmação do usuário e possibilidade de reversão.",
                    22, 78, width - 52, 117, 11, FontStyle.Regular, Muted);
                if (section == "Programas" || section == "Desinstalador") {
                    AddButton(info, "Abrir aplicativos do Windows", 22, 260, 250,
                        delegate { OpenWindowsSettings("ms-settings:appsfeatures"); });
                } else if (section == "Limpeza") {
                    AddButton(info, "Abrir armazenamento do Windows", 22, 260, 275,
                        delegate { OpenWindowsSettings("ms-settings:storagesense"); });
                } else if (section == "Windows 11") {
                    AddButton(info, "Abrir informações do Windows", 22, 260, 280,
                        delegate { OpenWindowsSettings("ms-settings:about"); });
                }
            }
            AddText(workspace, "Módulo independente em desenvolvimento — sem alterações automáticas.",
                    20, 517, width - 20, 35, 9, FontStyle.Regular, Muted);
        }

        private void AddInfoRow(Control parent, int index, string key, string value) {
            int y = 16 + index * 48;
            AddText(parent, key, 18, y, 170, 34, 9, FontStyle.Bold, Muted);
            AddText(parent, value, 195, y, Math.Max(200, parent.Width - 218), 34, 10);
        }

        private static string FormatBytes(double n) {
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            if (n < 1024) return n.ToString("0") + " B";
            int unit = 0;
            while (n >= 1024 && unit < units.Length - 1) { n /= 1024; unit++; }
            return n.ToString("0.0") + " " + units[unit];
        }

        private string RamSummary() {
            if (snapshot.TotalRam == 0) return "Não disponível";
            return FormatBytes(snapshot.TotalRam - snapshot.FreeRam) + " de " + FormatBytes(snapshot.TotalRam);
        }
        private string DiskSummary() {
            if (snapshot.DiskTotal == 0) return "Não disponível";
            return FormatBytes(snapshot.DiskFree) + " livres de " + FormatBytes(snapshot.DiskTotal);
        }

        private static string WmiFirst(string name, string field) {
            try {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT " + field + " FROM " + name)) {
                    foreach (ManagementObject obj in s.Get()) {
                        object v = obj[field];
                        if (v != null) return Convert.ToString(v);
                    }
                }
            } catch { }
            return "Indisponível";
        }

        private void RefreshSnapshot() {
            SystemSnapshot s = new SystemSnapshot();
            s.Os = WmiFirst("Win32_OperatingSystem", "Caption");
            s.Cpu = WmiFirst("Win32_Processor", "Name");
            s.Gpu = WmiFirst("Win32_VideoController", "Name");
            try {
                using (ManagementObjectSearcher q = new ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem")) {
                    foreach (ManagementObject v in q.Get()) {
                        s.TotalRam = Convert.ToUInt64(v["TotalVisibleMemorySize"]) * 1024;
                        s.FreeRam = Convert.ToUInt64(v["FreePhysicalMemory"]) * 1024;
                        break;
                    }
                }
            } catch { }
            try {
                DriveInfo drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                if (drive.IsReady) {
                    s.DiskTotal = drive.TotalSize;
                    s.DiskFree = drive.AvailableFreeSpace;
                    s.DiskName = drive.Name;
                }
                foreach (DriveInfo d in DriveInfo.GetDrives()) if (d.IsReady && d.DriveType == DriveType.Fixed) s.DiskCount++;
            } catch { }
            try {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces()) {
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback) s.NetworkCount++;
                }
            } catch { }
            try {
                using (ManagementObjectSearcher b = new ManagementObjectSearcher("SELECT * FROM Win32_Battery")) {
                    foreach (ManagementObject unused in b.Get()) s.BatteryCount++;
                }
            } catch { }
            snapshot = s;
        }

        private int CalculateScore() {
            // Indicador inicial limitado a RAM + espaço livre, não representa diagnóstico completo.
            int score = 100;
            if (snapshot.DiskTotal > 0) {
                double fraction = (double)snapshot.DiskFree / snapshot.DiskTotal;
                if (fraction < .05) score -= 35;
                else if (fraction < .15) score -= 20;
            }
            if (snapshot.TotalRam > 0) {
                double used = (double)(snapshot.TotalRam - snapshot.FreeRam) / snapshot.TotalRam;
                if (used > .95) score -= 30;
                else if (used > .85) score -= 15;
            }
            return Math.Max(0, score);
        }
        private void OpenWindowsSettings(string uri) {
            try { Process.Start(uri); }
            catch (Exception ex) { ShowError(ex.Message); }
        }
        private void ShowError(string error) {
            MessageBox.Show(error, "StorHUB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private class Ring : Control {
            public int Score = -1;
            public Ring() { DoubleBuffered = true; }
            protected override void OnPaint(PaintEventArgs e) {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle arc = new Rectangle(23, 12, 171, 171);
                using (Pen track = new Pen(Color.FromArgb(35, 43, 65), 8)) {
                    track.StartCap = LineCap.Round; track.EndCap = LineCap.Round;
                    g.DrawArc(track, arc, 0, 360);
                }
                if (Score >= 0) using (Pen prog = new Pen(Score >= 80 ? Color.FromArgb(83, 195, 159) : Danger, 8)) {
                    prog.StartCap = LineCap.Round; prog.EndCap = LineCap.Round;
                    g.DrawArc(prog, arc, -90, 360f * Score / 100f);
                }
                using (Font f = new Font("Segoe UI", 36, FontStyle.Bold))
                using (Brush brush = new SolidBrush(Score >= 80 ? Success : Score >= 0 ? Danger : TextMain)) {
                    string t = Score >= 0 ? Score.ToString() : "--";
                    SizeF sz = g.MeasureString(t, f);
                    g.DrawString(t, f, brush, 108 - sz.Width / 2, 58);
                }
                using (Font f = new Font("Segoe UI", 9))
                using (Brush b = new SolidBrush(Muted)) {
                    string t = Score < 0 ? "iniciar análise" : "indicador básico";
                    SizeF s = g.MeasureString(t, f);
                    g.DrawString(t, f, b, 108 - s.Width / 2, 125);
                }
            }
        }
    }
}
