using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dlss5Injector
{
    /// <summary>
    /// Traduções do app (pt = idioma-fonte, en, es). L.T("texto em português") devolve o texto no idioma atual.
    /// O idioma vem da configuração "lang" (auto/pt/en/es); em "auto" segue o idioma do Windows.
    /// </summary>
    public static class L
    {
        public static string Code = "pt";
        static readonly Dictionary<string, string[]> D = new Dictionary<string, string[]>(StringComparer.Ordinal);

        public static readonly string[] Codes = { "auto", "pt", "en", "es" };

        public static string SystemLang()
        {
            string t = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            return t == "pt" ? "pt" : t == "es" ? "es" : "en";
        }

        public static string Setting
        {
            get
            {
                string s = null;
                try { s = Settings.Get("lang"); } catch { }
                return s == "pt" || s == "en" || s == "es" ? s : "auto";
            }
        }

        public static void Init()
        {
            string s = Setting;
            Code = s == "auto" ? SystemLang() : s;
        }

        public static string NativeName(string code)
        {
            switch (code)
            {
                case "pt": return "Português (Brasil)";
                case "es": return "Español";
                default: return "English";
            }
        }

        public static string T(string pt)
        {
            if (Code == "pt" || pt == null) return pt;
            string[] v;
            if (!D.TryGetValue(pt, out v)) return pt;
            return Code == "es" ? v[1] : v[0];
        }

        static void A(string pt, string en, string es) { D[pt] = new[] { en, es }; }

        static L()
        {
            A("Copiando arquivos...", "Copying files...", "Copiando archivos...");
            A("Criando atalhos...", "Creating shortcuts...", "Creando accesos directos...");
            A("Registrando em Aplicativos instalados...", "Registering in Installed apps...", "Registrando en Aplicaciones instaladas...");
            A("Concluído", "Done", "Listo");
            A("O ", "", "");
            A(" está aberto. Fechar o programa para continuar?", " is open. Close the program to continue?", " está abierto. ¿Cerrar el programa para continuar?");
            A("  ·  versão ", "  ·  version ", "  ·  versión ");
            A("  ·  criado por ", "  ·  created by ", "  ·  creado por ");
            A("Instalador", "Installer", "Instalador");
            A("Atualizar instalação", "Update installation", "Actualizar instalación");
            A("Instalar no computador", "Install on this computer", "Instalar en el equipo");
            A("Pasta de instalação", "Installation folder", "Carpeta de instalación");
            A("Alterar", "Change", "Cambiar");
            A("Criar atalho na Área de Trabalho", "Create a Desktop shortcut", "Crear acceso directo en el Escritorio");
            A("Abrir o ", "Open ", "Abrir ");
            A(" ao terminar", " when finished", " al terminar");
            A("Não precisa de administrador · Windows 10/11 64-bit · atalho no Menu Iniciar · desinstalador incluso", "No administrator needed · Windows 10/11 64-bit · Start menu shortcut · uninstaller included", "No requiere administrador · Windows 10/11 64-bit · acceso en el menú Inicio · desinstalador incluido");
            A("  Atualizar", "  Update", "  Actualizar");
            A("  Instalar", "  Install", "  Instalar");
            A("Cancelar", "Cancel", "Cancelar");
            A("Escolha onde instalar (uma pasta \"", "Choose where to install (a \"", "Elija dónde instalar (se creará una carpeta \"");
            A("\" será criada dentro dela)", "\" folder will be created inside it)", "\" dentro de ella)");
            A("Erro: ", "Error: ", "Error: ");
            A("\r\n\r\nEscolha uma pasta sua (ex.: a padrão) ou execute como administrador.", "\r\n\r\nChoose a folder of your own (e.g. the default one) or run as administrator.", "\r\n\r\nElija una carpeta propia (p. ej., la predeterminada) o ejecute como administrador.");
            A("  Concluir", "  Finish", "  Finalizar");
            A("Atualizando", "Updating", "Actualizando");
            A("✔ Atualizado para a versão ", "✔ Updated to version ", "✔ Actualizado a la versión ");
            A(" — abrindo...", " — opening...", " — abriendo...");
            A("Não foi possível atualizar:\r\n", "Could not update:\r\n", "No se pudo actualizar:\r\n");
            A("Desinstalar", "Uninstall", "Desinstalar");
            A("Remover do computador", "Remove from this computer", "Quitar del equipo");
            A("Remove o programa, os atalhos e a entrada em Aplicativos instalados.\r\n", "Removes the program, the shortcuts and the entry in Installed apps.\r\n", "Quita el programa, los accesos directos y la entrada en Aplicaciones instaladas.\r\n");
            A("Os jogos em que você instalou o DLSS 5 não são alterados — para restaurá-los, use \"Remover\" no app antes.", "Games where you installed DLSS 5 are not changed — to restore them, use \"Remove\" in the app first.", "Los juegos donde instaló DLSS 5 no se modifican — para restaurarlos, use \"Quitar\" en la app antes.");
            A("Apagar também configurações, biblioteca, capas e downloads", "Also delete settings, library, covers and downloads", "Borrar también configuración, biblioteca, portadas y descargas");
            A("  Desinstalar", "  Uninstall", "  Desinstalar");
            A("  Fechar", "  Close", "  Cerrar");
            A("Desconhecida", "Unknown", "Desconocida");
            A("não-NVIDIA", "non-NVIDIA", "no NVIDIA");
            A("RTX ", "RTX ", "RTX ");
            A("NVIDIA (série não identificada)", "NVIDIA (series not identified)", "NVIDIA (serie no identificada)");
            A("ShortFuse cross-gen 310.8 (FP16)", "ShortFuse cross-gen 310.8 (FP16)", "ShortFuse cross-gen 310.8 (FP16)");
            A("NVIDIA oficial 310.8 · assinada", "Official NVIDIA 310.8 · signed", "NVIDIA oficial 310.8 · firmada");
            A("NVIDIA oficial ", "Official NVIDIA ", "NVIDIA oficial ");
            A(" · assinada", " · signed", " · firmada");
            A("Versão ", "Version ", "Versión ");
            A(" não reconhecida (sem assinatura NVIDIA válida)", " not recognized (no valid NVIDIA signature)", " no reconocida (sin firma NVIDIA válida)");
            A("GPU não-NVIDIA: o DLSS-NR não inicia sem NVIDIA (o fork tem um port 'vendor-neutral' opcional, não instalado por este app).", "Non-NVIDIA GPU: DLSS-NR does not start without NVIDIA (the fork has an optional 'vendor-neutral' port, not installed by this app).", "GPU no NVIDIA: DLSS-NR no se inicia sin NVIDIA (el fork tiene un port 'vendor-neutral' opcional, que esta app no instala).");
            A("Arquivo não reconhecido: confira se é mesmo o nvngx_dlssnr.dll da NVIDIA.", "Unrecognized file: make sure it really is NVIDIA's nvngx_dlssnr.dll.", "Archivo no reconocido: verifique que sea realmente el nvngx_dlssnr.dll de NVIDIA.");
            A("A runtime cross-gen é para RTX 20/30/40; na sua RTX 50 use a oficial NVIDIA.", "The cross-gen runtime is for RTX 20/30/40; on your RTX 50 use the official NVIDIA one.", "El runtime cross-gen es para RTX 20/30/40; en su RTX 50 use el oficial de NVIDIA.");
            A("Na RTX ", "On the RTX ", "En la RTX ");
            A(" o fork documenta só a cross-gen 310.8 do ShortFuse (SHA-256 E67DEE20...); a oficial pode não iniciar.", " the fork only documents ShortFuse's cross-gen 310.8 (SHA-256 E67DEE20...); the official one may not start.", " el fork solo documenta el cross-gen 310.8 de ShortFuse (SHA-256 E67DEE20...); el oficial puede no iniciarse.");
            A("  ! Sem acesso à internet/GitHub para ", "  ! No internet/GitHub access for ", "  ! Sin acceso a internet/GitHub para ");
            A("  Usando ", "  Using ", "  Usando ");
            A(" do cache: ", " from cache: ", " de la caché: ");
            A("Não foi possível obter ", "Could not get ", "No se pudo obtener ");
            A(" (sem internet e sem cache).", " (no internet and no cache).", " (sin internet y sin caché).");
            A(" (já em cache)", " (already cached)", " (ya en caché)");
            A("  Baixando ", "  Downloading ", "  Descargando ");
            A("SHA-256 do download não confere (", "Download SHA-256 does not match (", "El SHA-256 de la descarga no coincide (");
            A("). Download descartado.", "). Download discarded.", "). Descarga descartada.");
            A("  SHA-256 verificado: ", "  SHA-256 verified: ", "  SHA-256 verificado: ");
            A("Pacote do OptiScaler sem OptiScaler.dll: ", "OptiScaler package without OptiScaler.dll: ", "Paquete de OptiScaler sin OptiScaler.dll: ");
            A("Pacote RenoDX sem renodx-dlss5.addon64", "RenoDX package without renodx-dlss5.addon64", "Paquete de RenoDX sin renodx-dlss5.addon64");
            A("Pacote DXVK sem pasta x64", "DXVK package without x64 folder", "Paquete de DXVK sin carpeta x64");
            A("Pacote dgVoodoo2 sem MS\\x64\\D3D9.dll", "dgVoodoo2 package without MS\\x64\\D3D9.dll", "Paquete de dgVoodoo2 sin MS\\x64\\D3D9.dll");
            A("dlssg_for_sm86 (MFG para RTX 30/20)", "dlssg_for_sm86 (MFG for RTX 30/20)", "dlssg_for_sm86 (MFG para RTX 30/20)");
            A("Pacote dlssg_for_sm86 incompleto: ", "Incomplete dlssg_for_sm86 package: ", "Paquete dlssg_for_sm86 incompleto: ");
            A("O nvngx_dlssg.dll do pacote dlssg_for_sm86 não tem assinatura NVIDIA válida — instalação cancelada.", "The nvngx_dlssg.dll in the dlssg_for_sm86 package has no valid NVIDIA signature — installation cancelled.", "El nvngx_dlssg.dll del paquete dlssg_for_sm86 no tiene una firma NVIDIA válida — instalación cancelada.");
            A("Pacote do Streamline sem bin\\x64: ", "Streamline package without bin\\x64: ", "Paquete de Streamline sin bin\\x64: ");
            A("link do ReShade não encontrado em reshade.me", "ReShade link not found on reshade.me", "No se encontró el enlace de ReShade en reshade.me");
            A("ReShade64.dll não encontrado no instalador do ReShade.", "ReShade64.dll not found in the ReShade installer.", "No se encontró ReShade64.dll en el instalador de ReShade.");
            A("Genérica / desconhecida", "Generic / unknown", "Genérico / desconocido");
            A("RE Engine", "RE Engine", "RE Engine");
            A("Unreal Engine 5 (padrão DX12)", "Unreal Engine 5 (DX12 by default)", "Unreal Engine 5 (DX12 por defecto)");
            A("Unreal + Agility SDK/D3D12", "Unreal + Agility SDK/D3D12", "Unreal + Agility SDK/D3D12");
            A("Unreal Engine 4 (padrão DX11)", "Unreal Engine 4 (DX11 by default)", "Unreal Engine 4 (DX11 por defecto)");
            A("Jogos UE4 às vezes têm modo DX12 (-dx12). Se você usa DX12 no jogo, escolha DX12 manualmente.", "UE4 games sometimes have a DX12 mode (-dx12). If you play in DX12, choose DX12 manually.", "Los juegos UE4 a veces tienen modo DX12 (-dx12). Si usa DX12 en el juego, elija DX12 manualmente.");
            A("Unity (padrão no Windows: DX11)", "Unity (Windows default: DX11)", "Unity (predeterminado en Windows: DX11)");
            A("Sem Player.log para confirmar. Se o jogo roda em DX12 (opção do jogo ou -force-d3d12), escolha DX12 manualmente. Rodar o jogo uma vez melhora a detecção.", "No Player.log to confirm. If the game runs in DX12 (game option or -force-d3d12), choose DX12 manually. Running the game once improves detection.", "No hay Player.log para confirmar. Si el juego usa DX12 (opción del juego o -force-d3d12), elija DX12 manualmente. Ejecutar el juego una vez mejora la detección.");
            A("importa d3d12.dll", "imports d3d12.dll", "importa d3d12.dll");
            A("O executável também importa d3d11.dll (pode ter modo DX11).", "The executable also imports d3d11.dll (it may have a DX11 mode).", "El ejecutable también importa d3d11.dll (puede tener modo DX11).");
            A("Também importa vulkan-1.dll (pode ter modo Vulkan).", "Also imports vulkan-1.dll (it may have a Vulkan mode).", "También importa vulkan-1.dll (puede tener modo Vulkan).");
            A("O executável referencia d3d12.dll (pode ter modo DX12).", "The executable references d3d12.dll (it may have a DX12 mode).", "El ejecutable hace referencia a d3d12.dll (puede tener modo DX12).");
            A("referência a d3d12.dll no executável", "reference to d3d12.dll in the executable", "referencia a d3d12.dll en el ejecutable");
            A("referência a vulkan-1.dll", "reference to vulkan-1.dll", "referencia a vulkan-1.dll");
            A("referência a d3d11.dll", "reference to d3d11.dll", "referencia a d3d11.dll");
            A("referência a d3d10.dll", "reference to d3d10.dll", "referencia a d3d10.dll");
            A("referência a d3d9.dll", "reference to d3d9.dll", "referencia a d3d9.dll");
            A("não detectado", "not detected", "no detectado");
            A("log/config do Unreal (", "Unreal log/config (", "log/config de Unreal (");
            A("Player.log do Unity", "Unity Player.log", "Player.log de Unity");
            A("    backup: ", "    backup: ", "    copia: ");
            A("    desativado: ", "    disabled: ", "    desactivado: ");
            A("\r\n", "\r\n", "\r\n");
            A("Arquivo não encontrado na pasta Streamline: ", "File not found in the Streamline folder: ", "Archivo no encontrado en la carpeta de Streamline: ");
            A("Assinatura NVIDIA inválida em ", "Invalid NVIDIA signature in ", "Firma NVIDIA no válida en ");
            A(" — instalação cancelada.", " — installation cancelled.", " — instalación cancelada.");
            A("Nenhum método compatível selecionado.", "No compatible method selected.", "No hay ningún método compatible seleccionado.");
            A("Selecione o arquivo nvngx_dlssnr.dll (runtime do DLSS 5 / Neural Rendering).", "Select the nvngx_dlssnr.dll file (DLSS 5 / Neural Rendering runtime).", "Seleccione el archivo nvngx_dlssnr.dll (runtime de DLSS 5 / Neural Rendering).");
            A("Pasta do Streamline SDK não encontrada neste PC — baixando a versão oficial da NVIDIA.", "Streamline SDK folder not found on this PC — downloading NVIDIA's official version.", "No se encontró la carpeta del Streamline SDK en este PC — descargando la versión oficial de NVIDIA.");
            A("1/4 Obtendo componentes", "1/4 Getting components", "1/4 Obteniendo componentes");
            A("2/4 Verificando assinaturas NVIDIA da pasta Streamline", "2/4 Checking NVIDIA signatures in the Streamline folder", "2/4 Verificando las firmas NVIDIA de la carpeta Streamline");
            A("3/4 Removendo instalação anterior do DLSS 5 Definitive", "3/4 Removing the previous DLSS 5 Definitive installation", "3/4 Quitando la instalación anterior de DLSS 5 Definitive");
            A("3/4 Nenhuma instalação anterior", "3/4 No previous installation", "3/4 Sin instalación anterior");
            A("4/4 Copiando arquivos", "4/4 Copying files", "4/4 Copiando archivos");
            A("    dlssg_for_sm86 ", "    dlssg_for_sm86 ", "    dlssg_for_sm86 ");
            A(" como ", " as ", " como ");
            A(" (até ", " (up to ", " (hasta ");
            A("X, runtime DLSS-G ", "X, DLSS-G runtime ", "X, runtime DLSS-G ");
            A(" já está em ", " is already at ", " ya está en ");
            A(" (mantido)", " (kept)", " (se mantiene)");
            A("!! Erro durante a cópia — desfazendo alterações...", "!! Error while copying — undoing changes...", "!! Error al copiar — deshaciendo cambios...");
            A("== Concluído. ", "== Done. ", "== Listo. ");
            A(" arquivo(s) instalados, ", " file(s) installed, ", " archivo(s) instalados, ");
            A(" backup(s).", " backup(s).", " copia(s) de seguridad.");
            A("Nada para remover (sem ", "Nothing to remove (no ", "Nada que quitar (sin ");
            A("Removido: ", "Removed: ", "Quitado: ");
            A(" arquivo(s) apagados, ", " file(s) deleted, ", " archivo(s) borrados, ");
            A(" backup(s) restaurados.", " backup(s) restored.", " copia(s) de seguridad restauradas.");
            A("Epic Games", "Epic Games", "Epic Games");
            A("Ubisoft Connect", "Ubisoft Connect", "Ubisoft Connect");
            A("EA app", "EA app", "EA app");
            A("Escaneando ", "Scanning ", "Escaneando ");
            A("Escaneando disco ", "Scanning drive ", "Escaneando disco ");
            A("Biblioteca", "Library", "Biblioteca");
            A("Injetor DLSS 5", "DLSS 5 Injector", "Inyector DLSS 5");
            A("Frame Gen", "Frame Gen", "Frame Gen");
            A("✔  DLSS 5 Definitive atualizado para a versão ", "✔  DLSS 5 Definitive updated to version ", "✔  DLSS 5 Definitive actualizado a la versión ");
            A("  ·  atualizações", "  ·  updates", "  ·  actualizaciones");
            A("Novidades", "What's new", "Novedades");
            A("  Atualizar agora", "  Update now", "  Actualizar ahora");
            A("⬆  Nova versão ", "⬆  New version ", "⬆  Nueva versión ");
            A(" do DLSS 5 Definitive disponível  (você tem a ", " of DLSS 5 Definitive available  (you have ", " de DLSS 5 Definitive disponible  (usted tiene la ");
            A("Não foi possível verificar atualizações:\r\n", "Could not check for updates:\r\n", "No se pudieron buscar actualizaciones:\r\n");
            A("Você já está na versão mais recente (", "You are already on the latest version (", "Ya tiene la versión más reciente (");
            A("Espere terminar a instalação/análise em andamento e clique em Atualizar de novo.", "Wait for the installation/analysis in progress to finish and click Update again.", "Espere a que termine la instalación/análisis en curso y haga clic en Actualizar de nuevo.");
            A("Atualizar o DLSS 5 Definitive de ", "Update DLSS 5 Definitive from ", "¿Actualizar DLSS 5 Definitive de la ");
            A(" para ", " to ", " a la ");
            A("?\r\n\r\n", "?\r\n\r\n", "?\r\n\r\n");
            A("\r\n\r\n", "\r\n\r\n", "\r\n\r\n");
            A("O app baixa a versão nova, confere o SHA-256, fecha e abre de novo sozinho. Suas configurações e a biblioteca são mantidas.", "The app downloads the new version, checks the SHA-256, closes and reopens by itself. Your settings and library are kept.", "La app descarga la nueva versión, verifica el SHA-256, se cierra y se vuelve a abrir sola. Se conservan su configuración y la biblioteca.");
            A("  Baixando...", "  Downloading...", "  Descargando...");
            A("  Instalando...", "  Installing...", "  Instalando...");
            A("A atualização falhou:\r\n", "The update failed:\r\n", "La actualización falló:\r\n");
            A("\r\n\r\nVocê pode baixar a versão nova manualmente em \"Novidades\".", "\r\n\r\nYou can download the new version manually in \"What's new\".", "\r\n\r\nPuede descargar la nueva versión manualmente en \"Novedades\".");
            A("DLSS 5 Definitive", "DLSS 5 Definitive", "DLSS 5 Definitive");
            A("criado por", "created by", "creado por");
            A("Buscar jogo...", "Search game...", "Buscar juego...");
            A("  Escanear PC", "  Scan PC", "  Escanear PC");
            A("  Adicionar pasta", "  Add folder", "  Agregar carpeta");
            A("Nenhum jogo ainda.\r\nClique em \"Escanear PC\" ou \"Adicionar pasta\".", "No games yet.\r\nClick \"Scan PC\" or \"Add folder\".", "Aún no hay juegos.\r\nHaga clic en \"Escanear PC\" o \"Agregar carpeta\".");
            A("Configurar DLSS 5", "Set up DLSS 5", "Configurar DLSS 5");
            A("Abrir pasta do jogo", "Open game folder", "Abrir carpeta del juego");
            A("Buscar capa novamente", "Fetch cover again", "Buscar portada de nuevo");
            A("Remover da biblioteca", "Remove from library", "Quitar de la biblioteca");
            A(" jogo(s)", " game(s)", " juego(s)");
            A(" na busca", " in search", " en la búsqueda");
            A(" com DLSS 5", " with DLSS 5", " con DLSS 5");
            A("Escaneamento concluído · ", "Scan finished · ", "Escaneo terminado · ");
            A(" jogo(s) encontrados", " game(s) found", " juego(s) encontrados");
            A(" novo(s)", " new", " nuevo(s)");
            A("Escolha uma pasta com jogos (ou a pasta de um jogo) para escanear", "Choose a folder with games (or a game's folder) to scan", "Elija una carpeta con juegos (o la carpeta de un juego) para escanear");
            A("Jogo", "Game", "Juego");
            A("Executável", "Executable", "Ejecutable");
            A("Análise do jogo", "Game analysis", "Análisis del juego");
            A("API gráfica", "Graphics API", "API gráfica");
            A("Engine", "Engine", "Motor");
            A("Arquitetura", "Architecture", "Arquitectura");
            A("Upscalers do jogo", "Game upscalers", "Upscalers del juego");
            A("Mods na pasta", "Mods in folder", "Mods en la carpeta");
            A("Atividade", "Activity", "Actividad");
            A("Configuração", "Configuration", "Configuración");
            A("Método (escolhido conforme a compatibilidade do jogo)", "Method (chosen according to the game's compatibility)", "Método (elegido según la compatibilidad del juego)");
            A("Detectar automaticamente", "Detect automatically", "Detectar automáticamente");
            A("Passes do Neural Rendering", "Neural Rendering passes", "Pasadas de Neural Rendering");
            A("1 passe", "1 pass", "1 pasada");
            A("2 passes", "2 passes", "2 pasadas");
            A("3 passes", "3 passes", "3 pasadas");
            A("  Frame Gen", "  Frame Gen", "  Frame Gen");
            A("NR antes do upscaler", "NR before the upscaler", "NR antes del upscaler");
            A("Atualizar DLSS do jogo (310.9)", "Update game DLSS (310.9)", "Actualizar DLSS del juego (310.9)");
            A("Log de diagnóstico", "Diagnostic log", "Log de diagnóstico");
            A("NVIDIA Streamline SDK", "NVIDIA Streamline SDK", "NVIDIA Streamline SDK");
            A("Runtime DLSS 5 (nvngx_dlssnr.dll)", "DLSS 5 runtime (nvngx_dlssnr.dll)", "Runtime DLSS 5 (nvngx_dlssnr.dll)");
            A("Plano de instalação", "Installation plan", "Plan de instalación");
            A("  Instalar DLSS 5", "  Install DLSS 5", "  Instalar DLSS 5");
            A("  Remover", "  Remove", "  Quitar");
            A("  Abrir pasta", "  Open folder", "  Abrir carpeta");
            A("Multi Frame Generation por IA  ·  NVIDIA DLSS 4", "AI Multi Frame Generation  ·  NVIDIA DLSS 4", "Multi Frame Generation por IA  ·  NVIDIA DLSS 4");
            A("O modelo de IA do DLSS Frame Generation da NVIDIA gera até 5 quadros novos para cada quadro real — também em ", "NVIDIA's DLSS Frame Generation AI model generates up to 5 new frames for every real frame — also in ", "El modelo de IA de DLSS Frame Generation de NVIDIA genera hasta 5 fotogramas nuevos por cada fotograma real — también en ");
            A("jogos que não têm Frame Generation. O OptiScaler F5 entrega ao modelo os vetores de movimento do jogo ou, nos jogos ", "games without Frame Generation. OptiScaler F5 feeds the model the game's motion vectors or, in games ", "juegos sin Frame Generation. OptiScaler F5 entrega al modelo los vectores de movimiento del juego o, en los juegos ");
            A("sem suporte, a profundidade e o movimento que ele mesmo calcula. Funciona nas RTX 50, 40, 30 e 20.", "without support, the depth and motion it computes itself. Works on RTX 50, 40, 30 and 20.", "sin soporte, la profundidad y el movimiento que calcula por sí mismo. Funciona en RTX 50, 40, 30 y 20.");
            A("Multiplicador de quadros  (1X = desligado)", "Frame multiplier  (1X = off)", "Multiplicador de fotogramas  (1X = desactivado)");
            A("MFG dinâmico: varia de 2X a 6X para acompanhar o Hz do monitor (RTX 50)", "Dynamic MFG: varies from 2X to 6X to match the monitor's Hz (RTX 50)", "MFG dinámico: varía de 2X a 6X para seguir los Hz del monitor (RTX 50)");
            A("  Aplicar no jogo", "  Apply to game", "  Aplicar al juego");
            A("Sua GPU", "Your GPU", "Su GPU");
            A("Modelo de IA", "AI model", "Modelo de IA");
            A("Multiplicador máximo", "Maximum multiplier", "Multiplicador máximo");
            A("Jogos com DLSS / FSR / XeSS", "Games with DLSS / FSR / XeSS", "Juegos con DLSS / FSR / XeSS");
            A("Jogos sem Frame Generation", "Games without Frame Generation", "Juegos sin Frame Generation");
            A("Componente extra", "Extra component", "Componente extra");
            A("Latência", "Latency", "Latencia");
            A("Dica: comece em 2X ou 3X. Para menos artefatos, tenha pelo menos 50–60 FPS reais antes de gerar quadros. ", "Tip: start at 2X or 3X. For fewer artifacts, have at least 50–60 real FPS before generating frames. ", "Consejo: empiece en 2X o 3X. Para menos artefactos, tenga al menos 50–60 FPS reales antes de generar fotogramas. ");
            A("Desligue o V-Sync do jogo; aperte Insert no jogo para ajustar em tempo real.", "Turn off the game's V-Sync; press Insert in the game to adjust in real time.", "Desactive el V-Sync del juego; presione Insert en el juego para ajustar en tiempo real.");
            A("Suporte por geração de GPU", "Support by GPU generation", "Soporte por generación de GPU");
            A("GeForce RTX 50", "GeForce RTX 50", "GeForce RTX 50");
            A("GeForce RTX 40", "GeForce RTX 40", "GeForce RTX 40");
            A("GeForce RTX 30 / RTX 20", "GeForce RTX 30 / RTX 20", "GeForce RTX 30 / RTX 20");
            A("RTX 50 — DLSS 4 Multi Frame Generation nativo, 2X a 6X, ou modo dinâmico (acompanha o Hz do monitor).\r\n", "RTX 50 — native DLSS 4 Multi Frame Generation, 2X to 6X, or dynamic mode (follows the monitor's Hz).\r\n", "RTX 50 — DLSS 4 Multi Frame Generation nativo, de 2X a 6X, o modo dinámico (sigue los Hz del monitor).\r\n");
            A("RTX 40 — DLSS Frame Generation 2X nativo; 3X e 4X pelo desbloqueio de MFG embutido no OptiScaler F5 (experimental, sem arquivo extra).\r\n", "RTX 40 — native 2X DLSS Frame Generation; 3X and 4X through the MFG unlock built into OptiScaler F5 (experimental, no extra file).\r\n", "RTX 40 — DLSS Frame Generation 2X nativo; 3X y 4X con el desbloqueo de MFG integrado en OptiScaler F5 (experimental, sin archivo extra).\r\n");
            A("RTX 30 / 20 — DLSS Multi Frame Generation até 4X pelo dlssg_for_sm86 (sdli1995, GPLv3), baixado automaticamente do GitHub na instalação. ", "RTX 30 / 20 — DLSS Multi Frame Generation up to 4X via dlssg_for_sm86 (sdli1995, GPLv3), downloaded automatically from GitHub during installation. ", "RTX 30 / 20 — DLSS Multi Frame Generation hasta 4X con dlssg_for_sm86 (sdli1995, GPLv3), descargado automáticamente de GitHub durante la instalación. ");
            A("Ele usa o runtime oficial da NVIDIA (nvngx_dlssg 310.9.16, assinatura conferida). Requer DX12 (ou DX9 via dgVoodoo); experimental.\r\n", "It uses NVIDIA's official runtime (nvngx_dlssg 310.9.16, signature checked). Requires DX12 (or DX9 via dgVoodoo); experimental.\r\n", "Usa el runtime oficial de NVIDIA (nvngx_dlssg 310.9.16, firma verificada). Requiere DX12 (o DX9 con dgVoodoo); experimental.\r\n");
            A("Jogos sem Frame Generation próprio recebem o MFG pelo OptiScaler; jogos que já têm recebem o multiplicador forçado no FG do próprio jogo.", "Games without their own Frame Generation get MFG through OptiScaler; games that have it get the multiplier forced into their own FG.", "Los juegos sin Frame Generation propio reciben el MFG con OptiScaler; los que ya lo tienen reciben el multiplicador forzado en su propio FG.");
            A("Dinâmico: 2X a 6X conforme o Hz do monitor", "Dynamic: 2X to 6X following the monitor's Hz", "Dinámico: de 2X a 6X según los Hz del monitor");
            A("Desligado — só quadros reais", "Off — real frames only", "Desactivado — solo fotogramas reales");
            A("60 FPS reais  →  até ", "60 real FPS  →  up to ", "60 FPS reales  →  hasta ");
            A(" FPS na tela", " FPS on screen", " FPS en pantalla");
            A("   (limitado a ", "   (limited to ", "   (limitado a ");
            A("X)", "X)", "X)");
            A("  (3X–4X: desbloqueio experimental)", "  (3X–4X: experimental unlock)", "  (3X–4X: desbloqueo experimental)");
            A("  (experimental)", "  (experimental)", "  (experimental)");
            A("Usa os vetores de movimento do jogo", "Uses the game's motion vectors", "Usa los vectores de movimiento del juego");
            A("Liberado pelo OptiScaler F5", "Enabled by OptiScaler F5", "Habilitado por OptiScaler F5");
            A("Nenhum — suporte nativo", "None — native support", "Ninguno — soporte nativo");
            A("Desbloqueio MFG embutido no OptiScaler F5", "MFG unlock built into OptiScaler F5", "Desbloqueo de MFG integrado en OptiScaler F5");
            A("dlssg_for_sm86 já baixado", "dlssg_for_sm86 already downloaded", "dlssg_for_sm86 ya descargado");
            A("dlssg_for_sm86 — baixado na instalação", "dlssg_for_sm86 — downloaded during installation", "dlssg_for_sm86 — se descarga durante la instalación");
            A("DLSS-G exige NVIDIA RTX — usando XeSS-FG", "DLSS-G requires NVIDIA RTX — using XeSS-FG", "DLSS-G requiere NVIDIA RTX — usando XeSS-FG");
            A("NVIDIA Reflex reduz a latência extra", "NVIDIA Reflex reduces the extra latency", "NVIDIA Reflex reduce la latencia extra");
            A("XeLL / Anti-Lag reduzem a latência extra", "XeLL / Anti-Lag reduce the extra latency", "XeLL / Anti-Lag reducen la latencia extra");
            A("Até 6X · dinâmico", "Up to 6X · dynamic", "Hasta 6X · dinámico");
            A(" · sua GPU", " · your GPU", " · su GPU");
            A("Até 4X (desbloqueio)", "Up to 4X (unlock)", "Hasta 4X (desbloqueo)");
            A("Até 4X (dlssg_for_sm86)", "Up to 4X (dlssg_for_sm86)", "Hasta 4X (dlssg_for_sm86)");
            A("Nenhum jogo selecionado. Escolha um na Biblioteca para aplicar.", "No game selected. Choose one in the Library to apply.", "Ningún juego seleccionado. Elija uno en la Biblioteca para aplicar.");
            A("Jogo: ", "Game: ", "Juego: ");
            A("\r\n\"Aplicar\" reinstala com o novo multiplicador.", "\r\n\"Apply\" reinstalls with the new multiplier.", "\r\n\"Aplicar\" reinstala con el nuevo multiplicador.");
            A("\r\n\"Aplicar\" instala o DLSS 5 com este Frame Gen.", "\r\n\"Apply\" installs DLSS 5 with this Frame Gen.", "\r\n\"Aplicar\" instala DLSS 5 con este Frame Gen.");
            A("  Ir para a Biblioteca", "  Go to Library", "  Ir a la Biblioteca");
            A("  Frame Gen: ", "  Frame Gen: ", "  Frame Gen: ");
            A("dinâmico", "dynamic", "dinámico");
            A("ERRO: ", "ERROR: ", "ERROR: ");
            A("Será baixado da NVIDIA (GitHub oficial) na instalação", "Will be downloaded from NVIDIA (official GitHub) during installation", "Se descargará de NVIDIA (GitHub oficial) durante la instalación");
            A("Pasta do NVIDIA Streamline SDK (contém bin\\x64)", "NVIDIA Streamline SDK folder (contains bin\\x64)", "Carpeta del NVIDIA Streamline SDK (contiene bin\\x64)");
            A("Essa pasta não tem bin\\x64\\nvngx_dlss.dll e sl.interposer.dll.", "This folder does not have bin\\x64\\nvngx_dlss.dll and sl.interposer.dll.", "Esta carpeta no tiene bin\\x64\\nvngx_dlss.dll y sl.interposer.dll.");
            A("Selecione a runtime do DLSS 5 (nvngx_dlssnr.dll)", "Select the DLSS 5 runtime (nvngx_dlssnr.dll)", "Seleccione el runtime de DLSS 5 (nvngx_dlssnr.dll)");
            A("Não encontrado — clique em Alterar e selecione o arquivo", "Not found — click Change and select the file", "No encontrado — haga clic en Cambiar y seleccione el archivo");
            A("Verificando assinatura e SHA-256...", "Checking signature and SHA-256...", "Verificando firma y SHA-256...");
            A("Não foi possível ler o arquivo", "Could not read the file", "No se pudo leer el archivo");
            A("  ·  compatível com sua GPU", "  ·  compatible with your GPU", "  ·  compatible con su GPU");
            A("  ·  veja o aviso no plano", "  ·  see the warning in the plan", "  ·  vea el aviso en el plan");
            A("Selecione a pasta de instalação do jogo", "Select the game's installation folder", "Seleccione la carpeta de instalación del juego");
            A("Procurando executáveis...", "Looking for executables...", "Buscando ejecutables...");
            A("Nenhum .exe encontrado nessa pasta.", "No .exe found in this folder.", "No se encontró ningún .exe en esta carpeta.");
            A("Analisando ", "Analyzing ", "Analizando ");
            A("Analisando o jogo...", "Analyzing the game...", "Analizando el juego...");
            A("DLSS 5 Definitive já está instalado neste jogo (", "DLSS 5 Definitive is already installed in this game (", "DLSS 5 Definitive ya está instalado en este juego (");
            A("Análise concluída.", "Analysis finished.", "Análisis terminado.");
            A("Inválido", "Invalid", "No válido");
            A("32-bit (não suportado)", "32-bit (not supported)", "32-bit (no compatible)");
            A("Nenhum · Optical F5Low", "None · Optical F5Low", "Ninguno · Optical F5Low");
            A("Nenhum detectado", "None detected", "Ninguno detectado");
            A("Nenhum", "None", "Ninguno");
            A("Detecção: ", "Detection: ", "Detección: ");
            A("Já instalado — instalar de novo substitui; Remover restaura o original.", "Already installed — installing again replaces it; Remove restores the original.", "Ya instalado — instalar de nuevo lo reemplaza; Quitar restaura el original.");
            A("★ Recomendado: ", "★ Recommended: ", "★ Recomendado: ");
            A("Alternativa: ", "Alternative: ", "Alternativa: ");
            A("Escolha um jogo na Biblioteca ou arraste a pasta dele para cá.", "Choose a game in the Library or drag its folder here.", "Elija un juego en la Biblioteca o arrastre su carpeta aquí.");
            A("Nenhum método compatível. ", "No compatible method. ", "Ningún método compatible. ");
            A("Falta a runtime do DLSS 5 (nvngx_dlssnr.dll): use Alterar na seção 3.", "DLSS 5 runtime (nvngx_dlssnr.dll) is missing: use Change in section 3.", "Falta el runtime de DLSS 5 (nvngx_dlssnr.dll): use Cambiar en la sección 3.");
            A("Falta a runtime do DLSS 5: nvngx_dlssnr.dll.\r\n\r\nEla não vem no OptiScaler, no RenoDX nem no Streamline SDK. ", "The DLSS 5 runtime is missing: nvngx_dlssnr.dll.\r\n\r\nIt does not come with OptiScaler, RenoDX or the Streamline SDK. ", "Falta el runtime de DLSS 5: nvngx_dlssnr.dll.\r\n\r\nNo viene con OptiScaler, RenoDX ni el Streamline SDK. ");
            A("Use o botão Alterar ao lado de \"Runtime DLSS 5\".", "Use the Change button next to \"DLSS 5 runtime\".", "Use el botón Cambiar junto a \"Runtime DLSS 5\".");
            A("Sem permissão para gravar em:\r\n", "No permission to write to:\r\n", "Sin permiso para escribir en:\r\n");
            A("\r\n\r\nReiniciar o programa como administrador?", "\r\n\r\nRestart the program as administrator?", "\r\n\r\n¿Reiniciar el programa como administrador?");
            A("Instalar ", "Install ", "Instalar ");
            A(" em:\r\n", " in:\r\n", " en:\r\n");
            A("\r\n\r\nArquivos existentes serão guardados como .dlss5bak e podem ser restaurados com \"Remover\".", "\r\n\r\nExisting files will be kept as .dlss5bak and can be restored with \"Remove\".", "\r\n\r\nLos archivos existentes se guardarán como .dlss5bak y se pueden restaurar con \"Quitar\".");
            A("\r\n\r\n⚠ ANTI-CHEAT DETECTADO (", "\r\n\r\n⚠ ANTI-CHEAT DETECTED (", "\r\n\r\n⚠ ANTI-CHEAT DETECTADO (");
            A("): use apenas offline. Em modo online isso pode resultar em banimento.", "): use offline only. Online, this may result in a ban.", "): úselo solo sin conexión. En línea esto puede causar un baneo.");
            A("✔ DLSS 5 instalado em ", "✔ DLSS 5 installed in ", "✔ DLSS 5 instalado en ");
            A("DLSS 5 instalado!\r\n\r\n", "DLSS 5 installed!\r\n\r\n", "¡DLSS 5 instalado!\r\n\r\n");
            A("Abra o jogo e ative o DLSS nas opções gráficas. A tecla Home abre o ReShade/RenoDX.", "Open the game and enable DLSS in the graphics options. The Home key opens ReShade/RenoDX.", "Abra el juego y active DLSS en las opciones gráficas. La tecla Inicio abre ReShade/RenoDX.");
            A("Abra o jogo e aperte Insert para abrir o menu do OptiScaler → Neural Rendering (comece com 1 passe).", "Open the game and press Insert to open the OptiScaler menu → Neural Rendering (start with 1 pass).", "Abra el juego y presione Insert para abrir el menú de OptiScaler → Neural Rendering (empiece con 1 pasada).");
            A("Não há instalação do DLSS 5 Definitive nesta pasta.", "There is no DLSS 5 Definitive installation in this folder.", "No hay ninguna instalación de DLSS 5 Definitive en esta carpeta.");
            A("Remover tudo que o DLSS 5 Definitive instalou e restaurar os arquivos originais?", "Remove everything DLSS 5 Definitive installed and restore the original files?", "¿Quitar todo lo que instaló DLSS 5 Definitive y restaurar los archivos originales?");
            A("✔ Arquivos originais restaurados.", "✔ Original files restored.", "✔ Archivos originales restaurados.");
            A("OptiScaler F5 (DLSS-NR Multipass)", "OptiScaler F5 (DLSS-NR Multipass)", "OptiScaler F5 (DLSS-NR Multipass)");
            A("RenoDX DLSS 5 (ReShade add-on)", "RenoDX DLSS 5 (ReShade add-on)", "RenoDX DLSS 5 (add-on de ReShade)");
            A("DXVK (→Vulkan) + OptiScaler F5", "DXVK (→Vulkan) + OptiScaler F5", "DXVK (→Vulkan) + OptiScaler F5");
            A("dgVoodoo2 (DX9→DX12) + OptiScaler F5", "dgVoodoo2 (DX9→DX12) + OptiScaler F5", "dgVoodoo2 (DX9→DX12) + OptiScaler F5");
            A("DirectX 9", "DirectX 9", "DirectX 9");
            A("DirectX 10", "DirectX 10", "DirectX 10");
            A("DirectX 11", "DirectX 11", "DirectX 11");
            A("DirectX 12", "DirectX 12", "DirectX 12");
            A("Jogo 32-bit: o OptiScaler, o DLSS-NR e o add-on RenoDX DLSS 5 só existem em 64-bit.", "32-bit game: OptiScaler, DLSS-NR and the RenoDX DLSS 5 add-on only exist in 64-bit.", "Juego de 32 bits: OptiScaler, DLSS-NR y el add-on RenoDX DLSS 5 solo existen en 64 bits.");
            A("DX12 nativo com upscaler: caminho mais completo do fork (NR antes/depois do SR, multipass, FG).", "Native DX12 with upscaler: the fork's most complete path (NR before/after SR, multipass, FG).", "DX12 nativo con upscaler: la ruta más completa del fork (NR antes/después del SR, multipass, FG).");
            A("DX12 sem upscaler: o fork usa o 'Optical F5Low' (profundidade e movimento próprios) para aplicar o NR na imagem final.", "DX12 without upscaler: the fork uses 'Optical F5Low' (its own depth and motion) to apply NR to the final image.", "DX12 sin upscaler: el fork usa 'Optical F5Low' (profundidad y movimiento propios) para aplicar NR a la imagen final.");
            A("DX11 com DLSS nativo: o add-on RenoDX DLSS 5 engancha o DLSS do próprio jogo direto no DX11.", "DX11 with native DLSS: the RenoDX DLSS 5 add-on hooks the game's own DLSS directly in DX11.", "DX11 con DLSS nativo: el add-on RenoDX DLSS 5 engancha el DLSS del propio juego directamente en DX11.");
            A("DX11 com FSR/XeSS: o OptiScaler troca o upscaler por DLSS via ponte DX11→DX12 (dlss_12), a única que roda o NR.", "DX11 with FSR/XeSS: OptiScaler replaces the upscaler with DLSS through the DX11→DX12 bridge (dlss_12), the only one that runs NR.", "DX11 con FSR/XeSS: OptiScaler cambia el upscaler por DLSS con el puente DX11→DX12 (dlss_12), el único que ejecuta NR.");
            A("DX11 sem upscaler: o NR na imagem final em DX11 é feito pelo fork através do DXVK (NativeDxvkVulkan).", "DX11 without upscaler: NR on the final image in DX11 is done by the fork through DXVK (NativeDxvkVulkan).", "DX11 sin upscaler: el NR en la imagen final en DX11 lo hace el fork a través de DXVK (NativeDxvkVulkan).");
            A("Vulkan: só o OptiScaler funciona (o ReShade/RenoDX DLSS 5 não carrega em Vulkan).", "Vulkan: only OptiScaler works (ReShade/RenoDX DLSS 5 does not load in Vulkan).", "Vulkan: solo funciona OptiScaler (ReShade/RenoDX DLSS 5 no carga en Vulkan).");
            A("DX10: convertido para Vulkan pelo DXVK; o OptiScaler aplica o NR na imagem final (experimental).", "DX10: converted to Vulkan by DXVK; OptiScaler applies NR to the final image (experimental).", "DX10: convertido a Vulkan por DXVK; OptiScaler aplica NR a la imagen final (experimental).");
            A("DX9: convertido para DX12 pelo dgVoodoo2; o OptiScaler aplica o NR na imagem final (experimental).", "DX9: converted to DX12 by dgVoodoo2; OptiScaler applies NR to the final image (experimental).", "DX9: convertido a DX12 por dgVoodoo2; OptiScaler aplica NR a la imagen final (experimental).");
            A("OpenGL não é suportado pelo OptiScaler nem pelo RenoDX.", "OpenGL is not supported by OptiScaler or RenoDX.", "OpenGL no es compatible con OptiScaler ni con RenoDX.");
            A("API não detectada: escolha manualmente a API gráfica.", "API not detected: choose the graphics API manually.", "API no detectada: elija la API gráfica manualmente.");
            A("Anti-cheat detectado (", "Anti-cheat detected (", "Anti-cheat detectado (");
            A("). Injetar DLLs pode causar BANIMENTO em modo online. Use só offline/single-player.", "). Injecting DLLs may cause a BAN in online mode. Use offline/single-player only.", "). Inyectar DLLs puede causar un BANEO en modo en línea. Úselo solo sin conexión/un jugador.");
            A("GPU sem núcleos Tensor (pré-RTX): o DLSS não roda.", "GPU without Tensor cores (pre-RTX): DLSS does not run.", "GPU sin núcleos Tensor (pre-RTX): DLSS no funciona.");
            A("ReShade (com suporte a add-ons) como dxgi.dll", "ReShade (with add-on support) as dxgi.dll", "ReShade (con soporte de add-ons) como dxgi.dll");
            A("Add-on RenoDX DLSS 5 (renodx-dlss5.addon64) — engancha o DLSS do próprio jogo", "RenoDX DLSS 5 add-on (renodx-dlss5.addon64) — hooks the game's own DLSS", "Add-on RenoDX DLSS 5 (renodx-dlss5.addon64) — engancha el DLSS del propio juego");
            A("Runtime DLSS-NR (nvngx_dlssnr.dll)", "DLSS-NR runtime (nvngx_dlssnr.dll)", "Runtime DLSS-NR (nvngx_dlssnr.dll)");
            A("Já existe um dxgi.dll (", "A dxgi.dll already exists (", "Ya existe un dxgi.dll (");
            A("). Ele será guardado como backup.", "). It will be kept as a backup.", "). Se guardará como copia de seguridad.");
            A("No método RenoDX, use o Frame Generation do próprio jogo (o add-on não gera quadros).", "With the RenoDX method, use the game's own Frame Generation (the add-on does not generate frames).", "Con el método RenoDX, use el Frame Generation del propio juego (el add-on no genera fotogramas).");
            A("No jogo: ative o DLSS nas opções gráficas; o menu do ReShade (tecla Home) mostra o add-on.", "In the game: enable DLSS in the graphics options; the ReShade menu (Home key) shows the add-on.", "En el juego: active DLSS en las opciones gráficas; el menú de ReShade (tecla Inicio) muestra el add-on.");
            A("ReShade existente renomeado para ReShade64.dll e carregado pelo OptiScaler (LoadReshade=true)", "Existing ReShade renamed to ReShade64.dll and loaded by OptiScaler (LoadReshade=true)", "ReShade existente renombrado a ReShade64.dll y cargado por OptiScaler (LoadReshade=true)");
            A("DXVK (", "DXVK (", "DXVK (");
            A(" → Vulkan", " → Vulkan", " → Vulkan");
            A("Caminho experimental: o NR em jogos ", "Experimental path: NR in ", "Ruta experimental: el NR en juegos ");
            A(" via DXVK é suportado pelo fork, mas varia por jogo.", " games via DXVK is supported by the fork, but varies by game.", " con DXVK es compatible con el fork, pero varía según el juego.");
            A("dgVoodoo2 (D3D9.dll x64, saída d3d12_fl12_0, sem marca d'água) — DX9 → DX12", "dgVoodoo2 (D3D9.dll x64, d3d12_fl12_0 output, no watermark) — DX9 → DX12", "dgVoodoo2 (D3D9.dll x64, salida d3d12_fl12_0, sin marca de agua) — DX9 → DX12");
            A("Caminho experimental: DX9 → DX12 via dgVoodoo2. Se não funcionar, troque o método para DXVK.", "Experimental path: DX9 → DX12 via dgVoodoo2. If it doesn't work, switch the method to DXVK.", "Ruta experimental: DX9 → DX12 con dgVoodoo2. Si no funciona, cambie el método a DXVK.");
            A("OptiScaler F5 como ", "OptiScaler F5 as ", "OptiScaler F5 como ");
            A(" + OptiScaler.ini + pasta OptiScaler\\ + forwarder nvngx.dll_dlssnr.dll", " + OptiScaler.ini + OptiScaler\\ folder + nvngx.dll_dlssnr.dll forwarder", " + OptiScaler.ini + carpeta OptiScaler\\ + forwarder nvngx.dll_dlssnr.dll");
            A(": o NR roda num caminho FP16 mais pesado; resolução do modelo reduzida para 0.75.", ": NR runs on a heavier FP16 path; model resolution reduced to 0.75.", ": el NR usa una ruta FP16 más pesada; resolución del modelo reducida a 0.75.");
            A("O DLSS Frame Generation do jogo precisa de uma GPU NVIDIA RTX.", "The game's DLSS Frame Generation needs an NVIDIA RTX GPU.", "El DLSS Frame Generation del juego necesita una GPU NVIDIA RTX.");
            A("MFG por IA do próprio jogo (DLSS-G) em ", "Game's own AI MFG (DLSS-G) at ", "MFG por IA del propio juego (DLSS-G) en ");
            A("modo dinâmico", "dynamic mode", "modo dinámico");
            A(" — ative o Frame Generation no menu do jogo", " — enable Frame Generation in the game's menu", " — active Frame Generation en el menú del juego");
            A("O jogo tem Ray Reconstruction: com RR ativo o NR sempre roda depois do SR.", "The game has Ray Reconstruction: with RR active, NR always runs after SR.", "El juego tiene Ray Reconstruction: con RR activo, el NR siempre se ejecuta después del SR.");
            A("No jogo: selecione DLSS (ou FSR/XeSS) nas opções; aperte Insert para abrir o menu do OptiScaler.", "In the game: select DLSS (or FSR/XeSS) in the options; press Insert to open the OptiScaler menu.", "En el juego: seleccione DLSS (o FSR/XeSS) en las opciones; presione Insert para abrir el menú de OptiScaler.");
            A("Optical F5Low: NR + Frame Generation (profundidade/movimento estimados pelo fork)", "Optical F5Low: NR + Frame Generation (depth/motion estimated by the fork)", "Optical F5Low: NR + Frame Generation (profundidad/movimiento estimados por el fork)");
            A("Optical F5Low: NR na imagem final (jogo sem upscaler)", "Optical F5Low: NR on the final image (game without upscaler)", "Optical F5Low: NR en la imagen final (juego sin upscaler)");
            A("DX11 sem upscaler direto (sem DXVK) pode não ativar o NR. Se não funcionar, use o método DXVK.", "DX11 without upscaler directly (without DXVK) may not enable NR. If it doesn't work, use the DXVK method.", "DX11 sin upscaler directo (sin DXVK) puede no activar el NR. Si no funciona, use el método DXVK.");
            A("No jogo: aperte Insert → página Optical F5Low / Neural Rendering para ajustar.", "In the game: press Insert → Optical F5Low / Neural Rendering page to adjust.", "En el juego: presione Insert → página Optical F5Low / Neural Rendering para ajustar.");
            A("Streamline ", "Streamline ", "Streamline ");
            A("(sl.interposer/common/dlss_g/reflex/pcl + nvngx_dlssg 310.9) em OptiScaler\\streamline", "(sl.interposer/common/dlss_g/reflex/pcl + nvngx_dlssg 310.9) in OptiScaler\\streamline", "(sl.interposer/common/dlss_g/reflex/pcl + nvngx_dlssg 310.9) en OptiScaler\\streamline");
            A("O jogo tem Streamline próprio; os arquivos do jogo NÃO são tocados (o FG do OptiScaler usa a subpasta dele).", "The game has its own Streamline; the game's files are NOT touched (OptiScaler's FG uses its own subfolder).", "El juego tiene su propio Streamline; los archivos del juego NO se tocan (el FG de OptiScaler usa su propia subcarpeta).");
            A("Multi Frame Generation por IA: ", "AI Multi Frame Generation: ", "Multi Frame Generation por IA: ");
            A(" em ", " at ", " en ");
            A("modo dinâmico (até 6X, alvo = Hz do monitor)", "dynamic mode (up to 6X, target = monitor Hz)", "modo dinámico (hasta 6X, objetivo = Hz del monitor)");
            A(" usando os vetores de movimento do jogo", " using the game's motion vectors", " usando los vectores de movimiento del juego");
            A(" — jogo sem suporte: o OptiScaler gera profundidade e movimento (Optical F5Low)", " — unsupported game: OptiScaler generates depth and motion (Optical F5Low)", " — juego sin soporte: OptiScaler genera profundidad y movimiento (Optical F5Low)");
            A("Pedido ", "Requested ", "Se pidió ");
            A("X, mas ", "X, but ", "X, pero ");
            A(" nesta GPU/API vai até ", " on this GPU/API goes up to ", " en esta GPU/API llega hasta ");
            A("X — usando ", "X — using ", "X — usando ");
            A("NVIDIA DLSS 4 Multi Frame Generation", "NVIDIA DLSS 4 Multi Frame Generation", "NVIDIA DLSS 4 Multi Frame Generation");
            A("NVIDIA DLSS Multi Frame Generation (desbloqueio RTX 40)", "NVIDIA DLSS Multi Frame Generation (RTX 40 unlock)", "NVIDIA DLSS Multi Frame Generation (desbloqueo RTX 40)");
            A("NVIDIA DLSS Multi Frame Generation (dlssg_for_sm86)", "NVIDIA DLSS Multi Frame Generation (dlssg_for_sm86)", "NVIDIA DLSS Multi Frame Generation (dlssg_for_sm86)");
            A("Intel XeSS Frame Generation (IA)", "Intel XeSS Frame Generation (AI)", "Intel XeSS Frame Generation (IA)");
            A("AMD FSR Frame Generation", "AMD FSR Frame Generation", "AMD FSR Frame Generation");
            A("RTX 40 acima de 2X usa o desbloqueio de MFG embutido no OptiScaler F5 (experimental). Se travar, volte para 2X.", "RTX 40 above 2X uses the MFG unlock built into OptiScaler F5 (experimental). If it freezes, go back to 2X.", "RTX 40 por encima de 2X usa el desbloqueo de MFG integrado en OptiScaler F5 (experimental). Si se congela, vuelva a 2X.");
            A("dlssg_for_sm86 (sdli1995) como ", "dlssg_for_sm86 (sdli1995) as ", "dlssg_for_sm86 (sdli1995) como ");
            A(": libera o DLSS-G da NVIDIA na RTX ", ": unlocks NVIDIA's DLSS-G on the RTX ", ": habilita el DLSS-G de NVIDIA en la RTX ");
            A(" com o runtime oficial 310.9.16 (até ", " with the official 310.9.16 runtime (up to ", " con el runtime oficial 310.9.16 (hasta ");
            A(": o MFG vem do dlssg_for_sm86 (comunitário). Testado pelo autor em RTX 3070/2080 Ti; comece em 2X.", ": MFG comes from dlssg_for_sm86 (community). Tested by its author on RTX 3070/2080 Ti; start at 2X.", ": el MFG viene de dlssg_for_sm86 (comunitario). Probado por su autor en RTX 3070/2080 Ti; empiece en 2X.");
            A(" em jogo sem suporte: DLSS-G via OptiScaler + dlssg_for_sm86 é experimental. Se não ativar, use 1X (desligado) e teste de novo.", " in an unsupported game: DLSS-G via OptiScaler + dlssg_for_sm86 is experimental. If it doesn't turn on, use 1X (off) and test again.", " en un juego sin soporte: DLSS-G con OptiScaler + dlssg_for_sm86 es experimental. Si no se activa, use 1X (desactivado) y pruebe de nuevo.");
            A("Desativar ", "Disable ", "Desactivar ");
            A(" antigo (", " (old,", " (antiguo,");
            A(" → backup .dlss5bak) para não carregar duas vezes", " → .dlss5bak backup) so it is not loaded twice", " → copia .dlss5bak) para que no se cargue dos veces");
            A("Atualizar DLLs DLSS do jogo (", "Update the game's DLSS DLLs (", "Actualizar las DLL de DLSS del juego (");
            A(" arquivo(s)) para as versões da pasta Streamline, com backup", " file(s)) to the versions in the Streamline folder, with backup", " archivo(s)) a las versiones de la carpeta Streamline, con copia de seguridad");
            A("nvngx_dlss.dll 310.9.x da pasta Streamline ao lado do exe (se o jogo não tiver)", "nvngx_dlss.dll 310.9.x from the Streamline folder next to the exe (if the game doesn't have it)", "nvngx_dlss.dll 310.9.x de la carpeta Streamline junto al exe (si el juego no lo tiene)");
            A("Arraste a pasta do jogo aqui", "Drag the game folder here", "Arrastre aquí la carpeta del juego");
            A("ou clique para procurar · também aceita o .exe", "or click to browse · also accepts the .exe", "o haga clic para buscar · también acepta el .exe");
            A("Selecione a pasta do jogo para ver o plano.", "Select the game folder to see the plan.", "Seleccione la carpeta del juego para ver el plan.");
            A("DLSS 5", "DLSS 5", "DLSS 5");
            A("Quadro real (renderizado pelo jogo)", "Real frame (rendered by the game)", "Fotograma real (renderizado por el juego)");
            A("Gerado pela IA (varia até 6X)", "AI generated (varies up to 6X)", "Generado por IA (varía hasta 6X)");
            A("Quadro gerado pela IA (DLSS-G)", "AI-generated frame (DLSS-G)", "Fotograma generado por IA (DLSS-G)");
            A("A versão ", "Version ", "La versión ");
            A(" não tem o arquivo de ", " does not have the ", " no tiene el archivo de ");
            A("instalação", "installer file", "instalación");
            A("versão portable", "portable version", "versión portable");
            A("O arquivo baixado não confere com o SHA-256 publicado no GitHub. A atualização foi cancelada.", "The downloaded file does not match the SHA-256 published on GitHub. The update was cancelled.", "El archivo descargado no coincide con el SHA-256 publicado en GitHub. La actualización se canceló.");
            A("O pacote da atualização não contém o DLSS 5 Definitive.exe.", "The update package does not contain DLSS 5 Definitive.exe.", "El paquete de actualización no contiene DLSS 5 Definitive.exe.");
            A("Pasta", "Folder", "Carpeta");
            A("Configurações", "Settings", "Configuración");
            A("Idioma", "Language", "Idioma");
            A("Idioma do aplicativo", "App language", "Idioma de la aplicación");
            A("Automático (idioma do Windows: ", "Automatic (Windows language: ", "Automático (idioma de Windows: ");
            A("O app reinicia para aplicar o novo idioma.", "The app restarts to apply the new language.", "La app se reinicia para aplicar el nuevo idioma.");
            A("Reiniciar o DLSS 5 Definitive agora para aplicar o idioma?", "Restart DLSS 5 Definitive now to apply the language?", "¿Reiniciar DLSS 5 Definitive ahora para aplicar el idioma?");
            A("Atualizações", "Updates", "Actualizaciones");
            A("Versão instalada", "Installed version", "Versión instalada");
            A("  Verificar agora", "  Check now", "  Comprobar ahora");
            A("Verificar atualizações automaticamente (ao abrir e a cada 6 h)", "Check for updates automatically (on start and every 6 h)", "Buscar actualizaciones automáticamente (al abrir y cada 6 h)");
            A("Nova versão disponível: ", "New version available: ", "Nueva versión disponible: ");
            A("Você está na versão mais recente", "You are on the latest version", "Tiene la versión más reciente");
            A("Ainda não verificado", "Not checked yet", "Aún no comprobado");
            A("Arquivos do DLSS", "DLSS files", "Archivos de DLSS");
            A("Dados do aplicativo", "App data", "Datos de la aplicación");
            A("Modo: ", "Mode: ", "Modo: ");
            A("Portable (dados ao lado do exe)", "Portable (data next to the exe)", "Portable (datos junto al exe)");
            A("Instalado", "Installed", "Instalado");
            A("Executando fora da pasta instalada", "Running outside the installed folder", "Ejecutándose fuera de la carpeta instalada");
            A("Downloads em cache: ", "Cached downloads: ", "Descargas en caché: ");
            A("  Abrir pasta de dados", "  Open data folder", "  Abrir carpeta de datos");
            A("  Limpar downloads", "  Clear downloads", "  Borrar descargas");
            A("Apagar os downloads em cache (OptiScaler, Streamline, DXVK...)? Eles são baixados de novo quando necessário. As capas e a biblioteca são mantidas.", "Delete the cached downloads (OptiScaler, Streamline, DXVK...)? They are downloaded again when needed. Covers and the library are kept.", "¿Borrar las descargas en caché (OptiScaler, Streamline, DXVK...)? Se vuelven a descargar cuando hace falta. Se conservan las portadas y la biblioteca.");
            A("✔ Downloads em cache apagados.", "✔ Cached downloads deleted.", "✔ Descargas en caché borradas.");
            A("Sobre", "About", "Acerca de");
            A("DLSS 5 Neural Rendering e Multi Frame Generation por IA para DX12, DX11, Vulkan, DX10 e DX9.", "DLSS 5 Neural Rendering and AI Multi Frame Generation for DX12, DX11, Vulkan, DX10 and DX9.", "DLSS 5 Neural Rendering y Multi Frame Generation por IA para DX12, DX11, Vulkan, DX10 y DX9.");
            A("  Página no GitHub", "  GitHub page", "  Página en GitHub");
            A("  Créditos e licenças", "  Credits and licenses", "  Créditos y licencias");
            A("Componentes de terceiros: OptiScaler F5 (janblade), NVIDIA Streamline, dlssg_for_sm86 (sdli1995), RenoDX, ReShade, DXVK e dgVoodoo2 — cada um com a própria licença.", "Third-party components: OptiScaler F5 (janblade), NVIDIA Streamline, dlssg_for_sm86 (sdli1995), RenoDX, ReShade, DXVK and dgVoodoo2 — each under its own license.", "Componentes de terceros: OptiScaler F5 (janblade), NVIDIA Streamline, dlssg_for_sm86 (sdli1995), RenoDX, ReShade, DXVK y dgVoodoo2 — cada uno con su propia licencia.");
            A(" do jogo", " of the game", " del juego");
            A("Ocorreu um erro, mas o ", "An error occurred, but ", "Se produjo un error, pero ");
            A(" continua aberto.", " is still open.", " sigue abierto.");
            A("Detalhes salvos em:", "Details saved in:", "Detalles guardados en:");
            A("verificando...", "checking...", "comprobando...");
            A("Pronto.", "Ready.", "Listo.");
            A("desligado", "off", "desactivado");
            A("discos", "drives", "discos");
            A("Runtime do DLSS 5 encontrada: ", "DLSS 5 runtime found: ", "Runtime de DLSS 5 encontrado: ");
            A("Clique em Instalar DLSS 5 de novo.", "Click Install DLSS 5 again.", "Haga clic en Instalar DLSS 5 de nuevo.");
            A("Este arquivo não é a runtime do DLSS 5.", "This file is not the DLSS 5 runtime.", "Este archivo no es el runtime de DLSS 5.");
            A("O nvngx.dll_dlssnr.dll é o \"forwarder\" que já vem dentro do OptiScaler F5 — o app instala ele sozinho. ",
              "nvngx.dll_dlssnr.dll is the \"forwarder\" that already comes inside OptiScaler F5 — the app installs it by itself. ",
              "nvngx.dll_dlssnr.dll es el \"forwarder\" que ya viene dentro de OptiScaler F5 — la app lo instala sola. ");
            A("A runtime é o nvngx_dlssnr.dll da NVIDIA (cerca de 250 MB).", "The runtime is NVIDIA's nvngx_dlssnr.dll (about 250 MB).",
              "El runtime es el nvngx_dlssnr.dll de NVIDIA (unos 250 MB).");
            A("Copiando para a pasta do app...", "Copying into the app folder...", "Copiando a la carpeta de la app...");
            A("Runtime do DLSS 5 copiada para a pasta do app: ", "DLSS 5 runtime copied to the app folder: ", "Runtime de DLSS 5 copiado a la carpeta de la app: ");
            A("Streamline copiado para a pasta do app: ", "Streamline copied to the app folder: ", "Streamline copiado a la carpeta de la app: ");
            A("Não foi possível copiar para a pasta do app: ", "Could not copy into the app folder: ", "No se pudo copiar a la carpeta de la app: ");
            A("Guardados na pasta do app: ", "Stored in the app folder: ", "Guardados en la carpeta de la app: ");
            A("Copiando a runtime do DLSS 5...", "Copying the DLSS 5 runtime...", "Copiando el runtime de DLSS 5...");
            A("Copiando o Streamline...", "Copying Streamline...", "Copiando Streamline...");
            A("Encontrada — será copiada para a pasta do app: ", "Found — it will be copied into the app folder: ", "Encontrado — se copiará a la carpeta de la app: ");
            A("Não encontrada — escolha agora (opcional) ou depois no app", "Not found — choose it now (optional) or later in the app",
              "No encontrado — elíjalo ahora (opcional) o más tarde en la app");
            A("Encontrado — será copiado para a pasta do app: ", "Found — it will be copied into the app folder: ", "Encontrado — se copiará a la carpeta de la app: ");
            A("Será baixado da NVIDIA quando você instalar o DLSS 5 num jogo", "Will be downloaded from NVIDIA when you install DLSS 5 in a game",
              "Se descargará de NVIDIA cuando instale DLSS 5 en un juego");
            A("Escolher...", "Choose...", "Elegir...");
            A(" foi removido.", " was removed.", " fue eliminado.");
            A(" instalado em ", " installed in ", " instalado en ");
            A("Não encontrado — coloque o nvngx_dlssnr.dll em Downloads, arraste para cá ou clique em Alterar",
              "Not found — put nvngx_dlssnr.dll in Downloads, drag it here or click Change",
              "No encontrado — ponga el nvngx_dlssnr.dll en Descargas, arrástrelo aquí o haga clic en Cambiar");
        }
    }
}
