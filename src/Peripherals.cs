using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StorHUB {
    public partial class StorHubForm {
        private readonly HashSet<Keys> testedKeys = new HashSet<Keys>();
        private readonly HashSet<Keys> downKeys = new HashSet<Keys>();
        private KeyboardCanvas keyboardDisplay;
        private Label keyboardCount;
        private bool keyboardCaptureEnabled = true;
        private string peripheralTab = "Teclado";

        private void InitializePeripheralEvents() {
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e) {
                if (active != "Periféricos" || peripheralTab != "Teclado" || !keyboardCaptureEnabled) return;
                Keys key = e.KeyCode;
                testedKeys.Add(key);
                downKeys.Add(key);
                if (keyboardCount != null && !keyboardCount.IsDisposed)
                    keyboardCount.Text = testedKeys.Count + " tecla(s) distintas testadas";
                if (keyboardDisplay != null && !keyboardDisplay.IsDisposed) keyboardDisplay.Invalidate();
            };
            KeyUp += delegate(object sender, KeyEventArgs e) {
                downKeys.Remove(e.KeyCode);
                if (keyboardDisplay != null && !keyboardDisplay.IsDisposed) keyboardDisplay.Invalidate();
            };
        }

        private void ShowPeripherals() {
            peripheralTab = "Teclado";
            Panel main = ModuleCard("Periféricos",
                "Teste teclado, mouse, pixels e saída estéreo. Nada é modificado no Windows.", 620);
            string[] tabs = { "Teclado", "Mouse", "Tela", "Alto-falantes", "Microfone", "Webcam", "Controle" };
            int x = 14;
            Panel content = new Panel {
                Left=13, Top=56, Width=Math.Max(350,main.Width - 26),
                Height=main.Height - 64, BackColor=Color.FromArgb(17,23,39)
            };
            main.Controls.Add(content);
            Dictionary<string,Button> nav=new Dictionary<string,Button>();
            foreach(string tab in tabs) {
                int w=tab=="Alto-falantes" ? 123 : (tab=="Microfone" ? 110 : 88);
                Button button = new Button {
                    Left=x, Top=9, Width=w, Height=37, Text=tab,
                    FlatStyle=FlatStyle.Flat, BackColor=Side, ForeColor=Muted,
                    Font=bodyFont, Cursor=Cursors.Hand
                };
                button.FlatAppearance.BorderSize=0;
                nav[tab]=button;
                main.Controls.Add(button);
                button.Click += delegate {
                    peripheralTab = tab;
                    foreach(KeyValuePair<string,Button> n in nav) {
                        n.Value.ForeColor=n.Key==tab ? TextMain : Muted;
                        n.Value.BackColor=n.Key==tab ? Color.FromArgb(28,50,86) : Side;
                    }
                    content.Controls.Clear();
                    keyboardDisplay=null; keyboardCount=null;
                    DrawPeripheralTab(content,tab);
                };
                x+=w+3;
            }
            nav["Teclado"].PerformClick();
        }

        private void DrawPeripheralTab(Panel p, string tab) {
            if(tab=="Teclado") {
                AddText(p,"Aperte as teclas do teclado, uma por vez. Teclas detectadas ficam verdes.",
                    18,13,p.Width-33,42,10,FontStyle.Regular,Muted);
                Button reset=AddButton(p,"Recomeçar",Math.Max(16,p.Width-153),65,134,
                    delegate{testedKeys.Clear();downKeys.Clear();if(keyboardDisplay!=null)keyboardDisplay.Invalidate();if(keyboardCount!=null)keyboardCount.Text="0 teclas distintas testadas";},false);
                Button enabled=AddButton(p,keyboardCaptureEnabled?"Teste ligado":"Teste desligado",18,65,170,
                    delegate{keyboardCaptureEnabled=!keyboardCaptureEnabled;if(!enabled.IsDisposed){enabled.Text=keyboardCaptureEnabled?"Teste ligado":"Teste desligado";}},true);
                keyboardDisplay=new KeyboardCanvas(testedKeys,downKeys);
                keyboardDisplay.SetBounds(12,125,Math.Max(740,p.Width-24),320);
                p.Controls.Add(keyboardDisplay);
                keyboardCount=AddText(p,testedKeys.Count+" tecla(s) distintas testadas",
                    16,453,p.Width-32,36,12,FontStyle.Bold);
                AddText(p,"Algumas teclas reservadas pelo Windows podem não chegar ao aplicativo. O contador registra teclas diferentes.",
                    16,496,p.Width-32,45,8,FontStyle.Regular,Muted);
            }else if(tab=="Mouse") {
                AddText(p,"Clique dentro da área de teste e use os botões, a rolagem e o movimento.",
                    18,16,p.Width-30,40,11,FontStyle.Regular,Muted);
                Label status=AddText(p,"Esquerdo: 0   Direito: 0   Meio: 0   Rolagem: 0   Movimento: 0",
                    20,68,p.Width-40,36,11,FontStyle.Bold);
                Panel target=new Panel {
                    Left=18,Top=125,Width=Math.Max(340,p.Width-36),Height=330,
                    BackColor=Color.FromArgb(27,39,62),TabStop=true
                };
                p.Controls.Add(target);
                AddText(target,"ÁREA DE TESTE DO MOUSE",22,12,target.Width-44,35,12,FontStyle.Bold,TextMain);
                int left=0,right=0,middle=0,wheel=0,move=0;
                Action update=delegate {
                    status.Text="Esquerdo: "+left+"    Direito: "+right+"    Meio: "+middle+
                                "    Rolagem: "+wheel+"    Movimento: "+move;
                };
                target.MouseDown+=delegate(object s, MouseEventArgs e) {
                    if(e.Button==MouseButtons.Left) left++;
                    if(e.Button==MouseButtons.Right) right++;
                    if(e.Button==MouseButtons.Middle) middle++;
                    target.Focus();update();
                };
                target.MouseWheel+=delegate(object s, MouseEventArgs e) {wheel+=Math.Sign(e.Delta);update();};
                target.MouseMove+=delegate(object s, MouseEventArgs e) {move++;if(move%15==0)update();};
                AddButton(p,"Zerar contadores",18,469,180,delegate{left=0;right=0;middle=0;wheel=0;move=0;update();},false);
                AddText(p,"Coloque o cursor sobre a área azul para testar a rolagem.",212,469,p.Width-230,39,9,FontStyle.Regular,Muted);
            }else if(tab=="Tela") {
                AddText(p,"Verifique pixels mortos, manchas e uniformidade da imagem.",
                    20,20,p.Width-40,50,12,FontStyle.Bold);
                AddText(p,"As telas preta, branca, vermelha, verde e azul são exibidas uma por vez em tela cheia. "+
                    "Clique ou pressione Espaço para avançar. Escape fecha imediatamente.",
                    20,88,p.Width-40,103,11,FontStyle.Regular,Muted);
                AddButton(p,"Iniciar teste da tela",20,224,230,delegate{OpenScreenColors();});
            }else if(tab=="Alto-falantes") {
                AddText(p,"Saída de áudio estéreo",20,16,p.Width-40,50,14,FontStyle.Bold);
                AddText(p,"Teste o canal esquerdo e depois o direito. O resultado depende de seu dispositivo de áudio.",
                    20,72,p.Width-40,60,10,FontStyle.Regular,Muted);
                Label status=AddText(p,"Nenhum tom foi reproduzido.",20,148,p.Width-45,46,10,FontStyle.Regular,Muted);
                AddButton(p,"Canal esquerdo",20,209,185,async delegate{await PlayTone(0,status);});
                AddButton(p,"Canal direito",216,209,185,async delegate{await PlayTone(1,status);});
                AddText(p,"Volume moderado. Não testa microfones nem o equilíbrio físico dos alto-falantes.",
                    20,290,p.Width-45,65,9,FontStyle.Regular,Muted);
            }else {
                AddText(p,tab+" — teste completo em desenvolvimento",
                    20,26,p.Width-40,58,15,FontStyle.Bold);
                string detail = tab == "Controle"
                    ? "Os testes de botões, analógicos, drift, gatilhos e vibração ainda não estão implementados."
                    : tab == "Microfone"
                    ? "A captura e reprodução do microfone ainda não estão implementadas."
                    : "A visualização da webcam ainda não está implementada.";
                AddText(p,detail,20,94,p.Width-40,86,11,FontStyle.Regular,Muted);
                if(tab=="Microfone")
                    AddButton(p,"Abrir configurações de som",20,205,250,
                        delegate{OpenWindowsSettings("ms-settings:sound");},false);
                if(tab=="Webcam")
                    AddButton(p,"Abrir configurações da câmera",20,205,270,
                        delegate{OpenWindowsSettings("ms-settings:privacy-webcam");},false);
            }
        }

        private static void OpenScreenColors() {
            Color[] palette={Color.Black,Color.White,Color.Red,Color.Green,Color.Blue};
            string[] names={"PRETO","BRANCO","VERMELHO","VERDE","AZUL"};
            int index=0;
            Form test=new Form {
                FormBorderStyle=FormBorderStyle.None,
                WindowState=FormWindowState.Maximized,
                TopMost=true,BackColor=palette[0],KeyPreview=true,
                Text="StorHUB - Teste de pixels",StartPosition=FormStartPosition.CenterScreen
            };
            Label tip=new Label {
                Dock=DockStyle.Top,Height=80,TextAlign=ContentAlignment.MiddleCenter,
                BackColor=Color.Transparent,ForeColor=Color.Gray,
                Font=new Font("Segoe UI",15,FontStyle.Bold)
            };
            test.Controls.Add(tip);
            Action next=delegate {
                index=(index+1)%palette.Length;test.BackColor=palette[index];
                tip.Text=names[index]+" — clique ou Espaço para avançar • Esc para sair";
                tip.ForeColor=index==1 ? Color.Black : Color.White;
            };
            tip.Text="PRETO — clique ou Espaço para avançar • Esc para sair";
            test.KeyDown+=delegate(object s,KeyEventArgs e) {
                if(e.KeyCode==Keys.Escape) {test.Close();e.Handled=true;}
                else if(e.KeyCode==Keys.Space || e.KeyCode==Keys.Enter){next();e.Handled=true;}
            };
            test.Click+=delegate {next();};
            tip.Click+=delegate {next();};
            test.ShowDialog();
            test.Dispose();
        }

        private static async Task PlayTone(int channel,Label status) {
            if(status.IsDisposed) return;
            status.Text="Reproduzindo canal "+(channel==0?"esquerdo":"direito")+"...";
            try {
                await Task.Run(() => {
                    const int sampleRate=22050;
                    const int samples=sampleRate/2;
                    const int channels=2;
                    const int bytesPerSample=2;
                    byte[] wave;
                    using(MemoryStream ms=new MemoryStream())
                    using(BinaryWriter w=new BinaryWriter(ms,Encoding.ASCII,true)) {
                        int bytes=samples*channels*bytesPerSample;
                        w.Write(Encoding.ASCII.GetBytes("RIFF"));
                        w.Write(36+bytes);
                        w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                        w.Write(16);w.Write((short)1);w.Write((short)channels);
                        w.Write(sampleRate);w.Write(sampleRate*channels*bytesPerSample);
                        w.Write((short)(channels*bytesPerSample));w.Write((short)16);
                        w.Write(Encoding.ASCII.GetBytes("data"));w.Write(bytes);
                        for(int i=0;i<samples;i++) {
                            double envelope=Math.Sin(Math.PI*i/samples);
                            short tone=(short)(Math.Sin(2*Math.PI*440*i/sampleRate)*6000*envelope);
                            w.Write((short)(channel==0 ? tone : 0));
                            w.Write((short)(channel==1 ? tone : 0));
                        }
                        w.Flush();wave=ms.ToArray();
                    }
                    using(MemoryStream stream=new MemoryStream(wave))
                    using(SoundPlayer player=new SoundPlayer(stream)) player.PlaySync();
                });
                if(!status.IsDisposed)status.Text="Tom do canal "+(channel==0?"esquerdo":"direito")+" concluído.";
            }catch(Exception ex){if(!status.IsDisposed)status.Text="Áudio indisponível: "+ex.Message;}
        }

        private class KeyboardCanvas:Control {
            private class Cap {
                public string Text;public Keys Key;public RectangleF Rect;
            }
            private readonly List<Cap> caps=new List<Cap>();
            private readonly HashSet<Keys> seen;
            private readonly HashSet<Keys> pressed;
            private const float Unit=42;
            private const float Gap=4;
            public KeyboardCanvas(HashSet<Keys> seen,HashSet<Keys> pressed) {
                this.seen=seen;this.pressed=pressed;
                SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|
                    ControlStyles.OptimizedDoubleBuffer,true);
                BackColor=Color.FromArgb(17,23,39);
                BuildKeys();
            }
            private Keys MapKey(string text) {
                if(text.Length==1) {
                    char c=char.ToUpperInvariant(text[0]);
                    if(c>='A'&&c<='Z')return (Keys)((int)Keys.A+(c-'A'));
                    if(c>='0'&&c<='9')return (Keys)((int)Keys.D0+(c-'0'));
                }
                if(text.StartsWith("F") && text.Length>1) {
                    int num;
                    if(int.TryParse(text.Substring(1),out num) && num>=1 && num<=12)
                        return (Keys)((int)Keys.F1+num-1);
                }
                switch(text) {
                    case "Esc":return Keys.Escape;
                    case "Tab":return Keys.Tab;
                    case "Caps":return Keys.Capital;
                    case "Shift":return Keys.ShiftKey;
                    case "Ctrl":return Keys.ControlKey;
                    case "Alt":case "AltGr":return Keys.Menu;
                    case "Espaço":return Keys.Space;
                    case "Enter":return Keys.Return;
                    case "Bksp":return Keys.Back;
                    case "Ins":return Keys.Insert;
                    case "Del":return Keys.Delete;
                    case "Home":return Keys.Home;
                    case "End":return Keys.End;
                    case "PgUp":return Keys.Prior;
                    case "PgDn":return Keys.Next;
                    case "↑":return Keys.Up;
                    case "↓":return Keys.Down;
                    case "←":return Keys.Left;
                    case "→":return Keys.Right;
                    case "Win":return Keys.LWin;
                    case "PrtSc":return Keys.PrintScreen;
                    case "Scroll":return Keys.Scroll;
                    case "Pause":return Keys.Pause;
                    default:return Keys.None;
                }
            }
            private void Add(string t,float x,int row,float units=1) {
                caps.Add(new Cap {Text=t,Key=MapKey(t),
                    Rect=new RectangleF(x*Unit+4,row*49+5,Math.Max(Unit,Unit*units-Gap),38)});
            }
            private void AddRow(int row,string[] values) {
                float x=0;
                foreach(string spec in values) {
                    string[] parts=spec.Split(':');
                    float width=parts.Length==2?float.Parse(parts[1],System.Globalization.CultureInfo.InvariantCulture):1;
                    Add(parts[0],x,row,width);
                    x+=width;
                }
            }
            private void BuildKeys() {
                AddRow(0,new[]{"Esc:1.2","F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12","PrtSc","Scroll","Pause"});
                AddRow(1,new[]{"~","1","2","3","4","5","6","7","8","9","0","-","=","Bksp:1.6","Ins","Home","PgUp"});
                AddRow(2,new[]{"Tab:1.45","Q","W","E","R","T","Y","U","I","O","P","[","]","\\:1.3","Del","End","PgDn"});
                AddRow(3,new[]{"Caps:1.75","A","S","D","F","G","H","J","K","L",";","'","Enter:2.05"});
                AddRow(4,new[]{"Shift:2.15","Z","X","C","V","B","N","M",",",".","/","Shift:2.15","↑"});
                AddRow(5,new[]{"Ctrl:1.35","Win","Alt","Espaço:5.6","AltGr","Win","Ctrl","←","↓","→"});
            }
            protected override void OnPaint(PaintEventArgs e) {
                base.OnPaint(e);
                e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                float maxWidth=735;
                float scale=Math.Min(1.30f,Math.Max(.68f,(Width-12)/maxWidth));
                e.Graphics.ScaleTransform(scale,scale);
                using(Font font=new Font("Segoe UI",8,FontStyle.Regular))
                using(Brush label=new SolidBrush(Color.FromArgb(229,236,249)))
                using(Pen border=new Pen(Color.FromArgb(40,51,78))) {
                    foreach(Cap key in caps) {
                        Color bg=pressed.Contains(key.Key)&&key.Key!=Keys.None
                            ? Color.FromArgb(69,175,147) :
                            seen.Contains(key.Key)&&key.Key!=Keys.None
                            ? Color.FromArgb(28,100,76) : Color.FromArgb(35,44,67);
                        using(Brush brush=new SolidBrush(bg))
                            e.Graphics.FillRectangle(brush,key.Rect);
                        e.Graphics.DrawRectangle(border,
                            Rectangle.Round(key.Rect));
                        SizeF size=e.Graphics.MeasureString(key.Text,font);
                        e.Graphics.DrawString(key.Text,font,label,
                            key.Rect.X+Math.Max(3,(key.Rect.Width-size.Width)/2),
                            key.Rect.Y+(key.Rect.Height-size.Height)/2);
                    }
                }
            }
        }
    }
}
