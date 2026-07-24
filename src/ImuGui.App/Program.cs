using ImuGui.App.Presenters;
using ImuGui.App.Settings;
using ImuGui.Core.Abstractions;
using ImuGui.Core.Calibration;
using ImuGui.Core.Filtering;
using ImuGui.Core.Fusion;
using ImuGui.Core.Pipeline;
using ImuGui.Core.Sources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ImuGui.App;

/// <summary>Application entry point: logging, dependency injection, and the main form.</summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        string storageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImuGui");
        Directory.CreateDirectory(storageDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(storageDirectory, "logs", "imugui-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => HandleFatalException(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Log.Fatal(e.ExceptionObject as Exception, "Unhandled non-UI exception.");

            using IHost host = BuildHost(storageDirectory);

            var mainForm = host.Services.GetRequiredService<MainForm>();
            var presenter = host.Services.GetRequiredService<MainPresenter>();
            mainForm.AttachPresenter(presenter);

            Application.Run(mainForm);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "ImuGui failed to start.");
            MessageBox.Show(
                $"ImuGui failed to start:\n\n{ex.Message}\n\nSee the log in {storageDirectory}\\logs.",
                "ImuGui", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IHost BuildHost(string storageDirectory)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog();

        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<ISerialPortConnectionFactory, SerialPortConnectionFactory>();
        builder.Services.AddSingleton<ISettingsService>(sp => new JsonSettingsService(
            storageDirectory, sp.GetRequiredService<ILogger<JsonSettingsService>>()));
        builder.Services.AddSingleton<ICalibrationProfileStore>(sp => new JsonCalibrationProfileStore(
            storageDirectory, sp.GetRequiredService<ILogger<JsonCalibrationProfileStore>>()));
        builder.Services.AddSingleton<ICalibrationService>(sp => new CalibrationService(
            sp.GetRequiredService<ICalibrationProfileStore>(),
            sp.GetRequiredService<ILogger<CalibrationService>>()));

        builder.Services.AddSingleton(sp =>
        {
            UserSettings settings = sp.GetRequiredService<ISettingsService>().Current;
            return new SensorPipeline(
                FilterBank.CreateKalman(settings.FilterConfig),
                OrientationEstimatorFactory.Create(settings.EstimatorKind),
                OrientationEstimatorFactory.Create(settings.EstimatorKind),
                sp.GetRequiredService<ICalibrationService>(),
                sp.GetRequiredService<ILogger<SensorPipeline>>());
        });

        builder.Services.AddSingleton<MainForm>();
        builder.Services.AddSingleton<IMainView>(sp => sp.GetRequiredService<MainForm>());
        builder.Services.AddSingleton<MainPresenter>();

        return builder.Build();
    }

    private static void HandleFatalException(Exception exception)
    {
        Log.Error(exception, "Unhandled UI exception.");
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{exception.Message}\n\nThe error has been logged.",
            "ImuGui", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
