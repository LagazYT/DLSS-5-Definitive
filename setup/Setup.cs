using System;
using System.Collections.Generic;
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
        public const string Version = "2.3";
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
            if (uninstall && args.Any(a => a.Equals("/silent", StringComparison.OrdinalIgnoreCase)))
            {
                try { UninstallForm.Silent(); } catch { }
                return;
            }
            Form f = uninstall ? (Form)new UninstallForm() : update ? (Form)new UpdateForm(waitPid) : new SetupForm();
            Application.Run(f);
        }

        /// <summary>
        /// Copia o app, os arquivos do DLSS (runtime do DLSS 5 e Streamline) para &lt;pasta&gt;\components, cria atalhos e
        /// registra em Aplicativos instalados. nrSrc/slSrc podem ser null (o app procura/baixa depois).
        /// </summary>
        public static void InstallTo(string target, bool desktop, Action<string, int> status, string nrSrc, string slSrc)
        {
            status(L.T("Copiando arquivos..."), 15);
            Directory.CreateDirectory(target);
            string exe = Path.Combine(target, ExeName);
            Extract("app.exe", exe);
            status(L.T("Copiando arquivos..."), 35);
            Extract("readme.txt", Path.Combine(target, "LEIA-ME.txt"));
            string comp = Path.Combine(target, "components");
            if (HasEmbeddedRuntime)
            {
                // Instalador montado com o zip da runtime: o zip fica na pasta de instalação e a dll vai para components
                status(L.T("Copiando a runtime do DLSS 5..."), 40);
                string zip = Path.Combine(target, RuntimeZipName);
                if (!File.Exists(zip) || new FileInfo(zip).Length != EmbeddedRuntimeLength) Extract(RuntimeResource, zip);
                try { NrRuntime.ExtractFromZip(zip, comp); nrSrc = null; } catch { }
            }
            if (!string.IsNullOrEmpty(nrSrc) && NrRuntime.IsRuntimeSource(nrSrc))
            {
                status(L.T("Copiando a runtime do DLSS 5..."), 45);
                try { NrRuntime.ImportAny(nrSrc, comp); } catch { }
            }
            if (Installer.IsStreamlineDir(slSrc))
            {
                status(L.T("Copiando o Streamline..."), 55);
                Installer.ImportStreamline(slSrc, comp);
            }
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
            foreach (string f in Directory.GetFiles(target, "*", SearchOption.AllDirectories)) size += new FileInfo(f).Length;
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegPath))
            {
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", Author);
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", target);
                k.SetValue("UninstallString", "\"" + un + "\" /uninstall");
                k.SetValue("QuietUninstallString", "\"" + un + "\" /uninstall /silent");
                k.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            }
            status(L.T("Concluído"), 100);
        }

        /// <summary>Runtime do DLSS 5 neste PC: a já instalada, a usada pelo app antes, ou procura (Downloads, Área de Trabalho...).</summary>
        public static string FindNrSource(string target)
        {
            string own = Path.Combine(target, "components", NrRuntime.FileName);
            if (NrRuntime.IsRuntimeFile(own)) return own;
            string s = null;
            try { s = Settings.Get("nr"); } catch { }
            if (!string.IsNullOrEmpty(s) && NrRuntime.IsRuntimeFile(s)) return s;
            string found = null;
            try { found = NrRuntime.FindCandidate(null, Path.Combine(DataDir, "components")); } catch { }
            if (found != null) return found;
            // Zip com a runtime ao lado do instalador, na pasta de instalação, em Downloads ou na Área de Trabalho
            try
            {
                return NrRuntime.FindZip(Path.GetDirectoryName(Application.ExecutablePath), target, Util.Downloads,
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            }
            catch { return null; }
        }

        /// <summary>Streamline SDK neste PC: o já instalado, o usado pelo app antes, uma pasta streamline-sdk* em Downloads ou o já baixado.</summary>
        public static string FindSlSource(string target)
        {
            List<string> c = new List<string>();
            c.Add(Path.Combine(target, "components", "streamline"));
            try { c.Add(Settings.Get("streamline")); } catch { }
            c.Add(Path.Combine(DataDir, "components", "streamline"));
            try { c.AddRange(Directory.GetDirectories(Util.Downloads, "streamline-sdk*")); } catch { }
            try
            {
                string cache = Path.Combine(DataDir, "cache", "streamline");
                if (Directory.Exists(cache)) c.AddRange(Directory.GetDirectories(cache).OrderByDescending(x => x));
            }
            catch { }
            foreach (string d in c) if (Installer.IsStreamlineDir(d)) return d;
            return null;
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

        public const string RuntimeResource = "runtime.zip";
        public const string RuntimeZipName = "DLL DLSS 5.zip";

        /// <summary>true quando o instalador foi montado com o zip da runtime (build.bat "caminho\DLL DLSS 5.zip").</summary>
        public static bool HasEmbeddedRuntime
        {
            get { return EmbeddedRuntimeLength > 0; }
        }

        public static long EmbeddedRuntimeLength
        {
            get
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(RuntimeResource))
                    return s == null ? 0 : s.Length;
            }
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

        public SetupForm() : base(600, 570)
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

            // Arquivos do DLSS: copiados para a pasta de instalação (components), não dependem de Downloads
            Card cf = new Card(null, L.T("Arquivos do DLSS"));
            cf.SetBounds(24, 318, 552, 144);
            Controls.Add(cf);
            rowNr = new StatusRow(L.T("Runtime DLSS 5 (nvngx_dlssnr.dll)"));
            rowNr.SetBounds(16, 46, 424, 44);
            cf.Controls.Add(rowNr);
            ModernButton btnNr = new ModernButton(L.T("Escolher..."), null, ButtonKind.Ghost);
            btnNr.SetBounds(446, 52, 92, 30);
            btnNr.Click += delegate { if (!working) ChooseNr(); };
            cf.Controls.Add(btnNr);
            rowSl = new StatusRow(L.T("NVIDIA Streamline SDK"));
            rowSl.SetBounds(16, 92, 520, 44);
            cf.Controls.Add(rowSl);
            DetectSources();

            lblStatus = AddLabel(this, "", 24, 470, 552, 22, Theme.F(9f), Theme.Muted);
            bar = new ThinProgress();
            bar.SetBounds(24, 496, 552, 6);
            Controls.Add(bar);

            btnInstall = new ModernButton(update ? L.T("  Atualizar") : L.T("  Instalar"), Theme.GDownload, ButtonKind.Primary);
            btnInstall.SetBounds(376, 512, 200, 46);
            btnInstall.Click += delegate { if (done) Finish(); else Install(); };
            Controls.Add(btnInstall);
            btnCancel = new ModernButton(L.T("Cancelar"), null, ButtonKind.Secondary);
            btnCancel.SetBounds(254, 512, 110, 46);
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
                DetectSources();
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
                    lblStatus.Text = "✔ " + SetupApp.AppName + L.T(" instalado em ") + target;
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
            SetupApp.InstallTo(target, desktop, Status, nrSrc, slSrc);
        }

        // ---- arquivos do DLSS que vão para <pasta>\components
        StatusRow rowNr, rowSl;
        string nrSrc, slSrc;

        void DetectSources()
        {
            if (nrSrc == null || !File.Exists(nrSrc)) nrSrc = SetupApp.FindNrSource(dir);
            slSrc = SetupApp.FindSlSource(dir);
            if (SetupApp.HasEmbeddedRuntime)
                rowNr.Set(Theme.GCheck, Theme.Accent, L.T("Incluída neste instalador — vai para a pasta do app (") + SetupApp.RuntimeZipName + ")");
            else if (nrSrc != null)
                rowNr.Set(Theme.GCheck, Theme.Accent, L.T("Encontrada — será copiada para a pasta do app: ") + nrSrc);
            else if (!searchedGames)
                SearchGames();
            else
                rowNr.Set(Theme.GWarn, Theme.Warn, L.T("Não encontrada — escolha agora (opcional) ou depois no app"));
            if (slSrc != null)
                rowSl.Set(Theme.GCheck, Theme.Accent, L.T("Encontrado — será copiado para a pasta do app: ") + slSrc);
            else
                rowSl.Set(Theme.GDownload, Theme.Info, L.T("Será baixado da NVIDIA quando você instalar o DLSS 5 num jogo"));
        }

        bool searchedGames;

        /// <summary>Procura a runtime no GitHub oficial da NVIDIA (se publicada) e nos jogos instalados, sem travar a janela.</summary>
        void SearchGames()
        {
            searchedGames = true;
            rowNr.Set(Theme.GSync, Theme.Info, L.T("Procurando a runtime do DLSS 5 nos jogos instalados... "));
            if (!IsHandleCreated)
            {
                // Começa só quando a janela existir, senão o resultado chegaria antes dela e se perderia
                Shown += delegate { searchedGames = false; SearchGames(); };
                return;
            }
            Thread t = new Thread(delegate()
            {
                string got = null;
                try
                {
                    got = RuntimeProvider.Obtain(Path.Combine(Path.GetTempPath(), "DLSS5Definitive-runtime"),
                        delegate(string s) { try { BeginInvoke(new Action(delegate { if (nrSrc == null) rowNr.Set(Theme.GSync, Theme.Info, s); })); } catch { } });
                }
                catch { }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        if (nrSrc == null && got != null) nrSrc = got;
                        DetectSources();
                    }));
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        void ChooseNr()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = L.T("Runtime do DLSS 5 (nvngx_dlssnr.dll ou .zip)") + "|nvngx_dlssnr.dll;*.zip|DLL|*.dll|ZIP|*.zip";
                d.Title = L.T("Selecione a runtime do DLSS 5 (nvngx_dlssnr.dll)");
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (!NrRuntime.IsRuntimeSource(d.FileName))
                {
                    MessageBox.Show(this, L.T("Este arquivo não é a runtime do DLSS 5.") + "\r\n\r\n" +
                        L.T("O nvngx.dll_dlssnr.dll é o \"forwarder\" que já vem dentro do OptiScaler F5 — o app instala ele sozinho. ") +
                        L.T("A runtime é o nvngx_dlssnr.dll da NVIDIA (cerca de 250 MB)."),
                        SetupApp.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                nrSrc = d.FileName;
                DetectSources();
            }
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
                    try { SetupApp.InstallTo(dir, File.Exists(SetupApp.DesktopLnk), Status, SetupApp.FindNrSource(dir), SetupApp.FindSlSource(dir)); break; }
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
                RemoveAll(dir, tgData.Checked);
                // O desinstalador está em uso: ele se apaga (e a pasta, só se ficar vazia) quando esta janela fechar.
                FormClosed += delegate { SelfDelete(dir); };
                done = true;
                lblStatus.ForeColor = Theme.Accent;
                lblStatus.Text = "✔ " + SetupApp.AppName + L.T(" foi removido.");
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

        /// <summary>Remove atalhos, registro, os arquivos do app e a pasta components (runtime do DLSS 5 e Streamline).</summary>
        public static void RemoveAll(string dir, bool deleteData)
        {
            foreach (string l in new[] { SetupApp.StartMenuLnk, SetupApp.DesktopLnk })
                if (File.Exists(l)) File.Delete(l);
            Registry.CurrentUser.DeleteSubKeyTree(SetupApp.RegPath, false);
            foreach (string f in new[] { SetupApp.ExeName, "LEIA-ME.txt" })
            {
                string p = Path.Combine(dir, f);
                if (File.Exists(p)) { File.SetAttributes(p, FileAttributes.Normal); File.Delete(p); }
            }
            string comp = Path.Combine(dir, "components");
            if (Directory.Exists(comp)) Directory.Delete(comp, true);
            if (deleteData && Directory.Exists(SetupApp.DataDir)) Directory.Delete(SetupApp.DataDir, true);
        }

        /// <summary>Desinstalação sem janela ("/uninstall /silent"): fecha o app sem perguntar e remove tudo (mantém os dados).</summary>
        public static void Silent()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
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
            RemoveAll(dir, false);
            SelfDelete(dir);
        }

        public static void SelfDelete(string dir)
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
