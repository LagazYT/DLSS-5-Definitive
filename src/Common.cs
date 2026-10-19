using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Dlss5Injector
{
    public static class AppPaths
    {
        static string root;

        /// <summary>Portable quando existe "portable.ini" ao lado do exe.</summary>
        public static bool IsPortable
        {
            get { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.ini")); }
        }

        public static string Root
        {
            get
            {
                if (root != null) return root;
                if (IsPortable)
                {
                    // Versão portable: configurações, biblioteca, capas e downloads ficam em .\Data ao lado do exe.
                    root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                    Directory.CreateDirectory(root);
                    return root;
                }
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string p = Path.Combine(local, "DLSS5Definitive");
                string old = Path.Combine(local, "DLSS5Injector");
                // Migra configurações e cache (downloads) da versão anterior "DLSS5 Injector".
                if (!Directory.Exists(p) && Directory.Exists(old))
                {
                    try { Directory.Move(old, p); } catch { }
                }
                Directory.CreateDirectory(p);
                root = p;
                return p;
            }
        }

        public static string Cache
        {
            get
            {
                string p = Path.Combine(Root, "cache");
                Directory.CreateDirectory(p);
                return p;
            }
        }
    }

    public static class Settings
    {
        static Dictionary<string, string> data;

        static string FilePath { get { return Path.Combine(AppPaths.Root, "settings.txt"); } }

        static void Load()
        {
            if (data != null) return;
            data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) data[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
            }
            catch { }
        }

        public static string Get(string key)
        {
            Load();
            string v;
            return data.TryGetValue(key, out v) ? v : null;
        }

        public static void Set(string key, string value)
        {
            Load();
            data[key] = value ?? "";
            try
            {
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, string> kv in data) lines.Add(kv.Key + "=" + kv.Value);
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch { }
        }
    }

    public static class Util
    {
        public static string Sha256(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

        public static Version FileVer(string path)
        {
            try
            {
                FileVersionInfo v = FileVersionInfo.GetVersionInfo(path);
                return new Version(v.FileMajorPart, v.FileMinorPart, v.FileBuildPart, v.FilePrivatePart);
            }
            catch { return new Version(0, 0, 0, 0); }
        }

        public static string FileVerStr(string path)
        {
            Version v = FileVer(path);
            return v.Major == 0 && v.Minor == 0 ? "?" : v.ToString();
        }

        public static string Describe(string path)
        {
            try
            {
                FileVersionInfo v = FileVersionInfo.GetVersionInfo(path);
                return ((v.ProductName ?? "") + " | " + (v.FileDescription ?? "") + " | " + (v.CompanyName ?? "") + " | " + (v.OriginalFilename ?? "")).ToLowerInvariant();
            }
            catch { return ""; }
        }

        public static string Downloads
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"); }
        }

        public static bool CanWrite(string dir)
        {
            try
            {
                string t = Path.Combine(dir, ".dlss5_write_test_" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(t, "x");
                File.Delete(t);
                return true;
            }
            catch { return false; }
        }
    }

    public static class Gpu
    {
        public static string Name = "Desconhecida";
        public static int Series;      // 20, 30, 40, 50 para RTX; 0 = desconhecido
        public static bool IsNvidia;

        public static void Detect()
        {
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    foreach (ManagementObject o in s.Get())
                    {
                        string n = Convert.ToString(o["Name"]);
                        if (string.IsNullOrEmpty(n)) continue;
                        bool nv = n.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (nv || !IsNvidia)
                        {
                            Name = n;
                            IsNvidia = nv;
                            Series = 0;
                            Match m = Regex.Match(n, @"RTX\s*(\d)\d{2,3}\b", RegexOptions.IgnoreCase);
                            if (m.Success)
                            {
                                int d = int.Parse(m.Groups[1].Value);
                                if (d >= 2 && d <= 9) Series = d * 10;
                            }
                        }
                        if (nv) break;
                    }
                }
            }
            catch { }
        }

        public static string SeriesText
        {
            get
            {
                if (!IsNvidia) return "não-NVIDIA";
                return Series > 0 ? "RTX " + Series : "NVIDIA (série não identificada)";
            }
        }
    }

    /// <summary>Runtime do DLSS Neural Rendering ("DLSS 5"). Não é distribuído pelo fork nem pela NVIDIA no Streamline.</summary>
    public static class NrRuntime
    {
        public const string HashRtx50 = "E16BCF15E16E13F527491CDF7845B2FE6521A738D8F7C9C721866A8496E1FC8E";
        public const string HashCrossGen = "E67DEE209320CDAFE0E93E45675D7AA34323A53ACC57A72B2E40A181581C989A";
        public const string FileName = "nvngx_dlssnr.dll";

        public enum Kind { Unknown, Official, CrossGen }

        public class Info
        {
            public string Hash;
            public Version Version;
            public Kind Kind;
            public string Label;
            public string Warning;   // null = compatível com a GPU
        }

        /// <summary>Lê hash, versão e assinatura. Lento para arquivos grandes: chamar fora da UI.</summary>
        public static Info Inspect(string path)
        {
            Info i = new Info();
            i.Hash = Util.Sha256(path);
            i.Version = Util.FileVer(path);
            string ver = i.Version.Major + "." + i.Version.Minor + "." + i.Version.Build;
            if (i.Hash == HashCrossGen) { i.Kind = Kind.CrossGen; i.Label = "ShortFuse cross-gen 310.8 (FP16)"; }
            else if (i.Hash == HashRtx50) { i.Kind = Kind.Official; i.Label = "NVIDIA oficial 310.8 · assinada"; }
            else if (Util.Describe(path).Contains("dlssnr") && Signature.IsNvidiaSigned(path))
            {
                i.Kind = Kind.Official;
                i.Label = "NVIDIA oficial " + ver + " · assinada";
            }
            else { i.Kind = Kind.Unknown; i.Label = "Versão " + ver + " não reconhecida (sem assinatura NVIDIA válida)"; }

            if (!Gpu.IsNvidia)
                i.Warning = "GPU não-NVIDIA: o DLSS-NR não inicia sem NVIDIA (o fork tem um port 'vendor-neutral' opcional, não instalado por este app).";
            else if (i.Kind == Kind.Unknown)
                i.Warning = "Arquivo não reconhecido: confira se é mesmo o nvngx_dlssnr.dll da NVIDIA.";
            else if (Gpu.Series >= 50 && i.Kind == Kind.CrossGen)
                i.Warning = "A runtime cross-gen é para RTX 20/30/40; na sua RTX 50 use a oficial NVIDIA.";
            else if (Gpu.Series >= 20 && Gpu.Series <= 40 && i.Kind == Kind.Official)
                i.Warning = "Na RTX " + Gpu.Series + " o fork documenta só a cross-gen 310.8 do ShortFuse (SHA-256 E67DEE20...); a oficial pode não iniciar.";
            return i;
        }

        public static string FindCandidate(string streamlineDir)
        {
            string newest = null;
            Version best = new Version(0, 0);
            foreach (string f in Candidates(streamlineDir))
            {
                Version v = Util.FileVer(f);
                if (newest == null || v > best) { newest = f; best = v; }
            }
            return newest;
        }

        static IEnumerable<string> Candidates(string streamlineDir)
        {
            List<string> dirs = new List<string>();
            dirs.Add(Path.Combine(AppPaths.Root, "runtime"));
            if (!string.IsNullOrEmpty(streamlineDir))
            {
                dirs.Add(streamlineDir);
                dirs.Add(Path.Combine(streamlineDir, "bin", "x64"));
            }
            dirs.Add(AppDomain.CurrentDomain.BaseDirectory);
            dirs.Add(Util.Downloads);
            dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            // Subpastas de Downloads (até 2 níveis)
            try
            {
                foreach (string d1 in Directory.GetDirectories(Util.Downloads))
                {
                    dirs.Add(d1);
                    try { dirs.AddRange(Directory.GetDirectories(d1)); } catch { }
                }
            }
            catch { }
            List<string> found = new List<string>();
            foreach (string d in dirs)
            {
                string f = Path.Combine(d, FileName);
                if (File.Exists(f)) found.Add(f);
            }
            return found;
        }
    }

    /// <summary>Verificação de assinatura Authenticode (DLLs NVIDIA da pasta Streamline).</summary>
    public static class Signature
    {
        // Thumbprints dos certificados NVIDIA usados no Streamline SDK (sl.*) e nas DLLs NGX (nvngx_*).
        public static readonly string[] NvidiaThumbprints = {
            "B66776FC8E70C58ED98199E8391264C827AAC534",
            "7B7B0B6697AFB438CF6F65A155F00E86676FB186"
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class WinTrustFileInfo
        {
            public uint cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo));
            public string pcwszFilePath;
            public IntPtr hFile = IntPtr.Zero;
            public IntPtr pgKnownSubject = IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        class WinTrustData
        {
            public uint cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustData));
            public IntPtr pPolicyCallbackData = IntPtr.Zero;
            public IntPtr pSIPClientData = IntPtr.Zero;
            public uint dwUIChoice = 2;            // WTD_UI_NONE
            public uint fdwRevocationChecks = 0;   // WTD_REVOKE_NONE
            public uint dwUnionChoice = 1;         // WTD_CHOICE_FILE
            public IntPtr pFile;
            public uint dwStateAction = 0;
            public IntPtr hWVTStateData = IntPtr.Zero;
            public IntPtr pwszURLReference = IntPtr.Zero;
            public uint dwProvFlags = 0x1000;      // WTD_CACHE_ONLY_URL_RETRIEVAL
            public uint dwUIContext = 0;
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
        static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, IntPtr data);

        static readonly Guid GenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        public static bool IsValid(string path)
        {
            WinTrustFileInfo fi = new WinTrustFileInfo();
            fi.pcwszFilePath = path;
            IntPtr pFi = Marshal.AllocHGlobal(Marshal.SizeOf(fi));
            WinTrustData wd = new WinTrustData();
            IntPtr pWd = IntPtr.Zero;
            try
            {
                Marshal.StructureToPtr(fi, pFi, false);
                wd.pFile = pFi;
                pWd = Marshal.AllocHGlobal(Marshal.SizeOf(wd));
                Marshal.StructureToPtr(wd, pWd, false);
                return WinVerifyTrust(IntPtr.Zero, GenericVerifyV2, pWd) == 0;
            }
            finally
            {
                if (pWd != IntPtr.Zero) Marshal.FreeHGlobal(pWd);
                Marshal.FreeHGlobal(pFi);
            }
        }

        public static bool IsNvidiaSigned(string path)
        {
            try
            {
                if (!IsValid(path)) return false;
                X509Certificate2 c = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
                return Array.IndexOf(NvidiaThumbprints, c.Thumbprint.ToUpperInvariant()) >= 0;
            }
            catch { return false; }
        }
    }

    public static class IniEdit
    {
        /// <summary>Define section/key=value preservando comentários e a formatação do arquivo.</summary>
        public static void Set(List<string> lines, string section, string key, string value)
        {
            int start = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("[") && t.EndsWith("]") &&
                    string.Equals(t.Substring(1, t.Length - 2).Trim(), section, StringComparison.OrdinalIgnoreCase))
                {
                    start = i;
                    break;
                }
            }
            if (start < 0)
            {
                lines.Add("");
                lines.Add("[" + section + "]");
                lines.Add(key + "=" + value);
                return;
            }
            int end = lines.Count;
            for (int i = start + 1; i < lines.Count; i++)
            {
                if (lines[i].TrimStart().StartsWith("[")) { end = i; break; }
            }
            for (int i = start + 1; i < end; i++)
            {
                string t = lines[i];
                string tt = t.TrimStart();
                if (tt.StartsWith(";") || tt.StartsWith("#")) continue;
                int eq = t.IndexOf('=');
                if (eq < 0) continue;
                if (string.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    string left = t.Substring(0, eq);
                    lines[i] = left + "=" + (left.EndsWith(" ") ? " " : "") + value;
                    return;
                }
            }
            int ins = end;
            while (ins > start + 1 && (lines[ins - 1].Trim().Length == 0 || lines[ins - 1].TrimStart().StartsWith("; ---")))
                ins--;
            lines.Insert(ins, key + "=" + value);
        }
    }
}
