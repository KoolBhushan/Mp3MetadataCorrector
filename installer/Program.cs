// Author:
// Bhushan Kamble
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Mp3MetadataCorrector.Installer;

/// <summary>
/// Entry point for the Windows Forms setup installer and uninstaller application.
/// </summary>
public static class Program
{
    /// <summary>
    /// The main entry point for the installer application.
    /// </summary>
    /// <param name="args">Command line arguments passed to the installer (supports /UNINSTALL or /U).</param>
    [STAThread]
    public static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Check if uninstall mode
        foreach (var arg in args)
        {
            if (arg.Equals("/UNINSTALL", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/U", StringComparison.OrdinalIgnoreCase))
            {
                RunUninstaller();
                return;
            }
        }

        // Run Setup Form
        Application.Run(new SetupForm());
    }

    /// <summary>
    /// Executes the uninstallation workflow removing shortcuts, registry entries, and program binaries.
    /// </summary>
    private static void RunUninstaller()
    {
        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Mp3MetadataCorrector");

        var result = MessageBox.Show(
            "Are you sure you want to completely remove MP3 Metadata Corrector?",
            "Uninstall MP3 Metadata Corrector",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        try
        {
            // Remove shortcuts
            string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MP3 Metadata Corrector.lnk");
            if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

            string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "MP3 Metadata Corrector");
            if (Directory.Exists(startMenuDir)) Directory.Delete(startMenuDir, true);

            // Remove registry key
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Mp3MetadataCorrector", false);

            // Schedule self-delete of installation folder
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c timeout /t 2 & rmdir /s /q \"{defaultPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            MessageBox.Show(
                "MP3 Metadata Corrector was successfully removed from your computer.",
                "Uninstall Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error during uninstallation: {ex.Message}",
                "Uninstall Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

/// <summary>
/// Graphical installation wizard providing folder selection, shortcut configuration, and extraction progress.
/// </summary>
public class SetupForm : Form
{
    private TextBox _txtPath = null!;
    private CheckBox _chkDesktop = null!;
    private CheckBox _chkStartMenu = null!;
    private CheckBox _chkLaunch = null!;
    private ProgressBar _progressBar = null!;
    private Label _lblStatus = null!;
    private Button _btnInstall = null!;
    private Button _btnCancel = null!;
    private Button _btnBrowse = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetupForm"/> class.
    /// </summary>
    public SetupForm()
    {
        InitializeComponents();
    }

    private static string GetApplicationVersion()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
    }

    private void InitializeComponents()
    {
        string version = GetApplicationVersion();
        Text = $"MP3 Metadata Corrector v{version} Setup";
        Size = new Size(580, 440);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch { }

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = Color.FromArgb(15, 32, 67)
        };

        var lblTitle = new Label
        {
            Text = $"MP3 Metadata Corrector v{version}",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
            Location = new Point(20, 16),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Windows 11 WinUI 3 Metadata Corrector & YouTube MP3 Downloader",
            ForeColor = Color.FromArgb(200, 215, 240),
            Font = new Font("Segoe UI", 9F),
            Location = new Point(21, 46),
            AutoSize = true
        };

        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSubtitle);
        Controls.Add(pnlHeader);

        // Body Panel
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20)
        };

        var lblDest = new Label
        {
            Text = "Destination Folder:",
            Location = new Point(20, 100),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F)
        };

        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Mp3MetadataCorrector");

        _txtPath = new TextBox
        {
            Text = defaultPath,
            Location = new Point(20, 124),
            Width = 420
        };

        _btnBrowse = new Button
        {
            Text = "Browse...",
            Location = new Point(450, 122),
            Width = 90,
            Height = 27
        };
        _btnBrowse.Click += OnBrowseClick;

        var lblOptions = new Label
        {
            Text = "Installation Options:",
            Location = new Point(20, 165),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F)
        };

        _chkDesktop = new CheckBox
        {
            Text = "Create a Desktop shortcut",
            Checked = true,
            Location = new Point(24, 190),
            AutoSize = true
        };

        _chkStartMenu = new CheckBox
        {
            Text = "Create a Start Menu shortcut",
            Checked = true,
            Location = new Point(24, 215),
            AutoSize = true
        };

        _chkLaunch = new CheckBox
        {
            Text = "Launch MP3 Metadata Corrector when setup finishes",
            Checked = true,
            Location = new Point(24, 240),
            AutoSize = true
        };

        _lblStatus = new Label
        {
            Text = "Ready to install. Click 'Install' to begin.",
            Location = new Point(20, 275),
            Width = 520,
            ForeColor = Color.DimGray
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(20, 300),
            Width = 520,
            Height = 20,
            Visible = false
        };

        _btnInstall = new Button
        {
            Text = "Install",
            Location = new Point(340, 350),
            Width = 100,
            Height = 32,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9F)
        };
        _btnInstall.FlatAppearance.BorderSize = 0;
        _btnInstall.Click += OnInstallClick;

        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(450, 350),
            Width = 90,
            Height = 32
        };
        _btnCancel.Click += (s, e) => Close();

        Controls.Add(lblDest);
        Controls.Add(_txtPath);
        Controls.Add(_btnBrowse);
        Controls.Add(lblOptions);
        Controls.Add(_chkDesktop);
        Controls.Add(_chkStartMenu);
        Controls.Add(_chkLaunch);
        Controls.Add(_lblStatus);
        Controls.Add(_progressBar);
        Controls.Add(_btnInstall);
        Controls.Add(_btnCancel);
    }

    private void OnBrowseClick(object? sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Select Installation Folder",
            SelectedPath = _txtPath.Text
        };
        if (fbd.ShowDialog() == DialogResult.OK)
        {
            _txtPath.Text = Path.Combine(fbd.SelectedPath, "Mp3MetadataCorrector");
        }
    }

    private async void OnInstallClick(object? sender, EventArgs e)
    {
        string installDir = _txtPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(installDir))
        {
            MessageBox.Show("Please specify an installation path.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnInstall.Enabled = false;
        _btnCancel.Enabled = false;
        _btnBrowse.Enabled = false;
        _txtPath.Enabled = false;
        _chkDesktop.Enabled = false;
        _chkStartMenu.Enabled = false;
        _chkLaunch.Enabled = false;

        _progressBar.Visible = true;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _lblStatus.Text = "Extracting application files and tools...";

        try
        {
            await Task.Run(() => PerformInstallation(installDir));

            _progressBar.Style = ProgressBarStyle.Blocks;
            _progressBar.Value = 100;
            _lblStatus.Text = "Installation completed successfully!";
            _btnInstall.Text = "Finish";
            _btnInstall.Enabled = true;
            _btnInstall.Click -= OnInstallClick;
            _btnInstall.Click += (s, ev) =>
            {
                if (_chkLaunch.Checked)
                {
                    string exePath = Path.Combine(installDir, "Mp3MetadataCorrector.exe");
                    if (File.Exists(exePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            WorkingDirectory = installDir,
                            UseShellExecute = true
                        });
                    }
                }
                Close();
            };

            MessageBox.Show(
                "MP3 Metadata Corrector has been installed successfully!",
                "Setup Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Installation failed: {ex.Message}", "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnInstall.Enabled = true;
            _btnCancel.Enabled = true;
        }
    }

    private void PerformInstallation(string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        // Extract embedded zip payload
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("app_payload.zip"))
        {
            if (stream == null)
            {
                // Fallback to local app_payload.zip if running from installer directory
                string localZip = Path.Combine(AppContext.BaseDirectory, "app_payload.zip");
                if (File.Exists(localZip))
                {
                    ZipFile.ExtractToDirectory(localZip, targetDir, overwriteFiles: true);
                }
                else
                {
                    throw new FileNotFoundException("Setup payload archive not found in resources or local folder.");
                }
            }
            else
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                archive.ExtractToDirectory(targetDir, overwriteFiles: true);
            }
        }

        string exePath = Path.Combine(targetDir, "Mp3MetadataCorrector.exe");

        // Copy self as uninstaller
        string uninstallerPath = Path.Combine(targetDir, "Uninstall.exe");
        try
        {
            File.Copy(Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe"), uninstallerPath, true);
        }
        catch { }

        // Create Start Menu shortcut
        if (_chkStartMenu.Checked)
        {
            string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "MP3 Metadata Corrector");
            Directory.CreateDirectory(startMenuDir);
            CreateShortcut(Path.Combine(startMenuDir, "MP3 Metadata Corrector.lnk"), exePath, targetDir, "MP3 Metadata Corrector & YouTube Downloader");
            CreateShortcut(Path.Combine(startMenuDir, "Uninstall.lnk"), uninstallerPath, targetDir, "Uninstall MP3 Metadata Corrector", "/UNINSTALL");
        }

        // Create Desktop shortcut
        if (_chkDesktop.Checked)
        {
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            CreateShortcut(Path.Combine(desktopDir, "MP3 Metadata Corrector.lnk"), exePath, targetDir, "MP3 Metadata Corrector & YouTube Downloader");
        }

        // Register in Windows Add/Remove Programs
        RegisterUninstall(targetDir, exePath, uninstallerPath);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description, string arguments = "")
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.Description = description;
                    if (!string.IsNullOrEmpty(arguments))
                    {
                        shortcut.Arguments = arguments;
                    }
                    shortcut.Save();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create shortcut: {ex.Message}");
        }
    }

    private static void RegisterUninstall(string installDir, string exePath, string uninstallerPath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Mp3MetadataCorrector");
            if (key != null)
            {
                string version = GetApplicationVersion();
                key.SetValue("DisplayName", $"MP3 Metadata Corrector v{version}");
                key.SetValue("DisplayVersion", version);
                key.SetValue("Publisher", "Bhushan Kamble");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", exePath);
                key.SetValue("UninstallString", $"\"{uninstallerPath}\" /UNINSTALL");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register uninstall: {ex.Message}");
        }
    }
}
