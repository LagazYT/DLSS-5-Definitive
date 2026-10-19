using System;
using System.Collections.Generic;
using System.IO;

namespace Dlss5Injector
{
    public enum Method { None, Opti, RenoDX, DxvkOpti, DgVoodooOpti }

    public class Options
    {
        public int FgMult = 1;          // 1 = desligado, 2..6 = 2X..6X
        public bool FgDynamic;          // MFG dinâmico (RTX 50)
        public bool FrameGen { get { return FgMult > 1 || FgDynamic; } }
        public bool UpdateDlss = true;
        public bool Log;
        public int Passes = 1;
        public bool RunBeforeSR = true;
    }

    public class InstallPlan
    {
        public Method Method;
        public GfxApi Api;
        public string Proxy;
        public List<string[]> Ini = new List<string[]>();
        public bool MoveReShade;
        public bool CopyStreamlineFG;
        public bool PlaceDlss;
        public bool UpdateGameDlss;
        public string[] DxvkDlls;
        public bool DgVoodoo;
        public bool Sm86;               // dlssg_for_sm86 (DLSS-G da NVIDIA nas RTX 30/20)
        public string Sm86Proxy;
        public int Sm86MaxFrames = 3;   // quadros gerados: 1..5 (2X..6X)
        public string Reason = "";
        public List<string> Steps = new List<string>();
        public List<string> Warnings = new List<string>();

        public void SetIni(string s, string k, string v) { Ini.Add(new[] { s, k, v }); }
    }

    public static class Planner
    {
        public static string Name(Method m)
        {
            switch (m)
            {
                case Method.Opti: return "OptiScaler F5 (DLSS-NR Multipass)";
                case Method.RenoDX: return "RenoDX DLSS 5 (ReShade add-on)";
                case Method.DxvkOpti: return "DXVK (→Vulkan) + OptiScaler F5";
                case Method.DgVoodooOpti: return "dgVoodoo2 (DX9→DX12) + OptiScaler F5";
            }
            return "Nenhum";
        }

        public static string ApiName(GfxApi a)
        {
            switch (a)
            {
                case GfxApi.DX9: return "DirectX 9";
                case GfxApi.DX10: return "DirectX 10";
                case GfxApi.DX11: return "DirectX 11";
                case GfxApi.DX12: return "DirectX 12";
                case GfxApi.Vulkan: return "Vulkan";
                case GfxApi.OpenGL: return "OpenGL";
            }
            return "Desconhecida";
        }

        /// <summary>Métodos compatíveis, do melhor para o pior.</summary>
        public static List<Method> Candidates(GameInfo g, GfxApi api, out string why)
        {
            List<Method> l = new List<Method>();
            why = "";
            if (!g.Is64)
            {
                why = "Jogo 32-bit: o OptiScaler, o DLSS-NR e o add-on RenoDX DLSS 5 só existem em 64-bit.";
                return l;
            }
            switch (api)
            {
                case GfxApi.DX12:
                    l.Add(Method.Opti);
                    if (g.HasDlss) l.Add(Method.RenoDX);
                    why = g.HasUpscaler
                        ? "DX12 nativo com upscaler: caminho mais completo do fork (NR antes/depois do SR, multipass, FG)."
                        : "DX12 sem upscaler: o fork usa o 'Optical F5Low' (profundidade e movimento próprios) para aplicar o NR na imagem final.";
                    break;
                case GfxApi.DX11:
                    if (g.HasDlss)
                    {
                        l.Add(Method.RenoDX);
                        l.Add(Method.Opti);
                        why = "DX11 com DLSS nativo: o add-on RenoDX DLSS 5 engancha o DLSS do próprio jogo direto no DX11.";
                    }
                    else if (g.HasFsr || g.HasXess)
                    {
                        l.Add(Method.Opti);
                        l.Add(Method.DxvkOpti);
                        why = "DX11 com FSR/XeSS: o OptiScaler troca o upscaler por DLSS via ponte DX11→DX12 (dlss_12), a única que roda o NR.";
                    }
                    else
                    {
                        l.Add(Method.DxvkOpti);
                        l.Add(Method.Opti);
                        why = "DX11 sem upscaler: o NR na imagem final em DX11 é feito pelo fork através do DXVK (NativeDxvkVulkan).";
                    }
                    break;
                case GfxApi.Vulkan:
                    l.Add(Method.Opti);
                    why = "Vulkan: só o OptiScaler funciona (o ReShade/RenoDX DLSS 5 não carrega em Vulkan).";
                    break;
                case GfxApi.DX10:
                    l.Add(Method.DxvkOpti);
                    why = "DX10: convertido para Vulkan pelo DXVK; o OptiScaler aplica o NR na imagem final (experimental).";
                    break;
                case GfxApi.DX9:
                    l.Add(Method.DgVoodooOpti);
                    l.Add(Method.DxvkOpti);
                    why = "DX9: convertido para DX12 pelo dgVoodoo2; o OptiScaler aplica o NR na imagem final (experimental).";
                    break;
                case GfxApi.OpenGL:
                    why = "OpenGL não é suportado pelo OptiScaler nem pelo RenoDX.";
                    break;
                default:
                    why = "API não detectada: escolha manualmente a API gráfica.";
                    break;
            }
            return l;
        }

        static string PickSafeProxy(GameInfo g, string preferred)
        {
            string f = Path.Combine(g.ExeDir, preferred);
            if (!File.Exists(f)) return preferred;
            string kind = GameScanner.ClassifyModule(f, g.Ours);
            if (kind == "DLSS5 Injector" || kind == "OptiScaler" || kind == "Microsoft (original)") return preferred;
            if (preferred == "dxgi.dll" && kind == "ReShade") return preferred; // ReShade vira ReShade64.dll
            // Nome ocupado por outro mod: usa um que o jogo importa
            foreach (string alt in new[] { "winmm.dll", "version.dll", "dbghelp.dll" })
                if (alt != preferred && !File.Exists(Path.Combine(g.ExeDir, alt))) return alt;
            return preferred;
        }

        static string GenericProxy(GameInfo g)
        {
            if (g.ImportsDll("winmm.dll")) return "winmm.dll";
            if (g.ImportsDll("version.dll")) return "version.dll";
            return "winmm.dll";
        }

        public static InstallPlan Build(GameInfo g, GfxApi api, Method m, Options o)
        {
            InstallPlan p = new InstallPlan();
            p.Method = m;
            p.Api = api;
            if (m == Method.None) return p;

            if (!string.IsNullOrEmpty(g.AntiCheat))
                p.Warnings.Add("Anti-cheat detectado (" + g.AntiCheat + "). Injetar DLLs pode causar BANIMENTO em modo online. Use só offline/single-player.");
            if (Gpu.IsNvidia && Gpu.Series > 0 && Gpu.Series < 20)
                p.Warnings.Add("GPU sem núcleos Tensor (pré-RTX): o DLSS não roda.");

            p.UpdateGameDlss = o.UpdateDlss && g.DlssDlls.Count > 0;

            if (m == Method.RenoDX)
            {
                p.Proxy = "dxgi.dll";
                p.Steps.Add("ReShade (com suporte a add-ons) como dxgi.dll");
                p.Steps.Add("Add-on RenoDX DLSS 5 (renodx-dlss5.addon64) — engancha o DLSS do próprio jogo");
                p.Steps.Add("Runtime DLSS-NR (nvngx_dlssnr.dll)");
                if (g.DxgiKind != null && g.DxgiKind != "DLSS5 Injector" && g.DxgiKind != "ReShade")
                    p.Warnings.Add("Já existe um dxgi.dll (" + g.DxgiKind + "). Ele será guardado como backup.");
                if (o.FrameGen)
                    p.Warnings.Add("No método RenoDX, use o Frame Generation do próprio jogo (o add-on não gera quadros).");
                p.Steps.Add("No jogo: ative o DLSS nas opções gráficas; o menu do ReShade (tecla Home) mostra o add-on.");
                AddDlssUpdateStep(p, g);
                return p;
            }

            bool nativeUpscaler = m == Method.Opti && g.HasUpscaler;
            GfxApi optiApi = m == Method.DxvkOpti ? GfxApi.Vulkan : (m == Method.DgVoodooOpti ? GfxApi.DX12 : api);

            // ---------- Proxy ----------
            if (m == Method.Opti && (api == GfxApi.DX12 || api == GfxApi.DX11))
            {
                string pref = g.ExeName.Equals("Cyberpunk2077.exe", StringComparison.OrdinalIgnoreCase) ? "dbghelp.dll" : "dxgi.dll";
                p.Proxy = PickSafeProxy(g, pref);
            }
            else if (m == Method.DgVoodooOpti) p.Proxy = PickSafeProxy(g, "dxgi.dll");
            else p.Proxy = PickSafeProxy(g, GenericProxy(g));

            if (p.Proxy == "dxgi.dll" && g.DxgiKind == "ReShade")
            {
                p.MoveReShade = true;
                p.SetIni("Plugins", "LoadReshade", "true");
                p.Steps.Add("ReShade existente renomeado para ReShade64.dll e carregado pelo OptiScaler (LoadReshade=true)");
            }

            // ---------- Camadas de tradução ----------
            if (m == Method.DxvkOpti)
            {
                if (api == GfxApi.DX9) p.DxvkDlls = new[] { "d3d9.dll" };
                else if (api == GfxApi.DX10) p.DxvkDlls = new[] { "d3d10core.dll", "d3d11.dll", "dxgi.dll" };
                else p.DxvkDlls = new[] { "d3d11.dll", "dxgi.dll" };
                p.Steps.Add("DXVK (" + string.Join(", ", p.DxvkDlls) + ") — " + ApiName(api) + " → Vulkan");
                if (api == GfxApi.DX10 || api == GfxApi.DX11) p.SetIni("DlssNr", "NativeDxvkVulkan", "true");
                p.Warnings.Add("Caminho experimental: o NR em jogos " + ApiName(api) + " via DXVK é suportado pelo fork, mas varia por jogo.");
            }
            if (m == Method.DgVoodooOpti)
            {
                p.DgVoodoo = true;
                p.Steps.Add("dgVoodoo2 (D3D9.dll x64, saída d3d12_fl12_0, sem marca d'água) — DX9 → DX12");
                p.Warnings.Add("Caminho experimental: DX9 → DX12 via dgVoodoo2. Se não funcionar, troque o método para DXVK.");
            }

            p.Steps.Add("OptiScaler F5 como " + p.Proxy + " + OptiScaler.ini + pasta OptiScaler\\ + forwarder nvngx.dll_dlssnr.dll");
            p.Steps.Add("Runtime DLSS-NR (nvngx_dlssnr.dll)");
            p.PlaceDlss = true;

            // ---------- INI ----------
            p.SetIni("DlssNr", "Enabled", "true");
            p.SetIni("DlssNr", "Passes", o.Passes.ToString());
            if (Gpu.Series == 20 || Gpu.Series == 30)
            {
                p.SetIni("DlssNr", "WorkingScale", "0.75");
                p.Warnings.Add("RTX " + Gpu.Series + ": o NR roda num caminho FP16 mais pesado; resolução do modelo reduzida para 0.75.");
            }
            else p.SetIni("DlssNr", "WorkingScale", "1.0");

            if (optiApi == GfxApi.DX12) p.SetIni("Upscalers", "Dx12Upscaler", "dlss");
            else if (optiApi == GfxApi.DX11) p.SetIni("Upscalers", "Dx11Upscaler", "dlss_12");
            else if (optiApi == GfxApi.Vulkan) p.SetIni("Upscalers", "VulkanUpscaler", "dlss");

            bool fg = o.FrameGen;
            int cap;
            string fgLabel;
            string fgOut = FgOutput(optiApi, out cap, out fgLabel);
            int mult = Math.Min(o.FgMult, cap);
            if (fg && g.HasDlssG && nativeUpscaler)
            {
                // O jogo já tem DLSS-G: o OptiScaler só força o multiplicador no MFG do próprio jogo.
                fg = false;
                int nat = Math.Min(o.FgMult, cap);
                if (Gpu.Series >= 40)
                {
                    p.SetIni("DLSSG", "OverrideInterpolationCount", (nat - 1).ToString());
                    if (o.FgDynamic && Gpu.Series >= 50) p.SetIni("DLSSG", "OverrideForceDMFG", "true");
                    if (Gpu.Series == 40 && nat > 2) AdaUnlock(p);
                }
                else if (fgOut == "dlssg") AddSm86(p, g, nat, true);   // RTX 30/20: libera o DLSS-G do próprio jogo
                else p.Warnings.Add("O DLSS Frame Generation do jogo precisa de uma GPU NVIDIA RTX.");
                if (fgOut == "dlssg" || Gpu.Series >= 40) p.Steps.Add("MFG por IA do próprio jogo (DLSS-G) em " + (o.FgDynamic && Gpu.Series >= 50 ? "modo dinâmico" : nat + "X") +
                            " — ative o Frame Generation no menu do jogo");
            }

            if (nativeUpscaler)
            {
                p.SetIni("DlssNr", "RunBeforeSR", o.RunBeforeSR ? "true" : "false");
                if (g.HasDlssD) p.Warnings.Add("O jogo tem Ray Reconstruction: com RR ativo o NR sempre roda depois do SR.");
                p.Steps.Add("No jogo: selecione DLSS (ou FSR/XeSS) nas opções; aperte Insert para abrir o menu do OptiScaler.");
            }
            else
            {
                // Optical F5Low: o fork estima profundidade e movimento sozinho.
                if (fg)
                {
                    p.SetIni("DlssNr", "NativeDepthFinder", "true");
                    p.SetIni("DlssNr", "NativeMotion", "true");
                    p.SetIni("DlssNr", "NativeUpscaler", "true");
                    p.SetIni("DlssNr", "NativeInput", "false");
                    p.Steps.Add("Optical F5Low: NR + Frame Generation (profundidade/movimento estimados pelo fork)");
                }
                else
                {
                    if (m == Method.DxvkOpti || m == Method.DgVoodooOpti) p.SetIni("DlssNr", "NativeDepthFinder", "true");
                    p.SetIni("DlssNr", "NativeInput", "true");
                    p.Steps.Add("Optical F5Low: NR na imagem final (jogo sem upscaler)");
                }
                if (m == Method.Opti && api == GfxApi.DX11)
                    p.Warnings.Add("DX11 sem upscaler direto (sem DXVK) pode não ativar o NR. Se não funcionar, use o método DXVK.");
                p.Steps.Add("No jogo: aperte Insert → página Optical F5Low / Neural Rendering para ajustar.");
            }

            if (fg)
            {
                p.SetIni("FrameGen", "Enabled", "true");
                p.SetIni("FrameGen", "FGInput", "upscaler");
                p.SetIni("FrameGen", "FGOutput", fgOut);
                bool dyn = o.FgDynamic && fgOut == "dlssg" && Gpu.Series >= 50;
                if (fgOut == "dlssg")
                {
                    p.SetIni("FrameGen", "External", "false");
                    p.SetIni("DLSSG", "InterpolationCount", (Math.Max(2, mult) - 1).ToString());
                    if (dyn) p.SetIni("DLSSG", "ForceDMFG", "true");
                    if (Gpu.Series == 40 && mult > 2) AdaUnlock(p);
                    if (Gpu.Series >= 20 && Gpu.Series <= 30) AddSm86(p, g, Math.Max(2, mult), false);
                    p.CopyStreamlineFG = true;
                    p.Steps.Add("Streamline " + "(sl.interposer/common/dlss_g/reflex/pcl + nvngx_dlssg 310.9) em OptiScaler\\streamline");
                    if (g.HasStreamline) p.Warnings.Add("O jogo tem Streamline próprio; os arquivos do jogo NÃO são tocados (o FG do OptiScaler usa a subpasta dele).");
                }
                else if (fgOut == "xefg") p.SetIni("XeFG", "InterpolationCount", (Math.Max(2, mult) - 1).ToString());
                p.Steps.Add("Multi Frame Generation por IA: " + fgLabel + " em " + (dyn ? "modo dinâmico (até 6X, alvo = Hz do monitor)" : Math.Max(2, mult) + "X") +
                    (nativeUpscaler ? " usando os vetores de movimento do jogo" : " — jogo sem suporte: o OptiScaler gera profundidade e movimento (Optical F5Low)"));
                if (o.FgMult > cap && !dyn)
                    p.Warnings.Add("Pedido " + o.FgMult + "X, mas " + fgLabel + " nesta GPU/API vai até " + cap + "X — usando " + cap + "X.");
            }
            else
            {
                p.SetIni("FrameGen", "Enabled", "false");
            }

            if (o.Log)
            {
                p.SetIni("Log", "LogToFile", "true");
                p.SetIni("Log", "LogLevel", "2");
            }
            AddDlssUpdateStep(p, g);
            return p;
        }

        /// <summary>
        /// Melhor Frame Gen por IA para a GPU/API e o multiplicador máximo dele.
        /// RTX 50: DLSS MFG nativo (até 6X). RTX 40: DLSS-G + desbloqueio MFG embutido no fork (até 4X).
        /// RTX 30/20 em DX12: DLSS-G da NVIDIA via dlssg_for_sm86 (até 4X). Outros: XeSS-FG / FSR-FG.
        /// </summary>
        public static string FgOutput(GfxApi optiApi, out int cap, out string label)
        {
            if (Gpu.IsNvidia && Gpu.Series >= 50) { cap = 6; label = "NVIDIA DLSS 4 Multi Frame Generation"; return "dlssg"; }
            if (Gpu.IsNvidia && Gpu.Series == 40) { cap = 4; label = "NVIDIA DLSS Multi Frame Generation (desbloqueio RTX 40)"; return "dlssg"; }
            if (Gpu.IsNvidia && Gpu.Series >= 20 && optiApi == GfxApi.DX12)
            {
                cap = 4;
                label = "NVIDIA DLSS Multi Frame Generation (dlssg_for_sm86)";
                return "dlssg";
            }
            if (optiApi != GfxApi.Vulkan) { cap = 4; label = "Intel XeSS Frame Generation (IA)"; return "xefg"; }
            cap = 2;
            label = "AMD FSR Frame Generation";
            return "fsrfg";
        }

        static void AdaUnlock(InstallPlan p)
        {
            p.SetIni("DLSSG", "AdaMfgUnlock", "true");
            p.Warnings.Add("RTX 40 acima de 2X usa o desbloqueio de MFG embutido no OptiScaler F5 (experimental). Se travar, volte para 2X.");
        }

        /// <summary>RTX 30/20: módulo dlssg_for_sm86 ao lado do jogo, com o runtime DLSS-G oficial da NVIDIA.</summary>
        static void AddSm86(InstallPlan p, GameInfo g, int mult, bool gameOwnFg)
        {
            p.Sm86 = true;
            p.Sm86MaxFrames = Math.Max(1, Math.Min(5, mult - 1));
            foreach (string n in new[] { "version.dll", "winmm.dll", "dinput8.dll", "dbghelp.dll" })
            {
                if (string.Equals(n, p.Proxy, StringComparison.OrdinalIgnoreCase)) continue;
                string f = Path.Combine(g.ExeDir, n);
                if (File.Exists(f))
                {
                    string kind = GameScanner.ClassifyModule(f, g.Ours);
                    if (kind != "DLSS5 Injector" && kind != "Microsoft (original)") continue;
                }
                p.Sm86Proxy = n;
                break;
            }
            if (p.Sm86Proxy == null) p.Sm86Proxy = "version.dll";
            p.Steps.Add("dlssg_for_sm86 (sdli1995) como " + p.Sm86Proxy + ": libera o DLSS-G da NVIDIA na RTX " + Gpu.Series +
                        " com o runtime oficial 310.9.16 (até " + (p.Sm86MaxFrames + 1) + "X)");
            p.Warnings.Add(gameOwnFg
                ? "RTX " + Gpu.Series + ": o MFG vem do dlssg_for_sm86 (comunitário). Testado pelo autor em RTX 3070/2080 Ti; comece em 2X."
                : "RTX " + Gpu.Series + " em jogo sem suporte: DLSS-G via OptiScaler + dlssg_for_sm86 é experimental. Se não ativar, use 1X (desligado) e teste de novo.");
        }

        /// <summary>
        /// Proxies de outros injetores que carregariam junto com o nosso. Considera o estado da pasta
        /// depois de remover uma instalação anterior deste app (os backups dela voltam ao lugar).
        /// </summary>
        public static List<KeyValuePair<string, string>> FindConflicts(InstallPlan p, string exeDir, Manifest ours)
        {
            List<string> mine = new List<string>();
            mine.Add(p.Proxy);
            if (p.DxvkDlls != null) mine.AddRange(p.DxvkDlls);
            if (p.DgVoodoo) mine.Add("d3d9.dll");
            if (p.Sm86Proxy != null) mine.Add(p.Sm86Proxy);
            List<KeyValuePair<string, string>> l = new List<KeyValuePair<string, string>>();
            foreach (string n in GameScanner.ProxyNames)
            {
                if (mine.Exists(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase))) continue;
                string f = Path.Combine(exeDir, n);
                if (ours != null)
                {
                    string[] bak = ours.E.Find(e => e[0] == "BAK" && string.Equals(e[1], n, StringComparison.OrdinalIgnoreCase));
                    if (bak != null) f = ours.Abs(bak[2]);
                    else if (ours.IsAdded(f)) continue;
                }
                if (!File.Exists(f)) continue;
                string kind = GameScanner.ClassifyModule(f, null);
                if (kind == "OptiScaler" || (p.Method == Method.RenoDX && kind == "ReShade"))
                    l.Add(new KeyValuePair<string, string>(n, kind));
            }
            return l;
        }

        static void AddConflicts(InstallPlan p, GameInfo g)
        {
            foreach (KeyValuePair<string, string> c in FindConflicts(p, g.ExeDir, g.Ours))
                p.Steps.Insert(0, "Desativar " + c.Value + " antigo (" + c.Key + " → backup .dlss5bak) para não carregar duas vezes");
        }

        static void AddDlssUpdateStep(InstallPlan p, GameInfo g)
        {
            AddConflicts(p, g);
            if (p.UpdateGameDlss)
                p.Steps.Add("Atualizar DLLs DLSS do jogo (" + g.DlssDlls.Count + " arquivo(s)) para as versões da pasta Streamline, com backup");
            if (p.PlaceDlss)
                p.Steps.Add("nvngx_dlss.dll 310.9.x da pasta Streamline ao lado do exe (se o jogo não tiver)");
        }
    }
}
