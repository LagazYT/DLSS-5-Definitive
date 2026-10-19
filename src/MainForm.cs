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
        ToggleSwitch tgUpdate, tgLog, tgBefore;
        ModernButton btnFgLink;
        Segmented segPasses;
        StatusRow rowSl, rowNr;
        ModernButton btnSl, btnNr, btnInstall, btnUninstall, btnOpen;
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

        public MainForm(string initialFolder)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = Program.AppName + " " + Program.Version + " — por " + Program.Author + (AppPaths.IsPortable ? "  (Portable)" : "");
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
            tabs = new TabBar(new[] { "Biblioteca", "Injetor DLSS 5", "Frame Gen" }, new[] { "", "", "" });
            tabs.SetBounds(318, 20, 470, 40);
            tabs.SelectedChanged += delegate { ShowPage(tabs.Selected); };
            Controls.Add(tabs);

            pageLib = NewPage();
            pageInj = NewPage();
            pageFg = NewPage();
            BuildLibrary();
            BuildInjector();
            BuildFrameGen();
            ShowPage(0);
            ResumeLayout(false);

            slDir = Settings.Get("streamline");
            if (!Installer.IsStreamlineDir(slDir)) slDir = FindStreamline();
            UpdateSlRow();
            string nr = Settings.Get("nr");
            string found = NrRuntime.FindCandidate(slDir);
            if (string.IsNullOrEmpty(nr) || !File.Exists(nr) ||
                (found != null && Settings.Get("nrManual") != "1" && Util.FileVer(found) > Util.FileVer(nr))) nr = found;
            SetNr(nr, false);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            Log("Pronto.");

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
            };
            FormClosing += delegate { if (scanner != null) scanner.Cancel = true; LibraryStore.Save(games); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitleBar(this);
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
                Theme.Draw(g, "DLSS 5 Definitive", f, Theme.Text, new Rectangle(80, 14, 240, 28), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            using (Font f = Theme.F(8.5f))
            using (Font fb = Theme.F(8.5f, FontStyle.Bold))
            {
                Theme.Draw(g, "criado por", f, Theme.Muted, new Rectangle(81, 41, 70, 20), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                int w = TextRenderer.MeasureText("criado por", f).Width;
                Theme.Draw(g, Program.Author, fb, Theme.Accent, new Rectangle(76 + w, 41, 80, 20), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            string gpu = Gpu.Name.Replace("NVIDIA ", "") + "  ·  " + Gpu.SeriesText;
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
            TextLabel(p, "Biblioteca", 24, 4, 300, 34, Theme.Title(16f), Theme.Text);
            lblLibCount = TextLabel(p, "", 26, 38, 380, 20, Theme.F(9f), Theme.Muted);

            search = new SearchBox("Buscar jogo...");
            search.SetBounds(420, 12, 300, 38);
            search.Box.TextChanged += delegate { ApplyFilter(); };
            p.Controls.Add(search);

            btnScan = new ModernButton("  Escanear PC", Theme.GSync, ButtonKind.Secondary);
            btnScan.SetBounds(732, 10, 172, 42);
            btnScan.Click += delegate { StartScan(null); };
            p.Controls.Add(btnScan);

            btnAddFolder = new ModernButton("  Adicionar pasta", Theme.GFolder, ButtonKind.Primary);
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

            lblEmpty = TextLabel(p, "Nenhum jogo ainda.\r\nClique em \"Escanear PC\" ou \"Adicionar pasta\".", 0, 330, 1120, 60, Theme.F(11f), Theme.Dim);
            lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            lblEmpty.BringToFront();

            cardMenu = new ContextMenuStrip();
            cardMenu.Renderer = new DarkMenuRenderer();
            cardMenu.ShowImageMargin = false;
            cardMenu.BackColor = Theme.Field;
            cardMenu.Font = Theme.F(9.5f);
            AddMenu("Configurar DLSS 5", delegate { if (menuCard != null) OpenGame(menuCard.Game); });
            AddMenu("Abrir pasta do jogo", delegate { if (menuCard != null) Process.Start("explorer.exe", "\"" + menuCard.Game.Path + "\""); });
            AddMenu("Buscar capa novamente", delegate
            {
                if (menuCard == null) return;
                menuCard.Game.Cover = null;
                menuCard.Game.CoverTried = false;
                if (menuCard.Game.Source != "Steam") menuCard.Game.AppId = null;
                covers.Request(menuCard.Game);
            });
            AddMenu("Remover da biblioteca", delegate { if (menuCard != null) RemoveGame(menuCard); });
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
            lblLibCount.Text = games.Count + " jogo(s)" + (vis != games.Count ? "  ·  " + vis + " na busca" : "") + (inst > 0 ? "  ·  " + inst + " com DLSS 5" : "");
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
                    lblScan.Text = "Escaneamento concluído · " + foundKeys.Count + " jogo(s) encontrados" + (added > 0 ? " · " + added + " novo(s)" : "") +
                                   "  ·  " + DateTime.Now.ToString("HH:mm");
                    UpdateLibCount();
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        void SafeInvoke(Action a)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); } catch { }
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
                d.Description = "Escolha uma pasta com jogos (ou a pasta de um jogo) para escanear";
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

            Card cGame = AddCard(P, "1", "Jogo", 24, 84 - o, 528, 232);
            drop = new DropZone();
            drop.SetBounds(16, 52, 496, 96);
            drop.Click += delegate { if (!busy) Browse(); };
            drop.DragEnter += OnDragEnter;
            drop.DragDrop += OnDragDrop;
            drop.DragLeave += delegate { drop.IsDragOver = false; drop.Invalidate(); };
            cGame.Controls.Add(drop);
            Place(cGame, new FieldLabel("Executável"), 18, 158);
            cmbExe = Combo(cGame, 16, 178, 496);
            cmbExe.SelectedIndexChanged += delegate { if (!loadingUi && cmbExe.SelectedIndex >= 0) AnalyzeExe(ExeFromItem(cmbExe.SelectedIndex)); };

            Card cAn = AddCard(P, "2", "Análise do jogo", 24, 328 - o, 528, 268);
            tApi = Tile(cAn, "API gráfica", Theme.GChip, 16, 52);
            tEngine = Tile(cAn, "Engine", Theme.GGame, 268, 52);
            tArch = Tile(cAn, "Arquitetura", Theme.GInfo, 16, 108);
            tUpscale = Tile(cAn, "Upscalers do jogo", Theme.GSync, 268, 108);
            tAnti = Tile(cAn, "Anti-cheat", Theme.GWarn, 16, 164);
            tMods = Tile(cAn, "Mods na pasta", Theme.GOpen, 268, 164);
            lblNotes = TextLabel(cAn, "", 16, 220, 496, 42, Theme.F(8.5f), Theme.Muted);

            Card cLog = AddCard(P, null, "Atividade", 24, 608 - o, 528, 120);
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

            Card cCfg = AddCard(P, "3", "Configuração", 568, 84 - o, 528, 340);
            Place(cCfg, new FieldLabel("Método (escolhido conforme a compatibilidade do jogo)"), 18, 50);
            cmbMethod = Combo(cCfg, 16, 70, 496);
            cmbMethod.SelectedIndexChanged += delegate { if (!loadingUi) RefreshPlan(); };
            Place(cCfg, new FieldLabel("API gráfica"), 18, 114);
            cmbApi = Combo(cCfg, 16, 134, 240);
            foreach (GfxApi a in ApiChoices) cmbApi.Items.Add(a == GfxApi.Unknown ? "Detectar automaticamente" : Planner.ApiName(a));
            cmbApi.SelectedIndex = 0;
            cmbApi.SelectedIndexChanged += delegate { if (!loadingUi) RefreshMethods(); };
            Place(cCfg, new FieldLabel("Passes do Neural Rendering"), 274, 114);
            segPasses = new Segmented("1 passe", "2 passes", "3 passes");
            segPasses.SetBounds(272, 135, 240, 34);
            segPasses.SelectedChanged += delegate { RefreshPlan(); };
            cCfg.Controls.Add(segPasses);

            btnFgLink = new ModernButton("  Frame Gen", "", ButtonKind.Secondary);
            btnFgLink.SetBounds(16, 180, 240, 30);
            btnFgLink.Font = Theme.F(9f);
            btnFgLink.Click += delegate { tabs.Selected = 2; };
            cCfg.Controls.Add(btnFgLink);
            tgBefore = Toggle(cCfg, "NR antes do upscaler", true, 272, 180, 240);
            tgUpdate = Toggle(cCfg, "Atualizar DLSS do jogo (310.9)", true, 16, 212, 240);
            tgLog = Toggle(cCfg, "Log de diagnóstico", false, 272, 212, 240);

            rowSl = new StatusRow("NVIDIA Streamline SDK");
            rowSl.SetBounds(16, 250, 400, 42);
            cCfg.Controls.Add(rowSl);
            btnSl = new ModernButton("Alterar", null, ButtonKind.Ghost);
            btnSl.SetBounds(430, 256, 82, 30);
            btnSl.Click += delegate { BrowseStreamline(); };
            cCfg.Controls.Add(btnSl);

            rowNr = new StatusRow("Runtime DLSS 5 (nvngx_dlssnr.dll)");
            rowNr.SetBounds(16, 292, 400, 42);
            cCfg.Controls.Add(rowNr);
            btnNr = new ModernButton("Alterar", null, ButtonKind.Ghost);
            btnNr.SetBounds(430, 298, 82, 30);
            btnNr.Click += delegate { BrowseNr(); };
            cCfg.Controls.Add(btnNr);

            Card cPlan = AddCard(P, "4", "Plano de instalação", 568, 436 - o, 528, 292);
            steps = new StepsView();
            steps.SetBounds(18, 50, 496, 232);
            cPlan.Controls.Add(steps);

            btnInstall = new ModernButton("  Instalar DLSS 5", Theme.GDownload, ButtonKind.Primary);
            btnInstall.SetBounds(24, 744 - o, 240, 48);
            btnInstall.Click += delegate { DoInstall(); };
            P.Controls.Add(btnInstall);
            btnUninstall = new ModernButton("  Remover", Theme.GDelete, ButtonKind.Secondary);
            btnUninstall.SetBounds(276, 744 - o, 136, 48);
            btnUninstall.Click += delegate { DoUninstall(); };
            P.Controls.Add(btnUninstall);
            btnOpen = new ModernButton("  Abrir pasta", Theme.GOpen, ButtonKind.Secondary);
            btnOpen.SetBounds(424, 744 - o, 128, 48);
            btnOpen.Click += delegate { if (game != null) Process.Start("explorer.exe", "\"" + game.ExeDir + "\""); };
            P.Controls.Add(btnOpen);

            lblStatus = TextLabel(P, "", 568, 748 - o, 528, 22, Theme.F(9f), Theme.Muted);
            bar = new ThinProgress();
            bar.SetBounds(568, 778 - o, 528, 6);
            P.Controls.Add(bar);

            foreach (ToggleSwitch t in new[] { tgUpdate, tgLog, tgBefore })
                t.CheckedChanged += delegate { RefreshPlan(); };
        }

        // ================================================================ FRAME GEN

        void BuildFrameGen()
        {
            Panel P = pageFg;
            Card c = AddCard(P, null, "Multi Frame Generation por IA  ·  NVIDIA DLSS 4", 24, 4, 700, 470);
            TextLabel(c, "O modelo de IA do DLSS Frame Generation da NVIDIA gera até 5 quadros novos para cada quadro real — também em " +
                    "jogos que não têm Frame Generation. O OptiScaler F5 entrega ao modelo os vetores de movimento do jogo ou, nos jogos " +
                    "sem suporte, a profundidade e o movimento que ele mesmo calcula. Funciona nas RTX 50, 40, 30 e 20.",
                    18, 48, 664, 54, Theme.F(9f), Theme.Muted);
            Place(c, new FieldLabel("Multiplicador de quadros  (1X = desligado)"), 20, 108);
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

            tgDyn = Toggle(c, "MFG dinâmico: varia de 2X a 6X para acompanhar o Hz do monitor (RTX 50)", Settings.Get("fgDyn") == "1", 18, 346, 664);
            tgDyn.CheckedChanged += delegate { Settings.Set("fgDyn", tgDyn.Checked ? "1" : "0"); RefreshPlan(); };

            lblFgGame = TextLabel(c, "", 18, 392, 440, 60, Theme.F(9f), Theme.Muted);
            btnFgApply = new ModernButton("  Aplicar no jogo", Theme.GCheck, ButtonKind.Primary);
            btnFgApply.SetBounds(472, 400, 210, 46);
            btnFgApply.Click += delegate
            {
                if (game == null) { tabs.Selected = 0; return; }
                DoInstall();
            };
            c.Controls.Add(btnFgApply);

            Card r = AddCard(P, null, "Sua GPU", 740, 4, 356, 470);
            rowFgOut = Row(r, "Modelo de IA", 50);
            rowFgMax = Row(r, "Multiplicador máximo", 100);
            rowFgVec = Row(r, "Jogos com DLSS / FSR / XeSS", 150);
            rowFgFlow = Row(r, "Jogos sem Frame Generation", 200);
            rowFgMod = Row(r, "Componente extra", 250);
            rowFgLat = Row(r, "Latência", 300);
            TextLabel(r, "Dica: comece em 2X ou 3X. Para menos artefatos, tenha pelo menos 50–60 FPS reais antes de gerar quadros. " +
                         "Desligue o V-Sync do jogo; aperte Insert no jogo para ajustar em tempo real.",
                18, 356, 320, 96, Theme.F(8.5f), Theme.Dim);

            Card gen = AddCard(P, null, "Suporte por geração de GPU", 24, 486, 1072, 236);
            rowGen50 = new StatusRow("GeForce RTX 50");
            rowGen50.SetBounds(18, 50, 336, 46);
            gen.Controls.Add(rowGen50);
            rowGen40 = new StatusRow("GeForce RTX 40");
            rowGen40.SetBounds(368, 50, 336, 46);
            gen.Controls.Add(rowGen40);
            rowGen30 = new StatusRow("GeForce RTX 30 / RTX 20");
            rowGen30.SetBounds(718, 50, 336, 46);
            gen.Controls.Add(rowGen30);
            TextLabel(gen,
                "RTX 50 — DLSS 4 Multi Frame Generation nativo, 2X a 6X, ou modo dinâmico (acompanha o Hz do monitor).\r\n" +
                "RTX 40 — DLSS Frame Generation 2X nativo; 3X e 4X pelo desbloqueio de MFG embutido no OptiScaler F5 (experimental, sem arquivo extra).\r\n" +
                "RTX 30 / 20 — DLSS Multi Frame Generation até 4X pelo dlssg_for_sm86 (sdli1995, GPLv3), baixado automaticamente do GitHub na instalação. " +
                "Ele usa o runtime oficial da NVIDIA (nvngx_dlssg 310.9.16, assinatura conferida). Requer DX12 (ou DX9 via dgVoodoo); experimental.\r\n" +
                "Jogos sem Frame Generation próprio recebem o MFG pelo OptiScaler; jogos que já têm recebem o multiplicador forçado no FG do próprio jogo.",
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
            Planner.FgOutput(api, out cap, out label);
            int eff = Math.Min(mult, cap);
            timeline.Set(dyn ? 4 : eff, dyn);
            if (dyn) lblFgFps.Text = "Dinâmico: 2X a 6X conforme o Hz do monitor";
            else if (mult == 1) lblFgFps.Text = "Desligado — só quadros reais";
            else lblFgFps.Text = "60 FPS reais  →  até " + (60 * eff) + " FPS na tela" + (eff < mult ? "   (limitado a " + cap + "X)" : "");

            bool nvAi = label.StartsWith("NVIDIA");
            rowFgOut.Set(nvAi ? Theme.GCheck : Theme.GInfo, nvAi ? Theme.Accent : Theme.Warn, label);
            rowFgMax.Set(cap >= 6 ? Theme.GCheck : Theme.GInfo, cap >= 6 ? Theme.Accent : Theme.Info,
                cap + "X" + (Gpu.Series == 40 ? "  (3X–4X: desbloqueio experimental)" : Gpu.Series == 30 || Gpu.Series == 20 ? "  (experimental)" : ""));
            rowFgVec.Set(Theme.GCheck, Theme.Accent, "Usa os vetores de movimento do jogo");
            rowFgFlow.Set(Theme.GCheck, Theme.Accent, "Liberado pelo OptiScaler F5");
            if (Gpu.IsNvidia && Gpu.Series >= 50) rowFgMod.Set(Theme.GCheck, Theme.Accent, "Nenhum — suporte nativo");
            else if (Gpu.IsNvidia && Gpu.Series == 40) rowFgMod.Set(Theme.GCheck, Theme.Accent, "Desbloqueio MFG embutido no OptiScaler F5");
            else if (Gpu.IsNvidia && Gpu.Series >= 20)
            {
                bool cached = Directory.Exists(Path.Combine(AppPaths.Cache, "dlssg_for_sm86"));
                rowFgMod.Set(cached ? Theme.GCheck : Theme.GDownload, cached ? Theme.Accent : Theme.Info,
                    cached ? "dlssg_for_sm86 já baixado" : "dlssg_for_sm86 — baixado na instalação");
            }
            else rowFgMod.Set(Theme.GInfo, Theme.Warn, "DLSS-G exige NVIDIA RTX — usando XeSS-FG");
            rowFgLat.Set(Theme.GInfo, Theme.Info, Gpu.IsNvidia ? "NVIDIA Reflex reduz a latência extra" : "XeLL / Anti-Lag reduzem a latência extra");

            StatusRow mine = Gpu.Series >= 50 ? rowGen50 : Gpu.Series == 40 ? rowGen40 : Gpu.Series >= 20 ? rowGen30 : null;
            rowGen50.Set(mine == rowGen50 ? Theme.GCheck : Theme.GChip, mine == rowGen50 ? Theme.Accent : Theme.Muted, "Até 6X · dinâmico" + (mine == rowGen50 ? " · sua GPU" : ""));
            rowGen40.Set(mine == rowGen40 ? Theme.GCheck : Theme.GChip, mine == rowGen40 ? Theme.Accent : Theme.Muted, "Até 4X (desbloqueio)" + (mine == rowGen40 ? " · sua GPU" : ""));
            rowGen30.Set(mine == rowGen30 ? Theme.GCheck : Theme.GChip, mine == rowGen30 ? Theme.Accent : Theme.Muted, "Até 4X (dlssg_for_sm86)" + (mine == rowGen30 ? " · sua GPU" : ""));

            lblFgGame.Text = game == null
                ? "Nenhum jogo selecionado. Escolha um na Biblioteca para aplicar."
                : "Jogo: " + game.ExeName + "  ·  " + Planner.ApiName(SelectedApi()) +
                  (game.Ours != null ? "\r\n\"Aplicar\" reinstala com o novo multiplicador." : "\r\n\"Aplicar\" instala o DLSS 5 com este Frame Gen.");
            btnFgApply.Text = game == null ? "  Ir para a Biblioteca" : "  Aplicar no jogo";
            btnFgApply.Enabled = !busy;
            btnFgLink.Text = "  Frame Gen: " + (dyn ? "dinâmico" : mult == 1 ? "desligado" : eff + "X") + "  ›";
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
                        Log("ERRO: " + err.Message);
                        lblStatus.ForeColor = Theme.Danger;
                        lblStatus.Text = "Erro: " + err.Message;
                        MessageBox.Show(this, err.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (done != null) done();
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        static string FindStreamline()
        {
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
            else rowSl.Set(Theme.GDownload, Theme.Info, "Será baixado da NVIDIA (GitHub oficial) na instalação");
        }

        void BrowseStreamline()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "Pasta do NVIDIA Streamline SDK (contém bin\\x64)";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (!Installer.IsStreamlineDir(d.SelectedPath))
                {
                    MessageBox.Show(this, "Essa pasta não tem bin\\x64\\nvngx_dlss.dll e sl.interposer.dll.", Program.AppName);
                    return;
                }
                slDir = d.SelectedPath;
                Settings.Set("streamline", slDir);
                UpdateSlRow();
            }
        }

        void BrowseNr()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "nvngx_dlssnr.dll|nvngx_dlssnr.dll|DLL|*.dll";
                d.Title = "Selecione a runtime do DLSS 5 (nvngx_dlssnr.dll)";
                if (d.ShowDialog(this) == DialogResult.OK) SetNr(d.FileName, true);
            }
        }

        void SetNr(string path, bool manual)
        {
            nrInfo = null;
            nrPath = path;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                rowNr.Set(Theme.GError, Theme.Danger, "Não encontrado — clique em Alterar e selecione o arquivo");
                RefreshPlan();
                return;
            }
            Settings.Set("nr", path);
            if (manual) Settings.Set("nrManual", "1");
            rowNr.Set(Theme.GSync, Theme.Info, "Verificando assinatura e SHA-256...");
            Thread t = new Thread(delegate()
            {
                NrRuntime.Info info = null;
                try { info = NrRuntime.Inspect(path); } catch { }
                SafeInvoke(delegate
                {
                    if (nrPath != path) return;
                    nrInfo = info;
                    if (info == null) rowNr.Set(Theme.GError, Theme.Danger, "Não foi possível ler o arquivo");
                    else if (info.Warning == null) rowNr.Set(Theme.GCheck, Theme.Accent, info.Label + "  ·  compatível com sua GPU");
                    else rowNr.Set(Theme.GWarn, Theme.Warn, info.Label + "  ·  veja o aviso no plano");
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
            tabs.Selected = 1;
            if (File.Exists(f[0]) && f[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) SetFolder(Path.GetDirectoryName(f[0]), f[0]);
            else if (Directory.Exists(f[0])) SetFolder(f[0], null);
        }

        void Browse()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "Selecione a pasta de instalação do jogo";
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
            RunBg("Procurando executáveis...", delegate { exes = GameScanner.FindExes(folder); }, delegate
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
                    lblNotes.Text = "Nenhum .exe encontrado nessa pasta.";
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
            Log("Analisando " + Path.GetFileName(exe));
            RunBg("Analisando o jogo...", delegate { g = GameScanner.Analyze(root, exe); }, delegate
            {
                game = g;
                ShowAnalysis();
                loadingUi = true;
                cmbApi.SelectedIndex = 0;
                loadingUi = false;
                RefreshMethods();
                lblStatus.Text = g.Ours != null ? "DLSS 5 Definitive já está instalado neste jogo (" + g.Ours.Method + ")." : "Análise concluída.";
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
            tArch.Set(!g.PeValid ? "Inválido" : g.Is64 ? "64-bit" : "32-bit (não suportado)", g.PeValid && g.Is64 ? Theme.Accent : Theme.Danger);
            List<string> up = new List<string>();
            if (g.HasDlss) up.Add("DLSS");
            if (g.HasDlssD) up.Add("RR");
            if (g.HasDlssG) up.Add("DLSS-FG");
            if (g.HasFsr) up.Add("FSR");
            if (g.HasXess) up.Add("XeSS");
            tUpscale.Set(up.Count == 0 ? "Nenhum · Optical F5Low" : string.Join(" · ", up.ToArray()), up.Count == 0 ? Theme.Muted : Theme.Accent);
            tAnti.Set(g.AntiCheat ?? "Nenhum detectado", g.AntiCheat == null ? Theme.Accent : Theme.Danger);
            List<string> mods = new List<string>();
            foreach (string m in g.Mods) if (!m.Contains("Microsoft (original)")) mods.Add(m.Split(' ')[0]);
            tMods.Set(mods.Count == 0 ? "Nenhum" : string.Join(", ", mods.ToArray()), g.Ours != null ? Theme.Accent : mods.Count == 0 ? Theme.Muted : Theme.Warn);

            List<string> notes = new List<string>();
            notes.Add("Detecção: " + g.ApiSource + ".");
            notes.AddRange(g.ApiNotes);
            if (g.Ours != null) notes.Add("Já instalado — instalar de novo substitui; Remover restaura o original.");
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
                    cmbMethod.Items.Add((i == 0 ? "★ Recomendado: " : "Alternativa: ") + Planner.Name(methods[i]));
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
                steps.SetContent(null, null, null, game == null ? "Escolha um jogo na Biblioteca ou arraste a pasta dele para cá." : "Nenhum método compatível. " + methodWhy);
                btnInstall.Enabled = false;
                return;
            }
            btnInstall.Enabled = !busy;
            bool opti = p.Method != Method.RenoDX;
            tgBefore.Enabled = opti && game.HasUpscaler && p.Method == Method.Opti;
            segPasses.Enabled = opti;
            tgLog.Enabled = opti;

            string header = Planner.Name(p.Method) + "  ·  " + Planner.ApiName(p.Api);
            if (cmbMethod.SelectedIndex == 0 && !string.IsNullOrEmpty(methodWhy)) header += "\r\n" + methodWhy;
            List<string> warns = new List<string>(p.Warnings);
            if (nrInfo != null && nrInfo.Warning != null) warns.Add(nrInfo.Warning);
            if (string.IsNullOrEmpty(nrPath) || !File.Exists(nrPath)) warns.Add("Falta a runtime do DLSS 5 (nvngx_dlssnr.dll): use Alterar na seção 3.");
            steps.SetContent(header, p.Steps, warns, null);
        }

        void DoInstall()
        {
            InstallPlan p = CurrentPlan();
            if (p == null || game == null) return;
            if (string.IsNullOrEmpty(nrPath) || !File.Exists(nrPath))
            {
                tabs.Selected = 1;
                MessageBox.Show(this, "Falta a runtime do DLSS 5: nvngx_dlssnr.dll.\r\n\r\nEla não vem no OptiScaler, no RenoDX nem no Streamline SDK. " +
                    "Use o botão Alterar ao lado de \"Runtime DLSS 5\".", Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Util.CanWrite(game.ExeDir))
            {
                if (MessageBox.Show(this, "Sem permissão para gravar em:\r\n" + game.ExeDir + "\r\n\r\nReiniciar o programa como administrador?", Program.AppName,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, "\"" + folder + "\"");
                        psi.Verb = "runas";
                        psi.UseShellExecute = true;
                        Process.Start(psi);
                        Close();
                    }
                    catch { }
                }
                return;
            }
            string msg = "Instalar " + Planner.Name(p.Method) + " em:\r\n" + game.ExeDir +
                         "\r\n\r\nArquivos existentes serão guardados como .dlss5bak e podem ser restaurados com \"Remover\".";
            if (!string.IsNullOrEmpty(game.AntiCheat))
                msg += "\r\n\r\n⚠ ANTI-CHEAT DETECTADO (" + game.AntiCheat + "): use apenas offline. Em modo online isso pode resultar em banimento.";
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
                lblStatus.Text = "✔ DLSS 5 instalado em " + g.ExeName;
                RefreshInstalledFlag(g.ExeDir);
                MessageBox.Show(this, "DLSS 5 instalado!\r\n\r\n" + (p.Method == Method.RenoDX
                    ? "Abra o jogo e ative o DLSS nas opções gráficas. A tecla Home abre o ReShade/RenoDX."
                    : "Abra o jogo e aperte Insert para abrir o menu do OptiScaler → Neural Rendering (comece com 1 passe)."),
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                AnalyzeExe(g.ExePath);
            });
        }

        void DoUninstall()
        {
            if (game == null) return;
            if (Manifest.Load(game.ExeDir) == null)
            {
                MessageBox.Show(this, "Não há instalação do DLSS 5 Definitive nesta pasta.", Program.AppName);
                return;
            }
            if (MessageBox.Show(this, "Remover tudo que o DLSS 5 Definitive instalou e restaurar os arquivos originais?", Program.AppName, MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;
            GameInfo g = game;
            RunBg("Removendo...", delegate { Installer.Uninstall(g.ExeDir, Log); }, delegate
            {
                lblStatus.ForeColor = Theme.Accent;
                lblStatus.Text = "✔ Arquivos originais restaurados.";
                RefreshInstalledFlag(g.ExeDir);
                AnalyzeExe(g.ExePath);
            });
        }
    }
}
