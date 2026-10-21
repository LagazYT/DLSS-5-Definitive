using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace Dlss5Injector
{
    static class Program
    {
        public const string AppName = "DLSS 5 Definitive";
        public const string Author = "Lagaz";
        public const string Version = "2.3";

        [STAThread]
        static void Main(string[] args)
        {
            L.Init();   // idioma: configuração do usuário ou idioma do Windows
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2 (GitHub/Steam)
            ServicePointManager.DefaultConnectionLimit = 16;

            // Erros da interface viram aviso e vão para o crash.log, sem fechar o app.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Report(e.Exception, true); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Report(e.ExceptionObject as Exception, false); };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool updated = Array.Exists(args, a => a.Equals("/updated", StringComparison.OrdinalIgnoreCase));
            string folder = Array.Find(args, a => !a.StartsWith("/"));
            Updater.CleanupAfterUpdate();
            if (Array.Exists(args, a => a.Equals("/update-now", StringComparison.OrdinalIgnoreCase)))
            {
                // Atualização sem interface (para scripts): verifica, baixa, aplica e reabre o app atualizado.
                try
                {
                    UpdateInfo u = Updater.Check();
                    if (u != null) Updater.ApplyAndRestart(Updater.Download(u, delegate(int p) { }));
                }
                catch (Exception ex) { Report(ex, false); }
                return;
            }
            Application.Run(new MainForm(folder, updated));
        }

        public static string CrashLog { get { return Path.Combine(AppPaths.Root, "crash.log"); } }

        static int showing;

        public static void Report(Exception ex, bool canContinue)
        {
            try
            {
                File.AppendAllText(CrashLog, "==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (v" + Version + ")\r\n" + ex + "\r\n\r\n");
            }
            catch { }
            if (!canContinue || Interlocked.Increment(ref showing) > 1) { if (canContinue) Interlocked.Decrement(ref showing); return; }
            try
            {
                MessageBox.Show(L.T("Ocorreu um erro, mas o ") + AppName + L.T(" continua aberto.") + "\r\n\r\n" + (ex == null ? "" : ex.Message) +
                    "\r\n\r\n" + L.T("Detalhes salvos em:") + "\r\n" + CrashLog, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch { }
            finally { Interlocked.Decrement(ref showing); }
        }
    }
}
