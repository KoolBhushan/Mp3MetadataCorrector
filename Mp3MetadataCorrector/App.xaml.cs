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
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Mp3MetadataCorrector.Services;

namespace Mp3MetadataCorrector;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Initializes the singleton application object and configures persistent diagnostics logging
    /// and unhandled exception safety handlers.
    /// </summary>
    public App()
    {
        InitializeLoggingAndCrashHandlers();
        this.InitializeComponent();
    }

    /// <summary>
    /// Sets up global crash interception hooks across WinUI, AppDomain, and TaskScheduler threads.
    /// </summary>
    private void InitializeLoggingAndCrashHandlers()
    {
        // 1. WinUI 3 XAML unhandled exceptions
        this.UnhandledException += (sender, e) =>
        {
            Logger.Fatal($"[WinUI Unhandled Exception] {e.Message}", e.Exception);
            // Mark as handled to attempt to keep the app alive if safe
            e.Handled = true;
        };

        // 2. AppDomain unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.Fatal($"[AppDomain Unhandled Exception] (IsTerminating: {e.IsTerminating})", ex);
            }
            else
            {
                Logger.Fatal($"[AppDomain Unhandled Non-Exception Object] (IsTerminating: {e.IsTerminating}): {e.ExceptionObject}");
            }
        };

        // 3. Unobserved Task exceptions
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Logger.Error("[TaskScheduler UnobservedTaskException]", e.Exception);
            e.SetObserved();
        };

        // 4. Log startup diagnostic telemetry
        string appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        Logger.Info("================================================================================");
        Logger.Info($"MP3 Metadata Corrector starting up. Version: {appVersion}");
        Logger.Info($"OS: {Environment.OSVersion} | 64-bit OS: {Environment.Is64BitOperatingSystem} | 64-bit Process: {Environment.Is64BitProcess}");
        Logger.Info($".NET Runtime: {Environment.Version}");
        Logger.Info($"Base Directory: {AppContext.BaseDirectory}");
        Logger.Info($"Logs Path: {Logger.CurrentLogFilePath}");
        Logger.Info("================================================================================");
    }

    /// <summary>
    /// Invoked when the application is launched normally by the end user.
    /// Other entry points will be used when the application is launched to open a specific file.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Logger.Info("App.OnLaunched invoked, creating MainWindow...");
            _window = new MainWindow();
            _window.Activate();
            Logger.Info("MainWindow created and activated successfully.");
        }
        catch (Exception ex)
        {
            Logger.Fatal("Failed to create or activate MainWindow during OnLaunched", ex);
            throw;
        }
    }
}
