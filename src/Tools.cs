using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;

namespace StorHUB {
    public partial class StorHubForm {
        private class DuplicateFile {
            public string Path;
            public long Length;
            public string Group;
        }
        private class FolderBucket {
            public string Name;
            public long Length;
            public int Files;
        }
        private CancellationTokenSource duplicateCancellation;
        private CancellationTokenSource diskMapCancellation;

        private void ShowDuplicates() {
            Panel pane = ModuleCard("Arquivos duplicados",
                "Busca por hash SHA-256 • não marca nada sozinho • pode enviar cópias escolhidas à Lixeira", 605);
            TextBox path = new TextBox {
                Left = 17, Top = 18, Width = Math.Max(200, pane.Width - 176), Height = 28,
                BackColor = Bg, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true, Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            pane.Controls.Add(path);
            AddButton(pane, "Escolher pasta", Math.Max(220, pane.Width - 148), 14, 134, delegate {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog()) {
                    dialog.Description = "Selecione a pasta onde deseja procurar arquivos duplicados";
                    dialog.SelectedPath = path.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) path.Text = dialog.SelectedPath;
                }
            }, false);
            Label status = AddText(pane, "Aguardando. A pesquisa não altera nem apaga arquivos.",
                17, 62, pane.Width - 34, 39, 9, FontStyle.Regular, Muted);
            ListView table = NewTable(pane, 180, "Grupo", "Arquivo", "Tamanho");
            table.CheckBoxes = true;
            Dictionary<string,int> visibleGroups = new Dictionary<string,int>();
            Button start = AddButton(pane, "Procurar duplicados", 17, 117, 191, null);
            Button cancel = AddButton(pane, "Parar", 218, 117, 87, null, false);
            Button extra = AddButton(pane, "Marcar cópias extras", 316, 117, 187, null, false);
            Button recycle = AddButton(pane, "Mandar para Lixeira", 514, 117, 193, null, false);
            cancel.Enabled = false; extra.Enabled = false; recycle.Enabled = false;
            start.Click += async delegate {
                if (duplicateCancellation != null) return;
                string root;
                try { root = Path.GetFullPath(path.Text).TrimEnd(Path.DirectorySeparatorChar) + "\\"; }
                catch { ShowError("Escolha uma pasta válida."); return; }
                if (!Directory.Exists(root)) { ShowError("Pasta não encontrada."); return; }
                duplicateCancellation = new CancellationTokenSource();
                CancellationToken cancelToken = duplicateCancellation.Token;
                start.Enabled = false; cancel.Enabled = true; extra.Enabled = false; recycle.Enabled = false;
                table.Items.Clear(); visibleGroups.Clear();
                status.Text = "Procurando arquivos iguais... a operação pode demorar em pastas grandes.";
                try {
                    List<DuplicateFile> files = await Task.Run(() => FindDuplicates(root, cancelToken));
                    if (IsDisposed || pane.IsDisposed) return;
                    table.BeginUpdate();
                    int groupIndex = 0;
                    foreach (IGrouping<string, DuplicateFile> group in files.GroupBy(x => x.Group)) {
                        groupIndex++;
                        visibleGroups[group.Key] = group.Count();
                        foreach (DuplicateFile file in group)
                            PutRow(table, file, "Grupo " + groupIndex, file.Path.Substring(root.Length),
                                FormatBytes(file.Length));
                    }
                    table.EndUpdate();
                    long redundant = files.GroupBy(x => x.Group).Sum(g => (long)g.First().Length * (g.Count() - 1));
                    status.Text = groupIndex + " grupo(s), " + files.Count + " arquivo(s), " +
                                  FormatBytes(redundant) + " potencialmente redundantes. Limite: 12.000 arquivos, até 1 GB cada.";
                    extra.Enabled = files.Count > 1; recycle.Enabled = files.Count > 1;
                } catch (OperationCanceledException) {
                    if (!pane.IsDisposed) status.Text = "Busca cancelada. Nenhum arquivo foi alterado.";
                } catch (Exception ex) {
                    if (!pane.IsDisposed) status.Text = "Erro: " + ex.Message;
                } finally {
                    duplicateCancellation.Dispose();
                    duplicateCancellation = null;
                    if (!pane.IsDisposed) { start.Enabled = true; cancel.Enabled = false; }
                }
            };
            cancel.Click += delegate { if (duplicateCancellation != null) duplicateCancellation.Cancel(); };
            extra.Click += delegate {
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ListViewItem item in table.Items) {
                    DuplicateFile f = item.Tag as DuplicateFile;
                    item.Checked = f != null && !seen.Add(f.Group);
                }
            };
            recycle.Click += delegate {
                List<DuplicateFile> selected = table.CheckedItems.Cast<ListViewItem>()
                    .Select(x => x.Tag as DuplicateFile).Where(x => x != null).ToList();
                if (selected.Count == 0) { MessageBox.Show("Marque as cópias que deseja excluir.", "StorHUB"); return; }
                List<DuplicateFile> all = table.Items.Cast<ListViewItem>().Select(x => x.Tag as DuplicateFile)
                    .Where(x => x != null).ToList();
                foreach (IGrouping<string,DuplicateFile> g in selected.GroupBy(x => x.Group)) {
                    if (!visibleGroups.ContainsKey(g.Key) || g.Count() >= visibleGroups[g.Key]) {
                        ShowError("É necessário manter pelo menos uma cópia de cada grupo. Nenhum arquivo foi excluído.");
                        return;
                    }
                    if (!all.Where(x => x.Group == g.Key && !selected.Any(s => s.Path == x.Path))
                        .Any(x => File.Exists(x.Path) && IsSameFileContent(x))) {
                        ShowError("Não foi possível confirmar a integridade de uma cópia restante. Nada foi excluído.");
                        return;
                    }
                }
                if (MessageBox.Show("Enviar " + selected.Count +
                    " cópia(s) para a Lixeira? Os arquivos selecionados serão verificados novamente.",
                    "StorHUB - Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                int ok = 0, skipped = 0;
                string origin;
                try { origin = Path.GetFullPath(path.Text).TrimEnd('\\') + "\\"; } catch { return; }
                foreach (DuplicateFile f in selected) {
                    try {
                        string full = Path.GetFullPath(f.Path);
                        if (!full.StartsWith(origin, StringComparison.OrdinalIgnoreCase) || !IsSameFileContent(f)) {
                            skipped++; continue;
                        }
                        FileSystem.DeleteFile(full, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        ok++;
                    } catch { skipped++; }
                }
                table.Items.Clear(); extra.Enabled = false; recycle.Enabled = false;
                status.Text = ok + " movido(s) para a Lixeira; " + skipped +
                              " ignorado(s). Execute uma nova busca para atualizar.";
            };
        }

        private static bool IsSameFileContent(DuplicateFile file) {
            try {
                FileInfo info = new FileInfo(file.Path);
                if (!info.Exists || info.Length != file.Length ||
                    (info.Attributes & FileAttributes.ReparsePoint) != 0) return false;
                string digest = FileDigest(file.Path, CancellationToken.None);
                return file.Group == file.Length + ":" + digest;
            } catch { return false; }
        }

        private static string FileDigest(string path, CancellationToken cancellation) {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                byte[] buffer = new byte[131072]; int count;
                while ((count = stream.Read(buffer,0,buffer.Length)) > 0) {
                    cancellation.ThrowIfCancellationRequested();
                    sha.TransformBlock(buffer,0,count,buffer,0);
                }
                sha.TransformFinalBlock(new byte[0],0,0);
                return BitConverter.ToString(sha.Hash).Replace("-","");
            }
        }

        private static List<DuplicateFile> FindDuplicates(string root, CancellationToken cancellation) {
            Dictionary<long, List<string>> lengths = new Dictionary<long, List<string>>();
            int seen = 0;
            foreach (string file in SafeEnumerateFiles(root, 12000, cancellation)) {
                cancellation.ThrowIfCancellationRequested();
                try {
                    FileInfo info = new FileInfo(file);
                    if (!info.Exists || info.Length > 1024L*1024*1024 || info.Length == 0) continue;
                    List<string> group;
                    if (!lengths.TryGetValue(info.Length, out group)) {
                        group = new List<string>(); lengths[info.Length] = group;
                    }
                    group.Add(file);
                } catch { }
                if (++seen >= 12000) break;
            }
            Dictionary<string,List<DuplicateFile>> hashes = new Dictionary<string,List<DuplicateFile>>();
            foreach (KeyValuePair<long,List<string>> size in lengths) {
                if (size.Value.Count < 2) continue;
                foreach (string file in size.Value) {
                    cancellation.ThrowIfCancellationRequested();
                    try {
                        string id = size.Key + ":" + FileDigest(file, cancellation);
                        List<DuplicateFile> group;
                        if (!hashes.TryGetValue(id, out group)) {
                            group = new List<DuplicateFile>(); hashes[id] = group;
                        }
                        group.Add(new DuplicateFile { Path = file, Length = size.Key, Group = id });
                    } catch (OperationCanceledException) { throw; } catch { }
                }
            }
            return hashes.Values.Where(g => g.Count > 1).SelectMany(g => g)
                .OrderByDescending(x => x.Length).ThenBy(x => x.Path).ToList();
        }

        private static IEnumerable<string> SafeEnumerateFiles(string root, int maxFiles, CancellationToken cancellation) {
            string canonical = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            Stack<string> pending = new Stack<string>();
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            pending.Push(canonical);
            int yielded = 0;
            while (pending.Count > 0 && yielded < maxFiles && visited.Count < 25000) {
                cancellation.ThrowIfCancellationRequested();
                string dir = pending.Pop();
                if (!visited.Add(dir)) continue;
                try {
                    if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                    foreach (string sub in Directory.EnumerateDirectories(dir)) {
                        try {
                            if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0)
                                pending.Push(sub);
                        } catch { }
                    }
                } catch { }
                List<string> files;
                try { files = Directory.EnumerateFiles(dir).ToList(); }
                catch { continue; }
                foreach (string file in files) {
                    cancellation.ThrowIfCancellationRequested();
                    try {
                        if (!Path.GetFullPath(file).StartsWith(canonical, StringComparison.OrdinalIgnoreCase) ||
                            (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                        yield return file;
                        if (++yielded >= maxFiles) yield break;
                    } catch { }
                }
            }
        }

        private void ShowDiskMap() {
            Panel pane = ModuleCard("Mapa do disco",
                "Leitura das pastas e tamanho ocupado • nenhum arquivo é alterado", 605);
            TextBox folder = new TextBox {
                Left = 18, Top = 13, Width = Math.Max(200, pane.Width - 179), Height = 28,
                BackColor = Bg, ForeColor = TextMain, ReadOnly = true,
                Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                BorderStyle = BorderStyle.FixedSingle
            };
            pane.Controls.Add(folder);
            AddButton(pane, "Escolher pasta", Math.Max(220, pane.Width - 145), 10, 128, delegate {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog()) {
                    dialog.Description = "Escolha a pasta para o mapa de ocupação";
                    dialog.SelectedPath = folder.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath;
                }
            }, false);
            Label status = AddText(pane,"Escolha uma pasta e inicie a análise.",
                17, 52, pane.Width - 30, 33, 9, FontStyle.Regular, Muted);
            List<FolderBucket> result = new List<FolderBucket>();
            Panel blocks = new Panel {
                Left = 17, Top = 147, Width = Math.Max(350, pane.Width - 35),
                Height = 164, BackColor = Color.FromArgb(11,15,25)
            };
            pane.Controls.Add(blocks);
            blocks.Paint += delegate(object sender, PaintEventArgs evt) {
                if (result.Count == 0) return;
                evt.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                double total = result.Sum(x => (double)x.Length);
                if (total <= 0) return;
                int x = 2;
                for (int i = 0; i < result.Count && i < 18; i++) {
                    FolderBucket b = result[i];
                    int w = Math.Max(1, (int)((blocks.Width - 4) * b.Length / total));
                    if (x + w > blocks.Width - 2) w = blocks.Width - 2 - x;
                    if (w <= 0) break;
                    Color color = Color.FromArgb(35 + (i*23)%92, 73 + (i*29)%98, 126 + (i*17)%106);
                    using (Brush brush = new SolidBrush(color))
                        evt.Graphics.FillRectangle(brush, x, 1, w - 1, blocks.Height - 2);
                    if (w > 88) {
                        using (Font font = new Font("Segoe UI", 8, FontStyle.Bold))
                        using (Brush brush = new SolidBrush(Color.White))
                            evt.Graphics.DrawString(b.Name, font, brush, x + 6, 8);
                    }
                    x += w;
                }
            };
            ListView table = NewTable(pane, 348, "Pasta / arquivos", "Tamanho", "Arquivos");
            Button start = AddButton(pane, "Analisar espaço", 17, 93, 166, null);
            Button stop = AddButton(pane, "Parar", 195, 93, 91, null, false);
            stop.Enabled = false;
            start.Click += async delegate {
                if (diskMapCancellation != null) return;
                diskMapCancellation = new CancellationTokenSource();
                start.Enabled = false; stop.Enabled = true;
                status.Text = "Analisando somente arquivos e tamanhos...";
                try {
                    List<FolderBucket> buckets = await Task.Run(() => GetDirectorySizes(folder.Text,diskMapCancellation.Token));
                    if (IsDisposed || pane.IsDisposed) return;
                    result = buckets;
                    table.Items.Clear();
                    foreach (FolderBucket b in result)
                        PutRow(table, b, b.Name, FormatBytes(b.Length), b.Files.ToString());
                    blocks.Invalidate();
                    status.Text = result.Count + " grupo(s) • " +
                        FormatBytes(result.Sum(x => (double)x.Length)) +
                        " contabilizados. Limite: primeiros 30.000 arquivos.";
                } catch (OperationCanceledException) {
                    if (!pane.IsDisposed) status.Text = "Análise interrompida; nenhum arquivo alterado.";
                } catch (Exception ex) {
                    if (!pane.IsDisposed) status.Text = "Erro: " + ex.Message;
                } finally {
                    diskMapCancellation.Dispose(); diskMapCancellation = null;
                    if (!pane.IsDisposed) { start.Enabled = true; stop.Enabled = false; }
                }
            };
            stop.Click += delegate { if (diskMapCancellation != null) diskMapCancellation.Cancel(); };
        }

        private static List<FolderBucket> GetDirectorySizes(string selected, CancellationToken cancellation) {
            string root = Path.GetFullPath(selected).TrimEnd('\\') + "\\";
            Dictionary<string,FolderBucket> names = new Dictionary<string,FolderBucket>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in SafeEnumerateFiles(root, 30000, cancellation)) {
                cancellation.ThrowIfCancellationRequested();
                try {
                    string relative = file.Substring(root.Length);
                    int slash = relative.IndexOf('\\');
                    string bucket = slash >= 0 ? relative.Substring(0, slash) : "[arquivos na raiz]";
                    FolderBucket entry;
                    if (!names.TryGetValue(bucket, out entry)) {
                        entry = new FolderBucket { Name = bucket };
                        names[bucket] = entry;
                    }
                    entry.Length += new FileInfo(file).Length;
                    entry.Files++;
                } catch { }
            }
            return names.Values.OrderByDescending(x => x.Length).ToList();
        }
    }
}
