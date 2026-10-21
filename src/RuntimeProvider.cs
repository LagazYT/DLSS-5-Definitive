using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Dlss5Injector
{
    /// <summary>
    /// Obtém a runtime do DLSS 5 (nvngx_dlssnr.dll) sem o usuário procurar o arquivo:
    ///   1) download do GitHub oficial da NVIDIA, se ela publicar o arquivo (hoje ela não publica);
    ///   2) procura nos jogos instalados no PC — jogos com DLSS 5 trazem o arquivo, e os jogos onde o app já
    ///      instalou o DLSS 5 têm uma cópia.
    /// Só aceita arquivos com assinatura NVIDIA válida. Não baixa de espelhos não oficiais: o arquivo é
    /// proprietário da NVIDIA e só é distribuído por ela.
    /// </summary>
    public static class RuntimeProvider
    {
        // Caminhos onde a NVIDIA publicaria a runtime nos repositórios oficiais
        static readonly string[][] Official = {
            new[] { "NVIDIA/DLSS", "lib/Windows_x86_64/rel/" + NrRuntime.FileName },
            new[] { "NVIDIA-RTX/Streamline", "bin/x64/" + NrRuntime.FileName }
        };

        static HttpWebRequest Req(string url)
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "DLSS5Definitive";
            r.Timeout = 20000;
            r.ReadWriteTimeout = 120000;
            if (url.Contains("api.github.com")) r.Accept = "application/vnd.github+json";
            return r;
        }

        /// <summary>Baixa a runtime do GitHub oficial da NVIDIA, se estiver publicada. Devolve o arquivo em destDir ou null.</summary>
        public static string TryOfficialDownload(string destDir, Action<string> status)
        {
            foreach (string[] o in Official)
            {
                string downloadUrl;
                try
                {
                    string json;
                    using (WebResponse resp = Req("https://api.github.com/repos/" + o[0] + "/contents/" + o[1]).GetResponse())
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) json = sr.ReadToEnd();
                    Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                    downloadUrl = d == null ? null : Convert.ToString(d["download_url"]);
                }
                catch { continue; }   // 404 = ainda não publicado
                if (string.IsNullOrEmpty(downloadUrl)) continue;
                if (status != null) status(L.T("Baixando a runtime do DLSS 5 do GitHub oficial da NVIDIA..."));
                string tmp = Path.Combine(Path.GetTempPath(), "dlss5def-" + Guid.NewGuid().ToString("N") + ".dll");
                try
                {
                    using (WebResponse resp = Req(downloadUrl).GetResponse())
                    using (Stream s = resp.GetResponseStream())
                    using (FileStream f = File.Create(tmp)) s.CopyTo(f);
                    // Arquivo grande, com o nome certo e assinado pela NVIDIA — senão é descartado
                    string named = Path.Combine(Path.GetDirectoryName(tmp), Guid.NewGuid().ToString("N"), NrRuntime.FileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(named));
                    File.Move(tmp, named);
                    tmp = named;
                    if (NrRuntime.IsRuntimeFile(tmp) && Signature.IsNvidiaSigned(tmp)) return NrRuntime.Import(tmp, destDir);
                }
                catch { }
                finally
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); Directory.Delete(Path.GetDirectoryName(tmp)); } catch { }
                }
            }
            return null;
        }

        /// <summary>Pastas dos jogos instalados: a biblioteca do app ou, se ela estiver vazia, um escaneamento rápido.</summary>
        static List<string> GameRoots()
        {
            List<string> roots = new List<string>();
            try { roots.AddRange(LibraryStore.Load().Where(g => g != null && !string.IsNullOrEmpty(g.Path)).Select(g => g.Path)); } catch { }
            if (roots.Count == 0)
            {
                try
                {
                    LibraryScanner sc = new LibraryScanner(delegate(GameEntry g) { lock (roots) roots.Add(g.Path); }, delegate(string s) { });
                    sc.ScanAll();
                }
                catch { }
            }
            return roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Procura nvngx_dlssnr.dll assinado pela NVIDIA nos jogos instalados; devolve a versão mais nova (ou null).</summary>
        public static string FindInGames(Action<string> status)
        {
            string best = null;
            Version bestV = new Version(0, 0);
            foreach (string root in GameRoots())
            {
                if (status != null) status(L.T("Procurando a runtime do DLSS 5 nos jogos instalados... ") + Path.GetFileName(root));
                int budget = 40000;
                foreach (string f in FindFiles(root, 0, ref budget))
                {
                    if (!NrRuntime.IsRuntimeFile(f)) continue;
                    Version v = Util.FileVer(f);
                    if (best != null && v <= bestV) continue;
                    if (!Signature.IsNvidiaSigned(f)) continue;
                    best = f;
                    bestV = v;
                }
            }
            return best;
        }

        static IEnumerable<string> FindFiles(string dir, int depth, ref int budget)
        {
            List<string> hits = new List<string>();
            Walk(dir, depth, ref budget, hits);
            return hits;
        }

        static void Walk(string dir, int depth, ref int budget, List<string> hits)
        {
            if (budget <= 0) return;
            try
            {
                string f = Path.Combine(dir, NrRuntime.FileName);
                if (File.Exists(f)) hits.Add(f);
                if (depth >= 7) return;
                foreach (string d in Directory.GetDirectories(dir))
                {
                    if (--budget <= 0) return;
                    try { if ((new DirectoryInfo(d).Attributes & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                    Walk(d, depth + 1, ref budget, hits);
                }
            }
            catch { }
        }

        /// <summary>Download oficial (se houver) → jogos instalados. Copia o achado para destDir e devolve o caminho (ou null).</summary>
        public static string Obtain(string destDir, Action<string> status)
        {
            // Zip com a runtime (ex.: "DLL DLSS 5.zip") na pasta do app, em Downloads ou na Área de Trabalho
            string zip = NrRuntime.FindZip(AppDomain.CurrentDomain.BaseDirectory, destDir, Util.Downloads,
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            if (zip != null)
            {
                if (status != null) status(L.T("Extraindo a runtime do DLSS 5 do zip: ") + zip);
                try { return NrRuntime.ExtractFromZip(zip, destDir); } catch (Exception ex) { if (status != null) status(ex.Message); }
            }
            string got = TryOfficialDownload(destDir, status);
            if (got != null) return got;
            string inGame = FindInGames(status);
            if (inGame == null) return null;
            if (status != null) status(L.T("Runtime do DLSS 5 encontrada no jogo: ") + inGame);
            return NrRuntime.Import(inGame, destDir);
        }
    }
}
