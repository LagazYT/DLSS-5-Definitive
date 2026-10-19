using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Dlss5Injector
{
    public class UpdateInfo
    {
        public Version Version;
        public string Tag, Notes, PageUrl, AssetName, AssetUrl, Sha;
        public long Size;
    }

    /// <summary>
    /// Atualização automática pelas Releases do GitHub do projeto.
    /// Instalado: baixa o "Setup.exe" novo e roda em modo /update (silencioso), que reabre o app.
    /// Portable (ou rodando fora da pasta instalada): baixa o zip portable e troca o exe no lugar.
    /// </summary>
    public static class Updater
    {
        public const string Repo = "LagazYT/DLSS-5-Definitive";
        const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DLSS5Definitive";

        public static Version Current { get { return ParseVersion(Program.Version); } }

        static string ExePath { get { return Application.ExecutablePath; } }
        static string ExeDir { get { return Path.GetDirectoryName(ExePath); } }

        public static Version ParseVersion(string s)
        {
            Match m = Regex.Match(s ?? "", @"(\d+)(?:\.(\d+))?(?:\.(\d+))?");
            if (!m.Success) return new Version(0, 0);
            int a = int.Parse(m.Groups[1].Value);
            int b = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            int c = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
            return new Version(a, b, c);
        }

        /// <summary>Está rodando da pasta registrada pelo instalador?</summary>
        public static bool IsInstalledCopy
        {
            get
            {
                if (AppPaths.IsPortable) return false;
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegPath))
                    {
                        string loc = k == null ? null : Convert.ToString(k.GetValue("InstallLocation"));
                        return !string.IsNullOrEmpty(loc) &&
                               string.Equals(Path.GetFullPath(loc).TrimEnd('\\'), ExeDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch { return false; }
            }
        }

        static HttpWebRequest Req(string url)
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "DLSS5Definitive/" + Program.Version;
            r.Timeout = 20000;
            r.ReadWriteTimeout = 120000;
            if (url.Contains("api.github.com")) r.Accept = "application/vnd.github+json";
            return r;
        }

        static string GetString(string url)
        {
            // Para testes: DLSS5DEF_UPDATE_FEED pode apontar para um JSON local no formato da API do GitHub.
            if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) || File.Exists(url))
                return File.ReadAllText(url.StartsWith("file:") ? new Uri(url).LocalPath : url, Encoding.UTF8);
            using (WebResponse resp = Req(url).GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
        }

        /// <summary>Retorna a versão nova disponível, ou null se já está atualizado. Lança exceção se não conseguir consultar.</summary>
        public static UpdateInfo Check()
        {
            string feed = Environment.GetEnvironmentVariable("DLSS5DEF_UPDATE_FEED");
            if (string.IsNullOrEmpty(feed)) feed = "https://api.github.com/repos/" + Repo + "/releases/latest";
            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> r = js.DeserializeObject(GetString(feed)) as Dictionary<string, object>;
            if (r == null) return null;
            UpdateInfo u = new UpdateInfo();
            u.Tag = Convert.ToString(r["tag_name"]);
            u.Version = ParseVersion(u.Tag);
            if (u.Version <= Current) return null;
            object o;
            u.Notes = r.TryGetValue("body", out o) ? Convert.ToString(o) : "";
            u.PageUrl = r.TryGetValue("html_url", out o) ? Convert.ToString(o) : "https://github.com/" + Repo + "/releases/latest";
            bool wantSetup = IsInstalledCopy;
            foreach (object ao in (object[])r["assets"])
            {
                Dictionary<string, object> a = (Dictionary<string, object>)ao;
                string name = Convert.ToString(a["name"]);
                bool isSetup = name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0 && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                bool isPortable = name.IndexOf("Portable", StringComparison.OrdinalIgnoreCase) >= 0 && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                if (wantSetup ? !isSetup : !isPortable) continue;
                u.AssetName = name;
                u.AssetUrl = Convert.ToString(a["browser_download_url"]);
                u.Size = a.ContainsKey("size") ? Convert.ToInt64(a["size"]) : 0;
                if (a.TryGetValue("digest", out o) && o != null && Convert.ToString(o).StartsWith("sha256:"))
                    u.Sha = Convert.ToString(o).Substring(7).ToUpperInvariant();
                break;
            }
            if (u.AssetUrl == null) throw new Exception("A versão " + u.Tag + " não tem o arquivo de " + (wantSetup ? "instalação" : "versão portable") + ".");
            return u;
        }

        static string WorkDir
        {
            get
            {
                string d = Path.Combine(Path.GetTempPath(), "DLSS5Definitive-update");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        /// <summary>Baixa o arquivo da atualização e confere o SHA-256 publicado no GitHub.</summary>
        public static string Download(UpdateInfo u, Action<int> progress)
        {
            string dest = Path.Combine(WorkDir, u.Tag + "-" + Regex.Replace(u.AssetName, @"[^\w.\-]", "_"));
            string tmp = dest + ".part";
            if (u.AssetUrl.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) File.Copy(new Uri(u.AssetUrl).LocalPath, tmp, true);
            else
            {
                using (WebResponse resp = Req(u.AssetUrl).GetResponse())
                using (Stream s = resp.GetResponseStream())
                using (FileStream f = File.Create(tmp))
                {
                    long total = resp.ContentLength > 0 ? resp.ContentLength : u.Size;
                    byte[] buf = new byte[1 << 16];
                    long done = 0;
                    int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0)
                    {
                        f.Write(buf, 0, n);
                        done += n;
                        if (total > 0) progress((int)(done * 100 / total));
                    }
                }
            }
            if (u.Sha != null && Util.Sha256(tmp) != u.Sha)
            {
                File.Delete(tmp);
                throw new Exception("O arquivo baixado não confere com o SHA-256 publicado no GitHub. A atualização foi cancelada.");
            }
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(tmp, dest);
            progress(100);
            return dest;
        }

        /// <summary>Aplica a atualização e reinicia. Quem chama deve fechar o app logo em seguida.</summary>
        public static void ApplyAndRestart(string file)
        {
            if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                // Instalador novo em modo silencioso: espera este processo fechar, atualiza e reabre o app.
                ProcessStartInfo psi = new ProcessStartInfo(file, "/update /waitpid:" + Process.GetCurrentProcess().Id);
                psi.UseShellExecute = true;
                Process.Start(psi);
                return;
            }
            // Portable: o exe em uso pode ser renomeado (não apagado); o novo entra no lugar.
            string ex = Path.Combine(WorkDir, "portable-" + Path.GetFileNameWithoutExtension(file));
            if (Directory.Exists(ex)) Directory.Delete(ex, true);
            ZipFile.ExtractToDirectory(file, ex);
            string newExe = Directory.GetFiles(ex, "DLSS 5 Definitive.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (newExe == null) throw new Exception("O pacote da atualização não contém o DLSS 5 Definitive.exe.");
            string old = ExePath + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(ExePath, old);
            try { File.Copy(newExe, ExePath, true); }
            catch { File.Move(old, ExePath); throw; }
            string readme = Path.Combine(Path.GetDirectoryName(newExe), "LEIA-ME.txt");
            if (File.Exists(readme)) File.Copy(readme, Path.Combine(ExeDir, "LEIA-ME.txt"), true);
            ProcessStartInfo run = new ProcessStartInfo(ExePath, "/updated");
            run.WorkingDirectory = ExeDir;
            run.UseShellExecute = true;
            Process.Start(run);
        }

        /// <summary>Na abertura: apaga o exe antigo deixado pela atualização portable e os downloads da atualização.</summary>
        public static void CleanupAfterUpdate()
        {
            try { if (File.Exists(ExePath + ".old")) File.Delete(ExePath + ".old"); } catch { }
            try
            {
                string d = Path.Combine(Path.GetTempPath(), "DLSS5Definitive-update");
                if (Directory.Exists(d)) Directory.Delete(d, true);
            }
            catch { }
        }

        /// <summary>Texto das novidades sem a marcação do Markdown, para mostrar no aviso.</summary>
        public static string PlainNotes(string md, int max)
        {
            string s = Regex.Replace(md ?? "", @"\r", "");
            s = Regex.Replace(s, @"^#+\s*", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"\*\*|__|`|\*(?=\S)|(?<=\S)\*", "");
            s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]+\)", "$1");
            s = Regex.Replace(s, @"^\|.*\|$\n?", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"\n{3,}", "\n\n").Trim();
            return s.Length > max ? s.Substring(0, max).TrimEnd() + "…" : s;
        }
    }
}
