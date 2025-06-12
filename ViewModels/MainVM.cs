using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text.Json;
using System.Windows.Input;
using TapRack;
using TapRack.Models;
using TapRack.ViewModels;

namespace TapRack.ViewModels;

public class MainVM : INotifyPropertyChanged
{
    public ObservableCollection<AppInfos> ActivePrograms { get; set; } = new();
    public ObservableCollection<string> Logs { get; set; } = new();
    public bool HasErrors { get; set; }

    public ICommand RestartCommand { get; }
    public ICommand ShowLogsCommand { get; }
    public ICommand CloseCommand { get; }

    public event PropertyChangedEventHandler PropertyChanged;

    public MainVM()
    {
        LoadActiveApplications();
        RestartCommand = new RelayCommand(async _ => await RestartAsync(), _ => ActivePrograms.Any());
        ShowLogsCommand = new RelayCommand(_ => ShowLogs());
        CloseCommand = new RelayCommand(_ => App.Current.Shutdown());
    }

    private void LoadActiveApplications()
    {
        if (!File.Exists("Apps.json")) return;
        var allPrograms = JsonSerializer.Deserialize<List<AppInfos>>(File.ReadAllText("Apps.json"));
        var activeProgs = new List<AppInfos>();
        Process[] allProcesses = Process.GetProcesses();

        foreach (var prog in allPrograms)
        {
            if (allProcesses.Any(p => p.ProcessName.Equals(prog.Id, StringComparison.OrdinalIgnoreCase)))
                activeProgs.Add(prog);
        }

        foreach (var prog in activeProgs)
            ActivePrograms.Add(prog);
    }

    private async Task RestartAsync()
    {
        await Task.Run(() =>
        {
            foreach (var prog in ActivePrograms)
            {
                foreach (var proc in Process.GetProcessesByName(prog.Name))
                {
                    try { proc.Kill(); proc.WaitForExit(); }
                    catch (Exception ex) { LogError($"Error while closing {prog.Name}", ex); }
                }
            }
            StopServices();
            StartServices();
            foreach (var prog in ActivePrograms)
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = prog.Path, Arguments = prog.Args ?? "", UseShellExecute = true });
                }
                catch (Exception ex) { LogError($"Error while starting {prog.Name}", ex); }
            }
        });
    }

    private void StopServices()
    {
        foreach (var service in LoadServicesFromJson("services.json"))
        {
            try
            {
                using var sc = new ServiceController(service.Name);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(300));
                }
            }
            catch (Exception ex)
            {
                LogError($"Error stopping service {service.Name}", ex);
            }
        }
    }

    private void StartServices()
    {
        foreach (var service in LoadServicesFromJson("services.json"))
        {
            try
            {
                using var sc = new ServiceController(service.Name);
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(300));
                }
            }
            catch (Exception ex)
            {
                LogError($"Error starting service {service.Name}", ex);
            }
        }
    }

    private List<ServiceInfos> LoadServicesFromJson(string filePath)
    {
        if (!File.Exists(filePath)) return new List<ServiceInfos>();
        string jsonContent = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<ServiceInfos>>(jsonContent);
    }

    private void LogError(string message, Exception ex)
    {
        HasErrors = true;
        Logs.Add($"{DateTime.Now:HH:mm:ss} | {message} | {ex.Message}");
        File.AppendAllText("error_log.txt", $"{DateTime.Now} | {message} | {ex}{Environment.NewLine}");
    }

    private void ShowLogs()
    {
        if (File.Exists("error_log.txt"))
        {
            Logs.Clear();
            foreach (var line in File.ReadAllLines("error_log.txt"))
                Logs.Add(line);
        }
    }
}
