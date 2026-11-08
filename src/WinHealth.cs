using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace StorHUB {
    public partial class StorHubForm {
        private sealed class RepairCheck {
            public string Key;
            public string Label;
            public string Exe;
            public string Arguments;
            public string Description;
        }
        private sealed class RepairResult {
            public int ExitCode;
            public bool Cancelled;
            public TimeSpan Duration;
            public string Output;
        }
        private CancellationTokenSource repairCancellation;

        // These are verification-only command lines. No /RestoreHealth, /ScanNow or /Fix.
        private static RepairCheck[] GetRepairChecks() {
            string system = Environment.SystemDirectory;
            return new [] {
                new RepairCheck {
                    Key="dism_check", Label="DISM - Verificar estado da imagem",
                    Exe=Path.Combine(system,"dism.exe"),
                    Arguments="/Online /Cleanup-Image /CheckHealth",
                    Description="Consulta o indicador de corrupção da imagem Windows (não repara)."
                },
                new RepairCheck {
                    Key="sfc_verify", Label="SFC - Verificar arquivos do Windows",
                    Exe=Path.Combine(system,"sfc.exe"),
                    Arguments="/verifyonly",
                    Description="Verifica arquivos protegidos, sem repará-los. Pode demorar e exigir administrador."
                }
            };
        }

        private static RepairResult RunRepairCheck(RepairCheck check, CancellationToken cancel) {
            if(check == null || string.IsNullOrEmpty(check.Exe)) throw new ArgumentException("Comando ausente.");
            if(!File.Exists(check.Exe)) throw new FileNotFoundException("Ferramenta do Windows não encontrada.",check.Exe);
            cancel.ThrowIfCancellationRequested();
            ProcessStartInfo info = new ProcessStartInfo {
                FileName=check.Exe, Arguments=check.Arguments, UseShellExecute=false,
                CreateNoWindow=true, RedirectStandardOutput=true,
                RedirectStandardError=true, WorkingDirectory=Environment.SystemDirectory
            };
            StringBuilder log = new StringBuilder();
            object guard = new object();
            Action<string> append = delegate(string line) {
                if(line==null)return;
                lock(guard) {
                    if(log.Length < 50000) {
                        int max = Math.Min(line.Length, Math.Max(0, 50000-log.Length));
                        log.Append(line.Substring(0,max)).AppendLine();
                    }
                }
            };
            Stopwatch clock=Stopwatch.StartNew();
            using(Process process = new Process { StartInfo=info }) {
                process.OutputDataReceived+=delegate(object sender, DataReceivedEventArgs e) { append(e.Data); };
                process.ErrorDataReceived+=delegate(object sender, DataReceivedEventArgs e) { append(e.Data); };
                if(!process.Start()) throw new InvalidOperationException("O Windows não iniciou a verificação.");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                using(cancel.Register(delegate {
                    try { if(!process.HasExited) process.Kill(); }
                    catch(InvalidOperationException) { }
                    catch(System.ComponentModel.Win32Exception) { }
                })) {
                    process.WaitForExit();
                }
                clock.Stop();
                return new RepairResult {
                    ExitCode=process.ExitCode, Cancelled=cancel.IsCancellationRequested,
                    Duration=clock.Elapsed, Output=log.ToString()
                };
            }
        }

        private void ShowRepairWindows() {
            Panel pane=ModuleCard("Reparar o Windows",
                "Verificações do sistema sob demanda • esta etapa NÃO executa correções automáticas",605);
            AddText(pane, "Diagnóstico da integridade do Windows",18,13,pane.Width-34,42,
                15,FontStyle.Bold);
            AddText(pane,
                "Selecione uma verificação. Algumas exigem executar o StorHUB como administrador. " +
                "Não são executados SFC /scannow, DISM /RestoreHealth ou alterações no registro.",
                18,56,pane.Width-37,55,9,FontStyle.Regular,Muted);

            RepairCheck[] checks=GetRepairChecks();
            Label status=AddText(pane,"Nenhuma verificação iniciada.",18,181,pane.Width-40,40,
                10,FontStyle.Regular,Muted);
            TextBox output=new TextBox {
                Bounds=new Rectangle(18,230,Math.Max(300,pane.Width-38),pane.Height-253),
                Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,
                BackColor=Color.FromArgb(12,18,31),ForeColor=TextMain,
                Font=new Font("Consolas",9),WordWrap=false
            };
            pane.Controls.Add(output);
            List<Button> actions=new List<Button>();
            Button stop=AddButton(pane,"Interromper",18,131,132,null,false);
            stop.Enabled=false;
            for(int i=0;i<checks.Length;i++) {
                RepairCheck current=checks[i];
                Button action=AddButton(pane,current.Label,18+i*347,112,330,null);
                actions.Add(action);
                action.Click+=async delegate {
                    if(repairCancellation!=null)return;
                    repairCancellation=new CancellationTokenSource();
                    CancellationToken token=repairCancellation.Token;
                    foreach(Button b in actions)b.Enabled=false;
                    stop.Enabled=true;
                    status.Text="Executando "+current.Label+"...";
                    output.Text="$ "+Path.GetFileName(current.Exe)+" "+current.Arguments+
                        Environment.NewLine+current.Description+Environment.NewLine+
                        "A verificação só começou após o clique."+
                        Environment.NewLine+Environment.NewLine;
                    try {
                        RepairResult result=await Task.Run(() => RunRepairCheck(current,token));
                        if(pane.IsDisposed || IsDisposed)return;
                        output.AppendText(result.Output);
                        output.AppendText(Environment.NewLine+
                            "Código de saída: "+result.ExitCode+
                            " | duração: "+result.Duration.ToString("g")+
                            " | cancelado: "+(result.Cancelled?"sim":"não"));
                        status.Text=result.Cancelled?"Execução interrompida.":result.ExitCode==0?
                            "Verificação concluída (código 0). Confira o relatório.":"Código de saída "+
                            result.ExitCode+". Confira o relatório e permissões.";
                    }catch(OperationCanceledException) {
                        if(!pane.IsDisposed)status.Text="Verificação cancelada antes de iniciar.";
                    }catch(Exception ex) {
                        if(!pane.IsDisposed) {
                            output.AppendText(Environment.NewLine+ex.ToString());
                            status.Text="Falha ou falta de permissão: "+ex.Message;
                        }
                    }finally {
                        if(repairCancellation!=null){repairCancellation.Dispose();repairCancellation=null;}
                        if(!pane.IsDisposed){
                            stop.Enabled=false;
                            foreach(Button b in actions)b.Enabled=true;
                        }
                    }
                };
            }
            // Keep control order by explicit coordinates. In narrow windows, scroll horizontally.
            stop.Top=169;
            stop.Left=Math.Max(18,pane.Width-158);
            stop.Click+=delegate{
                if(repairCancellation!=null)repairCancellation.Cancel();
                status.Text="Interrupção solicitada.";
            };
            AddButton(pane,"Copiar relatório",Math.Max(18,pane.Width-169),181,145,
                delegate{
                    try { Clipboard.SetText(output.Text); }
                    catch(Exception ex){ShowError(ex.Message);}
                },false);
        }

        private sealed class SystemServiceInfo {
            public string Name;
            public string Display;
            public string State;
            public string Startup;
        }

        private static List<SystemServiceInfo> ReadOptimizationServices() {
            string[] watch={"wuauserv","BITS","WinDefend","WSearch","SysMain"};
            List<SystemServiceInfo> found=new List<SystemServiceInfo>();
            using(ManagementObjectSearcher search=new ManagementObjectSearcher(
                "SELECT Name,DisplayName,State,StartMode FROM Win32_Service")) {
                foreach(ManagementObject obj in search.Get()) {
                    string name=Convert.ToString(obj["Name"]);
                    if(!watch.Contains(name,StringComparer.OrdinalIgnoreCase))continue;
                    found.Add(new SystemServiceInfo {
                        Name=name,Display=Convert.ToString(obj["DisplayName"]),
                        State=Convert.ToString(obj["State"]),
                        Startup=Convert.ToString(obj["StartMode"])
                    });
                }
            }
            return found.OrderBy(x=>Array.IndexOf(watch,x.Name)).ToList();
        }

        private void ShowOptimization() {
            Panel pane=ModuleCard("Otimizações",
                "Inventário real dos serviços e configurações • sem alterações automáticas no Windows",605);
            AddText(pane,"Estado dos serviços do sistema",18,12,pane.Width-35,45,15,FontStyle.Bold);
            AddText(pane,
                "Um serviço parado NÃO indica necessariamente problema. O StorHUB não recomenda desabilitar " +
                "antivírus, atualizações ou serviços essenciais.",
                18,56,pane.Width-38,55,9,FontStyle.Regular,Muted);
            ListView table=NewTable(pane,197,"Serviço","Nome amigável","Estado","Inicialização");
            table.Columns[0].Width=(int)(table.Width*.17);
            table.Columns[1].Width=(int)(table.Width*.42);
            table.Columns[2].Width=(int)(table.Width*.18);
            table.Columns[3].Width=(int)(table.Width*.19);
            Label status=AddText(pane,"Consultando serviços...",18,122,pane.Width-35,30,
                9,FontStyle.Regular,Muted);
            Action update=delegate {
                table.Items.Clear();
                try {
                    List<SystemServiceInfo> services=ReadOptimizationServices();
                    foreach(SystemServiceInfo s in services)
                        PutRow(table,s,s.Name,s.Display,s.State,s.Startup);
                    status.Text=services.Count+" serviço(s) consultado(s). Leitura concluída.";
                }catch(Exception ex){status.Text="Consulta indisponível: "+ex.Message;}
            };
            AddButton(pane,"Atualizar serviços",18,155,170,delegate{update();});
            AddButton(pane,"Energia",202,155,125,
                delegate{OpenWindowsSettings("ms-settings:powersleep");},false);
            AddButton(pane,"Armazenamento",340,155,160,
                delegate{OpenWindowsSettings("ms-settings:storagesense");},false);
            AddButton(pane,"Windows Update",512,155,175,
                delegate{OpenWindowsSettings("ms-settings:windowsupdate");},false);
            update();
        }
    }
}
