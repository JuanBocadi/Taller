using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Drawing; // Importante para los colores
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ==========================================
            // 1. PANTALLA DE CARGA MODERNA (DARK MODE)
            // ==========================================
            var splash = new Form
            {
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.None, // Quitamos los bordes viejos de Windows
                Width = 420,
                Height = 260,
                BackColor = Color.FromArgb(15, 23, 42), // Azul medianoche muy elegante (Slate 900)
                ShowInTaskbar = false
            };

            // Dibujar un borde azul de 2 píxeles alrededor de la ventana
            splash.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(59, 130, 246), 2)) // Tu azul primario
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, splash.Width - 1, splash.Height - 1);
                }
            };

            // Ícono visual (Usamos un emoji gigante para que parezca un logo SVG)
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

            // Título principal
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

            // Subtítulo que va cambiando
            var lblStatus = new Label
            {
                Text = "Iniciando motor web...",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184), // Gris clarito elegante (Slate 400)
                AutoSize = false,
                Width = splash.Width,
                Height = 30,
                Top = 150,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            // Fondo de la barrita de progreso
            var pnlProgressBg = new Panel
            {
                Width = 280,
                Height = 4,
                BackColor = Color.FromArgb(30, 41, 59), // Azul oscuro de fondo
                Left = 70,
                Top = 200
            };

            // La barrita que avanza (Color azul primario)
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

            // Timer para hacer que la barrita avance súper fluido
            var timer = new System.Windows.Forms.Timer { Interval = 15 };
            timer.Tick += (s, e) =>
            {
                // Avanza rápido hasta el 85% y ahí "espera" a que el servidor termine
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

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=taller.db"));

            builder.Services.AddControllersWithViews();

            // Agregamos el servicio en segundo plano para los backups automáticos
            builder.Services.AddHostedService<BackupBackgroundService>();

            var app = builder.Build();

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
                Opacity = 0 // MAGIA: Oculta la pantalla blanca
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
                    // Cuando termina de cargar, llenamos la barrita al 100% visualmente
                    pnlProgress.Width = pnlProgressBg.Width;
                    lblStatus.Text = "¡Listo!";
                    splash.Refresh();

                    // Cerramos carga y mostramos la app nativa
                    timer.Stop();
                    splash.Close();
                    form.Opacity = 1;
                };
            };

            Application.Run(form);
        }
    }
}