using Microsoft.AspNetCore.Builder;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Web.WebView2.WinForms;

using System;

using System.Drawing;

using System.IO;

using System.Linq;

using System.Threading;

using System.Threading.Tasks;

using System.Windows.Forms;

using System.Diagnostics;

using Microsoft.Win32;

using Taller.Data;

using Taller.Infrastructure;



namespace Taller;



internal static class Program

{

    [STAThread]

    static void Main(string[] args)

    {

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        Application.EnableVisualStyles();

        Application.SetCompatibleTextRenderingDefault(false);

        EnsureBrowserEmulation();



        string rutaDb = AppPaths.GetDbPath();

        string logPath = Path.Combine(AppPaths.GetOrCreateDataDirectory(), "startup.log");



        void Log(string mensaje)

        {

            try

            {

                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mensaje}{Environment.NewLine}");

            }

            catch

            {

            }

        }



        AppDomain.CurrentDomain.UnhandledException += (_, e) =>

            Log($"UnhandledException: {e.ExceptionObject}");



        Log("Inicio de aplicacion");



        var splash = CrearSplash();

        var splashLabel = splash.Controls
            .Find("lblEstado", true)
            .OfType<Label>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No se pudo inicializar el splash de estado.");

        splash.Show();

        splash.Refresh();

        ActualizarSplash(splashLabel, "Iniciando servidor...");



        var contentRoot = AppContext.BaseDirectory;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions

        {

            Args = args,

            ContentRootPath = contentRoot

        });



        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.AddDbContext<AppDbContext>(options =>

            options.UseSqlite($"Data Source={rutaDb}"));

        builder.Services.AddControllersWithViews();

        builder.Services.AddHostedService<BackupBackgroundService>();



        var app = builder.Build();



        app.UseExceptionHandler("/Home/Error");

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();



        app.MapControllerRoute(

            name: "default",

            pattern: "{controller=Home}/{action=Index}/{id?}");



        using (var scope = app.Services.CreateScope())

        {

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try

            {

                dbContext.Database.Migrate();

                Log("DB: migrate OK");

            }

            catch

            {

                dbContext.Database.EnsureCreated();

                Log("DB: ensure created (fallback)");

            }

        }



        ActualizarSplash(splashLabel, "Levantando interfaz...");

        app.Start();

        var serverUrl = app.Urls.FirstOrDefault() ?? "http://127.0.0.1:5000";

        Log($"Servidor iniciado en {serverUrl}");



        var mainForm = new Form

        {

            Text = "AutoSys - Sistema de Gestion",

            Width = 1200,

            Height = 800,

            StartPosition = FormStartPosition.CenterScreen,

            WindowState = FormWindowState.Maximized

        };

        try
        {
            var formIconPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "app.ico");
            if (File.Exists(formIconPath))
            {
                mainForm.Icon = new Icon(formIconPath);
            }
        }
        catch { }



        var hostPanel = new Panel { Dock = DockStyle.Fill };

        mainForm.Controls.Add(hostPanel);



        var cierreIniciado = false;

        var cierreCompleto = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);



        mainForm.Shown += async (_, _) =>

        {

            try

            {

                ActualizarSplash(splashLabel, "Cargando interfaz...");

                var browserControl = await CrearNavegadorAsync(serverUrl, Log);

                hostPanel.Controls.Clear();

                hostPanel.Controls.Add(browserControl);

                mainForm.Tag = browserControl;

            }

            catch (Exception ex)

            {

                Log($"Error mostrando interfaz: {ex}");

                hostPanel.Controls.Clear();

                var lblError = new Label

                {

                    Dock = DockStyle.Fill,

                    TextAlign = ContentAlignment.MiddleCenter,

                    Font = new Font("Segoe UI", 12),

                    Text = "No se pudo iniciar el visor interno.\nAbrí manualmente: " + serverUrl

                };

                hostPanel.Controls.Add(lblError);

            }

            finally

            {

                if (!splash.IsDisposed)

                {

                    splash.Close();

                }

            }

        };



        void CerrarAplicacion()

        {

            if (cierreIniciado)

            {

                return;

            }



            cierreIniciado = true;

            Log("Cierre de aplicacion iniciado");



            Task.Run(async () =>

            {

                try

                {

                    if (mainForm.InvokeRequired)

                    {

                        mainForm.Invoke(() => LiberarBrowser(hostPanel, Log));

                    }

                    else

                    {

                        LiberarBrowser(hostPanel, Log);

                    }



                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));

                    await app.StopAsync(cts.Token).ConfigureAwait(false);

                    await app.DisposeAsync();

                    Log("Servidor web detenido");

                }

                catch (Exception ex)

                {

                    Log($"Error al detener servidor web: {ex}");

                    try

                    {

                        await app.DisposeAsync();

                    }

                    catch

                    {

                    }

                }

                finally

                {

                    cierreCompleto.TrySetResult();

                }

            });



            ThreadPool.QueueUserWorkItem(_ =>

            {

                try

                {

                    cierreCompleto.Task.Wait();

                }

                finally

                {

                    try

                    {

                        mainForm.Invoke(Application.ExitThread);

                    }

                    catch

                    {

                        Environment.Exit(0);

                    }

                }

            });

        }



        mainForm.FormClosing += (_, e) =>

        {

            // Evitar cierre hasta liberar WebView / host (evita cuelgue en pantalla).

            if (!cierreIniciado)

            {

                e.Cancel = true;

                CerrarAplicacion();

            }

        };



        Application.Run(mainForm);

        Log("Aplicacion detenida");

    }



    private static void LiberarBrowser(Control panel, Action<string> log)

    {

        try

        {

            foreach (Control c in panel.Controls)

            {

                if (c is WebView2 wv)

                {

                    try

                    {

                        wv.CoreWebView2?.Stop();

                    }

                    catch

                    {

                    }



                    panel.Controls.Remove(wv);

                    wv.Dispose();

                }

                else if (c is WebBrowser wb)

                {

                    try

                    {

                        wb.Stop();

                    }

                    catch

                    {

                    }



                    panel.Controls.Remove(wb);

                    wb.Dispose();

                }

            }

        }

        catch (Exception ex)

        {

            log($"Liberacion visor: {ex.Message}");

        }

    }



    private static async Task<Control> CrearNavegadorAsync(string serverUrl, Action<string> log)

    {

        try

        {

            var webViewUserDataFolder = Path.Combine(AppPaths.GetOrCreateDataDirectory(), "WebView2");

            Directory.CreateDirectory(webViewUserDataFolder);



            var webView = new WebView2

            {

                Dock = DockStyle.Fill,

                CreationProperties = new CoreWebView2CreationProperties

                {

                    UserDataFolder = webViewUserDataFolder

                }

            };

            webView.CoreWebView2InitializationCompleted += (_, e) =>

            {

                if (e.IsSuccess)

                {

                    webView.Source = new Uri(serverUrl);

                }

                else

                {

                    log($"WebView2 no disponible: {e.InitializationException?.Message}");

                }

            };



            await webView.EnsureCoreWebView2Async(null).WaitAsync(TimeSpan.FromSeconds(12));

            return webView;

        }

        catch (Exception ex)

        {

            log($"Fallback a WebBrowser por error en WebView2: {ex.Message}");

            try

            {

                var browser = new WebBrowser

                {

                    Dock = DockStyle.Fill,

                    ScriptErrorsSuppressed = true

                };

                browser.Navigate(serverUrl);

                return browser;

            }

            catch (Exception ex2)

            {

                log($"Error en fallback WebBrowser: {ex2.Message}");

                throw;

            }

        }

    }



    private static Form CrearSplash()

    {

        var splash = new Form

        {

            Width = 430,

            Height = 300,

            FormBorderStyle = FormBorderStyle.None,

            StartPosition = FormStartPosition.CenterScreen,

            BackColor = Color.FromArgb(15, 23, 42),

            ShowInTaskbar = false,

            TopMost = true

        };



        var titulo = new Label

        {

            Text = "AutoSys",

            ForeColor = Color.White,

            Font = new Font("Segoe UI", 24, FontStyle.Bold),

            Dock = DockStyle.Top,

            Height = 70,

            TextAlign = ContentAlignment.BottomCenter

        };



        var logo = new PictureBox

        {

            Dock = DockStyle.Top,

            Height = 150,

            SizeMode = PictureBoxSizeMode.Zoom,

            BackColor = Color.Transparent

        };



        try

        {

            var iconPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "favicon.ico");

            if (File.Exists(iconPath))

            {

                var appIcon = new Icon(iconPath);

                splash.Icon = appIcon;

                logo.Image = appIcon.ToBitmap();

            }

        }

        catch

        {

        }



        var estado = new Label

        {

            Name = "lblEstado",

            Text = "Preparando...",

            ForeColor = Color.FromArgb(148, 163, 184),

            Font = new Font("Segoe UI", 10, FontStyle.Regular),

            Dock = DockStyle.Fill,

            TextAlign = ContentAlignment.TopCenter

        };



        splash.Controls.Add(estado);

        splash.Controls.Add(titulo);

        splash.Controls.Add(logo);

        return splash;

    }



    private static void ActualizarSplash(Label splashLabel, string texto)

    {

        splashLabel.Text = texto;

        splashLabel.Refresh();

        Application.DoEvents();

    }



    private static void EnsureBrowserEmulation()

    {

        try

        {

            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

            var exeName = Path.GetFileName(string.IsNullOrWhiteSpace(exePath) ? "Taller.exe" : exePath);

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION");

            key?.SetValue(exeName, 11001, RegistryValueKind.DWord);

        }

        catch

        {

        }

    }

}


