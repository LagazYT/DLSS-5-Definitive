using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Dlss5Injector
{
    public class MainForm : Form
    {
        // ---- estrutura
        TabBar tabs;
        Panel pageLib, pageInj, pageFg;

        // ---- biblioteca
        FlowLayoutPanel grid;
        SearchBox search;
        ModernButton btnScan, btnAddFolder;
        Label lblLibCount, lblScan, lblEmpty;
        ThinProgress barScan;
        ContextMenuStrip cardMenu;
        GameCard menuCard;
        List<GameEntry> games = new List<GameEntry>();
        Dictionary<string, GameCard> cards = new Dictionary<string, GameCard>();
        CoverService covers;
        LibraryScanner scanner;
        bool scanning;

        // ---- injetor
        DropZone drop;
        DarkCombo cmbExe, cmbApi, cmbMethod;
        InfoTile tApi, tEngine, tArch, tUpscale, tAnti, tMods;
        Label lblNotes, lblStatus;
        ToggleSwitch tgUpdate, tgLog, tgBefore, tgHdr;
        ModernButton btnFgLink;
        Segmented segPasses;
        StatusRow rowSl, rowNr;
        ModernButton btnSl, btnNr, btnInstall, btnUninstall, btnOpen, btnNrApp, btnNrAppCfg;
        ToggleSwitch tgTray, tgStartup;
        NotifyIcon tray;
        bool exiting, trayTipShown;
        StepsView steps;
        ThinProgress bar;
        TextBox txtLog;

        // ---- frame gen
        Segmented segFg;
        ToggleSwitch tgDyn;
        FrameTimeline timeline;
        Label lblFgFps, lblFgGame;
        StatusRow rowFgOut, rowFgMax, rowFgVec, rowFgFlow, rowFgMod, rowFgLat, rowGen50, rowGen40, rowGen30;
        ModernButton btnFgApply;

        string folder, slDir, nrPath;
        NrRuntime.Info nrInfo;
        GameInfo game;
        List<Method> methods = new List<Method>();
        List<string> exeList = new List<string>();
        string methodWhy = "";
        bool busy, loadingUi;

        static readonly GfxApi[] ApiChoices = { GfxApi.Unknown, GfxApi.DX12, GfxApi.DX11, GfxApi.Vulkan, GfxApi.DX10, GfxApi.DX9 };
        const int PageTop = 76;

        // ---- atualização automática
        Panel banner;
        Label lblBanner;
        ModernButton btnUpdate, btnNotes, btnDismiss, btnVersion;
        UpdateInfo pendingUpdate;
        System.Windows.Forms.Timer updTimer;
        bool checkingUpdate, updating;

        // ---- configurações
        Panel pageCfg;
        DarkCombo cmbLang;
        ToggleSwitch tgAutoUpd;
        StatusRow rowUpd, rowSlCfg, rowNrCfg;
        Label lblData;
        bool langLoading;

        public MainForm(string initialFolder) : this(initialFolder, false) { }

        public MainForm(string initialFolder, bool justUpdated)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = Program.AppName + " " + Program.Version + " — " + L.T("criado por") + " " + Program.Author + (AppPaths.IsPortable ? "  (Portable)" : "");
            Icon = Theme.AppIcon();
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9f);
            ClientSize = new Size(1120, 808);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            DoubleBuffered = true;

            Gpu.Detect();
            tabs = new TabBar(new[] { L.T("Biblioteca"), L.T("Injetor DLSS 5"), L.T("Frame Gen"), L.T("Configurações") },
                              new[] { "\uE8F1", "\uE945", "\uE916", "\uE713" });   // \uE713 = engrenagem
            tabs.SetBounds(338, 20, 600, 40);
            tabs.SelectedChanged += delegate { ShowPage(tabs.Selected); };
            Controls.Add(tabs);

            pageLib = NewPage();
            pageInj = NewPage();
            pageFg = NewPage();
            pageCfg = NewPage();
            BuildLibrary();
            BuildInjector();
            BuildFrameGen();
            BuildUpdateUi();
            BuildSettings();
            ShowPage(0);
            ResumeLayout(false);

            slDir = Settings.Get("streamline");
            if (!Installer.IsStreamlineDir(slDir))
            {
                slDir = FindStreamline();
                if (slDir != null) Settings.Set("streamline", slDir);
            }
            UpdateSlRow();
            ImportStreamlineAsync();
            string nr = Settings.Get("nr");
            string found = NrRuntime.FindCandidate(slDir, AppPaths.Components);
            if (string.IsNullOrEmpty(nr) || !File.Exists(nr) ||
                (found != null && Settings.Get("nrManual") != "1" && Util.FileVer(found) > Util.FileVer(nr))) nr = found;
            SetNr(nr, false);
            if (string.IsNullOrEmpty(nr) || !File.Exists(nr)) AutoObtainNr();

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            Log(L.T("Pronto."));

            covers = new CoverService(OnCoverReady);
            LoadLibrary();
            Shown += delegate
            {
                if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
                {
                    tabs.Selected = 1;
                    SetFolder(initialFolder, null);
                }
                StartScan(null);
                if (justUpdated)
                    ShowBanner(L.T("✔  DLSS 5 Definitive atualizado para a versão ") + Program.Version + ".", false);
                // Verifica atualizações alguns segundos depois de abrir e depois a cada 6 horas
                updTimer = new System.Windows.Forms.Timer();
                updTimer.Interval = 4000;
                updTimer.Tick += delegate
                {
                    updTimer.Interval = 6 * 60 * 60 * 1000;
                    if (Settings.Get("autoUpdate") != "0") CheckForUpdates(false);
                };
                updTimer.Start();
            };
            BuildTray();
            if (Program.StartMinimized)
            {
                // Aberto pelo Windows ao entrar: já começa minimizado (ou só na bandeja)
                WindowState = FormWindowState.Minimized;
                Shown += delegate { if (Settings.Get("trayOnClose") == "1") HideToTray(false); };
            }
            FixStartupPath();
            Program.ShowRequested += delegate { SafeInvoke(RestoreFromTray); };
            FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (!exiting && e.CloseReason == CloseReason.UserClosing && Settings.Get("trayOnClose") == "1")
                {
                    e.Cancel = true;
                    HideToTray(true);
                    return;
                }
                if (scanner != null) scanner.Cancel = true;
                LibraryStore.Save(games);
            };
            FormClosed += delegate { if (tray != null) { tray.Visible = false; tray.Dispose(); } };

            // Se a runtime do DLSS 5 ou o Streamline aparecerem depois que o app abriu (copiados/extraídos em
            // Downloads, por exemplo), detecta sozinho — sem precisar reabrir o app.
            System.Windows.Forms.Timer redetect = new System.Windows.Forms.Timer();
            redetect.Interval = 3000;
            redetect.Tick += delegate { RedetectFiles(); };
            redetect.Start();
            Activated += delegate { RedetectFiles(); };
        }

        /// <summary>Procura de novo a runtime do DLSS 5 e o Streamline quando eles estão faltando.</summary>
        void RedetectFiles()
        {
            if (busy || importingNr || importingSl) return;
            if (string.IsNullOrEmpty(nrPath) || !File.Exists(nrPath))
            {
                string found = NrRuntime.FindCandidate(slDir, AppPaths.Components);
                if (found != null)
                {
                    Log(L.T("Runtime do DLSS 5 encontrada: ") + found);
                    SetNr(found, false);
                }
            }
            if (!Installer.IsStreamlineDir(slDir))
            {
                string sl = FindStreamline();
                if (sl != null) { slDir = sl; UpdateSlRow(); RefreshPlan(); ImportStreamlineAsync(); }
            }
        }

        /// <summary>
        /// Recusa o forwarder do OptiScaler (nvngx.dll_dlssnr.dll), que tem nome parecido mas não é a runtime.
        /// </summary>
        bool AcceptNrFile(string file)
        {
            string n = Path.GetFileName(file);
            if (n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                if (NrRuntime.IsRuntimeZip(file)) return true;
                MessageBox.Show(this, L.T("Este zip não tem a runtime do DLSS 5 (nvngx_dlssnr.dll da NVIDIA, cerca de 250 MB)."),
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            if (n.Equals("nvngx.dll_dlssnr.dll", StringComparison.OrdinalIgnoreCase) ||
                (!n.Equals(NrRuntime.FileName, StringComparison.OrdinalIgnoreCase) && new FileInfo(file).Length < (20L << 20)))
            {
                MessageBox.Show(this, L.T("Este arquivo não é a runtime do DLSS 5.") + "\r\n\r\n" +
                    L.T("O nvngx.dll_dlssnr.dll é o \"forwarder\" que já vem dentro do OptiScaler F5 — o app instala ele sozinho. ") +
                    L.T("A runtime é o nvngx_dlssnr.dll da NVIDIA (cerca de 250 MB)."),
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            return true;
        }

        // ================================================================ ATUALIZAÇÃO

        void BuildUpdateUi()
        {
            btnVersion = new ModernButton("v" + Program.Version + L.T("  ·  atualizações"), null, ButtonKind.Ghost);
            btnVersion.Font = Theme.F(8f);
            btnVersion.SetBounds(178, 40, 156, 22);
            btnVersion.Click += delegate { if (pendingUpdate != null) DoUpdate(); else CheckForUpdates(true); };
            Controls.Add(btnVersion);

            banner = new Panel();
            banner.SetBounds(0, PageTop, 1120, 48);
            banner.BackColor = Theme.AccentDark;
            banner.Visible = false;
            Controls.Add(banner);
            lblBanner = TextLabel(banner, "", 24, 0, 600, 48, Theme.F(10f, FontStyle.Bold), Theme.AccentHover);
            lblBanner.TextAlign = ContentAlignment.MiddleLeft;
            btnNotes = new ModernButton(L.T("Novidades"), null, ButtonKind.Ghost);
            btnNotes.SetBounds(690, 9, 110, 30);
            btnNotes.Click += delegate { if (pendingUpdate != null) Process.Start(pendingUpdate.PageUrl); };
            banner.Controls.Add(btnNotes);
            btnUpdate = new ModernButton(L.T("  Atualizar agora"), Theme.GDownload, ButtonKind.Primary);
            btnUpdate.Font = Theme.F(9.5f, FontStyle.Bold);
            btnUpdate.SetBounds(810, 7, 230, 34);
            btnUpdate.Click += delegate { DoUpdate(); };
            banner.Controls.Add(btnUpdate);
            btnDismiss = new ModernButton("✕", null, ButtonKind.Ghost);
            btnDismiss.SetBounds(1052, 9, 44, 30);
            btnDismiss.Click += delegate { if (!updating) HideBanner(); };
            banner.Controls.Add(btnDismiss);
        }

        void ShowBanner(string text, bool update)
        {
            lblBanner.Text = text;
            btnUpdate.Visible = btnNotes.Visible = update;
            if (!banner.Visible)
            {
                int h = banner.Height;
                banner.Top = pageLib.Top;
                foreach (Panel p in new[] { pageLib, pageInj, pageFg, pageCfg }) p.Top += h;
                ClientSize = new Size(ClientSize.Width, ClientSize.Height + h);
                banner.Visible = true;
                banner.BringToFront();
            }
            if (!update)
            {
                // Aviso informativo some sozinho
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 9000;
                t.Tick += delegate { t.Stop(); t.Dispose(); if (pendingUpdate == null) HideBanner(); };
                t.Start();
            }
        }

        void HideBanner()
        {
            if (!banner.Visible) return;
            int h = banner.Height;
            banner.Visible = false;
            foreach (Panel p in new[] { pageLib, pageInj, pageFg, pageCfg }) p.Top -= h;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height - h);
        }

        void CheckForUpdates(bool manual)
        {
            if (checkingUpdate || updating) return;
            checkingUpdate = true;
            if (manual) btnVersion.Text = L.T("verificando...");
            if (rowUpd != null) rowUpd.Set(Theme.GSync, Theme.Info, "v" + Program.Version + "  ·  " + L.T("verificando..."));
            Thread t = new Thread(delegate()
            {
                UpdateInfo info = null;
                Exception err = null;
                try { info = Updater.Check(); } catch (Exception ex) { err = ex; }
                SafeInvoke(delegate
                {
                    checkingUpdate = false;
                    Settings.Set("lastUpdateCheck", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                    UpdateUpdRow(info, err);
                    if (info != null)
                    {
                        pendingUpdate = info;
                        btnVersion.Text = "v" + Program.Version + "  →  " + info.Tag;
                        btnVersion.ForeColor = Theme.Warn;
                        ShowBanner(L.T("⬆  Nova versão ") + info.Tag + L.T(" do DLSS 5 Definitive disponível  (você tem a ") + Program.Version + ")", true);
                        return;
                    }
                    btnVersion.Text = "v" + Program.Version + L.T("  ·  atualizações");
                    if (!manual) return;
                    if (err != null)
                        MessageBox.Show(this, L.T("Não foi possível verificar atualizações:\r\n") + err.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else
                        MessageBox.Show(this, L.T("Você já está na versão mais recente (") + Program.Version + ").", Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        void DoUpdate()
        {
            UpdateInfo u = pendingUpdate;
            if (u == null || updating) return;
            if (busy)
            {
                MessageBox.Show(this, L.T("Espere terminar a instalação/análise em andamento e clique em Atualizar de novo."), Program.AppName);
                return;
            }
            string notes = Updater.PlainNotes(u.Notes, 700);
            if (MessageBox.Show(this, L.T("Atualizar o DLSS 5 Definitive de ") + Program.Version + L.T(" para ") + u.Tag + L.T("?\r\n\r\n") +
                    (notes.Length > 0 ? notes + L.T("\r\n\r\n") : "") +
                    L.T("O app baixa a versão nova, confere o SHA-256, fecha e abre de novo sozinho. Suas configurações e a biblioteca são mantidas."),
                    Program.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            updating = true;
            btnUpdate.Enabled = btnDismiss.Enabled = btnNotes.Enabled = false;
            btnUpdate.Text = L.T("  Baixando...");
            Thread t = new Thread(delegate()
            {
                string file = null;
                Exception err = null;
                try
                {
                    file = Updater.Download(u, delegate(int pct) { SafeInvoke(delegate { btnUpdate.Text = L.T("  Baixando ") + pct + "%"; }); });
                }
                catch (Exception ex) { err = ex; }
                SafeInvoke(delegate
                {
                    if (err == null)
                    {
                        try
                        {
                            btnUpdate.Text = L.T("  Instalando...");
                            if (scanner != null) scanner.Cancel = true;
                            LibraryStore.Save(games);
                            Updater.ApplyAndRestart(file);
                            Application.Exit();
                            return;
                        }
                        catch (Exception ex) { err = ex; }
                    }
                    updating = false;
                    btnUpdate.Enabled = btnDismiss.Enabled = btnNotes.Enabled = true;
                    btnUpdate.Text = L.T("  Atualizar agora");
                    MessageBox.Show(this, L.T("A atualização falhou:\r\n") + err.Message + L.T("\r\n\r\nVocê pode baixar a versão nova manualmente em \"Novidades\"."),
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitleBar(this);
            lock (pendingUi)
            {
                uiReady = true;
                foreach (Action a in pendingUi) BeginInvoke(a);
                pendingUi.Clear();
            }
        }

        Panel NewPage()
        {
            Panel p = new Panel();
            p.SetBounds(0, PageTop, 1120, 808 - PageTop);
            p.BackColor = Theme.Bg;
            Controls.Add(p);
            return p;
        }

        void ShowPage(int i)
        {
            pageLib.Visible = i == 0;
            pageInj.Visible = i == 1;
            pageFg.Visible = i == 2;
            pageCfg.Visible = i == 3;
            if (i == 3) RefreshSettings();
            if (i == 2) RefreshFg();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Theme.Smooth(g);
            Theme.FillRound(g, Theme.Accent, new RectangleF(24, 18, 44, 44), 11);
            using (Font f = Theme.Title(19f))
                Theme.Draw(g, "5", f, Theme.Bg, new Rectangle(24, 18, 44, 44), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (Font f = Theme.Title(15f))
                Theme.Draw(g, L.T("DLSS 5 Definitive"), f, Theme.Text, new Rectangle(80, 14, 240, 28), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            using (Font f = Theme.F(8.5f))
            using (Font fb = Theme.F(8.5f, FontStyle.Bold))
            {
                Theme.Draw(g, L.T("criado por"), f, Theme.Muted, new Rectangle(81, 41, 70, 20), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                int w = TextRenderer.MeasureText(L.T("criado por"), f).Width;
                Theme.Draw(g, Program.Author, fb, Theme.Accent, new Rectangle(76 + w, 41, 80, 20), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            string gpu = Gpu.Name.Replace("NVIDIA ", "").Replace("GeForce ", "");
            using (Font f = Theme.F(8.5f, FontStyle.Bold))
            using (Font gf = Theme.Icon(10f))
            {
                int tw = TextRenderer.MeasureText(gpu, f).Width;
                int w = tw + 48, x = ClientSize.Width - 24 - w;
                Color c = Gpu.IsNvidia && Gpu.Series >= 20 ? Theme.Accent : Theme.Warn;
                RectangleF pill = new RectangleF(x, 24, w, 32);
                Theme.FillRound(g, Theme.Card, pill, 16);
                Theme.DrawRound(g, Theme.Border, pill, 16);
                Theme.Draw(g, Theme.GChip, gf, c, new Rectangle(x + 8, 24, 24, 32), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                Theme.Draw(g, gpu, f, Theme.Text, new Rectangle(x + 34, 24, tw + 8, 32), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        // ================================================================ helpers de layout

        static Card AddCard(Control parent, string step, string title, int x, int y, int w, int h)
        {
            Card c = new Card(step, title);
            c.SetBounds(x, y, w, h);
            parent.Controls.Add(c);
            return c;
        }

        static void Place(Control parent, Control c, int x, int y)
        {
            c.Location = new Point(x, y);
            parent.Controls.Add(c);
        }

        static DarkCombo Combo(Control parent, int x, int y, int w)
        {
            DarkCombo c = new DarkCombo();
            c.SetBounds(x, y, w, 36);
            parent.Controls.Add(c);
            return c;
        }

        static InfoTile Tile(Control parent, string caption, string glyph, int x, int y)
        {
            InfoTile t = new InfoTile(caption, glyph);
            t.SetBounds(x, y, 244, 50);
            parent.Controls.Add(t);
            return t;
        }

        static ToggleSwitch Toggle(Control parent, string text, bool on, int x, int y, int w)
        {
            ToggleSwitch t = new ToggleSwitch(text, on);
            t.SetBounds(x, y, w, 30);
            parent.Controls.Add(t);
            return t;
        }

        static Label TextLabel(Control parent, string text, int x, int y, int w, int h, Font f, Color c)
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

        // ================================================================ BIBLIOTECA

        void BuildLibrary()
        {
            Panel p = pageLib;
            TextLabel(p, L.T("Biblioteca"), 24, 4, 300, 34, Theme.Title(16f), Theme.Text);
            lblLibCount = TextLabel(p, "", 26, 38, 380, 20, Theme.F(9f), Theme.Muted);

            search = new SearchBox(L.T("Buscar jogo..."));
            search.SetBounds(420, 12, 300, 38);
            search.Box.TextChanged += delegate { ApplyFilter(); };
            p.Controls.Add(search);

            btnScan = new ModernButton(L.T("  Escanear PC"), Theme.GSync, ButtonKind.Secondary);
            btnScan.SetBounds(732, 10, 172, 42);
            btnScan.Click += delegate { StartScan(null); };
            p.Controls.Add(btnScan);

            btnAddFolder = new ModernButton(L.T("  Adicionar pasta"), Theme.GFolder, ButtonKind.Primary);
            btnAddFolder.Font = Theme.F(9.5f, FontStyle.Bold);
            btnAddFolder.SetBounds(914, 10, 182, 42);
            btnAddFolder.Click += delegate { AddFolder(); };
            p.Controls.Add(btnAddFolder);

            lblScan = TextLabel(p, "", 26, 62, 800, 20, Theme.F(8.5f), Theme.Muted);
            barScan = new ThinProgress();
            barScan.SetBounds(850, 70, 246, 5);
            barScan.Visible = false;
            p.Controls.Add(barScan);

            grid = new FlowLayoutPanel();
            grid.SetBounds(14, 88, 1098, 640);
            grid.AutoScroll = true;
            grid.BackColor = Theme.Bg;
            grid.Padding = new Padding(4, 0, 0, 12);
            grid.HandleCreated += delegate { Theme.DarkNative(grid, "DarkMode_Explorer"); };
            p.Controls.Add(grid);

            lblEmpty = TextLabel(p, L.T("Nenhum jogo ainda.\r\nClique em \"Escanear PC\" ou \"Adicionar pasta\"."), 0, 330, 1120, 60, Theme.F(11f), Theme.Dim);
            lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            lblEmpty.BringToFront();

            cardMenu = new ContextMenuStrip();
            cardMenu.Renderer = new DarkMenuRenderer();
            cardMenu.ShowImageMargin = false;
            cardMenu.BackColor = Theme.Field;
            cardMenu.Font = Theme.F(9.5f);
            AddMenu(L.T("Configurar DLSS 5"), delegate { if (menuCard != null) OpenGame(menuCard.Game); });
            AddMenu(L.T("Abrir pasta do jogo"), delegate { if (menuCard != null) Process.Start("explorer.exe", "\"" + menuCard.Game.Path + "\""); });
            AddMenu(L.T("Buscar capa novamente"), delegate
            {
                if (menuCard == null) return;
                menuCard.Game.Cover = null;
                menuCard.Game.CoverTried = false;
                if (menuCard.Game.Source != "Steam") menuCard.Game.AppId = null;
                covers.Request(menuCard.Game);
            });
            AddMenu(L.T("Remover da biblioteca"), delegate { if (menuCard != null) RemoveGame(menuCard); });
        }

        void AddMenu(string text, EventHandler click)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.AutoSize = false;
            it.Size = new Size(220, 32);
            it.ForeColor = Theme.Text;
            it.Click += click;
            cardMenu.Items.Add(it);
        }

        void LoadLibrary()
        {
            games = LibraryStore.Load();
            games.RemoveAll(g => g == null || string.IsNullOrEmpty(g.Path) || !Directory.Exists(g.Path));
            foreach (GameEntry g in games.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (string.IsNullOrEmpty(g.Cover) || !File.Exists(g.Cover)) g.CoverTried = false;
                AddCard(g);
            }
            UpdateLibCount();
        }

        GameCard AddCard(GameEntry g)
        {
            string key = LibraryStore.Key(g.Path);
            GameCard c;
            if (cards.TryGetValue(key, out c)) return c;
            c = new GameCard(g);
            GameCard card = c;
            c.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right) { menuCard = card; cardMenu.Show(card, e.Location); }
                else OpenGame(card.Game);
            };
            cards[key] = c;
            grid.Controls.Add(c);
            c.Visible = Matches(g);
            covers.Request(g);
            return c;
        }

        void OnCoverReady(GameEntry g)
        {
            // Roda na thread do serviço de capas: decodifica a imagem aqui e só entrega o bitmap pronto à interface.
            Bitmap b = null;
            if (!string.IsNullOrEmpty(g.Cover) && File.Exists(g.Cover)) b = CoverService.LoadScaled(g.Cover, GameCard.CoverW, GameCard.CoverH);
            if (b == null) return;
            try
            {
                if (IsDisposed || !IsHandleCreated) { b.Dispose(); return; }
                BeginInvoke(new Action(delegate
                {
                    GameCard c;
                    if (cards.TryGetValue(LibraryStore.Key(g.Path), out c)) c.SetCover(b);
                    else b.Dispose();
                }));
            }
            catch { b.Dispose(); }
        }

        bool Matches(GameEntry g)
        {
            string q = search.Box.Text.Trim();
            return q.Length == 0 || (g.Name ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        void ApplyFilter()
        {
            grid.SuspendLayout();
            foreach (GameCard c in cards.Values) c.Visible = Matches(c.Game);
            grid.ResumeLayout();
            UpdateLibCount();
        }

        void UpdateLibCount()
        {
            int vis = cards.Values.Count(c => Matches(c.Game));
            int inst = games.Count(g => g.Installed);
            lblLibCount.Text = games.Count + L.T(" jogo(s)") + (vis != games.Count ? "  ·  " + vis + L.T(" na busca") : "") + (inst > 0 ? "  ·  " + inst + L.T(" com DLSS 5") : "");
            lblEmpty.Visible = games.Count == 0 && !scanning;
        }

        void SortGrid()
        {
            grid.SuspendLayout();
            int i = 0;
            foreach (GameCard c in cards.Values.OrderBy(x => x.Game.Name, StringComparer.CurrentCultureIgnoreCase).ToList())
                grid.Controls.SetChildIndex(c, i++);
            grid.ResumeLayout();
        }

        void MergeGame(GameEntry n)
        {
            string key = LibraryStore.Key(n.Path);
            GameEntry old = games.Find(g => LibraryStore.Key(g.Path) == key);
            if (old != null)
            {
                bool changed = old.Installed != n.Installed;
                old.Installed = n.Installed;
                if (string.IsNullOrEmpty(old.AppId) && !string.IsNullOrEmpty(n.AppId))
                {
                    old.AppId = n.AppId;
                    old.CoverTried = false;
                    covers.Request(old);
                }
                if (n.Source != "Pasta" && old.Source == "Pasta") { old.Source = n.Source; old.Name = n.Name; changed = true; }
                GameCard c;
                if (changed && cards.TryGetValue(key, out c)) c.Invalidate();
                return;
            }
            games.Add(n);
            AddCard(n);
            UpdateLibCount();
        }

        void StartScan(string onlyFolder)
        {
            if (scanning) return;
            scanning = true;
            btnScan.Enabled = false;
            btnAddFolder.Enabled = false;
            barScan.Visible = true;
            barScan.Value = 10;
            lblEmpty.Visible = false;
            int before = games.Count;
            HashSet<string> foundKeys = new HashSet<string>();
            scanner = new LibraryScanner(
                delegate(GameEntry g)
                {
                    lock (foundKeys) foundKeys.Add(LibraryStore.Key(g.Path));
                    SafeInvoke(delegate { MergeGame(g); });
                },
                delegate(string s) { SafeInvoke(delegate { lblScan.Text = s; barScan.Value = Math.Min(95, barScan.Value + 8); }); });
            LibraryScanner sc = scanner;
            Thread t = new Thread(delegate()
            {
                try
                {
                    if (onlyFolder != null) sc.ScanFolder(onlyFolder);
                    else sc.ScanAll();
                }
                catch (Exception ex) { Program.Report(ex, false); }
                SafeInvoke(delegate
                {
                    scanning = false;
                    btnScan.Enabled = true;
                    btnAddFolder.Enabled = true;
                    barScan.Visible = false;
                    foreach (GameEntry g in games.FindAll(x => !Directory.Exists(x.Path))) RemoveGame(FindCard(g));
                    SortGrid();
                    LibraryStore.Save(games);
                    int added = games.Count - before;
                    lblScan.Text = L.T("Escaneamento concluído · ") + foundKeys.Count + L.T(" jogo(s) encontrados") + (added > 0 ? " · " + added + L.T(" novo(s)") : "") +
                                   "  ·  " + DateTime.Now.ToString("HH:mm");
                    UpdateLibCount();
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        // Ações vindas de threads antes de a janela existir (ex.: uma cópia que termina durante a abertura) ficam na
        // fila e rodam assim que a janela é criada — antes elas eram descartadas.
        readonly List<Action> pendingUi = new List<Action>();
        bool uiReady;

        void SafeInvoke(Action a)
        {
            try
            {
                if (IsDisposed) return;
                lock (pendingUi)
                {
                    if (!uiReady) { pendingUi.Add(a); return; }
                }
                BeginInvoke(a);
            }
            catch { }
        }

        GameCard FindCard(GameEntry g)
        {
            GameCard c;
            return cards.TryGetValue(LibraryStore.Key(g.Path), out c) ? c : null;
        }

        void RemoveGame(GameCard c)
        {
            if (c == null) return;
            games.Remove(c.Game);
            cards.Remove(LibraryStore.Key(c.Game.Path));
            grid.Controls.Remove(c);
            c.Dispose();
            LibraryStore.Save(games);
            UpdateLibCount();
        }

        void AddFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = L.T("Escolha uma pasta com jogos (ou a pasta de um jogo) para escanear");
                if (d.ShowDialog(this) != DialogResult.OK) return;
                LibraryStore.AddCustomFolder(d.SelectedPath);
                StartScan(d.SelectedPath);
            }
        }

        void OpenGame(GameEntry g)
        {
            if (busy) return;
            tabs.Selected = 1;
            SetFolder(g.Path, null);
        }

        void RefreshInstalledFlag(string exeDir)
        {
            foreach (GameEntry g in games)
            {
                if (exeDir == null || !LibraryStore.Key(exeDir).StartsWith(LibraryStore.Key(g.Path))) continue;
                g.Installed = LibraryScanner.FindManifest(g.Path, 0);
                GameCard c = FindCard(g);
                if (c != null) c.Invalidate();
            }
            LibraryStore.Save(games);
            UpdateLibCount();
        }

        // ================================================================ INJETOR

        void BuildInjector()
        {
            Panel P = pageInj;
            int o = 80; // coordenadas da tela inteira; a página começa abaixo do cabeçalho

            Card cGame = AddCard(P, "1", L.T("Jogo"), 24, 84 - o, 528, 232);
            drop = new DropZone();
            drop.SetBounds(16, 52, 496, 96);
            drop.Click += delegate { if (!busy) Browse(); };
            drop.DragEnter += OnDragEnter;
            drop.DragDrop += OnDragDrop;
            drop.DragLeave += delegate { drop.IsDragOver = false; drop.Invalidate(); };
            cGame.Controls.Add(drop);
            Place(cGame, new FieldLabel(L.T("Executável")), 18, 158);
            cmbExe = Combo(cGame, 16, 178, 496);
            cmbExe.SelectedIndexChanged += delegate { if (!loadingUi && cmbExe.SelectedIndex >= 0) AnalyzeExe(ExeFromItem(cmbExe.SelectedIndex)); };

            Card cAn = AddCard(P, "2", L.T("Análise do jogo"), 24, 328 - o, 528, 268);
            tApi = Tile(cAn, L.T("API gráfica"), Theme.GChip, 16, 52);
            tEngine = Tile(cAn, L.T("Engine"), Theme.GGame, 268, 52);
            tArch = Tile(cAn, L.T("Arquitetura"), Theme.GInfo, 16, 108);
            tUpscale = Tile(cAn, L.T("Upscalers do jogo"), Theme.GSync, 268, 108);
            tAnti = Tile(cAn, "Anti-cheat", Theme.GWarn, 16, 164);
            tMods = Tile(cAn, L.T("Mods na pasta"), Theme.GOpen, 268, 164);
            lblNotes = TextLabel(cAn, "", 16, 220, 496, 42, Theme.F(8.5f), Theme.Muted);

            Card cLog = AddCard(P, null, L.T("Atividade"), 24, 608 - o, 528, 120);
            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.ScrollBars = ScrollBars.None;
            txtLog.BackColor = Theme.Card;
            txtLog.ForeColor = Theme.Muted;
            txtLog.Font = new Font("Consolas", 8.5f);
            txtLog.SetBounds(18, 44, 496, 68);
            cLog.Controls.Add(txtLog);

            Card cCfg = AddCard(P, "3", L.T("Configuração"), 568, 84 - o, 528, 370);
            Place(cCfg, new FieldLabel(L.T("Método (escolhido conforme a compatibilidade do jogo)")), 18, 50);
            cmbMethod = Combo(cCfg, 16, 70, 496);
            cmbMethod.SelectedIndexChanged += delegate { if (!loadingUi) RefreshPlan(); };
            Place(cCfg, new FieldLabel(L.T("API gráfica")), 18, 114);
            cmbApi = Combo(cCfg, 16, 134, 240);
            foreach (GfxApi a in ApiChoices) cmbApi.Items.Add(a == GfxApi.Unknown ? L.T("Detectar automaticamente") : Planner.ApiName(a));
            cmbApi.SelectedIndex = 0;
            cmbApi.SelectedIndexChanged += delegate { if (!loadingUi) RefreshMethods(); };
            Place(cCfg, new FieldLabel(L.T("Passes do Neural Rendering")), 274, 114);
            segPasses = new Segmented(L.T("1 passe"), L.T("2 passes"), L.T("3 passes"));
            segPasses.SetBounds(272, 135, 240, 34);
            segPasses.SelectedChanged += delegate { RefreshPlan(); };
            cCfg.Controls.Add(segPasses);

            btnFgLink = new ModernButton(L.T("  Frame Gen"), "", ButtonKind.Secondary);
            btnFgLink.SetBounds(16, 180, 240, 30);
            btnFgLink.Font = Theme.F(9f);
            btnFgLink.Click += delegate { tabs.Selected = 2; };
            cCfg.Controls.Add(btnFgLink);
            tgBefore = Toggle(cCfg, L.T("NR antes do upscaler"), true, 272, 180, 240);
            tgUpdate = Toggle(cCfg, L.T("Atualizar DLSS do jogo (310.9)"), true, 16, 212, 240);
            tgLog = Toggle(cCfg, L.T("Log de diagnóstico"), false, 272, 212, 240);
            tgHdr = Toggle(cCfg, L.T("Saída HDR (DX11/DX12)"), false, 16, 244, 240);

            rowSl = new StatusRow(L.T("NVIDIA Streamline SDK"));
            rowSl.SetBounds(16, 280, 400, 42);
            cCfg.Controls.Add(rowSl);
            btnSl = new ModernButton(L.T("Alterar"), null, ButtonKind.Ghost);
            btnSl.SetBounds(430, 286, 82, 30);
            btnSl.Click += delegate { BrowseStreamline(); };
            cCfg.Controls.Add(btnSl);

            rowNr = new StatusRow(L.T("Runtime DLSS 5 (nvngx_dlssnr.dll)"));
            rowNr.SetBounds(16, 322, 400, 42);
            cCfg.Controls.Add(rowNr);
            btnNr = new ModernButton(L.T("Alterar"), null, ButtonKind.Ghost);
            btnNr.SetBounds(430, 328, 82, 30);
            btnNr.Click += delegate { BrowseNr(); };
            cCfg.Controls.Add(btnNr);
            btnNrApp = new ModernButton(L.T("Pasta do app"), null, ButtonKind.Ghost);
            btnNrApp.SetBounds(318, 328, 108, 30);
            btnNrApp.Click += delegate { BrowseNrInAppFolder(); };
            btnNrApp.Visible = false;
            cCfg.Controls.Add(btnNrApp);

            Card cPlan = AddCard(P, "4", L.T("Plano de instalação"), 568, 466 - o, 528, 262);
            steps = new StepsView();
            steps.SetBounds(18, 50, 496, 202);
            cPlan.Controls.Add(steps);

            btnInstall = new ModernButton(L.T("  Instalar DLSS 5"), Theme.GDownload, ButtonKind.Primary);
            btnInstall.SetBounds(24, 744 - o, 240, 48);
            btnInstall.Click += delegate { DoInstall(); };
            P.Controls.Add(btnInstall);
            btnUninstall = new ModernButton(L.T("  Remover"), Theme.GDelete, ButtonKind.Secondary);
            btnUninstall.SetBounds(276, 744 - o, 136, 48);
            btnUninstall.Click += delegate { DoUninstall(); };
            P.Controls.Add(btnUninstall);
            btnOpen = new ModernButton(L.T("  Abrir pasta"), Theme.GOpen, ButtonKind.Secondary);
            btnOpen.SetBounds(424, 744 - o, 128, 48);
            btnOpen.Click += delegate { if (game != null) Process.Start("explorer.exe", "\"" + game.ExeDir + "\""); };
            P.Controls.Add(btnOpen);

            lblStatus = TextLabel(P, "", 568, 748 - o, 528, 22, Theme.F(9f), Theme.Muted);
            bar = new ThinProgress();
            bar.SetBounds(568, 778 - o, 528, 6);
            P.Controls.Add(bar);

            foreach (ToggleSwitch t in new[] { tgUpdate, tgLog, tgBefore, tgHdr })
                t.CheckedChanged += delegate { RefreshPlan(); };
        }

        // ================================================================ FRAME GEN

        void BuildFrameGen()
        {
            Panel P = pageFg;
            Card c = AddCard(P, null, L.T("Multi Frame Generation por IA  ·  NVIDIA DLSS 4"), 24, 4, 700, 470);
            TextLabel(c, L.T("O modelo de IA do DLSS Frame Generation da NVIDIA gera até 5 quadros novos para cada quadro real — também em ") +
                    L.T("jogos que não têm Frame Generation. O OptiScaler F5 entrega ao modelo os vetores de movimento do jogo ou, nos jogos ") +
                    L.T("sem suporte, a profundidade e o movimento que ele mesmo calcula. Funciona nas RTX 50, 40, 30 e 20."),
                    18, 48, 664, 54, Theme.F(9f), Theme.Muted);
            Place(c, new FieldLabel(L.T("Multiplicador de quadros  (1X = desligado)")), 20, 108);
            segFg = new Segmented("1X", "2X", "3X", "4X", "5X", "6X");
            segFg.Font = Theme.F(13f, FontStyle.Bold);
            segFg.Height = 56;
            segFg.SetBounds(18, 130, 664, 56);
            int saved;
            if (int.TryParse(Settings.Get("fgMult"), out saved) && saved >= 1 && saved <= 6) segFg.Selected = saved - 1;
            segFg.SelectedChanged += delegate { Settings.Set("fgMult", (segFg.Selected + 1).ToString()); RefreshPlan(); };
            c.Controls.Add(segFg);

            lblFgFps = TextLabel(c, "", 18, 198, 664, 30, Theme.F(12f, FontStyle.Bold), Theme.Accent);
            timeline = new FrameTimeline();
            timeline.SetBounds(18, 234, 664, 96);
            c.Controls.Add(timeline);

            tgDyn = Toggle(c, L.T("MFG dinâmico: varia de 2X a 6X para acompanhar o Hz do monitor (RTX 50)"), Settings.Get("fgDyn") == "1", 18, 346, 664);
            tgDyn.CheckedChanged += delegate { Settings.Set("fgDyn", tgDyn.Checked ? "1" : "0"); RefreshPlan(); };

            lblFgGame = TextLabel(c, "", 18, 392, 440, 60, Theme.F(9f), Theme.Muted);
            btnFgApply = new ModernButton(L.T("  Aplicar no jogo"), Theme.GCheck, ButtonKind.Primary);
            btnFgApply.SetBounds(472, 400, 210, 46);
            btnFgApply.Click += delegate
            {
                if (game == null) { tabs.Selected = 0; return; }
                DoInstall();
            };
            c.Controls.Add(btnFgApply);

            Card r = AddCard(P, null, L.T("Sua GPU"), 740, 4, 356, 470);
            rowFgOut = Row(r, L.T("Modelo de IA"), 50);
            rowFgMax = Row(r, L.T("Multiplicador máximo"), 100);
            rowFgVec = Row(r, L.T("Jogos com DLSS / FSR / XeSS"), 150);
            rowFgFlow = Row(r, L.T("Jogos sem Frame Generation"), 200);
            rowFgMod = Row(r, L.T("Componente extra"), 250);
            rowFgLat = Row(r, L.T("Latência"), 300);
            TextLabel(r, L.T("Dica: comece em 2X ou 3X. Para menos artefatos, tenha pelo menos 50–60 FPS reais antes de gerar quadros. ") +
                         L.T("Desligue o V-Sync do jogo; aperte Insert no jogo para ajustar em tempo real."),
                18, 356, 320, 96, Theme.F(8.5f), Theme.Dim);

            Card gen = AddCard(P, null, L.T("Suporte por geração de GPU"), 24, 486, 1072, 236);
            rowGen50 = new StatusRow(L.T("GeForce RTX 50"));
            rowGen50.SetBounds(18, 50, 336, 46);
            gen.Controls.Add(rowGen50);
            rowGen40 = new StatusRow(L.T("GeForce RTX 40"));
            rowGen40.SetBounds(368, 50, 336, 46);
            gen.Controls.Add(rowGen40);
            rowGen30 = new StatusRow(L.T("GeForce RTX 30 / RTX 20"));
            rowGen30.SetBounds(718, 50, 336, 46);
            gen.Controls.Add(rowGen30);
            TextLabel(gen,
                L.T("RTX 50 — DLSS 4 Multi Frame Generation nativo, 2X a 6X, ou modo dinâmico (acompanha o Hz do monitor).\r\n") +
                L.T("RTX 40 — DLSS Frame Generation 2X nativo; 3X e 4X pelo Intel XeSS Frame Generation do OptiScaler F5 (o desbloqueio de MFG da RTX 40 saiu na versão 0.1.33).\r\n") +
                L.T("RTX 30 / 20 — DLSS Multi Frame Generation até 4X pelo dlssg_for_sm86 (sdli1995, GPLv3), baixado automaticamente do GitHub na instalação. ") +
                L.T("Ele usa o runtime oficial da NVIDIA (nvngx_dlssg 310.9.16, assinatura conferida). Requer DX12 (ou DX9 via dgVoodoo); experimental.\r\n") +
                L.T("Jogos sem Frame Generation próprio recebem o MFG pelo OptiScaler; jogos que já têm recebem o multiplicador forçado no FG do próprio jogo."),
                18, 108, 1036, 120, Theme.F(9f), Theme.Muted);
        }

        StatusRow Row(Control parent, string caption, int y)
        {
            StatusRow s = new StatusRow(caption);
            s.SetBounds(18, y, 320, 46);
            parent.Controls.Add(s);
            return s;
        }

        void RefreshFg()
        {
            if (segFg == null || rowFgOut == null) return;
            int mult = segFg.Selected + 1;
            bool rtx50 = Gpu.IsNvidia && Gpu.Series >= 50;
            tgDyn.Enabled = rtx50;
            bool dyn = tgDyn.Checked && rtx50;
            segFg.Enabled = !dyn;
            GfxApi api = game != null ? SelectedApi() : GfxApi.DX12;
            if (api == GfxApi.DX9) api = GfxApi.DX12;        // dgVoodoo → DX12
            else if (api == GfxApi.DX10) api = GfxApi.Vulkan; // DXVK → Vulkan
            int cap;
            string label;
            Planner.FgOutput(api, dyn ? 2 : mult, out cap, out label);
            int eff = Math.Min(mult, cap);
            int maxCap;
            string maxLabel;
            Planner.FgOutput(api, 6, out maxCap, out maxLabel);
            maxCap = Math.Max(maxCap, cap);
            timeline.Set(dyn ? 4 : eff, dyn);
            if (dyn) lblFgFps.Text = L.T("Dinâmico: 2X a 6X conforme o Hz do monitor");
            else if (mult == 1) lblFgFps.Text = L.T("Desligado — só quadros reais");
            else lblFgFps.Text = L.T("60 FPS reais  →  até ") + (60 * eff) + L.T(" FPS na tela") + (eff < mult ? L.T("   (limitado a ") + cap + L.T("X)") : "");

            bool nvAi = label.StartsWith("NVIDIA");
            rowFgOut.Set(nvAi ? Theme.GCheck : Theme.GInfo, nvAi ? Theme.Accent : Theme.Warn, label);
            rowFgMax.Set(maxCap >= 6 ? Theme.GCheck : Theme.GInfo, maxCap >= 6 ? Theme.Accent : Theme.Info,
                maxCap + "X" + (Gpu.IsNvidia && Gpu.Series == 40 && maxCap > 2 ? L.T("  (3X–4X via XeSS-FG)") : Gpu.Series == 30 || Gpu.Series == 20 ? L.T("  (experimental)") : ""));
            rowFgVec.Set(Theme.GCheck, Theme.Accent, L.T("Usa os vetores de movimento do jogo"));
            rowFgFlow.Set(Theme.GCheck, Theme.Accent, L.T("Liberado pelo OptiScaler F5"));
            if (Gpu.IsNvidia && Gpu.Series >= 50) rowFgMod.Set(Theme.GCheck, Theme.Accent, L.T("Nenhum — suporte nativo"));
            else if (Gpu.IsNvidia && Gpu.Series == 40) rowFgMod.Set(Theme.GCheck, Theme.Accent, L.T("Nenhum — XeSS-FG incluso no OptiScaler F5"));
            else if (Gpu.IsNvidia && Gpu.Series >= 20)
            {
                bool cached = Directory.Exists(Path.Combine(AppPaths.Cache, "dlssg_for_sm86"));
                rowFgMod.Set(cached ? Theme.GCheck : Theme.GDownload, cached ? Theme.Accent : Theme.Info,
                    cached ? L.T("dlssg_for_sm86 já baixado") : L.T("dlssg_for_sm86 — baixado na instalação"));
            }
            else rowFgMod.Set(Theme.GInfo, Theme.Warn, L.T("DLSS-G exige NVIDIA RTX — usando XeSS-FG"));
            rowFgLat.Set(Theme.GInfo, Theme.Info, Gpu.IsNvidia ? L.T("NVIDIA Reflex reduz a latência extra") : L.T("XeLL / Anti-Lag reduzem a latência extra"));

            StatusRow mine = Gpu.Series >= 50 ? rowGen50 : Gpu.Series == 40 ? rowGen40 : Gpu.Series >= 20 ? rowGen30 : null;
            rowGen50.Set(mine == rowGen50 ? Theme.GCheck : Theme.GChip, mine == rowGen50 ? Theme.Accent : Theme.Muted, L.T("Até 6X · dinâmico") + (mine == rowGen50 ? L.T(" · sua GPU") : ""));
            rowGen40.Set(mine == rowGen40 ? Theme.GCheck : Theme.GChip, mine == rowGen40 ? Theme.Accent : Theme.Muted, L.T("2X DLSS-G · 4X XeSS-FG") + (mine == rowGen40 ? L.T(" · sua GPU") : ""));
            rowGen30.Set(mine == rowGen30 ? Theme.GCheck : Theme.GChip, mine == rowGen30 ? Theme.Accent : Theme.Muted, L.T("Até 4X (dlssg_for_sm86)") + (mine == rowGen30 ? L.T(" · sua GPU") : ""));

            lblFgGame.Text = game == null
                ? L.T("Nenhum jogo selecionado. Escolha um na Biblioteca para aplicar.")
                : L.T("Jogo: ") + game.ExeName + "  ·  " + Planner.ApiName(SelectedApi()) +
                  (game.Ours != null ? L.T("\r\n\"Aplicar\" reinstala com o novo multiplicador.") : L.T("\r\n\"Aplicar\" instala o DLSS 5 com este Frame Gen."));
            btnFgApply.Text = game == null ? L.T("  Ir para a Biblioteca") : L.T("  Aplicar no jogo");
            btnFgApply.Enabled = !busy;
            btnFgLink.Text = L.T("  Frame Gen: ") + (dyn ? L.T("dinâmico") : mult == 1 ? L.T("desligado") : eff + "X") + "  ›";
        }

        // ================================================================ helpers gerais

        void Log(string s)
        {
            if (InvokeRequired) { SafeInvoke(delegate { Log(s); }); return; }
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + "\r\n");
        }

        void Progress(int pct)
        {
            if (InvokeRequired) { SafeInvoke(delegate { Progress(pct); }); return; }
            bar.Value = pct;
        }

        void Busy(bool b, string status)
        {
            busy = b;
            foreach (Control c in new Control[] { btnInstall, btnUninstall, btnOpen, cmbExe, cmbApi, cmbMethod, btnNr, btnSl, drop, btnFgApply })
                c.Enabled = !b;
            if (!b) btnInstall.Enabled = CurrentPlan() != null;
            lblStatus.ForeColor = Theme.Muted;
            lblStatus.Text = status ?? "";
            if (b) bar.Value = 0;
            UseWaitCursor = b;
        }

        void RunBg(string status, Action work, Action done)
        {
            Busy(true, status);
            Thread t = new Thread(delegate()
            {
                Exception err = null;
                try { work(); }
                catch (Exception ex) { err = ex; }
                SafeInvoke(delegate
                {
                    Busy(false, null);
                    if (err != null)
                    {
                        Log(L.T("ERRO: ") + err.Message);
                        lblStatus.ForeColor = Theme.Danger;
                        lblStatus.Text = L.T("Erro: ") + err.Message;
                        MessageBox.Show(this, err.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (done != null) done();
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        bool importingSl;

        /// <summary>Streamline fora da pasta do app: copia o necessário para components\streamline e passa a usar a cópia.</summary>
        void ImportStreamlineAsync()
        {
            string src = slDir;
            if (importingSl || !Installer.IsStreamlineDir(src) || AppPaths.IsInside(src, AppPaths.Components)) return;
            importingSl = true;
            rowSl.Set(Theme.GSync, Theme.Info, L.T("Copiando para a pasta do app..."));
            if (rowSlCfg != null) rowSlCfg.Set(rowSl.Glyph, rowSl.Accent, rowSl.Detail);
            Thread t = new Thread(delegate()
            {
                string dest = null;
                Exception err = null;
                try { dest = Installer.ImportStreamline(src, AppPaths.Components); } catch (Exception ex) { err = ex; }
                SafeInvoke(delegate
                {
                    importingSl = false;
                    if (err == null && Installer.IsStreamlineDir(dest))
                    {
                        if (slDir == src) slDir = dest;
                        Settings.Set("streamline", slDir);
                        Log(L.T("Streamline copiado para a pasta do app: ") + dest);
                    }
                    else if (err != null) Log(L.T("Não foi possível copiar para a pasta do app: ") + err.Message);
                    UpdateSlRow();
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        static string FindStreamline()
        {
            // 1º: a cópia na pasta do app
            string own = Path.Combine(AppPaths.Components, "streamline");
            if (Installer.IsStreamlineDir(own)) return own;
            string d = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 4 && d != null; i++)
            {
                if (Installer.IsStreamlineDir(d)) return d.TrimEnd('\\');
                DirectoryInfo p = Directory.GetParent(d.TrimEnd('\\'));
                d = p == null ? null : p.FullName;
            }
            try
            {
                foreach (string c in Directory.GetDirectories(Util.Downloads, "streamline-sdk*"))
                    if (Installer.IsStreamlineDir(c)) return c;
            }
            catch { }
            // Cópia oficial já baixada pelo app
            try
            {
                string cache = Path.Combine(AppPaths.Cache, "streamline");
                if (Directory.Exists(cache))
                    foreach (string c in Directory.GetDirectories(cache).OrderByDescending(x => x))
                        if (File.Exists(Path.Combine(c, ".ok")) && Installer.IsStreamlineDir(c)) return c;
            }
            catch { }
            return null;
        }

        void UpdateSlRow()
        {
            if (Installer.IsStreamlineDir(slDir))
            {
                string b = Installer.SlBin(slDir);
                rowSl.Set(Theme.GCheck, Theme.Accent, "Streamline " + Util.FileVerStr(Path.Combine(b, "sl.interposer.dll")) +
                    "  ·  DLSS " + Util.FileVerStr(Path.Combine(b, "nvngx_dlss.dll")) + "  ·  " + Path.GetFileName(slDir));
            }
            else rowSl.Set(Theme.GDownload, Theme.Info, L.T("Será baixado da NVIDIA (GitHub oficial) na instalação"));
            if (rowSlCfg != null) rowSlCfg.Set(rowSl.Glyph, rowSl.Accent, rowSl.Detail);
        }

        /// <summary>A linha da runtime do DLSS 5 aparece no Injetor e em Configurações.</summary>
        void SetNrRow(string glyph, Color accent, string text)
        {
            rowNr.Set(glyph, accent, text);
            if (rowNrCfg != null) rowNrCfg.Set(glyph, accent, text);
            // Sem a runtime: mostra o botão que abre a pasta de instalação do app (onde fica o zip da runtime)
            bool missing = string.IsNullOrEmpty(nrPath) || !File.Exists(nrPath);
            if (btnNrApp != null) { btnNrApp.Visible = missing; rowNr.Width = missing ? 296 : 400; }
            if (btnNrAppCfg != null && rowNrCfg != null) { btnNrAppCfg.Visible = missing; rowNrCfg.Width = missing ? 296 : 400; }
        }

        // ================================================================ bandeja e inicialização com o Windows

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "DLSS5Definitive";

        static bool StartupEnabled()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(RunName) != null;
            }
            catch { return false; }
        }

        static void SetStartup(bool on)
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" /minimized");
                else k.DeleteValue(RunName, false);
            }
        }

        /// <summary>Se o exe registrado para iniciar com o Windows não existe mais (app mudou de pasta), aponta para este.</summary>
        static void FixStartupPath()
        {
            try
            {
                string v;
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                    v = k == null ? null : k.GetValue(RunName) as string;
                if (string.IsNullOrEmpty(v)) return;
                int q = v.IndexOf('"', 1);
                string exe = v.StartsWith("\"") && q > 0 ? v.Substring(1, q - 1) : v;
                if (!File.Exists(exe)) SetStartup(true);
            }
            catch { }
        }

        void BuildTray()
        {
            tray = new NotifyIcon();
            tray.Icon = Icon;
            tray.Text = Program.AppName;
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add(L.T("Abrir"), null, delegate { RestoreFromTray(); });
            m.Items.Add(L.T("Sair"), null, delegate { exiting = true; Close(); });
            tray.ContextMenuStrip = m;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) RestoreFromTray(); };
            tray.Visible = Settings.Get("trayOnClose") == "1";
        }

        void HideToTray(bool tip)
        {
            tray.Visible = true;
            Hide();
            if (tip && !trayTipShown)
            {
                trayTipShown = true;
                tray.ShowBalloonTip(3000, Program.AppName, L.T("Continua aberto aqui. Clique no ícone para abrir; botão direito → Sair para fechar."), ToolTipIcon.Info);
            }
        }

        void RestoreFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            tray.Visible = Settings.Get("trayOnClose") == "1";
        }

        /// <summary>Escolher a runtime (zip ou dll) começando na pasta de instalação do app.</summary>
        void BrowseNrInAppFolder()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                d.Filter = L.T("Runtime do DLSS 5 (nvngx_dlssnr.dll ou .zip)") + "|*.zip;nvngx_dlssnr.dll|ZIP|*.zip|DLL|*.dll";
                d.Title = L.T("Selecione o zip da runtime do DLSS 5 na pasta do app");
                if (d.ShowDialog(this) == DialogResult.OK && AcceptNrFile(d.FileName)) SetNr(d.FileName, true);
            }
        }

        // ================================================================ CONFIGURAÇÕES

        void BuildSettings()
        {
            Panel P = pageCfg;

            // ---- Idioma
            Card cLang = AddCard(P, null, L.T("Idioma"), 24, 4, 528, 196);
            Place(cLang, new FieldLabel(L.T("Idioma do aplicativo")), 18, 50);
            cmbLang = Combo(cLang, 16, 70, 496);
            cmbLang.Items.Add(L.T("Automático (idioma do Windows: ") + L.NativeName(L.SystemLang()) + ")");
            cmbLang.Items.Add(L.NativeName("pt"));
            cmbLang.Items.Add(L.NativeName("en"));
            cmbLang.Items.Add(L.NativeName("es"));
            langLoading = true;
            cmbLang.SelectedIndex = Array.IndexOf(L.Codes, L.Setting);
            langLoading = false;
            cmbLang.SelectedIndexChanged += delegate { if (!langLoading) ChangeLanguage(L.Codes[cmbLang.SelectedIndex]); };
            TextLabel(cLang, L.T("O app reinicia para aplicar o novo idioma."), 18, 118, 492, 40, Theme.F(9f), Theme.Muted);

            // ---- Atualizações
            Card cUpd = AddCard(P, null, L.T("Atualizações"), 24, 212, 528, 220);
            rowUpd = new StatusRow(L.T("Versão instalada"));
            rowUpd.SetBounds(16, 50, 330, 46);
            cUpd.Controls.Add(rowUpd);
            ModernButton btnCheck = new ModernButton(L.T("  Verificar agora"), Theme.GSync, ButtonKind.Secondary);
            btnCheck.SetBounds(352, 56, 160, 36);
            btnCheck.Click += delegate { if (pendingUpdate != null) DoUpdate(); else CheckForUpdates(true); };
            cUpd.Controls.Add(btnCheck);
            tgAutoUpd = Toggle(cUpd, L.T("Verificar atualizações automaticamente (ao abrir e a cada 6 h)"), Settings.Get("autoUpdate") != "0", 16, 112, 496);
            tgAutoUpd.CheckedChanged += delegate { Settings.Set("autoUpdate", tgAutoUpd.Checked ? "1" : "0"); };
            UpdateUpdRow(null, null);

            // ---- Arquivos do DLSS
            Card cFiles = AddCard(P, null, L.T("Arquivos do DLSS"), 568, 4, 528, 196);
            rowSlCfg = new StatusRow(L.T("NVIDIA Streamline SDK"));
            rowSlCfg.SetBounds(16, 52, 400, 42);
            cFiles.Controls.Add(rowSlCfg);
            ModernButton b1 = new ModernButton(L.T("Alterar"), null, ButtonKind.Ghost);
            b1.SetBounds(430, 58, 82, 30);
            b1.Click += delegate { BrowseStreamline(); };
            cFiles.Controls.Add(b1);
            rowNrCfg = new StatusRow(L.T("Runtime DLSS 5 (nvngx_dlssnr.dll)"));
            rowNrCfg.SetBounds(16, 112, 400, 42);
            cFiles.Controls.Add(rowNrCfg);
            ModernButton b2 = new ModernButton(L.T("Alterar"), null, ButtonKind.Ghost);
            b2.SetBounds(430, 118, 82, 30);
            b2.Click += delegate { BrowseNr(); };
            cFiles.Controls.Add(b2);
            btnNrAppCfg = new ModernButton(L.T("Pasta do app"), null, ButtonKind.Ghost);
            btnNrAppCfg.SetBounds(318, 118, 108, 30);
            btnNrAppCfg.Click += delegate { BrowseNrInAppFolder(); };
            btnNrAppCfg.Visible = false;
            cFiles.Controls.Add(btnNrAppCfg);
            Label lblComp = TextLabel(cFiles, L.T("Guardados na pasta do app: ") + AppPaths.Components, 18, 160, 492, 30, Theme.F(8f), Theme.Dim);
            lblComp.AutoEllipsis = true;

            // ---- Dados do aplicativo
            Card cData = AddCard(P, null, L.T("Dados do aplicativo"), 568, 212, 528, 220);
            lblData = TextLabel(cData, "", 18, 48, 492, 92, Theme.F(9f), Theme.Muted);
            lblData.AutoEllipsis = true;
            ModernButton bOpen = new ModernButton(L.T("  Abrir pasta de dados"), Theme.GOpen, ButtonKind.Secondary);
            bOpen.SetBounds(16, 156, 240, 40);
            bOpen.Click += delegate { Process.Start("explorer.exe", "\"" + AppPaths.Root + "\""); };
            cData.Controls.Add(bOpen);
            ModernButton bClear = new ModernButton(L.T("  Limpar downloads"), Theme.GDelete, ButtonKind.Secondary);
            bClear.SetBounds(268, 156, 244, 40);
            bClear.Click += delegate { ClearDownloads(); };
            cData.Controls.Add(bClear);

            // ---- Sistema: bandeja e inicialização com o Windows
            Card cSys = AddCard(P, null, L.T("Sistema"), 24, 444, 528, 244);
            tgTray = Toggle(cSys, L.T("Minimizar para a bandeja ao fechar"), Settings.Get("trayOnClose") == "1", 16, 52, 496);
            TextLabel(cSys, L.T("O X esconde a janela e o app fica no ícone perto do relógio. Para sair de vez, clique com o botão direito no ícone → Sair."),
                18, 86, 492, 40, Theme.F(8.5f), Theme.Muted);
            tgStartup = Toggle(cSys, L.T("Iniciar com o Windows (minimizado)"), StartupEnabled(), 16, 140, 496);
            TextLabel(cSys, L.T("Abre sozinho quando você entra no Windows, já minimizado (na bandeja, se a opção acima estiver ligada)."),
                18, 174, 492, 40, Theme.F(8.5f), Theme.Muted);
            tgTray.CheckedChanged += delegate
            {
                Settings.Set("trayOnClose", tgTray.Checked ? "1" : "0");
                if (tray != null) tray.Visible = tgTray.Checked || !Visible;
            };
            tgStartup.CheckedChanged += delegate
            {
                try { SetStartup(tgStartup.Checked); }
                catch (Exception ex) { Log(L.T("Não foi possível alterar a inicialização com o Windows: ") + ex.Message); }
            };

            // ---- Sobre
            Card cAbout = AddCard(P, null, L.T("Sobre"), 568, 444, 528, 244);
            TextLabel(cAbout, Program.AppName + "  " + Program.Version + "  —  " + L.T("criado por") + " " + Program.Author,
                18, 48, 492, 28, Theme.F(12f, FontStyle.Bold), Theme.Accent);
            TextLabel(cAbout, L.T("DLSS 5 Neural Rendering e Multi Frame Generation por IA para DX12, DX11, Vulkan, DX10 e DX9."),
                18, 80, 492, 40, Theme.F(9.5f), Theme.Text);
            TextLabel(cAbout, L.T("Componentes de terceiros: OptiScaler F5 (janblade), NVIDIA Streamline, dlssg_for_sm86 (sdli1995), RenoDX, ReShade, DXVK e dgVoodoo2 — cada um com a própria licença."),
                18, 124, 492, 56, Theme.F(9f), Theme.Muted);
            ModernButton bGit = new ModernButton(L.T("  Página no GitHub"), Theme.GOpen, ButtonKind.Secondary);
            bGit.SetBounds(16, 188, 220, 40);
            bGit.Click += delegate { Process.Start("https://github.com/" + Updater.Repo); };
            cAbout.Controls.Add(bGit);
            ModernButton bLic = new ModernButton(L.T("  Créditos e licenças"), Theme.GInfo, ButtonKind.Secondary);
            bLic.SetBounds(248, 188, 264, 40);
            bLic.Click += delegate { Process.Start("https://github.com/" + Updater.Repo + "#créditos"); };
            cAbout.Controls.Add(bLic);
        }

        void RefreshSettings()
        {
            if (lblData == null) return;
            string mode = AppPaths.IsPortable ? L.T("Portable (dados ao lado do exe)")
                        : Updater.IsInstalledCopy ? L.T("Instalado") : L.T("Executando fora da pasta instalada");
            long bytes = 0;
            try
            {
                foreach (string d in Directory.GetDirectories(AppPaths.Cache))
                {
                    if (Path.GetFileName(d).Equals("covers", StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (string f in Directory.GetFiles(d, "*", SearchOption.AllDirectories)) bytes += new FileInfo(f).Length;
                }
            }
            catch { }
            lblData.Text = L.T("Modo: ") + mode + "\r\n" + AppPaths.Root + "\r\n" + L.T("Downloads em cache: ") + (bytes / 1048576) + " MB";
        }

        void UpdateUpdRow(UpdateInfo info, Exception err)
        {
            if (rowUpd == null) return;
            string last = Settings.Get("lastUpdateCheck");
            if (info != null)
                rowUpd.Set(Theme.GDownload, Theme.Warn, "v" + Program.Version + "  ·  " + L.T("Nova versão disponível: ") + info.Tag);
            else if (err != null)
                rowUpd.Set(Theme.GError, Theme.Danger, "v" + Program.Version + "  ·  " + err.Message);
            else if (!string.IsNullOrEmpty(last))
                rowUpd.Set(Theme.GCheck, Theme.Accent, "v" + Program.Version + "  ·  " + L.T("Você está na versão mais recente") + "  (" + last + ")");
            else
                rowUpd.Set(Theme.GInfo, Theme.Info, "v" + Program.Version + "  ·  " + L.T("Ainda não verificado"));
        }

        void ChangeLanguage(string code)
        {
            Settings.Set("lang", code);
            string newCode = code == "auto" ? L.SystemLang() : code;
            if (newCode == L.Code) return;
            // Pergunta no idioma novo, que é o que a pessoa escolheu
            string old = L.Code;
            L.Code = newCode;
            string q = L.T("Reiniciar o DLSS 5 Definitive agora para aplicar o idioma?");
            L.Code = old;
            if (busy || updating) return;
            if (MessageBox.Show(this, q, Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                if (scanner != null) scanner.Cancel = true;
                LibraryStore.Save(games);
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath);
                psi.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                psi.UseShellExecute = true;
                Process.Start(psi);
                Application.Exit();
            }
            catch { }
        }

        void ClearDownloads()
        {
            if (busy || updating) return;
            if (MessageBox.Show(this, L.T("Apagar os downloads em cache (OptiScaler, Streamline, DXVK...)? Eles são baixados de novo quando necessário. As capas e a biblioteca são mantidas."),
                    Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (string d in Directory.GetDirectories(AppPaths.Cache))
            {
                if (Path.GetFileName(d).Equals("covers", StringComparison.OrdinalIgnoreCase)) continue;
                try { Directory.Delete(d, true); } catch { }
            }
            // A pasta do Streamline baixado pelo app pode ter sido apagada
            if (!Installer.IsStreamlineDir(slDir)) { slDir = FindStreamline(); UpdateSlRow(); }
            RefreshSettings();
            MessageBox.Show(this, L.T("✔ Downloads em cache apagados."), Program.AppName);
        }

        void BrowseStreamline()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = L.T("Pasta do NVIDIA Streamline SDK (contém bin\\x64)");
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (!Installer.IsStreamlineDir(d.SelectedPath))
                {
                    MessageBox.Show(this, L.T("Essa pasta não tem bin\\x64\\nvngx_dlss.dll e sl.interposer.dll."), Program.AppName);
                    return;
                }
                slDir = d.SelectedPath;
                Settings.Set("streamline", slDir);
                UpdateSlRow();
                ImportStreamlineAsync();
            }
        }

        void BrowseNr()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = L.T("Runtime do DLSS 5 (nvngx_dlssnr.dll ou .zip)") + "|nvngx_dlssnr.dll;*.zip|DLL|*.dll|ZIP|*.zip";
                d.Title = L.T("Selecione a runtime do DLSS 5 (nvngx_dlssnr.dll)");
                if (d.ShowDialog(this) == DialogResult.OK && AcceptNrFile(d.FileName)) SetNr(d.FileName, true);
            }
        }

        bool importingNr, obtainingNr;

        static string NrMissingText()
        {
            return L.T("Falta a runtime do DLSS 5 (nvngx_dlssnr.dll). A NVIDIA ainda não publica esse arquivo para download: ele vem dentro de jogos com DLSS 5 (ex.: NBA 2K27), e o app procura nos seus jogos instalados automaticamente. ") +
                   L.T("Você também pode colocá-lo em Downloads, arrastá-lo para a janela ou usar Alterar.");
        }

        /// <summary>
        /// Runtime ausente em todo lugar conhecido: tenta o GitHub oficial da NVIDIA e os jogos instalados, em segundo plano.
        /// </summary>
        void AutoObtainNr()
        {
            if (obtainingNr || importingNr || (!string.IsNullOrEmpty(nrPath) && File.Exists(nrPath))) return;
            obtainingNr = true;
            SetNrRow(Theme.GSync, Theme.Info, L.T("Procurando a runtime do DLSS 5 nos jogos instalados... "));
            Thread t = new Thread(delegate()
            {
                string got = null;
                try
                {
                    got = RuntimeProvider.Obtain(AppPaths.Components, delegate(string s) { SafeInvoke(delegate { SetNrRow(Theme.GSync, Theme.Info, s); }); });
                }
                catch { }
                SafeInvoke(delegate
                {
                    obtainingNr = false;
                    if (got != null)
                    {
                        Log(L.T("Runtime do DLSS 5 copiada para a pasta do app: ") + got);
                        UseNr(got, false);
                    }
                    else if (string.IsNullOrEmpty(nrPath) || !File.Exists(nrPath))
                        SetNrRow(Theme.GError, Theme.Danger, L.T("Não encontrada no PC nem nos jogos — coloque o nvngx_dlssnr.dll em Downloads, arraste para cá ou clique em Alterar"));
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        void SetNr(string path, bool manual)
        {
            // Runtime fora da pasta do app (Downloads, escolhida, arrastada): copia para components e usa a cópia,
            // assim o app continua funcionando mesmo se o arquivo original for apagado.
            if (!string.IsNullOrEmpty(path) && File.Exists(path) && !AppPaths.IsInside(path, AppPaths.Components))
            {
                ImportNr(path, manual);
                return;
            }
            UseNr(path, manual);
        }

        void ImportNr(string src, bool manual)
        {
            if (importingNr) return;
            importingNr = true;
            nrInfo = null;
            nrPath = src;
            SetNrRow(Theme.GSync, Theme.Info, L.T("Copiando para a pasta do app..."));
            Thread t = new Thread(delegate()
            {
                string dest = null;
                Exception err = null;
                bool zip = src.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                try { dest = NrRuntime.ImportAny(src, AppPaths.Components); } catch (Exception ex) { err = ex; }
                SafeInvoke(delegate
                {
                    importingNr = false;
                    if (err != null)
                    {
                        Log(L.T("Não foi possível copiar para a pasta do app: ") + err.Message);
                        // Do zip não dá para usar o original: volta para a runtime que já existia (se houver)
                        UseNr(zip ? NrRuntime.FindCandidate(slDir, AppPaths.Components) : src, manual);
                        if (zip) MessageBox.Show(this, err.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Log(L.T("Runtime do DLSS 5 copiada para a pasta do app: ") + dest);
                    UseNr(dest, manual);
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        void UseNr(string path, bool manual)
        {
            nrInfo = null;
            nrPath = path;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                SetNrRow(Theme.GError, Theme.Danger, L.T("Não encontrado — coloque o nvngx_dlssnr.dll em Downloads, arraste para cá ou clique em Alterar"));
                RefreshPlan();
                return;
            }
            Settings.Set("nr", path);
            if (manual) Settings.Set("nrManual", "1");
            SetNrRow(Theme.GSync, Theme.Info, L.T("Verificando assinatura e SHA-256..."));
            Thread t = new Thread(delegate()
            {
                NrRuntime.Info info = null;
                try { info = NrRuntime.Inspect(path); } catch { }
                SafeInvoke(delegate
                {
                    if (nrPath != path) return;
                    nrInfo = info;
                    if (info == null) SetNrRow(Theme.GError, Theme.Danger, L.T("Não foi possível ler o arquivo"));
                    else if (info.Warning == null) SetNrRow(Theme.GCheck, Theme.Accent, info.Label + L.T("  ·  compatível com sua GPU"));
                    else SetNrRow(Theme.GWarn, Theme.Warn, info.Label + L.T("  ·  veja o aviso no plano"));
                    RefreshPlan();
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        // ================================================================ fluxo do injetor

        void OnDragEnter(object s, DragEventArgs e)
        {
            if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                drop.IsDragOver = true;
                drop.Invalidate();
            }
        }

        void OnDragDrop(object s, DragEventArgs e)
        {
            drop.IsDragOver = false;
            drop.Invalidate();
            string[] f = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (f == null || f.Length == 0) return;
            if (File.Exists(f[0]) && (f[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || f[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                // Arrastar a runtime (nvngx_dlssnr.dll ou um zip com ela) para a janela
                if (AcceptNrFile(f[0])) { SetNr(f[0], true); Log(L.T("Runtime do DLSS 5 encontrada: ") + f[0]); }
                return;
            }
            tabs.Selected = 1;
            if (File.Exists(f[0]) && f[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) SetFolder(Path.GetDirectoryName(f[0]), f[0]);
            else if (Directory.Exists(f[0])) SetFolder(f[0], null);
        }

        void Browse()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = L.T("Selecione a pasta de instalação do jogo");
                string last = Settings.Get("lastFolder");
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) d.SelectedPath = folder;
                else if (!string.IsNullOrEmpty(last) && Directory.Exists(last)) d.SelectedPath = last;
                if (d.ShowDialog(this) == DialogResult.OK) SetFolder(d.SelectedPath, null);
            }
        }

        string ExeFromItem(int i) { return i >= 0 && i < exeList.Count ? exeList[i] : null; }

        void SetFolder(string dir, string preferExe)
        {
            if (busy || string.IsNullOrEmpty(dir)) return;
            folder = dir.TrimEnd('\\');
            Settings.Set("lastFolder", folder);
            GameEntry known = games.Find(g => LibraryStore.Key(g.Path) == LibraryStore.Key(folder));
            drop.Filled = true;
            drop.Headline = known != null ? known.Name : Path.GetFileName(folder);
            drop.Sub = folder;
            drop.Invalidate();
            List<string> exes = null;
            RunBg(L.T("Procurando executáveis..."), delegate { exes = GameScanner.FindExes(folder); }, delegate
            {
                exeList = exes;
                loadingUi = true;
                cmbExe.Clear();
                foreach (string e in exes)
                    cmbExe.Items.Add(e.Length > folder.Length ? e.Substring(folder.Length).TrimStart('\\') : Path.GetFileName(e));
                loadingUi = false;
                if (exes.Count == 0)
                {
                    game = null;
                    ShowAnalysis();
                    lblNotes.Text = L.T("Nenhum .exe encontrado nessa pasta.");
                    RefreshMethods();
                    return;
                }
                int idx = 0;
                if (preferExe != null)
                {
                    int i = exes.FindIndex(x => string.Equals(x, preferExe, StringComparison.OrdinalIgnoreCase));
                    if (i >= 0) idx = i;
                }
                loadingUi = true;
                cmbExe.SelectedIndex = idx;
                loadingUi = false;
                AnalyzeExe(exes[idx]);
            });
        }

        void AnalyzeExe(string exe)
        {
            if (exe == null || busy) return;
            GameInfo g = null;
            string root = folder;
            Log(L.T("Analisando ") + Path.GetFileName(exe));
            RunBg(L.T("Analisando o jogo..."), delegate { g = GameScanner.Analyze(root, exe); }, delegate
            {
                game = g;
                ShowAnalysis();
                loadingUi = true;
                cmbApi.SelectedIndex = 0;
                loadingUi = false;
                RefreshMethods();
                lblStatus.Text = g.Ours != null ? L.T("DLSS 5 Definitive já está instalado neste jogo (") + g.Ours.Method + ")." : L.T("Análise concluída.");
            });
        }

        void ShowAnalysis()
        {
            GameInfo g = game;
            if (g == null)
            {
                foreach (InfoTile t in new[] { tApi, tEngine, tArch, tUpscale, tAnti, tMods }) t.Set("—", Theme.Muted);
                lblNotes.Text = "";
                return;
            }
            bool apiOk = g.Api != GfxApi.Unknown && g.Api != GfxApi.OpenGL;
            tApi.Set(Planner.ApiName(g.Api), apiOk ? Theme.Accent : Theme.Danger);
            tEngine.Set(g.Engine, Theme.Info);
            tArch.Set(!g.PeValid ? L.T("Inválido") : g.Is64 ? "64-bit" : L.T("32-bit (não suportado)"), g.PeValid && g.Is64 ? Theme.Accent : Theme.Danger);
            List<string> up = new List<string>();
            if (g.HasDlss) up.Add("DLSS");
            if (g.HasDlssD) up.Add("RR");
            if (g.HasDlssG) up.Add("DLSS-FG");
            if (g.HasFsr) up.Add("FSR");
            if (g.HasXess) up.Add("XeSS");
            tUpscale.Set(up.Count == 0 ? L.T("Nenhum · Optical F5Low") : string.Join(" · ", up.ToArray()), up.Count == 0 ? Theme.Muted : Theme.Accent);
            tAnti.Set(g.AntiCheat ?? L.T("Nenhum detectado"), g.AntiCheat == null ? Theme.Accent : Theme.Danger);
            List<string> mods = new List<string>();
            foreach (string m in g.Mods) if (!m.Contains("Microsoft (original)")) mods.Add(m.Split(' ')[0]);
            tMods.Set(mods.Count == 0 ? L.T("Nenhum") : string.Join(", ", mods.ToArray()), g.Ours != null ? Theme.Accent : mods.Count == 0 ? Theme.Muted : Theme.Warn);

            List<string> notes = new List<string>();
            notes.Add(L.T("Detecção: ") + g.ApiSource + ".");
            notes.AddRange(g.ApiNotes);
            if (g.Ours != null) notes.Add(L.T("Já instalado — instalar de novo substitui; Remover restaura o original."));
            lblNotes.Text = string.Join("  ", notes.ToArray());
        }

        GfxApi SelectedApi()
        {
            if (game == null) return GfxApi.Unknown;
            GfxApi a = ApiChoices[Math.Max(0, cmbApi.SelectedIndex)];
            return a == GfxApi.Unknown ? game.Api : a;
        }

        void RefreshMethods()
        {
            loadingUi = true;
            cmbMethod.Clear();
            methods.Clear();
            methodWhy = "";
            if (game != null)
            {
                methods = Planner.Candidates(game, SelectedApi(), out methodWhy);
                for (int i = 0; i < methods.Count; i++)
                    cmbMethod.Items.Add((i == 0 ? L.T("★ Recomendado: ") : L.T("Alternativa: ")) + Planner.Name(methods[i]));
                if (methods.Count > 0) cmbMethod.SelectedIndex = 0;
            }
            loadingUi = false;
            RefreshPlan();
        }

        Options CurrentOptions()
        {
            Options o = new Options();
            o.FgMult = segFg != null ? segFg.Selected + 1 : 1;
            o.FgDynamic = tgDyn != null && tgDyn.Checked && Gpu.IsNvidia && Gpu.Series >= 50;
            o.UpdateDlss = tgUpdate.Checked;
            o.Log = tgLog.Checked;
            o.Passes = segPasses.Selected + 1;
            o.RunBeforeSR = tgBefore.Checked;
            o.Hdr = tgHdr.Checked;
            return o;
        }

        InstallPlan CurrentPlan()
        {
            if (game == null || methods.Count == 0 || cmbMethod.SelectedIndex < 0 || cmbMethod.SelectedIndex >= methods.Count) return null;
            return Planner.Build(game, SelectedApi(), methods[cmbMethod.SelectedIndex], CurrentOptions());
        }

        void RefreshPlan()
        {
            if (steps == null || tgUpdate == null) return;
            RefreshFg();
            InstallPlan p = CurrentPlan();
            if (p == null)
            {
                steps.SetContent(null, null, null, game == null ? L.T("Escolha um jogo na Biblioteca ou arraste a pasta dele para cá.") : L.T("Nenhum método compatível. ") + methodWhy);
                btnInstall.Enabled = false;
                return;
            }
            btnInstall.Enabled = !busy;
            bool opti = p.Method != Method.RenoDX;
            tgBefore.Enabled = opti && game.HasUpscaler && p.Method == Method.Opti;
            segPasses.Enabled = opti;
            tgLog.Enabled = opti;
            tgHdr.Enabled = opti && p.Method != Method.DxvkOpti && p.Api != GfxApi.Vulkan;

            string header = Planner.Name(p.Method) + "  ·  " + Planner.ApiName(p.Api);
            if (cmbMethod.SelectedIndex == 0 && !string.IsNullOrEmpty(methodWhy)) header += L.T("\r\n") + methodWhy;
            List<string> warns = new List<string>(p.Warnings);
            if (nrInfo != null && nrInfo.Warning != null) warns.Add(nrInfo.Warning);
            if (string.IsNullOrEmpty(nrPath) || !File.Exists(nrPath)) warns.Add(NrMissingText());
            steps.SetContent(header, p.Steps, warns, null);
        }

        void DoInstall()
        {
            InstallPlan p = CurrentPlan();
            if (p == null || game == null) return;
            if (string.IsNullOrEmpty(nrPath) || !File.Exists(nrPath))
            {
                // Procura de novo antes de reclamar (o arquivo pode ter sido colocado depois que o app abriu)
                string again = NrRuntime.FindCandidate(slDir, AppPaths.Components);
                if (again != null)
                {
                    SetNr(again, false);
                    MessageBox.Show(this, L.T("Runtime do DLSS 5 encontrada: ") + again + "\r\n\r\n" + L.T("Clique em Instalar DLSS 5 de novo."),
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                // Busca completa: GitHub oficial da NVIDIA (se publicado) e jogos instalados no PC. Se achar, segue a instalação.
                if (obtainingNr) return;
                obtainingNr = true;
                string obtained = null;
                RunBg(L.T("Procurando a runtime do DLSS 5 nos jogos instalados... "), delegate
                {
                    try { obtained = RuntimeProvider.Obtain(AppPaths.Components, Log); } catch (Exception ex) { Log(ex.Message); }
                }, delegate
                {
                    obtainingNr = false;
                    if (obtained != null)
                    {
                        Log(L.T("Runtime do DLSS 5 copiada para a pasta do app: ") + obtained);
                        UseNr(obtained, false);
                        DoInstall();
                        return;
                    }
                    tabs.Selected = 1;
                    // Primeiro oferece a pasta de instalação do app (onde fica o zip da runtime); "Não" deixa o Alterar de sempre
                    DialogResult r = MessageBox.Show(this, NrMissingText() + "\r\n\r\n" +
                        L.T("Abrir a pasta de instalação do app para escolher o zip da runtime (ex.: DLL DLSS 5.zip)?") + "\r\n" +
                        L.T("Sim = pasta do app   ·   Não = Alterar (escolher em outro lugar)"),
                        Program.AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                    if (r == DialogResult.Yes) BrowseNrInAppFolder();
                    else if (r == DialogResult.No) BrowseNr();
                });
                return;
            }
            if (!Util.CanWrite(game.ExeDir))
            {
                if (MessageBox.Show(this, L.T("Sem permissão para gravar em:\r\n") + game.ExeDir + L.T("\r\n\r\nReiniciar o programa como administrador?"), Program.AppName,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, "\"" + folder + "\"");
                        psi.Verb = "runas";
                        psi.UseShellExecute = true;
                        Process.Start(psi);
                        exiting = true;
                        Close();
                    }
                    catch { }
                }
                return;
            }
            string msg = L.T("Instalar ") + Planner.Name(p.Method) + L.T(" em:\r\n") + game.ExeDir +
                         L.T("\r\n\r\nArquivos existentes serão guardados como .dlss5bak e podem ser restaurados com \"Remover\".");
            if (!string.IsNullOrEmpty(game.AntiCheat))
                msg += L.T("\r\n\r\n⚠ ANTI-CHEAT DETECTADO (") + game.AntiCheat + L.T("): use apenas offline. Em modo online isso pode resultar em banimento.");
            if (MessageBox.Show(this, msg, Program.AppName, MessageBoxButtons.OKCancel, string.IsNullOrEmpty(game.AntiCheat) ? MessageBoxIcon.Information : MessageBoxIcon.Warning) != DialogResult.OK)
                return;

            GameInfo g = game;
            string sl = slDir, nr = nrPath;
            RunBg("Instalando...", delegate
            {
                new Installer(Log, Progress).Install(g, p, sl, nr);
            }, delegate
            {
                bar.Value = 100;
                if (!Installer.IsStreamlineDir(slDir)) { slDir = FindStreamline(); UpdateSlRow(); }
                lblStatus.ForeColor = Theme.Accent;
                lblStatus.Text = L.T("✔ DLSS 5 instalado em ") + g.ExeName;
                RefreshInstalledFlag(g.ExeDir);
                MessageBox.Show(this, L.T("DLSS 5 instalado!\r\n\r\n") + (p.Method == Method.RenoDX
                    ? L.T("Abra o jogo e ative o DLSS nas opções gráficas. A tecla Home abre o ReShade/RenoDX.")
                    : L.T("Abra o jogo e aperte Insert para abrir o menu do OptiScaler → Neural Rendering (comece com 1 passe).")),
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                AnalyzeExe(g.ExePath);
            });
        }

        void DoUninstall()
        {
            if (game == null) return;
            if (Manifest.Load(game.ExeDir) == null)
            {
                MessageBox.Show(this, L.T("Não há instalação do DLSS 5 Definitive nesta pasta."), Program.AppName);
                return;
            }
            if (MessageBox.Show(this, L.T("Remover tudo que o DLSS 5 Definitive instalou e restaurar os arquivos originais?"), Program.AppName, MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;
            GameInfo g = game;
            RunBg("Removendo...", delegate { Installer.Uninstall(g.ExeDir, Log); }, delegate
            {
                lblStatus.ForeColor = Theme.Accent;
                lblStatus.Text = L.T("✔ Arquivos originais restaurados.");
                RefreshInstalledFlag(g.ExeDir);
                AnalyzeExe(g.ExePath);
            });
        }
    }
}
