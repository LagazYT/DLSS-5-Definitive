using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dlss5Injector
{
    public enum GfxApi { Unknown, DX9, DX10, DX11, DX12, Vulkan, OpenGL }

    public class PeInfo
    {
        public bool Valid;
        public bool Is64;
        public ushort Machine;
        public List<string> Imports = new List<string>();
    }

    public static class Pe
    {
        public static PeInfo Read(string path)
        {
            PeInfo pi = new PeInfo();
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length < 0x200) return pi;
                    if (br.ReadUInt16() != 0x5A4D) return pi;
                    fs.Position = 0x3C;
                    int lfanew = br.ReadInt32();
                    if (lfanew <= 0 || lfanew > fs.Length - 264) return pi;
                    fs.Position = lfanew;
                    if (br.ReadUInt32() != 0x4550) return pi;
                    pi.Machine = br.ReadUInt16();
                    ushort nsec = br.ReadUInt16();
                    br.ReadBytes(12);
                    ushort optSize = br.ReadUInt16();
                    br.ReadUInt16();
                    long optStart = fs.Position;
                    ushort magic = br.ReadUInt16();
                    pi.Is64 = magic == 0x20b;
                    long dd = optStart + (pi.Is64 ? 112 : 96);
                    fs.Position = dd - 4;
                    uint n = br.ReadUInt32();
                    uint imp = 0, dimp = 0;
                    if (n > 1) { fs.Position = dd + 8; imp = br.ReadUInt32(); }
                    if (n > 13) { fs.Position = dd + 13 * 8; dimp = br.ReadUInt32(); }
                    fs.Position = optStart + optSize;
                    List<uint[]> secs = new List<uint[]>();
                    for (int i = 0; i < nsec && i < 96; i++)
                    {
                        br.ReadBytes(8);
                        uint vs = br.ReadUInt32();
                        uint va = br.ReadUInt32();
                        uint rs = br.ReadUInt32();
                        uint rp = br.ReadUInt32();
                        br.ReadBytes(16);
                        secs.Add(new uint[] { va, Math.Max(vs, rs), rp });
                    }
                    pi.Valid = true;
                    ReadNames(fs, br, secs, imp, 20, 12, pi.Imports);
                    ReadNames(fs, br, secs, dimp, 32, 4, pi.Imports);
                }
            }
            catch { }
            return pi;
        }

        static long Off(List<uint[]> secs, uint rva)
        {
            foreach (uint[] s in secs)
                if (rva >= s[0] && rva < s[0] + s[1]) return (long)s[2] + (rva - s[0]);
            return -1;
        }

        static void ReadNames(FileStream fs, BinaryReader br, List<uint[]> secs, uint rva, int size, int nameOff, List<string> outp)
        {
            if (rva == 0) return;
            long off = Off(secs, rva);
            if (off < 0) return;
            for (int i = 0; i < 1024; i++)
            {
                long p = off + (long)i * size;
                if (p + size > fs.Length) return;
                fs.Position = p;
                byte[] d = br.ReadBytes(size);
                bool zero = true;
                foreach (byte b in d) if (b != 0) { zero = false; break; }
                if (zero) return;
                uint nr = BitConverter.ToUInt32(d, nameOff);
                if (nr == 0) return;
                long no = Off(secs, nr);
                if (no < 0) continue;
                fs.Position = no;
                byte[] s = br.ReadBytes(260);
                int len = Array.IndexOf(s, (byte)0);
                if (len < 0) len = s.Length;
                string name = Encoding.ASCII.GetString(s, 0, len).ToLowerInvariant();
                if (name.Length > 0 && !outp.Contains(name)) outp.Add(name);
            }
        }

        /// <summary>Procura strings ASCII (minúsculas) dentro de um binário.</summary>
        public static HashSet<string> ScanStrings(string path, string[] needles, long maxBytes)
        {
            HashSet<string> found = new HashSet<string>();
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] buf = new byte[4 << 20];
                    int carry = 0;
                    long total = 0;
                    Encoding lat = Encoding.GetEncoding(28591);
                    while (total < maxBytes)
                    {
                        int r = fs.Read(buf, carry, buf.Length - carry);
                        if (r <= 0) break;
                        total += r;
                        int len = carry + r;
                        string s = lat.GetString(buf, 0, len).ToLowerInvariant();
                        foreach (string nd in needles)
                            if (!found.Contains(nd) && s.IndexOf(nd, StringComparison.Ordinal) >= 0) found.Add(nd);
                        if (found.Count == needles.Length) break;
                        carry = Math.Min(128, len);
                        Buffer.BlockCopy(buf, len - carry, buf, 0, carry);
                    }
                }
            }
            catch { }
            return found;
        }
    }

    public class GameInfo
    {
        public string Root, ExePath, ExeDir, ExeName;
        public bool PeValid, Is64;
        public List<string> Imports = new List<string>();
        public GfxApi Api = GfxApi.Unknown;
        public string ApiSource = "";
        public List<string> ApiNotes = new List<string>();
        public string Engine = "Genérica / desconhecida";
        public bool HasDlss, HasDlssD, HasDlssG, HasStreamline, HasFsr, HasXess;
        public List<string> DlssDlls = new List<string>();
        public string AntiCheat;
        public List<string> Mods = new List<string>();
        public string DxgiKind;
        public Manifest Ours;

        public bool HasUpscaler { get { return HasDlss || HasFsr || HasXess; } }

        public bool ImportsDll(string n) { return Imports.Contains(n); }
    }

    public static class GameScanner
    {
        static readonly string[] BadExe = {
            "unins", "crash", "report", "launcher", "setup", "redist", "vcredist", "vc_redist", "dxsetup", "dxweb",
            "unitycrashhandler", "epicwebhelper", "cefsharp", "helper", "updater", "dotnet", "prereq", "qtwebengine",
            "start_protected_game", "easyanticheat", "beservice", "battleye", "installer", "uninstall", "touchup",
            "cleanup", "activation", "overlay", "register", "python", "node", "7z", "dlss5injector", "dxdiag", "ue4prereq"
        };

        static readonly string[] SkipDirs = {
            "_commonredist", "commonredist", "redist", "__installer", "directx", "vcredist", "installers", "_redist",
            "prereqs", "easyanticheat", "battleye", "dotnet", "support", "$recycle.bin"
        };

        public static readonly string[] ProxyNames = {
            "dxgi.dll", "d3d11.dll", "d3d12.dll", "d3d9.dll", "d3d10core.dll", "d3d10.dll", "winmm.dll", "version.dll",
            "dbghelp.dll", "dinput8.dll", "wininet.dll", "winhttp.dll", "optiscaler.asi"
        };

        public static List<string> FindExes(string root)
        {
            List<KeyValuePair<string, double>> list = new List<KeyValuePair<string, double>>();
            Walk(root, 0, 6, delegate(string f)
            {
                if (!f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return;
                string n = Path.GetFileName(f).ToLowerInvariant();
                double score;
                try { score = new FileInfo(f).Length / 1048576.0; } catch { score = 0; }
                if (n.Contains("-win64-shipping") || n.Contains("-wingdk-shipping")) score += 1000;
                string dl = f.ToLowerInvariant();
                if (dl.Contains(@"\binaries\win64\")) score += 300;
                if (dl.Contains(@"\bin\x64\") || dl.Contains(@"\x64\")) score += 100;
                foreach (string b in BadExe) if (n.Contains(b)) { score -= 5000; break; }
                score -= (f.Length - root.Length) / 50.0;
                list.Add(new KeyValuePair<string, double>(f, score));
            }, null);
            return list.OrderByDescending(k => k.Value).Select(k => k.Key).ToList();
        }

        static int walked;

        static void Walk(string dir, int depth, int maxDepth, Action<string> onFile, Action<string> onDir)
        {
            if (depth == 0) walked = 0;
            if (walked > 60000) return;
            string[] files;
            try { files = Directory.GetFiles(dir); } catch { return; }
            foreach (string f in files)
            {
                walked++;
                onFile(f);
            }
            if (depth >= maxDepth) return;
            string[] dirs;
            try { dirs = Directory.GetDirectories(dir); } catch { return; }
            foreach (string d in dirs)
            {
                string dn = Path.GetFileName(d).ToLowerInvariant();
                if (onDir != null) onDir(d);
                if (SkipDirs.Contains(dn)) continue;
                try
                {
                    if ((new DirectoryInfo(d).Attributes & FileAttributes.ReparsePoint) != 0) continue;
                }
                catch { continue; }
                Walk(d, depth + 1, maxDepth, onFile, onDir);
            }
        }

        public static string ClassifyModule(string path, Manifest ours)
        {
            if (ours != null && ours.IsAdded(path)) return "DLSS5 Injector";
            string d = Util.Describe(path);
            if (d.Contains("reshade")) return "ReShade";
            if (d.Contains("optiscaler")) return "OptiScaler";
            if (d.Contains("dgvoodoo")) return "dgVoodoo";
            if (d.Contains("special k")) return "Special K";
            if (d.Contains("dxvk")) return "DXVK";
            long len = 0;
            try { len = new FileInfo(path).Length; } catch { }
            if (len < 64L << 20)
            {
                HashSet<string> s = Pe.ScanStrings(path, new[] { "dxvk", "reshade", "optiscaler" }, 64L << 20);
                if (s.Contains("optiscaler")) return "OptiScaler";
                if (s.Contains("reshade")) return "ReShade";
                if (s.Contains("dxvk")) return "DXVK";
            }
            if (d.Contains("microsoft")) return "Microsoft (original)";
            return "Desconhecido";
        }

        public static GameInfo Analyze(string root, string exe)
        {
            GameInfo g = new GameInfo();
            g.Root = root;
            g.ExePath = exe;
            g.ExeDir = Path.GetDirectoryName(exe);
            g.ExeName = Path.GetFileName(exe);
            g.Ours = Manifest.Load(g.ExeDir);

            PeInfo pe = Pe.Read(exe);
            g.PeValid = pe.Valid;
            g.Is64 = pe.Is64 || pe.Machine == 0x8664;
            g.Imports = pe.Imports;

            string exeDirL = g.ExeDir.ToLowerInvariant();
            string exeNoExt = Path.GetFileNameWithoutExtension(exe);

            // ---------- Engine ----------
            bool unreal = exeDirL.EndsWith(@"\binaries\win64") || exeDirL.EndsWith(@"\binaries\wingdk") ||
                          g.ExeName.ToLowerInvariant().Contains("-shipping");
            bool unity = File.Exists(Path.Combine(g.ExeDir, "UnityPlayer.dll")) || Directory.Exists(Path.Combine(g.ExeDir, exeNoExt + "_Data"));
            bool reEngine = File.Exists(Path.Combine(g.ExeDir, "re_chunk_000.pak"));
            bool ue5 = false;
            if (unreal)
            {
                HashSet<string> s = Pe.ScanStrings(exe, new[] { "++ue5+", "++ue4+" }, 600L << 20);
                ue5 = s.Contains("++ue5+");
                g.Engine = ue5 ? "Unreal Engine 5" : (s.Contains("++ue4+") ? "Unreal Engine 4" : "Unreal Engine");
            }
            else if (unity) g.Engine = "Unity";
            else if (reEngine) g.Engine = "RE Engine";

            // ---------- API ----------
            GfxApi fromLog = GfxApi.Unknown;
            string logSrc = null;
            if (unreal) DetectUnrealLog(g, ref fromLog, ref logSrc);
            if (unity) DetectUnityLog(g, exeNoExt, ref fromLog, ref logSrc);

            List<string> imps = new List<string>(pe.Imports);
            if (unity && File.Exists(Path.Combine(g.ExeDir, "UnityPlayer.dll")))
                imps.AddRange(Pe.Read(Path.Combine(g.ExeDir, "UnityPlayer.dll")).Imports);
            // DLLs de renderização da própria engine ao lado do exe
            try
            {
                foreach (string dll in Directory.GetFiles(g.ExeDir, "*.dll"))
                {
                    string dn = Path.GetFileName(dll).ToLowerInvariant();
                    if (ProxyNames.Contains(dn) || dn.StartsWith("nvngx") || dn.StartsWith("sl.")) continue;
                    if (dn == "unityplayer.dll") continue;
                    long size = 0;
                    try { size = new FileInfo(dll).Length; } catch { }
                    // DLLs da engine (ex.: FC_m64.dll do Far Cry) costumam ser grandes ou ter nomes de renderer
                    if (size > (8L << 20) || dn.Contains("render") || dn.Contains("gfx") || dn.Contains("graphics") || dn.Contains("dx12") ||
                        dn.Contains("dx11") || dn.Contains("vulkan") || dn.Contains("engine"))
                        imps.AddRange(Pe.Read(dll).Imports);
                }
            }
            catch { }

            bool i12 = imps.Contains("d3d12.dll");
            bool i11 = imps.Contains("d3d11.dll");
            bool i10 = imps.Contains("d3d10.dll") || imps.Contains("d3d10_1.dll");
            bool i9 = imps.Contains("d3d9.dll");
            bool ivk = imps.Contains("vulkan-1.dll");
            bool igl = imps.Contains("opengl32.dll");
            bool agility = File.Exists(Path.Combine(g.ExeDir, "D3D12", "D3D12Core.dll"));

            if (fromLog != GfxApi.Unknown) { g.Api = fromLog; g.ApiSource = logSrc; }
            else if (unreal)
            {
                if (ue5 || agility || (i12 && !i11)) { g.Api = GfxApi.DX12; g.ApiSource = ue5 ? "Unreal Engine 5 (padrão DX12)" : "Unreal + Agility SDK/D3D12"; }
                else { g.Api = GfxApi.DX11; g.ApiSource = "Unreal Engine 4 (padrão DX11)"; g.ApiNotes.Add("Jogos UE4 às vezes têm modo DX12 (-dx12). Se você usa DX12 no jogo, escolha DX12 manualmente."); }
            }
            else if (unity)
            {
                // O UnityPlayer carrega a API dinamicamente; sem Player.log vale o padrão do Windows.
                if (agility) { g.Api = GfxApi.DX12; g.ApiSource = "Unity + Agility SDK (D3D12)"; }
                else
                {
                    g.Api = GfxApi.DX11; g.ApiSource = "Unity (padrão no Windows: DX11)";
                    g.ApiNotes.Add("Sem Player.log para confirmar. Se o jogo roda em DX12 (opção do jogo ou -force-d3d12), escolha DX12 manualmente. Rodar o jogo uma vez melhora a detecção.");
                }
            }
            else if (i12) { g.Api = GfxApi.DX12; g.ApiSource = "importa d3d12.dll"; if (i11) g.ApiNotes.Add("O executável também importa d3d11.dll (pode ter modo DX11)."); }
            else if (agility) { g.Api = GfxApi.DX12; g.ApiSource = "Agility SDK (D3D12\\D3D12Core.dll)"; }
            else if (ivk && !i11 && !i10 && !i9) { g.Api = GfxApi.Vulkan; g.ApiSource = "importa vulkan-1.dll"; }
            else if (i11)
            {
                g.Api = GfxApi.DX11; g.ApiSource = "importa d3d11.dll";
                if (ivk) g.ApiNotes.Add("Também importa vulkan-1.dll (pode ter modo Vulkan).");
                if (Pe.ScanStrings(exe, new[] { "d3d12.dll" }, 400L << 20).Count > 0) g.ApiNotes.Add("O executável referencia d3d12.dll (pode ter modo DX12).");
            }
            else if (i10) { g.Api = GfxApi.DX10; g.ApiSource = "importa d3d10.dll"; }
            else if (i9) { g.Api = GfxApi.DX9; g.ApiSource = "importa d3d9.dll"; }
            else if (ivk) { g.Api = GfxApi.Vulkan; g.ApiSource = "importa vulkan-1.dll"; }
            else
            {
                string[] needles = { "d3d12.dll", "vulkan-1.dll", "d3d11.dll", "d3d10.dll", "d3d9.dll" };
                HashSet<string> s = Pe.ScanStrings(exe, needles, 400L << 20);
                if (s.Count == 0)
                {
                    // Exe pequeno (launcher/stub): procura na maior DLL da engine ao lado dele
                    try
                    {
                        FileInfo big = new DirectoryInfo(g.ExeDir).GetFiles("*.dll").OrderByDescending(f => f.Length).FirstOrDefault();
                        if (big != null && big.Length > (8L << 20)) s = Pe.ScanStrings(big.FullName, needles, 400L << 20);
                    }
                    catch { }
                }
                if (s.Contains("d3d12.dll")) { g.Api = GfxApi.DX12; g.ApiSource = "referência a d3d12.dll no executável"; }
                else if (s.Contains("vulkan-1.dll") && !s.Contains("d3d11.dll")) { g.Api = GfxApi.Vulkan; g.ApiSource = "referência a vulkan-1.dll"; }
                else if (s.Contains("d3d11.dll")) { g.Api = GfxApi.DX11; g.ApiSource = "referência a d3d11.dll"; }
                else if (s.Contains("d3d10.dll")) { g.Api = GfxApi.DX10; g.ApiSource = "referência a d3d10.dll"; }
                else if (s.Contains("d3d9.dll")) { g.Api = GfxApi.DX9; g.ApiSource = "referência a d3d9.dll"; }
                else if (igl) { g.Api = GfxApi.OpenGL; g.ApiSource = "importa opengl32.dll"; }
                else { g.Api = GfxApi.Unknown; g.ApiSource = "não detectado"; }
            }

            // ---------- Upscalers / anti-cheat ----------
            Walk(root, 0, 8, delegate(string f)
            {
                string n = Path.GetFileName(f).ToLowerInvariant();
                string fl = f.ToLowerInvariant();
                if (fl.Contains(@"\optiscaler\")) return;              // arquivos do próprio OptiScaler
                if (n.EndsWith(".dlss5bak")) return;
                bool oursOnly = g.Ours != null && g.Ours.IsAdded(f) && !g.Ours.IsBackedUp(f);
                if (n == "nvngx_dlss.dll" || n == "nvngx_dlssd.dll" || n == "nvngx_dlssg.dll")
                {
                    if (oursOnly) return;
                    g.DlssDlls.Add(f);
                    if (n == "nvngx_dlss.dll") g.HasDlss = true;
                    if (n == "nvngx_dlssd.dll") g.HasDlssD = true;
                    if (n == "nvngx_dlssg.dll") g.HasDlssG = true;
                }
                else if (n == "sl.interposer.dll") g.HasStreamline = true;
                else if (n.StartsWith("libxess")) g.HasXess = true;
                else if (n.StartsWith("ffx_fsr2_api") || n.StartsWith("ffx_fsr3") || n.StartsWith("amd_fidelityfx_") || n.StartsWith("ffx_backend")) g.HasFsr = true;
                else if (n == "start_protected_game.exe" || n.StartsWith("easyanticheat")) g.AntiCheat = "Easy Anti-Cheat";
                else if (n.StartsWith("beservice") || n.StartsWith("battleye")) g.AntiCheat = "BattlEye";
                else if (n == "vgk.sys" || n == "vanguard.exe") g.AntiCheat = "Vanguard";
            }, delegate(string d)
            {
                string dn = Path.GetFileName(d).ToLowerInvariant();
                if (dn == "easyanticheat" || dn == "easyanticheat_eos") g.AntiCheat = "Easy Anti-Cheat";
                else if (dn == "battleye") g.AntiCheat = "BattlEye";
            });

            // ---------- Mods existentes ----------
            foreach (string p in ProxyNames)
            {
                string f = Path.Combine(g.ExeDir, p);
                if (!File.Exists(f)) continue;
                string kind = ClassifyModule(f, g.Ours);
                if (p == "dxgi.dll") g.DxgiKind = kind;
                g.Mods.Add(p + " → " + kind);
            }
            foreach (string extra in new[] { "OptiScaler.ini", "ReShade.ini", "dxvk.conf", "dgVoodoo.conf", "nvngx.dll_dlssnr.dll", "nvngx_dlssnr.dll" })
                if (File.Exists(Path.Combine(g.ExeDir, extra))) g.Mods.Add(extra);
            try
            {
                foreach (string a in Directory.GetFiles(g.ExeDir, "*.addon64")) g.Mods.Add(Path.GetFileName(a) + " (add-on ReShade)");
            }
            catch { }
            return g;
        }

        static GfxApi ApiFromText(string t, out string what)
        {
            what = null;
            string l = t.ToLowerInvariant();
            if (l.Contains("logd3d12rhi") || l.Contains("defaultgraphicsrhi_dx12") || l.Contains("direct3d 12")) { what = "DX12"; return GfxApi.DX12; }
            if (l.Contains("logvulkanrhi") || l.Contains("defaultgraphicsrhi_vulkan") || l.Contains("vulkan api version")) { what = "Vulkan"; return GfxApi.Vulkan; }
            if (l.Contains("logd3d11rhi") || l.Contains("defaultgraphicsrhi_dx11") || l.Contains("direct3d 11")) { what = "DX11"; return GfxApi.DX11; }
            return GfxApi.Unknown;
        }

        static string ReadHead(string path, int max)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    byte[] b = new byte[Math.Min(max, (int)Math.Min(fs.Length, int.MaxValue))];
                    int r = fs.Read(b, 0, b.Length);
                    return Encoding.UTF8.GetString(b, 0, r);
                }
            }
            catch { return ""; }
        }

        static void DetectUnrealLog(GameInfo g, ref GfxApi api, ref string src)
        {
            // <root>\<Projeto>\Binaries\Win64\jogo.exe
            DirectoryInfo bin = Directory.GetParent(g.ExeDir);
            if (bin == null || bin.Parent == null) return;
            string projDir = bin.Parent.FullName;
            string proj = bin.Parent.Name;
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            List<string> files = new List<string>();
            foreach (string baseDir in new[] { Path.Combine(local, proj, "Saved"), Path.Combine(projDir, "Saved") })
            {
                foreach (string cfg in new[] { @"Config\Windows\Engine.ini", @"Config\WindowsNoEditor\Engine.ini", @"Config\Windows\GameUserSettings.ini", @"Config\WindowsNoEditor\GameUserSettings.ini" })
                    files.Add(Path.Combine(baseDir, cfg));
                files.Add(Path.Combine(baseDir, "Logs", proj + ".log"));
            }
            foreach (string f in files)
            {
                if (!File.Exists(f)) continue;
                string what;
                GfxApi a = ApiFromText(ReadHead(f, 4 << 20), out what);
                if (a != GfxApi.Unknown)
                {
                    api = a;
                    src = "log/config do Unreal (" + Path.GetFileName(f) + ")";
                    return;
                }
            }
        }

        static void DetectUnityLog(GameInfo g, string exeNoExt, ref GfxApi api, ref string src)
        {
            string info = Path.Combine(g.ExeDir, exeNoExt + "_Data", "app.info");
            if (!File.Exists(info)) return;
            string[] l;
            try { l = File.ReadAllLines(info); } catch { return; }
            if (l.Length < 2) return;
            string low = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", l[0].Trim(), l[1].Trim());
            foreach (string f in new[] { Path.Combine(low, "Player.log"), Path.Combine(low, "Player-prev.log") })
            {
                if (!File.Exists(f)) continue;
                string what;
                GfxApi a = ApiFromText(ReadHead(f, 2 << 20), out what);
                if (a != GfxApi.Unknown)
                {
                    api = a;
                    src = "Player.log do Unity";
                    return;
                }
            }
        }
    }
}
