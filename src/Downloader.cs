using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Dlss5Injector
{
    public class Downloader
    {
        class Rel
        {
            public string Tag, Asset, Url, Sha;
            public DateTime Pub;
        }

        readonly Action<string> log;
        readonly Action<int> progress;

        public Downloader(Action<string> log, Action<int> progress)
        {
            this.log = log;
            this.progress = progress;
        }

        static HttpWebRequest Req(string url)
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "DLSS5Injector/1.0";
            r.AllowAutoRedirect = true;
            r.Timeout = 30000;
            r.ReadWriteTimeout = 120000;
            if (url.Contains("api.github.com")) r.Accept = "application/vnd.github+json";
            return r;
        }

        static string GetString(string url)
        {
            using (WebResponse resp = Req(url).GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        void GetFile(string url, string dest)
        {
            using (WebResponse resp = Req(url).GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (FileStream fs = File.Create(dest))
            {
                long total = resp.ContentLength;
                byte[] buf = new byte[1 << 16];
                long done = 0;
                int last = -1;
                int r;
                while ((r = s.Read(buf, 0, buf.Length)) > 0)
                {
                    fs.Write(buf, 0, r);
                    done += r;
                    if (total > 0)
                    {
                        int pct = (int)(done * 100 / total);
                        if (pct != last) { last = pct; progress(pct); }
                    }
                }
            }
            progress(100);
        }

        static List<Rel> Releases(string repo, Regex asset, int perPage, bool allowPre)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = int.MaxValue;
            object[] arr = js.DeserializeObject(GetString("https://api.github.com/repos/" + repo + "/releases?per_page=" + perPage)) as object[];
            List<Rel> l = new List<Rel>();
            if (arr == null) return l;
            foreach (object o in arr)
            {
                Dictionary<string, object> r = o as Dictionary<string, object>;
                if (r == null) continue;
                if (true.Equals(r["draft"])) continue;
                if (!allowPre && true.Equals(r["prerelease"])) continue;
                DateTime pub;
                DateTime.TryParse(Convert.ToString(r["published_at"]), out pub);
                object[] assets = r["assets"] as object[];
                if (assets == null) continue;
                foreach (object ao in assets)
                {
                    Dictionary<string, object> a = ao as Dictionary<string, object>;
                    string name = Convert.ToString(a["name"]);
                    if (!asset.IsMatch(name)) continue;
                    Rel x = new Rel();
                    x.Tag = Convert.ToString(r["tag_name"]);
                    x.Asset = name;
                    x.Url = Convert.ToString(a["browser_download_url"]);
                    x.Pub = pub;
                    object dg;
                    if (a.TryGetValue("digest", out dg) && dg != null)
                    {
                        string d = Convert.ToString(dg);
                        if (d.StartsWith("sha256:")) x.Sha = d.Substring(7).ToUpperInvariant();
                    }
                    l.Add(x);
                    break;
                }
            }
            return l;
        }

        static string Safe(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        static string NewestCached(string compDir)
        {
            if (!Directory.Exists(compDir)) return null;
            return Directory.GetDirectories(compDir)
                .Where(d => File.Exists(Path.Combine(d, ".ok")))
                .OrderByDescending(d => File.GetLastWriteTimeUtc(Path.Combine(d, ".ok")))
                .FirstOrDefault();
        }

        string Ensure(string component, string label, Func<Rel> find, Action<string, string> extract, string ext)
        {
            string compDir = Path.Combine(AppPaths.Cache, component);
            Directory.CreateDirectory(compDir);
            Rel best = null;
            try { best = find(); }
            catch (Exception ex) { log("  ! Sem acesso à internet/GitHub para " + label + ": " + ex.Message); }
            if (best == null)
            {
                string c = NewestCached(compDir);
                if (c != null)
                {
                    log("  Usando " + label + " do cache: " + Path.GetFileName(c));
                    return c;
                }
                throw new Exception("Não foi possível obter " + label + " (sem internet e sem cache).");
            }
            string dir = Path.Combine(compDir, Safe(best.Tag));
            if (File.Exists(Path.Combine(dir, ".ok")))
            {
                log("  " + label + " " + best.Tag + " (já em cache)");
                return dir;
            }
            log("  Baixando " + label + " " + best.Tag + " (" + best.Asset + ")...");
            string tmp = Path.Combine(compDir, "download-" + Guid.NewGuid().ToString("N") + ext);
            try
            {
                GetFile(best.Url, tmp);
                if (best.Sha != null)
                {
                    string h = Util.Sha256(tmp);
                    if (h != best.Sha) throw new Exception("SHA-256 do download não confere (" + best.Asset + "). Download descartado.");
                    log("  SHA-256 verificado: " + h.Substring(0, 16) + "...");
                }
                string tmpDir = dir + ".tmp";
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                Directory.CreateDirectory(tmpDir);
                extract(tmp, tmpDir);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.Move(tmpDir, dir);
                File.WriteAllText(Path.Combine(dir, ".ok"), best.Tag + "|" + best.Asset + "|" + (best.Sha ?? ""));
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
            return dir;
        }

        static Rel Newest(List<Rel> l) { return l.OrderByDescending(r => r.Pub).FirstOrDefault(); }

        static void Unzip(string zip, string dir) { ZipFile.ExtractToDirectory(zip, dir); }

        public string EnsureOptiScaler()
        {
            string d = Ensure("optiscaler-f5", "OptiScaler F5 (janblade)", delegate
            {
                return Newest(Releases("janblade/OptiScaler-F5-DLSSNR-Multipass", new Regex(@"^OptiScaler-DLSSNR-F5-(?!.*test).*\.zip$", RegexOptions.IgnoreCase), 15, true));
            }, Unzip, ".zip");
            if (!File.Exists(Path.Combine(d, "OptiScaler.dll"))) throw new Exception("Pacote do OptiScaler sem OptiScaler.dll: " + d);
            return d;
        }

        public string EnsureRenoDx()
        {
            string d = Ensure("renodx-dlss5", "RenoDX DLSS 5 add-on", delegate
            {
                return Newest(Releases("RankFTW/rhi-repo", new Regex(@"^renodx-dlss5_.*\.zip$", RegexOptions.IgnoreCase), 100, true));
            }, Unzip, ".zip");
            if (Directory.GetFiles(d, "renodx-dlss5.addon64", SearchOption.AllDirectories).Length == 0)
                throw new Exception("Pacote RenoDX sem renodx-dlss5.addon64");
            return d;
        }

        public string EnsureDxvk()
        {
            string d = Ensure("dxvk", "DXVK", delegate
            {
                return Newest(Releases("doitsujin/dxvk", new Regex(@"^dxvk-\d[\d.]*\.tar\.gz$", RegexOptions.IgnoreCase), 5, false));
            }, Tar.ExtractTarGz, ".tar.gz");
            string[] x64 = Directory.GetDirectories(d, "x64", SearchOption.AllDirectories);
            if (x64.Length == 0) throw new Exception("Pacote DXVK sem pasta x64");
            return x64[0];
        }

        public string EnsureDgVoodoo()
        {
            string d = Ensure("dgvoodoo2", "dgVoodoo2", delegate
            {
                return Newest(Releases("dege-diosg/dgVoodoo2", new Regex(@"^dgVoodoo2_\d+_\d+(_\d+)?\.zip$", RegexOptions.IgnoreCase), 5, false));
            }, Unzip, ".zip");
            if (!File.Exists(Path.Combine(d, "MS", "x64", "D3D9.dll"))) throw new Exception("Pacote dgVoodoo2 sem MS\\x64\\D3D9.dll");
            return d;
        }

        /// <summary>
        /// dlssg_for_sm86 (sdli1995, GPLv3): roda o DLSS Frame Generation da NVIDIA (runtime oficial 310.9.16, sem
        /// modificação) nas RTX 30/20. https://github.com/sdli1995/dlssg_for_sm86
        /// </summary>
        public string EnsureSm86()
        {
            string d = Ensure("dlssg_for_sm86", "dlssg_for_sm86 (MFG para RTX 30/20)", delegate
            {
                return Newest(Releases("sdli1995/dlssg_for_sm86", new Regex(@"^dlssg-release-[\d.]+-x64\.zip$", RegexOptions.IgnoreCase), 10, false));
            }, Unzip, ".zip");
            string rt = Path.Combine(d, "dlssg_sm86", "runtime", "nvngx_dlssg.dll");
            if (!File.Exists(Path.Combine(d, "version.dll")) || !File.Exists(rt))
                throw new Exception("Pacote dlssg_for_sm86 incompleto: " + d);
            if (!Signature.IsNvidiaSigned(rt))
                throw new Exception("O nvngx_dlssg.dll do pacote dlssg_for_sm86 não tem assinatura NVIDIA válida — instalação cancelada.");
            return d;
        }

        /// <summary>
        /// NVIDIA Streamline SDK oficial (github.com/NVIDIA-RTX/Streamline), usado quando o PC não tem a pasta do SDK.
        /// Do zip (~276 MB) só são extraídos bin/x64 (produção) e as licenças.
        /// </summary>
        public string EnsureStreamline()
        {
            string d = Ensure("streamline", "NVIDIA Streamline SDK", delegate
            {
                return Newest(Releases("NVIDIA-RTX/Streamline", new Regex(@"^streamline-sdk-v[\d.]+\.zip$", RegexOptions.IgnoreCase), 5, false));
            }, delegate(string zip, string outDir)
            {
                using (ZipArchive z = ZipFile.OpenRead(zip))
                {
                    foreach (ZipArchiveEntry e in z.Entries)
                    {
                        string n = e.FullName.Replace('\\', '/');
                        bool want = (n.StartsWith("bin/x64/") && n.IndexOf('/', 8) < 0 && e.Name.Length > 0) ||
                                    n == "license.txt" || n == "3rd-party-licenses.md";
                        if (!want) continue;
                        string dest = Path.Combine(outDir, n.Replace('/', '\\'));
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        e.ExtractToFile(dest, true);
                    }
                }
            }, ".zip");
            if (!Installer.IsStreamlineDir(d)) throw new Exception("Pacote do Streamline sem bin\\x64: " + d);
            return d;
        }

        public string EnsureReShade()
        {
            return Ensure("reshade", "ReShade (add-on)", delegate
            {
                string html = GetString("https://reshade.me/");
                Match m = Regex.Match(html, @"/downloads/ReShade_Setup_([\d.]+)_Addon\.exe");
                if (!m.Success) throw new Exception("link do ReShade não encontrado em reshade.me");
                Rel r = new Rel();
                r.Tag = m.Groups[1].Value;
                r.Asset = "ReShade_Setup_" + r.Tag + "_Addon.exe";
                r.Url = "https://reshade.me" + m.Value;
                return r;
            }, ExtractReShade, ".exe");
        }

        /// <summary>O instalador do ReShade carrega um ZIP embutido com ReShade64.dll.</summary>
        static void ExtractReShade(string exe, string outDir)
        {
            byte[] b = File.ReadAllBytes(exe);
            for (int i = 0; i < b.Length - 4; i++)
            {
                if (b[i] != 0x50 || b[i + 1] != 0x4B || b[i + 2] != 3 || b[i + 3] != 4) continue;
                try
                {
                    using (MemoryStream ms = new MemoryStream(b, i, b.Length - i))
                    using (ZipArchive z = new ZipArchive(ms, ZipArchiveMode.Read))
                    {
                        ZipArchiveEntry e = z.GetEntry("ReShade64.dll");
                        if (e == null) continue;
                        e.ExtractToFile(Path.Combine(outDir, "ReShade64.dll"));
                        return;
                    }
                }
                catch { }
            }
            throw new Exception("ReShade64.dll não encontrado no instalador do ReShade.");
        }
    }

    public static class Tar
    {
        static bool ReadFull(Stream s, byte[] b, int n)
        {
            int got = 0;
            while (got < n)
            {
                int r = s.Read(b, got, n - got);
                if (r <= 0) return false;
                got += r;
            }
            return true;
        }

        static void Skip(Stream s, long n)
        {
            byte[] b = new byte[65536];
            while (n > 0)
            {
                int r = s.Read(b, 0, (int)Math.Min(b.Length, n));
                if (r <= 0) return;
                n -= r;
            }
        }

        static string CStr(byte[] b, int off, int len)
        {
            int e = off;
            while (e < off + len && b[e] != 0) e++;
            return Encoding.UTF8.GetString(b, off, e - off);
        }

        static long Oct(byte[] b, int off, int len)
        {
            string s = CStr(b, off, len).Trim();
            return s.Length == 0 ? 0 : Convert.ToInt64(s, 8);
        }

        public static void ExtractTarGz(string path, string outDir)
        {
            string root = Path.GetFullPath(outDir).TrimEnd('\\') + "\\";
            using (FileStream fs = File.OpenRead(path))
            using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
            {
                byte[] h = new byte[512];
                string longName = null;
                while (ReadFull(gz, h, 512))
                {
                    bool zero = true;
                    foreach (byte x in h) if (x != 0) { zero = false; break; }
                    if (zero) break;
                    string name = CStr(h, 0, 100);
                    long size = Oct(h, 124, 12);
                    char type = (char)h[156];
                    string prefix = CStr(h, 345, 155);
                    if (prefix.Length > 0 && CStr(h, 257, 6).StartsWith("ustar")) name = prefix + "/" + name;
                    if (longName != null) { name = longName; longName = null; }
                    long pad = (512 - size % 512) % 512;
                    if (type == 'L')
                    {
                        byte[] d = new byte[size];
                        ReadFull(gz, d, (int)size);
                        longName = CStr(d, 0, d.Length);
                        Skip(gz, pad);
                        continue;
                    }
                    string target = Path.GetFullPath(Path.Combine(outDir, name.Replace('/', '\\')));
                    bool inside = target.StartsWith(root, StringComparison.OrdinalIgnoreCase);
                    if (type == '5')
                    {
                        if (inside) Directory.CreateDirectory(target);
                    }
                    else if ((type == '0' || type == '\0') && inside)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        using (FileStream o = File.Create(target))
                        {
                            byte[] buf = new byte[65536];
                            long left = size;
                            while (left > 0)
                            {
                                int r = gz.Read(buf, 0, (int)Math.Min(buf.Length, left));
                                if (r <= 0) break;
                                o.Write(buf, 0, r);
                                left -= r;
                            }
                        }
                        Skip(gz, pad);
                        continue;
                    }
                    else Skip(gz, size);
                    Skip(gz, pad);
                }
            }
        }
    }
}
