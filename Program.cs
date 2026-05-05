using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Taller.Data;
using Taller.Infrastructure;


namespace Taller
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ==========================================
            // 0. RUTA DE BASE DE DATOS UNIFICADA
            // ==========================================
            // Usamos una carpeta fija en Documentos del usuario.
            // Esta ruta es IDÉNTICA tanto en "dotnet run" como en el .exe publicado,
            // por lo que los datos migrados en desarrollo ya estarán disponibles al distribuir.
            string rutaDb = AppPaths.GetDbPath();
            string logPath = Path.Combine(AppPaths.GetOrCreateDataDirectory(), "startup.log");

            void Log(string mensaje)
            {
                try
                {
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mensaje}{Environment.NewLine}");
                }
                catch { }
            }

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Log($"UnhandledException: {e.ExceptionObject}");

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log($"UnobservedTaskException: {e.Exception}");
                e.SetObserved();
            };

            Log("Inicio de aplicacion");

            // ==========================================
            // 1. PANTALLA DE CARGA MODERNA (DARK MODE)
            // ==========================================
            var splash = new Form
            {
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.None,
                Width = 420,
                Height = 260,
                BackColor = Color.FromArgb(15, 23, 42),
                ShowInTaskbar = false
            };

            splash.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(59, 130, 246), 2))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, splash.Width - 1, splash.Height - 1);
                }
            };

            var lblIcon = new Label
            {
                Text = "⚙️",
                Font = new Font("Segoe UI", 36),
                AutoSize = false,
                Width = splash.Width,
                Height = 70,
                Top = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text = "AutoSys",
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Width = splash.Width,
                Height = 50,
                Top = 100,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var lblStatus = new Label
            {
                Text = "Iniciando motor web...",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = false,
                Width = splash.Width,
                Height = 30,
                Top = 150,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var pnlProgressBg = new Panel
            {
                Width = 280,
                Height = 4,
                BackColor = Color.FromArgb(30, 41, 59),
                Left = 70,
                Top = 200
            };

            var pnlProgress = new Panel
            {
                Width = 0,
                Height = 4,
                BackColor = Color.FromArgb(59, 130, 246),
                Left = 0,
                Top = 0
            };

            pnlProgressBg.Controls.Add(pnlProgress);
            splash.Controls.Add(pnlProgressBg);
            splash.Controls.Add(lblStatus);
            splash.Controls.Add(lblTitle);
            splash.Controls.Add(lblIcon);

            var timer = new System.Windows.Forms.Timer { Interval = 15 };
            timer.Tick += (s, e) =>
            {
                if (pnlProgress.Width < pnlProgressBg.Width * 0.85)
                {
                    pnlProgress.Width += 3;
                }
            };

            splash.Show();
            timer.Start();
            splash.Refresh();

            // ==========================================
            // 2. CONFIGURACIÓN DEL SERVIDOR WEB (MVC)
            // ==========================================
            var contentRoot = AppContext.BaseDirectory;
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = contentRoot
            });

            // Puerto dinámico para evitar colisiones cuando 5000 ya está ocupado.
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            Log($"ContentRoot: {contentRoot}");

            // Usamos la ruta unificada definida arriba
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={rutaDb}"));

            builder.Services.AddControllersWithViews();
            builder.Services.AddHostedService<BackupBackgroundService>();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

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
                    // Compatibilidad con bases existentes creadas con EnsureCreated.
                    // Si no hay historial de migraciones, evitamos que la app se cierre.
                    dbContext.Database.EnsureCreated();
                    Log("DB: ensure created (fallback)");
                }
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            string serverUrl = "http://127.0.0.1:5000";

            try
            {
                app.StartAsync().GetAwaiter().GetResult();
                serverUrl = app.Urls.FirstOrDefault() ?? serverUrl;
                Log($"Servidor web iniciado en {serverUrl}");
            }
            catch (Exception ex)
            {
                Log($"Error iniciando servidor web: {ex}");
                MessageBox.Show(
                    "No se pudo iniciar el servidor interno. Revisá startup.log en Documentos/AutoSys.",
                    "AutoSys",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // ==========================================
            // 3. CONFIGURACIÓN DE LA VENTANA PRINCIPAL
            // ==========================================
            var form = new Form
            {
                Text = "AutoSys - Sistema de Gestión",
                Width = 1024,
                Height = 768,
                WindowState = FormWindowState.Maximized,
                Icon = SystemIcons.Application,
                Opacity = 0
            };

            var webView = new WebView2
            {
                Dock = DockStyle.Fill
            };

            form.Controls.Add(webView);

            form.Load += async (sender, e) =>
            {
                bool principalMostrada = false;
                void MostrarPrincipal()
                {
                    if (principalMostrada) return;
                    principalMostrada = true;
                    pnlProgress.Width = pnlProgressBg.Width;
                    lblStatus.Text = "¡Listo!";
                    splash.Refresh();

                    timer.Stop();
                    if (!splash.IsDisposed) splash.Close();
                    form.Opacity = 1;
                }

                var timerFallback = new System.Windows.Forms.Timer { Interval = 6000 };
                timerFallback.Tick += (s, ev) =>
                {
                    timerFallback.Stop();
                    Log("Fallback UI activado (NavigationCompleted no llego a tiempo)");
                    MostrarPrincipal();
                };

                try
                {
                    lblStatus.Text = "Conectando a base de datos...";
                    lblStatus.Refresh();

                    await webView.EnsureCoreWebView2Async(null);

                    lblStatus.Text = "Cargando interfaz...";
                    lblStatus.Refresh();

                    webView.NavigationCompleted += (s, ev) =>
                    {
                        Log($"NavigationCompleted: Success={ev.IsSuccess}, Status={ev.WebErrorStatus}");
                        MostrarPrincipal();
                    };

                    timerFallback.Start();
                    webView.Source = new Uri(serverUrl);
                }
                catch (Exception ex)
                {
                    Log($"Error inicializando WebView2: {ex}");
                    timerFallback.Stop();
                    MostrarPrincipal();
                    MessageBox.Show(
                        "No se pudo inicializar WebView2. Revisá startup.log en Documentos/AutoSys.",
                        "AutoSys",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            };

            Application.Run(form);

            try
            {
                app.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
                Log("Aplicacion detenida correctamente");
            }
            catch (Exception ex)
            {
                Log($"Error al detener servidor web: {ex}");
            }
        }
    }
}