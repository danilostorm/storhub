using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StorHUB {
    public partial class StorHubForm {
        private class DriverEntry {
            public string Name;
            public string Manufacturer;
            public string DeviceId;
            public int ErrorCode;
            public string Status;
        }
        private static List<DriverEntry> QueryDevices() {
            List<DriverEntry> list = new List<DriverEntry>();
            using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                "SELECT Name,Manufacturer,PNPDeviceID,ConfigManagerErrorCode,Status FROM Win32_PnPEntity")) {
                foreach (ManagementObject o in s.Get()) {
                    try {
                        string name = Convert.ToString(o["Name"]);
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        object code = o["ConfigManagerErrorCode"];
                        list.Add(new DriverEntry {
                            Name = name,
                            Manufacturer = Convert.ToString(o["Manufacturer"]),
                            DeviceId = Convert.ToString(o["PNPDeviceID"]),
                            Status = Convert.ToString(o["Status"]),
                            ErrorCode = code == null ? -1 : Convert.ToInt32(code)
                        });
                    } catch { }
                }
            }
            return list.OrderByDescending(x => x.ErrorCode > 0)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        private static string DeviceIssue(int code) {
            if (code == 0) return "OK";
            if (code < 0) return "Não verificado";
            if (code == 22) return "Desabilitado (22)";
            if (code == 28) return "Sem driver (28)";
            if (code == 10) return "Falha ao iniciar (10)";
            if (code == 43) return "Dispositivo interrompido (43)";
            return "Código " + code;
        }
        private void ShowDrivers() {
            Panel pane = ModuleCard("Drivers",
                "Dispositivos Plug and Play detectados pelo Windows • diagnóstico, sem instalações automáticas", 605);
            Label status = AddText(pane, "Lendo dispositivos...", 18, 14, pane.Width - 36, 31,
                9, FontStyle.Regular, Muted);
            TextBox filter = new TextBox {
                Bounds = new Rectangle(18, 51, Math.Max(180, pane.Width - 375), 28),
                BackColor = Bg, ForeColor = TextMain, BorderStyle = BorderStyle.FixedSingle,
                Font = semibold
            };
            pane.Controls.Add(filter);
            Button refresh = AddButton(pane, "Atualizar", Math.Max(190, pane.Width - 340), 47, 120, null);
            AddButton(pane, "Windows Update", Math.Max(325, pane.Width - 210), 47, 188,
                delegate { OpenWindowsSettings("ms-settings:windowsupdate-optionalupdates"); }, false);
            ListView table = NewTable(pane, 142, "Dispositivo", "Fabricante", "Diagnóstico", "ID");
            table.Columns[0].Width = (int)(table.Width * .34);
            table.Columns[1].Width = (int)(table.Width * .20);
            table.Columns[2].Width = (int)(table.Width * .15);
            table.Columns[3].Width = (int)(table.Width * .28);
            List<DriverEntry> entries = new List<DriverEntry>();
            Action update = delegate {
                string query = filter.Text.Trim();
                table.BeginUpdate();
                table.Items.Clear();
                int issues = 0;
                foreach (DriverEntry d in entries) {
                    if (d.ErrorCode > 0) issues++;
                    if (query.Length > 0 &&
                        d.Name.IndexOf(query,StringComparison.CurrentCultureIgnoreCase) < 0 &&
                        d.DeviceId.IndexOf(query,StringComparison.CurrentCultureIgnoreCase) < 0 &&
                        d.Manufacturer.IndexOf(query,StringComparison.CurrentCultureIgnoreCase) < 0 &&
                        DeviceIssue(d.ErrorCode).IndexOf(query,StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                    PutRow(table,d,d.Name,d.Manufacturer,DeviceIssue(d.ErrorCode),d.DeviceId);
                }
                table.EndUpdate();
                status.Text = entries.Count + " dispositivos • " + issues +
                              " com código de erro. Contagem não comprova drivers atualizados.";
            };
            Action load = async delegate {
                if (refresh.IsDisposed || pane.IsDisposed) return;
                refresh.Enabled = false;
                status.Text = "Consultando Plug and Play pelo Windows...";
                try {
                    List<DriverEntry> results = await Task.Run(() => QueryDevices());
                    if (pane.IsDisposed) return;
                    entries = results;
                    update();
                } catch (Exception ex) {
                    if (!pane.IsDisposed) status.Text = "Não foi possível consultar: " + ex.Message;
                } finally { if (!refresh.IsDisposed) refresh.Enabled = true; }
            };
            filter.TextChanged += delegate { update(); };
            refresh.Click += delegate { load(); };
            table.DoubleClick += delegate {
                if (table.SelectedItems.Count == 0) return;
                DriverEntry d = table.SelectedItems[0].Tag as DriverEntry;
                if (d != null) MessageBox.Show(
                    "Dispositivo: " + d.Name + "\nFabricante: " + d.Manufacturer +
                    "\nDiagnóstico: " + DeviceIssue(d.ErrorCode) +
                    "\nID Plug and Play: " + d.DeviceId,
                    "StorHUB - Detalhes do dispositivo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            AddButton(pane, "Exportar lista CSV", 18, 92, 170, delegate {
                if (entries.Count == 0) { ShowError("Não há dispositivos para exportar."); return; }
                using (SaveFileDialog dialog = new SaveFileDialog()) {
                    dialog.Filter = "Arquivo CSV UTF-8 (*.csv)|*.csv";
                    dialog.FileName = "StorHUB-dispositivos.csv";
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    try {
                        StringBuilder sb = new StringBuilder("Dispositivo;Fabricante;Diagnostico;Identificador\r\n");
                        foreach (DriverEntry d in entries) {
                            sb.Append(Csv(d.Name)).Append(';').Append(Csv(d.Manufacturer)).Append(';')
                              .Append(Csv(DeviceIssue(d.ErrorCode))).Append(';').Append(Csv(d.DeviceId))
                              .Append("\r\n");
                        }
                        File.WriteAllText(dialog.FileName, sb.ToString(),new UTF8Encoding(true));
                        MessageBox.Show("Inventário exportado.", "StorHUB");
                    } catch (Exception ex) { ShowError(ex.Message); }
                }
            }, false);
            load();
        }
        private static string Csv(string value) {
            return "\"" + (value ?? "").Replace("\"","\"\"") + "\"";
        }
    }
}
