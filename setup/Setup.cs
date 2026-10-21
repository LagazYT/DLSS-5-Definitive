using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Dlss5Injector;

namespace Dlss5Setup
{
    /// <summary>Instalador/desinstalador do DLSS 5 Definitive (por usuário, sem administrador).</summary>
    static class SetupApp
    {
        public const string AppName = "DLSS 5 Definitive";
        public const string Version = "2.2.1";
        public const string Author = "Lagaz";
        public const string ExeName = "DLSS 5 Definitive.exe";
        public const string UninstName = "Desinstalar.exe";
        public const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DLSS5Definitive";

        [STAThread]
        static void Main(string[] args)
        {
            L.Init();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool uninstall = args.Any(a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase));
            bool update = args.Any(a => a.Equals("/update", StringComparison.OrdinalIgnoreCase));
            int waitPid = 0;
            foreach (string a in args)
                if (a.StartsWith("/waitpid:", StringComparison.OrdinalIgnoreCase)) int.TryParse(a.Substring(9), out waitPid);
            Form f = uninstall ? (Form)new UninstallForm() : update ? (Form)new UpdateForm(waitPid) : new SetupForm();
            Application.Run(f);
        }

        /// <summary>Copia o app, cria atalhos e registra em Aplicativos instalados.</summary>
        public static void InstallTo(string target, bool desktop, Action<string, int> status)
        {
            status(L.T("Copiando arquivos..."), 15);
            Directory.CreateDirectory(target);
            string exe = Path.Combine(target, ExeName);
            Extract("app.exe", exe);
            status(L.T("Copiando arquivos..."), 45);
            Extract("readme.txt", Path.Combine(target, "LEIA-ME.txt"));
            string un = Path.Combine(target, UninstName);
            if (!string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(un), StringComparison.OrdinalIgnoreCase))
                File.Copy(Application.ExecutablePath, un, true);
            // Uma instalação nunca é portable
            string pini = Path.Combine(target, "portable.ini");
            if (File.Exists(pini)) File.Delete(pini);

            status(L.T("Criando atalhos..."), 65);
            Shortcut(StartMenuLnk, exe, "DLSS 5 Neural Rendering e Multi Frame Generation — por " + Author);
            if (desktop) Shortcut(DesktopLnk, exe, "DLSS 5 Definitive — por " + Author);
            else if (File.Exists(DesktopLnk)) File.Delete(DesktopLnk);

            status(L.T("Registrando em Aplicativos instalados..."), 85);
            long size = 0;
            foreach (string f in Directory.GetFiles(target)) size += new FileInfo(f).Length;
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegPath))
            {
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", Author);
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", target);
                k.SetValue("UninstallString", "\"" + un + "\" /uninstall");
                k.SetValue("QuietUninstallString", "\"" + un + "\" /uninstall");
                k.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            }
            status(L.T("Concluído"), 100);
        }

        public static string DefaultDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName); }
        }

        public static string InstalledDir()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegPath))
                    if (k != null) return Convert.ToString(k.GetValue("InstallLocation"));
            }
            catch { }
            return null;
        }

        public static string StartMenuLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"); } }
        public static string DesktopLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"); } }
        public static string DataDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DLSS5Definitive"); } }

        public static void Shortcut(string lnk, string target, string description)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(target) });
            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { description });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            Marshal.FinalReleaseComObject(sc);
            Marshal.FinalReleaseComObject(sh);
        }

        /// <summary>Fecha o app se estiver aberto a partir da pasta indicada (pergunta antes).</summary>
        public static bool CloseRunningApp(IWin32Window owner, string dir)
        {
            Process[] ps = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName))
                .Where(p => { try { return p.MainModule.FileName.StartsWith(dir, StringComparison.OrdinalIgnoreCase); } catch { return false; } })
                .ToArray();
            if (ps.Length == 0) return true;
            if (MessageBox.Show(owner, L.T("O ") + AppName + L.T(" está aberto. Fechar o programa para continuar?"), AppName,
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return false;
            foreach (Process p in ps)
            {
                try
                {
                    p.CloseMainWindow();
                    if (!p.WaitForExit(4000)) { p.Kill(); p.WaitForExit(3000); }
                }
                catch { }
            }
            return true;
        }

        public static void Extract(string resource, string dest)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (s == null) throw new Exception("Recurso ausente no instalador: " + resource);
                string tmp = dest + ".new";
                using (FileStream f = File.Create(tmp)) s.CopyTo(f);
                if (File.Exists(dest)) { File.SetAttributes(dest, FileAttributes.Normal); File.Delete(dest); }
                File.Move(tmp, dest);
            }
        }
    }

    /// <summary>Janela base com o cabeçalho (logo, nome, autor) no tema do app.</summary>
    class SetupWindow : Form
    {
        protected string Subtitle = "";

        public SetupWindow(int w, int h)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = SetupApp.AppName;
            Icon = Theme.AppIcon();
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9f);
            ClientSize = new Size(w, h);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitleBar(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            Theme.FillRound(g, Theme.Accent, new RectangleF(24, 22, 52, 52), 13);
            using (Font f = Theme.Title(22f))
                Theme.Draw(g, "5", f, Theme.Bg, new Rectangle(24, 22, 52, 52), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (Font f = Theme.Title(17f))
                Theme.Draw(g, SetupApp.AppName, f, Theme.Text, new Rectangle(90, 18, 400, 32), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            using (Font f = Theme.F(9f))
            using (Font fb = Theme.F(9f, FontStyle.Bold))
            {
                string s = Subtitle + L.T("  ·  versão ") + SetupApp.Version + L.T("  ·  criado por ");
                Theme.Draw(g, s, f, Theme.Muted, new Rectangle(91, 50, 400, 22), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                int w = TextRenderer.MeasureText(s, f).Width;
                Theme.Draw(g, SetupApp.Author, fb, Theme.Accent, new Rectangle(85 + w, 50, 100, 22), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        protected Label AddLabel(Control parent, string text, int x, int y, int w, int h, Font f, Color c)
        {
            Label l = new Label();
            l.Text = text;
            l.SetBounds(x, y, w, h);
            l.Font = f;
            l.ForeColor = c;
            l.BackColor = parent.BackColor;
            parent.Controls.Add(l);
            return l;
        }
    }

    class SetupForm : SetupWindow
    {
        Label lblDir, lblStatus;
        ToggleSwitch tgDesktop, tgLaunch;
        ModernButton btnInstall, btnCancel, btnDir;
        ThinProgress bar;
        string dir;
        bool done, working;

        public SetupForm() : base(600, 420)
        {
            Subtitle = L.T("Instalador");
            string prev = SetupApp.InstalledDir();
            dir = !string.IsNullOrEmpty(prev) ? prev : SetupApp.DefaultDir;
            bool update = !string.IsNullOrEmpty(prev) && File.Exists(Path.Combine(prev, SetupApp.ExeName));

            Card c = new Card(null, update ? L.T("Atualizar instalação") : L.T("Instalar no computador"));
            c.SetBounds(24, 96, 552, 212);
            Controls.Add(c);
            Place(c, new FieldLabel(L.T("Pasta de instalação")), 18, 48);
            lblDir = AddLabel(c, dir, 18, 70, 420, 34, Theme.F(9f), Theme.Text);
            lblDir.AutoEllipsis = true;
            lblDir.TextAlign = ContentAlignment.MiddleLeft;
            btnDir = new ModernButton(L.T("Alterar"), null, ButtonKind.Ghost);
            btnDir.SetBounds(450, 72, 84, 30);
            btnDir.Click += delegate { ChooseDir(); };
            c.Controls.Add(btnDir);
            tgDesktop = new ToggleSwitch(L.T("Criar atalho na Área de Trabalho"), true);
            tgDesktop.SetBounds(18, 114, 500, 30);
            c.Controls.Add(tgDesktop);
            tgLaunch = new ToggleSwitch(L.T("Abrir o ") + SetupApp.AppName + L.T(" ao terminar"), true);
            tgLaunch.SetBounds(18, 146, 500, 30);
            c.Controls.Add(tgLaunch);
            AddLabel(c, L.T("Não precisa de administrador · Windows 10/11 64-bit · atalho no Menu Iniciar · desinstalador incluso"),
                18, 182, 520, 20, Theme.F(8f), Theme.Dim);

            lblStatus = AddLabel(this, "", 24, 318, 552, 22, Theme.F(9f), Theme.Muted);
            bar = new ThinProgress();
            bar.SetBounds(24, 344, 552, 6);
            Controls.Add(bar);

            btnInstall = new ModernButton(update ? L.T("  Atualizar") : L.T("  Instalar"), Theme.GDownload, ButtonKind.Primary);
            btnInstall.SetBounds(376, 360, 200, 46);
            btnInstall.Click += delegate { if (done) Finish(); else Install(); };
            Controls.Add(btnInstall);
            btnCancel = new ModernButton(L.T("Cancelar"), null, ButtonKind.Secondary);
            btnCancel.SetBounds(254, 360, 110, 46);
            btnCancel.Click += delegate { Close(); };
            Controls.Add(btnCancel);
            FormClosing += delegate(object s, FormClosingEventArgs e) { if (working) e.Cancel = true; };
        }

        static void Place(Control parent, Control c, int x, int y)
        {
            c.Location = new Point(x, y);
            parent.Controls.Add(c);
        }

        void ChooseDir()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = L.T("Escolha onde instalar (uma pasta \"") + SetupApp.AppName + L.T("\" será criada dentro dela)");
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string p = d.SelectedPath;
                if (!Path.GetFileName(p.TrimEnd('\\')).Equals(SetupApp.AppName, StringComparison.OrdinalIgnoreCase))
                    p = Path.Combine(p, SetupApp.AppName);
                dir = p;
                lblDir.Text = dir;
            }
        }

        void Install()
        {
            if (!SetupApp.CloseRunningApp(this, dir)) return;
            working = true;
            btnInstall.Enabled = btnCancel.Enabled = btnDir.Enabled = false;
            bool desktop = tgDesktop.Checked;
            string target = dir;
            Thread t = new Thread(delegate()
            {
                Exception err = null;
                try { DoInstall(target, desktop); }
                catch (Exception ex) { err = ex; }
                BeginInvoke(new Action(delegate
                {
                    working = false;
                    if (err != null)
                    {
                        lblStatus.ForeColor = Theme.Danger;
                        lblStatus.Text = L.T("Erro: ") + err.Message;
                        btnInstall.Enabled = btnCancel.Enabled = btnDir.Enabled = true;
                        MessageBox.Show(this, err.Message + (err is UnauthorizedAccessException ? L.T("\r\n\r\nEscolha uma pasta sua (ex.: a padrão) ou execute como administrador.") : ""),
                            SetupApp.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    done = true;
                    bar.Value = 100;
                    lblStatus.ForeColor = Theme.Accent;
                    lblStatus.Text = "✔ " + SetupApp.AppName + " instalado em " + target;
                    btnInstall.Text = L.T("  Concluir");
                    btnInstall.Glyph = Theme.GCheck;
                    btnInstall.Enabled = true;
                    btnInstall.Invalidate();
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void Status(string s, int pct)
        {
            BeginInvoke(new Action(delegate { lblStatus.Text = s; bar.Value = pct; }));
        }

        void DoInstall(string target, bool desktop)
        {
            SetupApp.InstallTo(target, desktop, Status);
        }

        void Finish()
        {
            if (tgLaunch.Checked)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(dir, SetupApp.ExeName));
                    psi.WorkingDirectory = dir;
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                catch { }
            }
            Close();
        }
    }

    /// <summary>
    /// Atualização automática chamada pelo próprio app ("Atualizar agora"): espera o app fechar, instala por cima na
    /// mesma pasta (mantendo o atalho da Área de Trabalho só se ele já existia) e reabre o app. Sem perguntas.
    /// </summary>
    class UpdateForm : SetupWindow
    {
        readonly int waitPid;
        Label lblStatus;
        ThinProgress bar;

        public UpdateForm(int waitPid) : base(520, 190)
        {
            this.waitPid = waitPid;
            Subtitle = L.T("Atualizando");
            lblStatus = AddLabel(this, "Aguardando o " + SetupApp.AppName + " fechar...", 24, 104, 472, 24, Theme.F(9.5f), Theme.Text);
            bar = new ThinProgress();
            bar.SetBounds(24, 140, 472, 6);
            Controls.Add(bar);
            Shown += delegate
            {
                Thread t = new Thread(Run);
                t.IsBackground = true;
                t.Start();
            };
        }

        void Status(string s, int pct)
        {
            try { BeginInvoke(new Action(delegate { lblStatus.Text = s; bar.Value = pct; })); } catch { }
        }

        void Run()
        {
            string dir = SetupApp.InstalledDir();
            if (string.IsNullOrEmpty(dir)) dir = SetupApp.DefaultDir;
            try
            {
                if (waitPid > 0)
                {
                    try { Process.GetProcessById(waitPid).WaitForExit(30000); } catch { }
                }
                // Outras janelas do app abertas a partir da pasta instalada também precisam fechar
                foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(SetupApp.ExeName)))
                {
                    try
                    {
                        if (!p.MainModule.FileName.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) continue;
                        p.CloseMainWindow();
                        if (!p.WaitForExit(5000)) p.Kill();
                    }
                    catch { }
                }
                // O exe pode ficar travado alguns instantes depois que o processo termina
                for (int i = 0; ; i++)
                {
                    try { SetupApp.InstallTo(dir, File.Exists(SetupApp.DesktopLnk), Status); break; }
                    catch (IOException) { if (i >= 10) throw; Thread.Sleep(700); }
                }
                Status(L.T("✔ Atualizado para a versão ") + SetupApp.Version + L.T(" — abrindo..."), 100);
                ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(dir, SetupApp.ExeName), "/updated");
                psi.WorkingDirectory = dir;
                psi.UseShellExecute = true;
                Process.Start(psi);
                Thread.Sleep(1200);
                BeginInvoke(new Action(Close));
            }
            catch (Exception ex)
            {
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        lblStatus.ForeColor = Theme.Danger;
                        lblStatus.Text = L.T("Erro: ") + ex.Message;
                        MessageBox.Show(this, L.T("Não foi possível atualizar:\r\n") + ex.Message, SetupApp.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Close();
                    }));
                }
                catch { }
            }
        }
    }

    class UninstallForm : SetupWindow
    {
        ToggleSwitch tgData;
        ModernButton btnGo, btnCancel;
        Label lblStatus;
        bool done;

        public UninstallForm() : base(600, 360)
        {
            Subtitle = L.T("Desinstalar");
            Card c = new Card(null, L.T("Remover do computador"));
            c.SetBounds(24, 96, 552, 176);
            Controls.Add(c);
            AddLabel(c, L.T("Remove o programa, os atalhos e a entrada em Aplicativos instalados.\r\n") +
                        L.T("Os jogos em que você instalou o DLSS 5 não são alterados — para restaurá-los, use \"Remover\" no app antes."),
                18, 46, 520, 52, Theme.F(9f), Theme.Muted);
            tgData = new ToggleSwitch(L.T("Apagar também configurações, biblioteca, capas e downloads"), false);
            tgData.SetBounds(18, 104, 520, 30);
            c.Controls.Add(tgData);
            AddLabel(c, SetupApp.DataDir, 66, 134, 470, 20, Theme.F(8f), Theme.Dim);

            lblStatus = AddLabel(this, "", 24, 284, 552, 22, Theme.F(9f), Theme.Muted);
            btnGo = new ModernButton(L.T("  Desinstalar"), Theme.GDelete, ButtonKind.Primary);
            btnGo.SetBounds(376, 300, 200, 46);
            btnGo.Click += delegate { if (done) Close(); else Uninstall(); };
            Controls.Add(btnGo);
            btnCancel = new ModernButton(L.T("Cancelar"), null, ButtonKind.Secondary);
            btnCancel.SetBounds(254, 300, 110, 46);
            btnCancel.Click += delegate { Close(); };
            Controls.Add(btnCancel);
        }

        void Uninstall()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            if (!SetupApp.CloseRunningApp(this, dir)) return;
            try
            {
                foreach (string l in new[] { SetupApp.StartMenuLnk, SetupApp.DesktopLnk })
                    if (File.Exists(l)) File.Delete(l);
                Registry.CurrentUser.DeleteSubKeyTree(SetupApp.RegPath, false);
                foreach (string f in new[] { SetupApp.ExeName, "LEIA-ME.txt" })
                {
                    string p = Path.Combine(dir, f);
                    if (File.Exists(p)) { File.SetAttributes(p, FileAttributes.Normal); File.Delete(p); }
                }
                if (tgData.Checked && Directory.Exists(SetupApp.DataDir)) Directory.Delete(SetupApp.DataDir, true);
                // O desinstalador está em uso: ele se apaga (e a pasta, só se ficar vazia) quando esta janela fechar.
                FormClosed += delegate { SelfDelete(dir); };
                done = true;
                lblStatus.ForeColor = Theme.Accent;
                lblStatus.Text = "✔ " + SetupApp.AppName + " foi removido.";
                btnGo.Text = L.T("  Fechar");
                btnGo.Glyph = Theme.GCheck;
                btnGo.Invalidate();
                btnCancel.Visible = false;
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = Theme.Danger;
                lblStatus.Text = L.T("Erro: ") + ex.Message;
            }
        }

        static void SelfDelete(string dir)
        {
            // Tenta por até ~20 s (o processo ainda está terminando quando o cmd começa).
            string self = Application.ExecutablePath;
            string cmd = "/c for /l %i in (1,1,20) do (ping 127.0.0.1 -n 2 > nul & del /f /q \"" + self + "\" 2> nul & if not exist \"" + self +
                         "\" (rmdir \"" + dir + "\" 2> nul & exit))";
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmd);
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Path.GetTempPath();   // o cmd não pode "segurar" a pasta que vai apagar
            Process.Start(psi);
        }
    }
}
