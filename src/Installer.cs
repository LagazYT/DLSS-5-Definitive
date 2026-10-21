using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dlss5Injector
{
    /// <summary>
    /// Registro de tudo que o app fez na pasta do jogo (DLSS5Injector.manifest), para desinstalar
    /// e restaurar os backups exatamente.
    /// ADD|arquivo  BAK|original|backup  DIR|pasta criada  RT|arquivo gerado em tempo de execução
    /// </summary>
    public class Manifest
    {
        public const string FileName = "DLSS5Injector.manifest";
        public string Dir;
        public string Method = "";
        public List<string[]> E = new List<string[]>();

        public static Manifest Load(string dir)
        {
            string f = Path.Combine(dir, FileName);
            if (!File.Exists(f)) return null;
            Manifest m = new Manifest();
            m.Dir = dir;
            foreach (string line in File.ReadAllLines(f))
            {
                if (line.StartsWith("#METHOD=")) { m.Method = line.Substring(8); continue; }
                if (line.StartsWith("#") || line.Trim().Length == 0) continue;
                m.E.Add(line.Split('|'));
            }
            return m;
        }

        public void Save()
        {
            List<string> l = new List<string>();
            l.Add("# DLSS 5 Definitive - nao apague: usado para desinstalar/restaurar backups");
            l.Add("#METHOD=" + Method);
            foreach (string[] e in E) l.Add(string.Join("|", e));
            File.WriteAllLines(Path.Combine(Dir, FileName), l.ToArray(), new UTF8Encoding(false));
        }

        public void Add(params string[] e)
        {
            E.Add(e);
            Save();
        }

        public string Rel(string abs)
        {
            string full = Path.GetFullPath(abs);
            string root = Path.GetFullPath(Dir).TrimEnd('\\') + "\\";
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : full;
        }

        public string Abs(string rel)
        {
            return Path.IsPathRooted(rel) ? rel : Path.Combine(Dir, rel);
        }

        public bool IsAdded(string path)
        {
            string r = Rel(path);
            return E.Any(e => e[0] == "ADD" && string.Equals(e[1], r, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsBackedUp(string path)
        {
            string r = Rel(path);
            return E.Any(e => e[0] == "BAK" && string.Equals(e[1], r, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class Installer
    {
        static readonly string[] SlFgFiles = { "sl.interposer.dll", "sl.common.dll", "sl.dlss_g.dll", "sl.reflex.dll", "sl.pcl.dll", "nvngx_dlssg.dll" };

        readonly Action<string> log;
        readonly Downloader dl;
        Manifest man;

        public Installer(Action<string> log, Action<int> progress)
        {
            this.log = log;
            dl = new Downloader(log, progress);
        }

        public static string SlBin(string slDir) { return Path.Combine(slDir, "bin", "x64"); }

        public static bool IsStreamlineDir(string d)
        {
            return !string.IsNullOrEmpty(d) && File.Exists(Path.Combine(SlBin(d), "nvngx_dlss.dll")) && File.Exists(Path.Combine(SlBin(d), "sl.interposer.dll"));
        }

        /// <summary>Arquivos do Streamline SDK que o app usa (o resto do SDK, ~270 MB, não é necessário).</summary>
        static readonly string[] SlNeeded = {
            "nvngx_dlss.dll", "nvngx_dlssd.dll", "nvngx_dlssg.dll", "sl.interposer.dll", "sl.common.dll", "sl.dlss_g.dll",
            "sl.reflex.dll", "sl.pcl.dll", "nvngx_dlss.license.txt", "reflex.license.txt"
        };

        /// <summary>
        /// Copia o necessário do Streamline para &lt;componentsDir&gt;\streamline (mesmo layout bin\x64 do SDK) e devolve
        /// essa pasta. Só copia arquivos que faltam ou mudaram.
        /// </summary>
        public static string ImportStreamline(string src, string componentsDir)
        {
            string dest = Path.Combine(componentsDir, "streamline");
            if (string.Equals(Path.GetFullPath(src).TrimEnd('\\'), Path.GetFullPath(dest).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return dest;
            string bin = SlBin(dest);
            Directory.CreateDirectory(bin);
            foreach (string n in SlNeeded)
            {
                string s = Path.Combine(SlBin(src), n), d = Path.Combine(bin, n);
                if (!File.Exists(s)) continue;
                if (File.Exists(d) && new FileInfo(d).Length == new FileInfo(s).Length && Util.FileVer(d) == Util.FileVer(s)) continue;
                File.Copy(s, d + ".copy", true);
                if (File.Exists(d)) { File.SetAttributes(d, FileAttributes.Normal); File.Delete(d); }
                File.Move(d + ".copy", d);
            }
            foreach (string lic in new[] { "license.txt", "3rd-party-licenses.md" })
                if (File.Exists(Path.Combine(src, lic))) File.Copy(Path.Combine(src, lic), Path.Combine(dest, lic), true);
            return IsStreamlineDir(dest) ? dest : src;
        }

        // ------------------------------------------------------------------ helpers

        void EnsureDir(string dir)
        {
            if (Directory.Exists(dir)) return;
            EnsureDir(Path.GetDirectoryName(dir));
            Directory.CreateDirectory(dir);
            man.Add("DIR", man.Rel(dir));
        }

        void Put(string src, byte[] content, string target)
        {
            string abs = man.Abs(target);
            EnsureDir(Path.GetDirectoryName(abs));
            if (File.Exists(abs) && !man.IsAdded(abs))
            {
                string bak = abs + ".dlss5bak";
                int k = 1;
                while (File.Exists(bak)) bak = abs + ".dlss5bak" + (k++);
                File.SetAttributes(abs, FileAttributes.Normal);
                File.Move(abs, bak);
                man.Add("BAK", man.Rel(abs), man.Rel(bak));
                log(L.T("    backup: ") + man.Rel(abs) + " → " + Path.GetFileName(bak));
            }
            else if (File.Exists(abs)) File.SetAttributes(abs, FileAttributes.Normal);
            if (src != null) File.Copy(src, abs, true);
            else File.WriteAllBytes(abs, content);
            if (!man.IsAdded(abs)) man.Add("ADD", man.Rel(abs));
        }

        void PutFile(string src, string target) { Put(src, null, target); }

        void DisableFile(string target)
        {
            string abs = man.Abs(target);
            if (!File.Exists(abs) || man.IsAdded(abs)) return;
            string bak = abs + ".dlss5bak";
            int k = 1;
            while (File.Exists(bak)) bak = abs + ".dlss5bak" + (k++);
            File.SetAttributes(abs, FileAttributes.Normal);
            File.Move(abs, bak);
            man.Add("BAK", man.Rel(abs), man.Rel(bak));
            log(L.T("    desativado: ") + man.Rel(abs) + " → " + Path.GetFileName(bak));
        }

        void PutText(List<string> lines, string target)
        {
            Put(null, new UTF8Encoding(false).GetBytes(string.Join(L.T("\r\n"), lines.ToArray()) + L.T("\r\n")), target);
        }

        void PutTree(string srcDir, string targetRel)
        {
            foreach (string f in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
                PutFile(f, Path.Combine(targetRel, f.Substring(srcDir.TrimEnd('\\').Length + 1)));
        }

        void Runtime(string name)
        {
            if (!File.Exists(man.Abs(name))) man.Add("RT", name);
        }

        string VerifiedSl(string slDir, string name)
        {
            string f = Path.Combine(SlBin(slDir), name);
            if (!File.Exists(f)) throw new Exception(L.T("Arquivo não encontrado na pasta Streamline: ") + f);
            if (!Signature.IsNvidiaSigned(f)) throw new Exception(L.T("Assinatura NVIDIA inválida em ") + f + L.T(" — instalação cancelada."));
            return f;
        }

        // ------------------------------------------------------------------ install

        public void Install(GameInfo g, InstallPlan p, string slDir, string nrPath)
        {
            if (p.Method == Method.None) throw new Exception(L.T("Nenhum método compatível selecionado."));
            if (string.IsNullOrEmpty(nrPath) || !File.Exists(nrPath))
                throw new Exception(L.T("Selecione o arquivo nvngx_dlssnr.dll (runtime do DLSS 5 / Neural Rendering)."));
            bool needSl = p.PlaceDlss || p.UpdateGameDlss || p.CopyStreamlineFG;
            if (needSl && !IsStreamlineDir(slDir))
            {
                log(L.T("Pasta do Streamline SDK não encontrada neste PC — baixando a versão oficial da NVIDIA."));
                string downloaded = dl.EnsureStreamline();
                // Guarda na pasta do app (components) e descarta a cópia do cache de downloads
                slDir = ImportStreamline(downloaded, AppPaths.Components);
                if (slDir != downloaded) { try { Directory.Delete(downloaded, true); } catch { } }
                log(L.T("Streamline copiado para a pasta do app: ") + slDir);
            }
            else if (needSl && !AppPaths.IsInside(slDir, AppPaths.Components))
            {
                slDir = ImportStreamline(slDir, AppPaths.Components);
            }

            log("== Instalando: " + Planner.Name(p.Method) + " em " + g.ExeDir);

            // 1) Baixa tudo antes de mexer no jogo
            log(L.T("1/4 Obtendo componentes"));
            string opti = null, reshade = null, renodx = null, dxvk = null, dgv = null, sm86 = null;
            if (p.Sm86) sm86 = dl.EnsureSm86();
            if (p.Method == Method.RenoDX)
            {
                reshade = dl.EnsureReShade();
                renodx = dl.EnsureRenoDx();
            }
            else opti = dl.EnsureOptiScaler();
            if (p.DxvkDlls != null) dxvk = dl.EnsureDxvk();
            if (p.DgVoodoo) dgv = dl.EnsureDgVoodoo();

            // 2) Valida DLLs NVIDIA da pasta Streamline
            log(L.T("2/4 Verificando assinaturas NVIDIA da pasta Streamline"));
            Dictionary<string, string> sl = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (needSl)
            {
                foreach (string n in new[] { "nvngx_dlss.dll", "nvngx_dlssd.dll", "nvngx_dlssg.dll" })
                    if (File.Exists(Path.Combine(SlBin(slDir), n))) sl[n] = VerifiedSl(slDir, n);
                if (p.CopyStreamlineFG)
                    foreach (string n in SlFgFiles) sl[n] = VerifiedSl(slDir, n);
                log("    OK (Streamline " + Util.FileVerStr(Path.Combine(SlBin(slDir), "sl.interposer.dll")) + ", DLSS " + Util.FileVerStr(sl["nvngx_dlss.dll"]) + ")");
            }

            // 3) Remove instalação anterior deste app
            if (Manifest.Load(g.ExeDir) != null)
            {
                log(L.T("3/4 Removendo instalação anterior do DLSS 5 Definitive"));
                Uninstall(g.ExeDir, log);
            }
            else log(L.T("3/4 Nenhuma instalação anterior"));

            man = new Manifest();
            man.Dir = g.ExeDir;
            man.Method = p.Method.ToString();
            man.Save();

            log(L.T("4/4 Copiando arquivos"));
            try
            {
                foreach (string rt in new[] { "OptiScaler.log", "ReShade.log", "ReShade.ini", Path.GetFileNameWithoutExtension(g.ExeName) + "_d3d11.log",
                                              Path.GetFileNameWithoutExtension(g.ExeName) + "_dxgi.log", Path.GetFileNameWithoutExtension(g.ExeName) + "_d3d9.log",
                                              Path.GetFileNameWithoutExtension(g.ExeName) + "_d3d10core.log" })
                    Runtime(rt);

                foreach (KeyValuePair<string, string> c in Planner.FindConflicts(p, g.ExeDir, null)) DisableFile(c.Key);

                if (p.Method == Method.RenoDX) InstallRenoDx(g, reshade, renodx);
                else InstallOpti(g, p, opti);

                if (dxvk != null)
                    foreach (string d in p.DxvkDlls) PutFile(Path.Combine(dxvk, d), d);

                if (dgv != null)
                {
                    PutFile(Path.Combine(dgv, "MS", "x64", "D3D9.dll"), "D3D9.dll");
                    List<string> conf = File.ReadAllLines(Path.Combine(dgv, "dgVoodoo.conf")).ToList();
                    IniEdit.Set(conf, "General", "OutputAPI", "d3d12_fl12_0");
                    IniEdit.Set(conf, "DirectX", "dgVoodooWatermark", "false");
                    PutText(conf, "dgVoodoo.conf");
                }

                if (sm86 != null)
                {
                    // Proxy + backend/kernels/runtime ao lado do exe (o módulo procura dlssg_sm86\ relativo a si mesmo).
                    // A pasta sr\ (super-resolução INT8, ~180 MB) não é usada: [SuperResolution] Mode=Off.
                    PutFile(Path.Combine(sm86, "version.dll"), p.Sm86Proxy);
                    foreach (string sub in new[] { "backend", "kernels", "runtime", "xefg" })
                    {
                        string src = Path.Combine(sm86, "dlssg_sm86", sub);
                        if (Directory.Exists(src)) PutTree(src, Path.Combine("dlssg_sm86", sub));
                    }
                    foreach (string lic in new[] { "LICENSE.md", "THIRD_PARTY_NOTICES.txt", "README.en.md" })
                        if (File.Exists(Path.Combine(sm86, lic))) PutFile(Path.Combine(sm86, lic), Path.Combine("dlssg_sm86", lic));
                    List<string> ini = File.ReadAllLines(Path.Combine(sm86, "dlssg_sm86.ini")).ToList();
                    IniEdit.Set(ini, "General", "Enabled", "1");
                    IniEdit.Set(ini, "FrameGeneration", "Engine", "DLSS");
                    IniEdit.Set(ini, "FrameGeneration", "MaxGeneratedFrames", p.Sm86MaxFrames.ToString());
                    IniEdit.Set(ini, "SuperResolution", "Mode", "Off");
                    PutText(ini, "dlssg_sm86.ini");
                    log(L.T("    dlssg_for_sm86 ") + Path.GetFileName(sm86) + L.T(" como ") + p.Sm86Proxy + L.T(" (até ") + (p.Sm86MaxFrames + 1) + L.T("X, runtime DLSS-G ") +
                        Util.FileVerStr(Path.Combine(sm86, "dlssg_sm86", "runtime", "nvngx_dlssg.dll")) + " assinado pela NVIDIA)");
                }

                PutFile(nrPath, NrRuntime.FileName);

                if (p.PlaceDlss && !File.Exists(Path.Combine(g.ExeDir, "nvngx_dlss.dll")))
                    PutFile(sl["nvngx_dlss.dll"], "nvngx_dlss.dll");

                if (p.UpdateGameDlss)
                {
                    foreach (string f in g.DlssDlls)
                    {
                        string n = Path.GetFileName(f);
                        string src;
                        if (!sl.TryGetValue(n, out src) || !File.Exists(f)) continue;
                        Version have = Util.FileVer(f), nv = Util.FileVer(src);
                        if (nv > have)
                        {
                            PutFile(src, f);
                            log("    " + n + " " + have + " → " + nv + " (" + man.Rel(f) + ")");
                        }
                        else log("    " + n + L.T(" já está em ") + have + L.T(" (mantido)"));
                    }
                }

                if (p.CopyStreamlineFG)
                {
                    foreach (string n in SlFgFiles) PutFile(sl[n], Path.Combine("OptiScaler", "streamline", n));
                    string[][] lic = {
                        new[] { Path.Combine(slDir, "license.txt"), "Streamline-LICENSE.txt" },
                        new[] { Path.Combine(SlBin(slDir), "nvngx_dlss.license.txt"), "nvngx_dlss.license.txt" },
                        new[] { Path.Combine(SlBin(slDir), "reflex.license.txt"), "reflex.license.txt" },
                        new[] { Path.Combine(slDir, "3rd-party-licenses.md"), "Streamline-3rd-party-licenses.md" }
                    };
                    foreach (string[] l in lic)
                        if (File.Exists(l[0])) PutFile(l[0], Path.Combine("OptiScaler", "streamline", l[1]));
                }
            }
            catch
            {
                log(L.T("!! Erro durante a cópia — desfazendo alterações..."));
                try { Uninstall(g.ExeDir, log); } catch { }
                throw;
            }
            log(L.T("== Concluído. ") + man.E.Count(e => e[0] == "ADD") + L.T(" arquivo(s) instalados, ") + man.E.Count(e => e[0] == "BAK") + L.T(" backup(s)."));
        }

        void InstallOpti(GameInfo g, InstallPlan p, string opti)
        {
            if (p.MoveReShade && File.Exists(Path.Combine(g.ExeDir, "dxgi.dll")))
                PutFile(Path.Combine(g.ExeDir, "dxgi.dll"), "ReShade64.dll");

            PutFile(Path.Combine(opti, "OptiScaler.dll"), p.Proxy);
            List<string> ini = File.ReadAllLines(Path.Combine(opti, "OptiScaler.ini")).ToList();
            foreach (string[] kv in p.Ini)
            {
                IniEdit.Set(ini, kv[0], kv[1], kv[2]);
                log("    [" + kv[0] + "] " + kv[1] + "=" + kv[2]);
            }
            PutText(ini, "OptiScaler.ini");
            PutFile(Path.Combine(opti, "nvngx.dll_dlssnr.dll"), "nvngx.dll_dlssnr.dll");
            PutTree(Path.Combine(opti, "OptiScaler"), "OptiScaler");
            foreach (string doc in new[] { "INSTALL-DLSSNR.md", "TROUBLESHOOTING.md", "README.md" })
                if (File.Exists(Path.Combine(opti, doc))) PutFile(Path.Combine(opti, doc), Path.Combine("OptiScaler", "docs", doc));
            log("    OptiScaler " + Util.FileVerStr(Path.Combine(opti, "OptiScaler.dll")) + " instalado como " + p.Proxy);
        }

        void InstallRenoDx(GameInfo g, string reshade, string renodx)
        {
            PutFile(Path.Combine(reshade, "ReShade64.dll"), "dxgi.dll");
            string addon = Directory.GetFiles(renodx, "renodx-dlss5.addon64", SearchOption.AllDirectories)[0];
            PutFile(addon, "renodx-dlss5.addon64");
            if (!File.Exists(Path.Combine(g.ExeDir, "ReShade.ini")))
            {
                PutText(new List<string> { "[GENERAL]", "NoReloadOnInit=0", "", "[OVERLAY]", "TutorialProgress=4" }, "ReShade.ini");
            }
            log("    ReShade " + Util.FileVerStr(Path.Combine(reshade, "ReShade64.dll")) + " + RenoDX DLSS 5 (" + Path.GetFileName(renodx) + ")");
        }

        // ------------------------------------------------------------------ uninstall

        public static void Uninstall(string exeDir, Action<string> log)
        {
            Manifest m = Manifest.Load(exeDir);
            if (m == null)
            {
                log(L.T("Nada para remover (sem ") + Manifest.FileName + ").");
                return;
            }
            int removed = 0, restored = 0;
            for (int i = m.E.Count - 1; i >= 0; i--)
            {
                string[] e = m.E[i];
                try
                {
                    switch (e[0])
                    {
                        case "ADD":
                        case "RT":
                            {
                                string f = m.Abs(e[1]);
                                if (File.Exists(f)) { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); removed++; }
                                break;
                            }
                        case "BAK":
                            {
                                string orig = m.Abs(e[1]), bak = m.Abs(e[2]);
                                if (File.Exists(bak))
                                {
                                    if (File.Exists(orig)) { File.SetAttributes(orig, FileAttributes.Normal); File.Delete(orig); }
                                    File.Move(bak, orig);
                                    restored++;
                                }
                                break;
                            }
                        case "DIR":
                            {
                                string d = m.Abs(e[1]);
                                if (Directory.Exists(d)) Directory.Delete(d, true);
                                break;
                            }
                    }
                }
                catch (Exception ex) { log("  ! " + string.Join("|", e) + ": " + ex.Message); }
            }
            File.Delete(Path.Combine(exeDir, Manifest.FileName));
            log(L.T("Removido: ") + removed + L.T(" arquivo(s) apagados, ") + restored + L.T(" backup(s) restaurados."));
        }
    }
}
