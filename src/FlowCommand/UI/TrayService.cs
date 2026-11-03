using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FlowCommand.Platform;

namespace FlowCommand.UI
{
    /// <summary>System-tray icon and menu (WinForms NotifyIcon; WPF has none built in).</summary>
    internal sealed class TrayService : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _dryRunItem;
        private readonly ServiceHost _host;

        public TrayService(ServiceHost host, Action openPalette, Action openSettings, Action reloaded, Action exit)
        {
            _host = host;
            var menu = new ContextMenuStrip();
            var open = new ToolStripMenuItem("Open command palette") { Font = new Font(SystemFonts.MenuFont ?? SystemFonts.DefaultFont, FontStyle.Bold) };
            open.Click += (s, e) => openPalette();
            var settings = new ToolStripMenuItem("Settings\u2026");
            settings.Click += (s, e) => openSettings();
            _dryRunItem = new ToolStripMenuItem("Dry-run mode (don't execute)") { CheckOnClick = false };
            _dryRunItem.Click += (s, e) =>
            {
                _host.Config.DryRun = !_host.Config.DryRun;
                _host.SaveConfig();
                Notify("Dry-run mode", _host.Config.DryRun ? "ON: commands are interpreted but not executed." : "OFF: commands will run.");
            };
            var cfgFolder = new ToolStripMenuItem("Open config folder");
            cfgFolder.Click += (s, e) => Shell(AppPaths.ConfigDir);
            var logFolder = new ToolStripMenuItem("Open log folder");
            logFolder.Click += (s, e) => Shell(AppPaths.LogDir);
            var reload = new ToolStripMenuItem("Reload config.json");
            reload.Click += (s, e) =>
            {
                _host.Reload();
                reloaded();
            };
            var quit = new ToolStripMenuItem("Exit");
            quit.Click += (s, e) => exit();

            menu.Items.AddRange(new ToolStripItem[]
            {
                open, settings, new ToolStripSeparator(), _dryRunItem, reload, cfgFolder, logFolder,
                new ToolStripSeparator(), quit,
            });
            menu.Opening += (s, e) => _dryRunItem.Checked = _host.Config.DryRun;

            _icon = new NotifyIcon
            {
                Icon = MakeIcon(),
                Text = "FlowCommand " + VersionText(),
                ContextMenuStrip = menu,
                Visible = true,
            };
            _icon.DoubleClick += (s, e) => openPalette();
        }

        private static string VersionText()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "" : v.ToString(3);
        }

        public void Notify(string title, string text)
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = text.Length > 250 ? text.Substring(0, 250) : text;
            _icon.ShowBalloonTip(4000);
        }

        private static void Shell(string path)
        {
            try
            {
                System.IO.Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch { }
        }

        private static Icon MakeIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var fill = new SolidBrush(Color.FromArgb(255, 106, 167, 255)))
                        g.FillEllipse(fill, 1, 1, 30, 30);
                    using (var font = new Font("Segoe UI", 17, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var white = new SolidBrush(Color.White))
                    {
                        var size = g.MeasureString("F", font);
                        g.DrawString("F", font, white, (32 - size.Width) / 2, (32 - size.Height) / 2);
                    }
                }
                var hIcon = bmp.GetHicon();
                try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
                finally { NativeMethods.DestroyIcon(hIcon); }
            }
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
    }
}
