using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO; // Necesario para manejar rutas de archivos
using System.Drawing; 
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Taller.Data;


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
            // 0. CONFIGURACIÓN DE RUTAS (SOLUCIÓN BASE DE DATOS)
            // ==========================================
            // Obtenemos la ruta exacta de la carpeta donde se está ejecutando el .exe
            string rutaEjecutable = AppDomain.CurrentDomain.BaseDirectory;
            string rutaDb = Path.Combine(rutaEjecutable, "taller.db");

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
            var builder = WebApplication.CreateBuilder(args);

            // Usamos la ruta absoluta configurada arriba para la base de datos
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={rutaDb}"));

            builder.Services.AddControllersWithViews();
            builder.Services.AddHostedService<BackupBackgroundService>();

            var app = builder.Build();

            app.UseDeveloperExceptionPage();

            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Database.EnsureCreated(); 
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            Task.Run(() => app.Run("http://localhost:5000"));

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
                lblStatus.Text = "Conectando a base de datos...";
                lblStatus.Refresh();

                await webView.EnsureCoreWebView2Async(null);
                
                lblStatus.Text = "Cargando interfaz...";
                lblStatus.Refresh();
                
                webView.Source = new Uri("http://localhost:5000");

                webView.NavigationCompleted += (s, ev) =>
                {
                    pnlProgress.Width = pnlProgressBg.Width;
                    lblStatus.Text = "¡Listo!";
                    splash.Refresh();

                    timer.Stop();
                    splash.Close();
                    form.Opacity = 1;
                };
            };

            Application.Run(form);
        }
    }
}