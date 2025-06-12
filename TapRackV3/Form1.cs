using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.ServiceProcess;
using Microsoft.VisualBasic.Logging;

namespace TapRackV3;

public partial class Form1 : Form
{
    private Label messageLabel;
    private ListBox programListBox;
    private Button yesButton;
    private Button noButton;
    private Button viewLogsButton;
    private Button finishButton;

    private Label progressionLabel;
    private ProgressBar loadingBar;
    private bool hasErrors = false;

    private List<AppInfos> allPrograms = new();
    private List<AppInfos> activePrograms = new();
    public Form1()
    {
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        InitializeComponent();
        InitializeRestartPromptUI();
    }

    private void InitializeRestartPromptUI()
    {
        this.Text = "TapRack - Restart Services and Applications";
        this.Size = new System.Drawing.Size(500, 500);

        int buttonSpacing = 20;
        int buttonWidth = (this.ClientSize.Width - (3 * buttonSpacing)) / 2;
        int buttonHeight = 50;

        allPrograms = LoadProgramsFromJson("Apps.json");
        activePrograms = GetActiveApplications(allPrograms);

        messageLabel = new Label
        {
            Text = "Are you sure you want to restart all services and app?\nThis action will close:",
            AutoSize = true,
            Location = new System.Drawing.Point(20, 20)
        };

        this.Controls.Add(messageLabel);

        programListBox = new ListBox
        {
            Location = new System.Drawing.Point(20, 80),
            Size = new System.Drawing.Size(440, 250)
        };

        foreach (var prog in activePrograms)
        {
            programListBox.Items.Add(prog.Name);
        }
        this.Controls.Add(programListBox);

        yesButton = new Button
        {
            Text = "Yes",
            Size = new Size(buttonWidth, buttonHeight),
            Location = new Point(buttonSpacing, programListBox.Bottom + 20)
        };

        yesButton.Click += async (s, e) =>
        {
            // Fermer les programmes actifs
            foreach (var prog in activePrograms)
            {
                foreach (var proc in Process.GetProcessesByName(prog.Name))
                {
                    try { proc.Kill(); proc.WaitForExit(); }
                    catch (Exception ex) { LogError($"Error while closing {prog.Name}", ex); }
                }
            }

            // Redémarrage des services
            await ShowLoadingScreen(); // Pause pour laisser le temps aux programmes de se fermer

            progressionLabel.Text = "Restarting applications...";
            CenterControlsTogether(progressionLabel, loadingBar);
            await Task.Delay(2000); // Pause pour laisser le temps de lire le message
            // Relancer les applications précédemment actives
            foreach (var prog in activePrograms)
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = prog.Path,
                        Arguments = prog.Args ?? "",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    LogError($"Error while starting {prog.Name}", ex);
                }
            }
            // Afficher un message de confirmation sur l'écran actuel
            Label confirmationLabel = new Label
            {
                Text = "Restart completed successfully!",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 14),
                Location = new System.Drawing.Point(50, 100)
            };
            this.Controls.Clear();
            this.Controls.Add(confirmationLabel);

            // Afficher le bouton pour voir les logs
            viewLogsButton = new Button
            {
                Text = "Logs",
                Size = new System.Drawing.Size(75, 50),
                Location = new Point(20, this.ClientSize.Height - buttonHeight - 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            this.Controls.Add(viewLogsButton);
            viewLogsButton.Enabled = hasErrors; // Activer le bouton si des erreurs ont été enregistrées
            viewLogsButton.Click += (sender, e) => ShowLogs();

            finishButton = new Button
            {
                Text = "Finish",
                Size = new Size(buttonWidth, buttonHeight),
                Location = new Point(viewLogsButton.Right + buttonSpacing, confirmationLabel.Bottom + 20)
            };
            finishButton.Click += (sender, e) =>
            {
                File.Delete("error_log.txt");
                Application.Exit();
            };
            this.Controls.Add(finishButton);
            CenterControlsTogether(confirmationLabel, finishButton);
        };
        this.Controls.Add(yesButton);

        noButton = new Button
        {
            Text = "No",
            Size = new Size(buttonWidth, buttonHeight),
            Location = new Point(yesButton.Right + buttonSpacing, programListBox.Bottom + 20)
        };
        noButton.Click += (sender, e) => this.Close();
        this.Controls.Add(noButton);
    }
    private List<AppInfos> LoadProgramsFromJson(string filePath)
    {
        if (!File.Exists(filePath))
        {
            LogError($"File {filePath} not found.", new FileNotFoundException());
            return new List<AppInfos>();
        }

        string jsonContent = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<AppInfos>>(jsonContent);
    }

    private List<AppInfos> GetActiveApplications(List<AppInfos> programList)
    {
        List<AppInfos> active = new List<AppInfos>();
        Process[] allProcesses = Process.GetProcesses();

        foreach (var prog in programList)
        {
            foreach (var proc in allProcesses)
            {
                try
                {
                    if (proc.ProcessName.Equals(prog.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        active.Add(prog);
                        break;
                    }
                }
                catch { }
            }
        }

        return active;
    }
    private async Task ShowLoadingScreen()
    {

        this.Controls.Clear();

        progressionLabel = new Label
        {
            Text = "Stopping services...",
            AutoSize = true,
            Font = new Font("Segoe UI", 14),
            Location = new Point(140, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };
        this.Controls.Add(progressionLabel);

        loadingBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Size = new Size(200, 30),
            Location = new Point(140, 60)
        };
        this.Controls.Add(loadingBar);

        CenterControlsTogether(progressionLabel, loadingBar);
        await Task.Delay(3000); // Pause pour laisser le temps à l'utilisateur de lire le message
        await Task.Run(() => StopServicesAsync());
        progressionLabel.Text = "Services stopped successfully!";
        CenterControlsTogether(progressionLabel, loadingBar);
        await Task.Delay(3000); // Pause pour laisser le temps aux services de s'arrêter
        progressionLabel.Text = "Starting services...";
        CenterControlsTogether(progressionLabel, loadingBar);
        await Task.Run(() => StartServicesAsync());
        await Task.Delay(3000); // Pause pour laisser le temps aux services de démarrer
        progressionLabel.Text = "Services restarted successfully!";
        CenterControlsTogether(progressionLabel, loadingBar);
        await Task.Delay(3000); // Pause de 3 secondes pour afficher le message final
    }

    private void CenterControlsTogether(params Control[] controls)
    {
        int totalHeight = controls.Sum(c => c.Height + 10); // 10 px d'espacement
        int startY = (this.ClientSize.Height - totalHeight) / 2;

        foreach (Control ctrl in controls)
        {
            ctrl.Left = (this.ClientSize.Width - ctrl.Width) / 2;
            ctrl.Top = startY;
            startY += ctrl.Height + 10;
        }
    }
    private List<ServiceInfos> LoadServicesFromJson(string filePath)
    {
        if (!File.Exists(filePath))
        {
            LogError($"File {filePath} not found.", new FileNotFoundException());
            return new List<ServiceInfos>();
        }

        string jsonContent = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<ServiceInfos>>(jsonContent);
    }

    private async Task StopServicesAsync()
    {
        var servicesToRestart = LoadServicesFromJson("services.json");

        foreach (var service in servicesToRestart)
        {
            try
            {
                using (ServiceController sc = new ServiceController(service.Name))
                {

                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        Console.WriteLine($" -> Stopping {service}...");
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(300));
                        await Task.Delay(5000);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error stopping service {service.Name}", ex);
            }
        }


    }
    private async Task StartServicesAsync()
    {
        var servicesToRestart = LoadServicesFromJson("services.json");

        foreach (var service in servicesToRestart)
        {
            try
            {
                using (ServiceController sc = new ServiceController(service.Name))
                {

                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        Console.WriteLine($" -> Restarting {service}...");
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(300));
                        await Task.Delay(5000);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error restarting service {service.Name}", ex);
            }
        }
    }
    private void LogError(string message, Exception ex)
    {
        hasErrors = true;
        string logFilePath = "error_log.txt";
        string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message} | {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";

        try
        {
            File.AppendAllText(logFilePath, logEntry);
        }
        catch
        {
            // Optionnel : ignorer si le log échoue ou afficher un message
        }
    }

    private void ShowLogs()
    {
        this.Controls.Clear();
        string logContent = File.ReadAllText("error_log.txt");

        TextBox logTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Size = new Size(this.ClientSize.Width - 40, 200),
            Text = logContent,
            Font = new Font("Consolas", 9)
        };
        this.Controls.Add(logTextBox);
        CenterControlsTogether(logTextBox);
    }
}
