using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StorHUB {
    public partial class StorHubForm {
        private class BenchMetric {
            public string Id;
            public string Label;
            public string Unit;
            public double Value;
            public DateTime RecordedUtc;
        }

        private CancellationTokenSource benchmarkCancellation;
        private static long benchmarkSink;

        private static string BenchmarkHistoryPath {
            get {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StorHUB", "benchmark-history.tsv");
            }
        }

        private void ShowBenchmark() {
            Panel pane = ModuleCard("Benchmark",
                "Medições próprias de CPU, cópia de memória e disco. Não são a pontuação do HUB-Pro.", 605);
            AddText(pane, "Teste de desempenho", 18, 13, pane.Width - 36, 39, 14, System.Drawing.FontStyle.Bold);
            Label status = AddText(pane,
                "Clique em Iniciar. O teste cria e apaga somente um arquivo temporário próprio (32 MiB).",
                17, 57, pane.Width - 33, 36, 9, System.Drawing.FontStyle.Regular, Muted);

            ListView table = NewTable(pane, 173, "Medição", "Resultado", "Comparação com anterior");
            table.Columns[0].Width = (int)(table.Width * .38);
            table.Columns[1].Width = (int)(table.Width * .25);
            table.Columns[2].Width = (int)(table.Width * .32);

            Button start = AddButton(pane, "Iniciar benchmark", 18, 108, 185, null);
            Button cancel = AddButton(pane, "Parar", 215, 108, 105, null, false);
            cancel.Enabled = benchmarkCancellation != null;
            Button history = AddButton(pane, "Ver histórico", 333, 108, 161, null, false);

            Action<List<BenchMetric>> render = delegate(List<BenchMetric> latest) {
                List<BenchMetric> previous = ReadBenchmarkHistory().Where(x => x.RecordedUtc <
                    (latest.Count > 0 ? latest[0].RecordedUtc : DateTime.MinValue))
                    .GroupBy(x => x.Id).Select(g => g.OrderByDescending(x => x.RecordedUtc).First())
                    .ToList();
                table.BeginUpdate(); table.Items.Clear();
                foreach (BenchMetric metric in latest) {
                    BenchMetric prior = previous.FirstOrDefault(x => x.Id == metric.Id);
                    string compare = prior == null || prior.Value <= 0 ? "Sem teste anterior" :
                        (100 * (metric.Value / prior.Value - 1)).ToString("+0.0;-0.0;0.0",
                        CultureInfo.CurrentCulture) + "% vs anterior";
                    PutRow(table, metric, metric.Label,
                        metric.Value.ToString("N1", CultureInfo.CurrentCulture) + " " + metric.Unit, compare);
                }
                table.EndUpdate();
            };

            List<BenchMetric> priorRun = ReadBenchmarkHistory()
                .GroupBy(x => x.RecordedUtc).OrderByDescending(g => g.Key)
                .Select(g => g.ToList()).FirstOrDefault();
            if (priorRun != null && priorRun.Count > 0) {
                render(priorRun);
                status.Text = "Último teste: " + priorRun[0].RecordedUtc.ToLocalTime()
                    .ToString("dd/MM/yyyy HH:mm") + ". Testes anteriores salvos localmente.";
            }

            start.Click += async delegate {
                if (benchmarkCancellation != null) return;
                benchmarkCancellation = new CancellationTokenSource();
                CancellationToken token = benchmarkCancellation.Token;
                start.Enabled = false;
                cancel.Enabled = true;
                status.Text = "Iniciando medições...";
                try {
                    List<BenchMetric> results = await Task.Run(() => RunBenchmark(token,
                        delegate(string msg) {
                            if (pane.IsDisposed || status.IsDisposed) return;
                            try { status.BeginInvoke(new Action(() => {
                                if (!status.IsDisposed) status.Text = msg;
                            })); } catch (InvalidOperationException) { }
                        }));
                    if (pane.IsDisposed || IsDisposed) return;
                    if (results.Count > 0) {
                        SaveBenchmarkHistory(results);
                        render(results);
                        status.Text = "Concluído: " + results.Count +
                            " medições. Histórico salvo apenas neste usuário do Windows.";
                    }
                } catch (OperationCanceledException) {
                    if (!status.IsDisposed) status.Text = "Benchmark interrompido pelo usuário.";
                } catch (Exception ex) {
                    if (!status.IsDisposed) status.Text = "Não foi possível concluir: " + ex.Message;
                } finally {
                    if (benchmarkCancellation != null) {
                        benchmarkCancellation.Dispose(); benchmarkCancellation = null;
                    }
                    if (!start.IsDisposed) start.Enabled = true;
                    if (!cancel.IsDisposed) cancel.Enabled = false;
                }
            };
            cancel.Click += delegate {
                if (benchmarkCancellation != null) benchmarkCancellation.Cancel();
            };
            history.Click += delegate {
                List<BenchMetric> hist = ReadBenchmarkHistory();
                if (hist.Count == 0) { MessageBox.Show("Ainda não há histórico.", "StorHUB"); return; }
                StringBuilder summary = new StringBuilder();
                foreach (IGrouping<DateTime, BenchMetric> run in hist
                    .GroupBy(x => x.RecordedUtc).OrderByDescending(g => g.Key).Take(6)) {
                    summary.AppendLine(run.Key.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                    foreach (BenchMetric item in run) {
                        summary.AppendLine("  " + item.Label + ": " +
                            item.Value.ToString("N1", CultureInfo.CurrentCulture) + " " + item.Unit);
                    }
                    summary.AppendLine();
                }
                MessageBox.Show(summary.ToString(), "StorHUB - Histórico de benchmarks",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            AddText(pane, "Os resultados dependem de cache, energia, carga do PC, temperatura e armazenamento. " +
                "Sem calibração oficial, estes números não podem ser comparados às notas do HUB-Pro.",
                18, 145, pane.Width - 35, 26, 8, System.Drawing.FontStyle.Regular, Muted);
        }

        private static List<BenchMetric> RunBenchmark(CancellationToken cancellation,
            Action<string> progress) {
            DateTime started = DateTime.UtcNow;
            List<BenchMetric> results = new List<BenchMetric>();

            cancellation.ThrowIfCancellationRequested();
            progress("1/5 — CPU: executando um núcleo...");
            double single = CpuLoop(800, cancellation);
            results.Add(new BenchMetric { Id="cpu_single", Label="CPU — 1 núcleo",
                Unit="milhões de operações/s", Value=single / 1000000.0, RecordedUtc=started });

            cancellation.ThrowIfCancellationRequested();
            progress("2/5 — CPU: processando núcleos em paralelo...");
            int workers = Math.Min(8, Math.Max(1, Environment.ProcessorCount));
            long total=0;
            Parallel.For(0, workers, new ParallelOptions {
                MaxDegreeOfParallelism=workers, CancellationToken=cancellation
            }, index => {
                long amount = (long)CpuLoop(800, cancellation);
                Interlocked.Add(ref total, amount);
            });
            results.Add(new BenchMetric { Id="cpu_multi", Label="CPU — até "+workers+" núcleos",
                Unit="milhões de operações/s", Value=(double)total/1000000.0,
                RecordedUtc=started });

            cancellation.ThrowIfCancellationRequested();
            progress("3/5 — Memória: medindo cópia de buffers...");
            byte[] source = new byte[8 * 1024 * 1024];
            byte[] dest = new byte[source.Length];
            for(int j=0;j<source.Length;j+=4096)source[j]=(byte)(j%239);
            Stopwatch timer=Stopwatch.StartNew();
            int loops=96;
            for(int j=0;j<loops;j++) {
                cancellation.ThrowIfCancellationRequested();
                Buffer.BlockCopy(source,0,dest,0,source.Length);
            }
            timer.Stop();
            double memMiB=(double)(source.Length*2L*loops)/(1024.0*1024.0);
            results.Add(new BenchMetric { Id="memory_copy",Label="Memória — cópia de buffers",
                Unit="MiB/s",Value=memMiB/Math.Max(.0001,timer.Elapsed.TotalSeconds),
                RecordedUtc=started });
            benchmarkSink += dest[0];

            string tempPath=Path.Combine(Path.GetTempPath(),
                "StorHUB-benchmark-"+Guid.NewGuid().ToString("N")+".tmp");
            try {
                cancellation.ThrowIfCancellationRequested();
                progress("4/5 — Disco: gravando arquivo temporário de 32 MiB...");
                byte[] buffer = new byte[1024*1024];
                for(int i=0;i<buffer.Length;i+=4096)buffer[i]=(byte)(i%251);
                timer.Restart();
                using(FileStream output = new FileStream(tempPath,
                    FileMode.CreateNew,FileAccess.Write,FileShare.None,buffer.Length,
                    FileOptions.SequentialScan)) {
                    for(int i=0;i<32;i++) {
                        cancellation.ThrowIfCancellationRequested();
                        output.Write(buffer,0,buffer.Length);
                    }
                    output.Flush(true);
                }
                timer.Stop();
                results.Add(new BenchMetric {Id="disk_write",Label="Disco — escrita temporária",
                    Unit="MiB/s",Value=32.0/Math.Max(.0001,timer.Elapsed.TotalSeconds),
                    RecordedUtc=started});

                cancellation.ThrowIfCancellationRequested();
                progress("5/5 — Disco: lendo o arquivo temporário...");
                int count;
                long read=0;
                timer.Restart();
                using(FileStream input = new FileStream(tempPath,
                    FileMode.Open,FileAccess.Read,FileShare.Read,buffer.Length,
                    FileOptions.SequentialScan)) {
                    while((count=input.Read(buffer,0,buffer.Length))>0) {
                        cancellation.ThrowIfCancellationRequested();
                        read+=count;
                    }
                }
                timer.Stop();
                if(read != 32L*1024*1024)
                    throw new IOException("Arquivo de benchmark incompleto.");
                results.Add(new BenchMetric { Id="disk_read",Label="Disco — leitura temporária",
                    Unit="MiB/s",Value=32.0/Math.Max(.0001,timer.Elapsed.TotalSeconds),
                    RecordedUtc=started});
                return results;
            } finally {
                try { if(File.Exists(tempPath)) File.Delete(tempPath); }
                catch { /* Pode ter sido bloqueado por outro processo; nunca apaga outros caminhos. */ }
            }
        }

        private static double CpuLoop(int durationMilliseconds,CancellationToken cancellation) {
            Stopwatch t=Stopwatch.StartNew();
            ulong state=0x9E3779B97F4A7C15UL;
            long n=0;
            while(t.ElapsedMilliseconds<durationMilliseconds) {
                cancellation.ThrowIfCancellationRequested();
                for(int i=0;i<20000;i++) {
                    state ^= state << 7;
                    state ^= state >> 9;
                    state ^= state << 8;
                }
                n+=20000;
            }
            benchmarkSink = unchecked((long)state);
            return n/Math.Max(.0001,t.Elapsed.TotalSeconds);
        }

        private static void SaveBenchmarkHistory(List<BenchMetric> result) {
            string file=BenchmarkHistoryPath;
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            using(StreamWriter writer=new StreamWriter(file,true,new UTF8Encoding(false))) {
                foreach(BenchMetric m in result) {
                    writer.WriteLine(m.RecordedUtc.ToString("O",CultureInfo.InvariantCulture)+"\t"+
                        m.Id+"\t"+m.Label+"\t"+m.Unit+"\t"+
                        m.Value.ToString("R",CultureInfo.InvariantCulture));
                }
            }
        }

        private static List<BenchMetric> ReadBenchmarkHistory() {
            List<BenchMetric> items = new List<BenchMetric>();
            string path=BenchmarkHistoryPath;
            if(!File.Exists(path))return items;
            try {
                foreach(string line in File.ReadLines(path).Take(2000)) {
                    string[] cells=line.Split('\t');
                    DateTime when;
                    double value;
                    if(cells.Length != 5 ||
                       !DateTime.TryParse(cells[0],CultureInfo.InvariantCulture,
                           DateTimeStyles.RoundtripKind,out when) ||
                       !double.TryParse(cells[4],NumberStyles.Float,CultureInfo.InvariantCulture,
                           out value) || double.IsNaN(value) || double.IsInfinity(value))continue;
                    items.Add(new BenchMetric { RecordedUtc=when,Id=cells[1],Label=cells[2],
                        Unit=cells[3],Value=value });
                }
            } catch { }
            return items;
        }
    }
}
