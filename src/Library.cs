using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Dlss5Injector
{
    public class GameEntry
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Source { get; set; }
        public string AppId { get; set; }      // Steam appid (também usado para a capa)
        public string Cover { get; set; }      // arquivo de capa em cache
        public bool CoverTried { get; set; }
        public bool Installed { get; set; }    // DLSS 5 Definitive instalado nele
        public long Added { get; set; }
    }

    /// <summary>Biblioteca persistida em library.json.</summary>
    public static class LibraryStore
    {
        static string FilePath { get { return System.IO.Path.Combine(AppPaths.Root, "library.json"); } }

        public static List<GameEntry> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    js.MaxJsonLength = int.MaxValue;
                    List<GameEntry> l = js.Deserialize<List<GameEntry>>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (l != null) return l;
                }
            }
            catch { }
            return new List<GameEntry>();
        }

        public static void Save(List<GameEntry> games)
        {
            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                js.MaxJsonLength = int.MaxValue;
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, js.Serialize(games), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch { }
        }

        public static List<string> CustomFolders()
        {
            string s = Settings.Get("customFolders") ?? "";
            return s.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public static void AddCustomFolder(string dir)
        {
            List<string> l = CustomFolders();
            if (!l.Exists(x => string.Equals(x, dir, StringComparison.OrdinalIgnoreCase))) l.Add(dir);
            Settings.Set("customFolders", string.Join("|", l.ToArray()));
        }

        public static string Key(string path)
        {
            return System.IO.Path.GetFullPath(path).TrimEnd('\\').ToLowerInvariant();
        }
    }

    /// <summary>Procura jogos nos launchers (Steam, Epic, GOG, Ubisoft, EA, Xbox) e em pastas de jogos de todos os discos.</summary>
    public class LibraryScanner
    {
        readonly Action<GameEntry> found;
        readonly Action<string> status;
        readonly HashSet<string> seen = new HashSet<string>();
        public volatile bool Cancel;

        static readonly Regex NotGame = new Regex(
            @"redistributable|proton|steam linux runtime|steamworks|steamvr|dedicated server|\bsdk\b|soundtrack|benchmark tool|directx|vc\+\+|\.net|easyanticheat|battleye|wallpaper engine|source filmmaker|lossless scaling|social club|^launcher$|games launcher",
            RegexOptions.IgnoreCase);

        static readonly string[] SystemDirs = {
            "windows", "program files", "program files (x86)", "programdata", "users", "$recycle.bin", "system volume information",
            "recovery", "perflogs", "msocache", "intel", "amd", "nvidia", "drivers", "boot", "efi", "onedrivetemp", "windowsapps"
        };

        public LibraryScanner(Action<GameEntry> found, Action<string> status)
        {
            this.found = found;
            this.status = status;
        }

        void Add(string name, string path, string source, string appId)
        {
            if (Cancel || string.IsNullOrEmpty(path)) return;
            try
            {
                if (!Directory.Exists(path)) return;
                string key = LibraryStore.Key(path);
                if (seen.Contains(key)) return;
                if (string.IsNullOrEmpty(name)) name = PrettyName(System.IO.Path.GetFileName(path.TrimEnd('\\')));
                if (NotGame.IsMatch(name)) return;
                seen.Add(key);
                GameEntry g = new GameEntry();
                g.Name = name.Replace("™", "").Replace("®", "").Replace("©", "").Trim();
                g.Path = path.TrimEnd('\\');
                g.Source = source;
                g.AppId = appId;
                g.Installed = FindManifest(path, 0);
                g.Added = DateTime.UtcNow.Ticks;
                found(g);
            }
            catch { }
        }

        public static bool FindManifest(string dir, int depth)
        {
            try
            {
                if (File.Exists(System.IO.Path.Combine(dir, Manifest.FileName))) return true;
                if (depth >= 4) return false;
                foreach (string d in Directory.GetDirectories(dir))
                    if (FindManifest(d, depth + 1)) return true;
            }
            catch { }
            return false;
        }

        public void ScanAll()
        {
            Step("Steam", ScanSteam);
            Step("Epic Games", ScanEpic);
            Step("GOG", ScanGog);
            Step("Ubisoft Connect", ScanUbisoft);
            Step("EA app", ScanEa);
            Step("discos", ScanDrives);
            foreach (string f in LibraryStore.CustomFolders()) ScanFolder(f);
        }

        void Step(string what, Action a)
        {
            if (Cancel) return;
            status("Escaneando " + what + "...");
            try { a(); } catch { }
        }

        // ---------------------------------------------------------------- Steam

        public static string SteamPath()
        {
            foreach (string k in new[] { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam" })
            {
                try
                {
                    string p = Convert.ToString(Registry.GetValue(k, k.StartsWith("HKEY_CURRENT") ? "SteamPath" : "InstallPath", null));
                    if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p.Replace('/', '\\');
                }
                catch { }
            }
            return null;
        }

        void ScanSteam()
        {
            List<string> libs = new List<string>();
            string steam = SteamPath();
            if (steam != null)
            {
                libs.Add(steam);
                string vdf = System.IO.Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
            foreach (DriveInfo d in FixedDrives())
                foreach (string rel in new[] { @"SteamLibrary", @"Steam", @"Games\Steam", @"Games\SteamLibrary", @"Program Files (x86)\Steam" })
                    libs.Add(System.IO.Path.Combine(d.RootDirectory.FullName, rel));

            foreach (string lib in libs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string apps = System.IO.Path.Combine(lib, "steamapps");
                if (!Directory.Exists(apps)) continue;
                foreach (string acf in Directory.GetFiles(apps, "appmanifest_*.acf"))
                {
                    try
                    {
                        string t = File.ReadAllText(acf);
                        string id = Rx(t, "appid"), name = Rx(t, "name"), dir = Rx(t, "installdir");
                        if (dir == null) continue;
                        Add(name, System.IO.Path.Combine(apps, "common", dir), "Steam", id);
                    }
                    catch { }
                }
            }
        }

        static string Rx(string t, string key)
        {
            Match m = Regex.Match(t, "\"" + key + "\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        // ---------------------------------------------------------------- Epic

        void ScanEpic()
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Manifests");
            if (!Directory.Exists(dir)) return;
            JavaScriptSerializer js = new JavaScriptSerializer();
            foreach (string f in Directory.GetFiles(dir, "*.item"))
            {
                try
                {
                    Dictionary<string, object> d = js.DeserializeObject(File.ReadAllText(f)) as Dictionary<string, object>;
                    if (d == null) continue;
                    object inc;
                    if (d.TryGetValue("bIsIncompleteInstall", out inc) && true.Equals(inc)) continue;
                    Add(Convert.ToString(d["DisplayName"]), Convert.ToString(d["InstallLocation"]), "Epic", null);
                }
                catch { }
            }
        }

        // ---------------------------------------------------------------- GOG / Ubisoft / EA (registro)

        static IEnumerable<RegistryKey> SubKeys(string path)
        {
            RegistryKey root = null;
            try { root = Registry.LocalMachine.OpenSubKey(path); } catch { }
            if (root == null) yield break;
            foreach (string n in root.GetSubKeyNames())
            {
                RegistryKey k = null;
                try { k = root.OpenSubKey(n); } catch { }
                if (k != null) yield return k;
            }
        }

        void ScanGog()
        {
            foreach (RegistryKey k in SubKeys(@"SOFTWARE\WOW6432Node\GOG.com\Games"))
                using (k) Add(Convert.ToString(k.GetValue("gameName")), Convert.ToString(k.GetValue("path")), "GOG", null);
        }

        void ScanUbisoft()
        {
            foreach (RegistryKey k in SubKeys(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs"))
                using (k)
                {
                    string p = Convert.ToString(k.GetValue("InstallDir"));
                    if (!string.IsNullOrEmpty(p)) Add(null, p.Replace('/', '\\'), "Ubisoft", null);
                }
        }

        void ScanEa()
        {
            foreach (RegistryKey k in SubKeys(@"SOFTWARE\WOW6432Node\EA Games"))
                using (k)
                {
                    string p = Convert.ToString(k.GetValue("Install Dir"));
                    if (!string.IsNullOrEmpty(p)) Add(System.IO.Path.GetFileName(k.Name), p, "EA", null);
                }
        }

        // ---------------------------------------------------------------- discos

        static IEnumerable<DriveInfo> FixedDrives()
        {
            DriveInfo[] all;
            try { all = DriveInfo.GetDrives(); } catch { yield break; }
            foreach (DriveInfo d in all)
            {
                bool ok = false;
                try { ok = d.DriveType == DriveType.Fixed && d.IsReady; } catch { }
                if (ok) yield return d;
            }
        }

        void ScanDrives()
        {
            string[] roots = {
                @"Games", @"Jogos", @"Epic Games", @"Program Files\Epic Games", @"XboxGames", @"GOG Games", @"GOG Galaxy\Games",
                @"Program Files\EA Games", @"EA Games", @"Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games", @"Ubisoft Games",
                @"Program Files\Rockstar Games", @"Rockstar Games", @"Riot Games", @"Program Files (x86)\GOG Galaxy\Games",
                @"Program Files\Ubisoft\Ubisoft Game Launcher\games", @"Amazon Games\Library", @"Battle.net"
            };
            foreach (DriveInfo d in FixedDrives())
            {
                if (Cancel) return;
                string drive = d.RootDirectory.FullName;
                status("Escaneando disco " + drive + "...");
                List<string> dirs = new List<string>();
                foreach (string r in roots) dirs.Add(System.IO.Path.Combine(drive, r));
                // Pastas da raiz com nome de jogos (ex.: D:\Meus Jogos, E:\Games2)
                try
                {
                    foreach (string top in Directory.GetDirectories(drive))
                    {
                        string n = System.IO.Path.GetFileName(top).ToLowerInvariant();
                        if (SystemDirs.Contains(n)) continue;
                        if (n.Contains("game") || n.Contains("jogo") || n.Contains("steam") || n.Contains("epic")) dirs.Add(top);
                    }
                }
                catch { }
                foreach (string root in dirs.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!Directory.Exists(root)) continue;
                    string common = System.IO.Path.Combine(root, "steamapps", "common");
                    ScanFolderChildren(Directory.Exists(common) ? common : root, root.ToLowerInvariant().Contains("xboxgames") ? "Xbox" : "Pasta", 0);
                }
            }
        }

        public void ScanFolder(string folder)
        {
            if (!Directory.Exists(folder)) return;
            status("Escaneando " + folder + "...");
            string common = System.IO.Path.Combine(folder, "steamapps", "common");
            if (Directory.Exists(common)) { ScanFolderChildren(common, "Pasta", 0); return; }
            // A própria pasta é um jogo?
            if (LooksLikeGame(folder, true)) { Add(null, folder, "Pasta", null); return; }
            ScanFolderChildren(folder, "Pasta", 0);
        }

        void ScanFolderChildren(string root, string source, int depth)
        {
            string[] subs;
            try { subs = Directory.GetDirectories(root); } catch { return; }
            foreach (string s in subs)
            {
                if (Cancel) return;
                if (seen.Contains(LibraryStore.Key(s))) continue;
                string n = System.IO.Path.GetFileName(s).ToLowerInvariant();
                if (n.StartsWith("$") || n.StartsWith(".") || n == "steamapps" || n == "_commonredist") continue;
                if (LooksLikeGame(s, false)) Add(null, s, source, null);
                else if (depth == 0 && Directory.Exists(System.IO.Path.Combine(s, "steamapps", "common")))
                    ScanFolderChildren(System.IO.Path.Combine(s, "steamapps", "common"), source, 1);
            }
        }

        static readonly string[] BadExe = {
            "unins", "crash", "setup", "redist", "vc_redist", "vcredist", "dxsetup", "helper", "updater", "installer", "launcherpatcher",
            "uninstall", "dotnet", "prereq", "unitycrashhandler", "crashreport", "cefsharp", "easyanticheat", "beservice", "7z"
        };

        /// <summary>Tem um .exe de jogo (64/32-bit, maior que 1,5 MB, nome que não é instalador)?</summary>
        public static bool LooksLikeGame(string dir, bool shallowOk)
        {
            int budget = 4000;
            return FindGameExe(dir, 0, ref budget);
        }

        static bool FindGameExe(string dir, int depth, ref int budget)
        {
            string[] files;
            try { files = Directory.GetFiles(dir, "*.exe"); } catch { return false; }
            foreach (string f in files)
            {
                if (--budget < 0) return false;
                string n = System.IO.Path.GetFileName(f).ToLowerInvariant();
                bool bad = false;
                foreach (string b in BadExe) if (n.Contains(b)) { bad = true; break; }
                if (bad) continue;
                try { if (new FileInfo(f).Length > 1500000) return true; } catch { }
            }
            if (depth >= 4) return false;
            string[] dirs;
            try { dirs = Directory.GetDirectories(dir); } catch { return false; }
            foreach (string d in dirs)
            {
                if (--budget < 0) return false;
                string n = System.IO.Path.GetFileName(d).ToLowerInvariant();
                if (n == "_commonredist" || n == "redist" || n == "directx" || n == "__installer" || n == "support" || n == "easyanticheat") continue;
                if (FindGameExe(d, depth + 1, ref budget)) return true;
            }
            return false;
        }

        /// <summary>"FarCry5" → "Far Cry 5", "the_witcher-3" → "the witcher 3".</summary>
        public static string PrettyName(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace('_', ' ').Replace('.', ' ');
            s = Regex.Replace(s, @"(?<=[a-z])(?=[A-Z])|(?<=[A-Za-z])(?=\d)|(?<=\d)(?=[A-Za-z])", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }
    }

    /// <summary>Capas: cache local do Steam → CDN do Steam (por appid) → busca do nome na loja Steam.</summary>
    public class CoverService
    {
        readonly Queue<GameEntry> queue = new Queue<GameEntry>();
        readonly Action<GameEntry> done;
        int workers;
        const int MaxWorkers = 4;

        public CoverService(Action<GameEntry> done) { this.done = done; }

        public static string Dir
        {
            get
            {
                string d = System.IO.Path.Combine(AppPaths.Cache, "covers");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public void Request(GameEntry g)
        {
            lock (queue)
            {
                if (queue.Contains(g)) return;
                queue.Enqueue(g);
                if (workers >= MaxWorkers) return;
                workers++;
            }
            Thread t = new Thread(Work);
            t.IsBackground = true;
            t.Start();
        }

        void Work()
        {
            while (true)
            {
                GameEntry g;
                lock (queue)
                {
                    if (queue.Count == 0) { workers--; return; }
                    g = queue.Dequeue();
                }
                try { Resolve(g); } catch { }
                g.CoverTried = true;
                try { done(g); } catch { }
            }
        }

        static HttpWebRequest Req(string url)
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "DLSS5Definitive/2.0 (game library cover lookup)";
            r.Timeout = 15000;
            r.ReadWriteTimeout = 20000;
            return r;
        }

        static bool TryDownload(string url, string dest)
        {
            try
            {
                using (WebResponse resp = Req(url).GetResponse())
                using (Stream s = resp.GetResponseStream())
                using (MemoryStream ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    if (ms.Length < 2000) return false;
                    using (Image img = Image.FromStream(new MemoryStream(ms.ToArray()))) { if (img.Width < 50) return false; }
                    File.WriteAllBytes(dest, ms.ToArray());
                    return true;
                }
            }
            catch { return false; }
        }

        static string StripAccents(string s)
        {
            string d = (s ?? "").Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in d)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString();
        }

        static string Norm(string s)
        {
            return Regex.Replace(StripAccents(s).ToLowerInvariant(), "[^a-z0-9]", "");
        }

        static readonly Dictionary<string, string> Roman = new Dictionary<string, string> {
            { "ii", "2" }, { "iii", "3" }, { "iv", "4" }, { "vi", "6" }, { "vii", "7" }, { "viii", "8" }, { "ix", "9" }
        };

        /// <summary>Palavras normalizadas (sem acento, minúsculas, "III" → "3").</summary>
        static List<string> Words(string s)
        {
            return Regex.Split(StripAccents(s).ToLowerInvariant(), "[^a-z0-9]+")
                .Where(w => w.Length > 0)
                .Select(w => Roman.ContainsKey(w) ? Roman[w] : w)
                .ToList();
        }

        /// <summary>Números da sequência (2, 3, 2077...) precisam bater; e a maioria das palavras também.</summary>
        static bool SameGame(List<string> want, List<string> got)
        {
            if (want.Count == 0) return false;
            foreach (string w in want)
                if (Regex.IsMatch(w, @"^\d+$") && !got.Contains(w)) return false;
            foreach (string w in got)
                if (Regex.IsMatch(w, @"^\d{1,2}$") && !want.Contains(w)) return false;   // "Forza Horizon 4" ≠ "Forza Horizon"
            int hit = want.Count(w => got.Contains(w));
            return hit * 4 >= want.Count * 3;
        }

        /// <summary>Tira "(2026)", "[FitGirl]", "PC Port", "Repack"... que atrapalham a busca.</summary>
        public static string CleanName(string n)
        {
            n = Regex.Replace(n ?? "", @"\(.*?\)|\[.*?\]", " ");
            n = Regex.Replace(n, @"\b(pc port|pc|repack|portable|goty|v\d+(\.\d+)*)\s*$", " ", RegexOptions.IgnoreCase);
            return Regex.Replace(n, @"\s+", " ").Trim();
        }

        static string GetJson(string url)
        {
            // Steam e Wikipedia limitam requisições seguidas: tenta de novo com espera crescente.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using (WebResponse resp = Req(url).GetResponse())
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
                }
                catch (WebException)
                {
                    if (attempt >= 2) throw;
                    Thread.Sleep(1500 * (attempt + 1));
                }
            }
        }

        /// <summary>Busca o appid na loja Steam pelo nome do jogo.</summary>
        public static string SearchSteam(string name)
        {
            string id = SearchSteamOnce(name);
            string clean = CleanName(name);
            if (id == null && clean.Length > 0 && clean != name) id = SearchSteamOnce(clean);
            return id;
        }

        static string SearchSteamOnce(string name)
        {
            try
            {
                Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(
                    GetJson("https://store.steampowered.com/api/storesearch/?l=english&cc=US&term=" + Uri.EscapeDataString(name))) as Dictionary<string, object>;
                object[] items = d != null && d.ContainsKey("items") ? d["items"] as object[] : null;
                if (items == null || items.Length == 0) return null;
                string want = Norm(name);
                List<string> wantWords = Words(name);
                foreach (object o in items)
                {
                    Dictionary<string, object> it = o as Dictionary<string, object>;
                    if (it != null && Norm(Convert.ToString(it["name"])) == want) return Convert.ToString(it["id"]);
                }
                // Sem nome idêntico: aceita o primeiro resultado se ele contém todas as palavras buscadas
                Dictionary<string, object> f0 = items[0] as Dictionary<string, object>;
                string n0 = Convert.ToString(f0["name"]);
                List<string> got = Words(n0);
                bool all = wantWords.Count > 0 && wantWords.All(w => got.Contains(w)) && SameGame(wantWords, got.Where(w => !Regex.IsMatch(w, @"^\d{1,2}$") || wantWords.Contains(w)).ToList());
                return all ? Convert.ToString(f0["id"]) : null;
            }
            catch { return null; }
        }

        /// <summary>URLs exatas da capa vertical (library capsule) pela API da loja Steam — jogos novos usam caminhos com hash.</summary>
        static List<string> SteamAssetUrls(string appId)
        {
            List<string> l = new List<string>();
            try
            {
                string q = "{\"ids\":[{\"appid\":" + int.Parse(appId) + "}],\"context\":{\"language\":\"english\",\"country_code\":\"US\"},\"data_request\":{\"include_assets\":true}}";
                Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(
                    GetJson("https://api.steampowered.com/IStoreBrowseService/GetItems/v1/?input_json=" + Uri.EscapeDataString(q))) as Dictionary<string, object>;
                object[] items = ((Dictionary<string, object>)d["response"])["store_items"] as object[];
                Dictionary<string, object> assets = ((Dictionary<string, object>)items[0])["assets"] as Dictionary<string, object>;
                string fmt = Convert.ToString(assets["asset_url_format"]);
                foreach (string k in new[] { "library_capsule_2x", "library_capsule", "header" })
                    if (assets.ContainsKey(k))
                        l.Add("https://shared.cloudflare.steamstatic.com/store_item_assets/" + fmt.Replace("${FILENAME}", Convert.ToString(assets[k])));
            }
            catch { }
            return l;
        }

        /// <summary>Capa pela Wikipedia (arte da caixa do artigo do jogo) — para jogos que não estão na Steam.</summary>
        public static string SearchWikipediaCover(string name)
        {
            try
            {
                string clean = CleanName(name);
                string url = "https://en.wikipedia.org/w/api.php?action=query&generator=search&gsrlimit=6&prop=pageimages&piprop=original&pilicense=any&format=json&gsrsearch=" +
                             Uri.EscapeDataString(clean + " video game");
                Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(GetJson(url)) as Dictionary<string, object>;
                if (d == null || !d.ContainsKey("query")) return null;
                Dictionary<string, object> pages = ((Dictionary<string, object>)d["query"])["pages"] as Dictionary<string, object>;
                if (pages == null) return null;
                List<string> want = Words(clean);
                // As páginas vêm sem ordem: usa o "index" da busca
                foreach (Dictionary<string, object> pg in pages.Values.Cast<Dictionary<string, object>>()
                         .OrderBy(x => x.ContainsKey("index") ? Convert.ToInt32(x["index"]) : 99))
                {
                    if (!pg.ContainsKey("original")) continue;
                    // Título sem o "(video game)" / "(2018 video game)"
                    List<string> title = Words(Regex.Replace(Convert.ToString(pg["title"]), @"\(.*?\)", " "));
                    if (!SameGame(want, title)) continue;
                    string src = Convert.ToString(((Dictionary<string, object>)pg["original"])["source"]);
                    int q = src.IndexOf('?');
                    return q > 0 ? src.Substring(0, q) : src;
                }
            }
            catch { }
            return null;
        }

        static void Resolve(GameEntry g)
        {
            if (!string.IsNullOrEmpty(g.Cover) && File.Exists(g.Cover)) return;
            if (string.IsNullOrEmpty(g.AppId)) g.AppId = SearchSteam(g.Name);
            if (string.IsNullOrEmpty(g.AppId))
            {
                string wfile = System.IO.Path.Combine(Dir, "wiki_" + Norm(g.Name) + ".jpg");
                if (File.Exists(wfile)) { g.Cover = wfile; return; }
                string img = SearchWikipediaCover(g.Name);
                if (img != null && TryDownload(img, wfile)) g.Cover = wfile;
                return;
            }
            string dest = System.IO.Path.Combine(Dir, g.AppId + ".jpg");
            if (File.Exists(dest)) { g.Cover = dest; return; }

            string steam = LibraryScanner.SteamPath();
            if (steam != null)
            {
                string lc = System.IO.Path.Combine(steam, "appcache", "librarycache");
                foreach (string c in new[] {
                    System.IO.Path.Combine(lc, g.AppId + "_library_600x900.jpg"),
                    System.IO.Path.Combine(lc, g.AppId, "library_600x900.jpg") })
                {
                    if (File.Exists(c)) { File.Copy(c, dest, true); g.Cover = dest; return; }
                }
                string sub = System.IO.Path.Combine(lc, g.AppId);
                if (Directory.Exists(sub))
                {
                    string any = Directory.GetFiles(sub, "library_600x900*.jpg", SearchOption.AllDirectories).FirstOrDefault();
                    if (any != null) { File.Copy(any, dest, true); g.Cover = dest; return; }
                }
            }
            foreach (string url in SteamAssetUrls(g.AppId))
                if (TryDownload(url, dest)) { g.Cover = dest; return; }
            foreach (string url in new[] {
                "https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/" + g.AppId + "/library_600x900.jpg",
                "https://cdn.cloudflare.steamstatic.com/steam/apps/" + g.AppId + "/library_600x900.jpg",
                "https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/" + g.AppId + "/header.jpg" })
            {
                if (TryDownload(url, dest)) { g.Cover = dest; return; }
            }
        }

        /// <summary>Carrega a capa já reduzida para o tamanho do card (não trava o arquivo).</summary>
        public static Bitmap LoadScaled(string file, int w, int h)
        {
            try
            {
                using (Image img = Image.FromStream(new MemoryStream(File.ReadAllBytes(file))))
                {
                    Bitmap b = new Bitmap(w, h);
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        // "cover": preenche mantendo a proporção (capas horizontais são cortadas no centro)
                        float s = Math.Max(w / (float)img.Width, h / (float)img.Height);
                        float dw = img.Width * s, dh = img.Height * s;
                        g.DrawImage(img, (w - dw) / 2, (h - dh) / 2, dw, dh);
                    }
                    return b;
                }
            }
            catch { return null; }
        }
    }
}
