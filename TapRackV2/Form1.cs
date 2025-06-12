using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.ServiceProcess;

namespace TapRackV2
{
    public partial class Form1 : Form
    {
        private Label messageLabel;
        private ListBox programListBox;
        private Button yesButton;
        private Button noButton;

        private Label loadingLabel;
        private ProgressBar loadingBar;
        private FlowLayoutPanel serviceStatusPanel;


        private List<AppsInfo> allPrograms = new();
        private List<AppsInfo> activePrograms = new();

        public Form1()
        {
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            InitializeComponent();
            InitializeRestartPromptUI();
        }

        private void InitializeRestartPromptUI()
        {
            this.Text = "Restart Confirmation";
            this.Size = new System.Drawing.Size(500, 500);

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
                Location = new System.Drawing.Point(280, 320),
                Size = new System.Drawing.Size(75, 30)
            };

            yesButton.Click += async (s, e) =>
            {
                ShowLoadingScreen();

                // Fermer les programmes actifs
                foreach (var prog in activePrograms)
                {
                    foreach (var proc in Process.GetProcessesByName(prog.Name))
                    {
                        try { proc.Kill(); proc.WaitForExit(); }
                        catch (Exception ex) { Console.WriteLine($"Erreur lors de la fermeture de {prog.Name}: {ex.Message}"); }
                    }
                }

                // Redémarrage des services
                ShowLoadingScreen();
                await Task.Run(() => RestartServicesWithUI(serviceStatusPanel));

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
                        Console.WriteLine($"Erreur lors du démarrage de {prog.Name}: {ex.Message}");
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

                await Task.Delay(3000); // Pause de 2 secondes pour afficher le message

                Application.Exit();
            };
            this.Controls.Add(yesButton);

            noButton = new Button
            {
                Text = "No",
                Location = new System.Drawing.Point(380, 320),
                Size = new System.Drawing.Size(75, 30)
            };
            noButton.Click += (sender, e) => this.Close();
            this.Controls.Add(noButton);
        }

        private List<AppsInfo> LoadProgramsFromJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show("File Apps.json not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return new List<AppsInfo>();
            }

            string jsonContent = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<AppsInfo>>(jsonContent);
        }

        private List<AppsInfo> GetActiveApplications(List<AppsInfo> programList)
        {
            List<AppsInfo> active = new List<AppsInfo>();
            Process[] allProcesses = Process.GetProcesses();

            foreach (var prog in programList)
            {
                foreach (var proc in allProcesses)
                {
                    try
                    {
                        if (proc.ProcessName.Equals(prog.Name, StringComparison.OrdinalIgnoreCase))
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

        private void ShowLoadingScreen()
        {

            this.Controls.Clear();

            loadingLabel = new Label
            {
                Text = "Redémarrage en cours...",
                AutoSize = true,
                Font = new Font("Segoe UI", 14),
                Location = new Point(140, 20)
            };
            this.Controls.Add(loadingLabel);

            loadingBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Size = new Size(200, 30),
                Location = new Point(140, 60)
            };
            this.Controls.Add(loadingBar);

            serviceStatusPanel = new FlowLayoutPanel
            {
                Location = new Point(20, 110),
                Size = new Size(440, 300),
                AutoScroll = true
            };
            this.Controls.Add(serviceStatusPanel);
        }

        private List<ServiceInfo> LoadServicesFromJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("File services.json not found.");
                return new List<ServiceInfo>();
            }

            string jsonContent = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<ServiceInfo>>(jsonContent);
        }

        private async Task RestartServicesWithUI(FlowLayoutPanel uiPanel)
        {
            var servicesToRestart = LoadServicesFromJson("services.json");
            Dictionary<string, PictureBox> statusIcons = new();

            // Étape 1 : affichage initial avec icônes vertes ou rouges
            foreach (var serviceInfo in servicesToRestart)
            {
                try
                {
                    using (ServiceController service = new ServiceController(serviceInfo.Name))
                    {
                        bool isRunning = service.Status == ServiceControllerStatus.Running;

                        // Crée une ligne avec l'icône + nom
                        Panel entryPanel = new Panel { Size = new Size(400, 30), Margin = new Padding(5) };

                        PictureBox icon = new PictureBox
                        {
                            Size = new Size(20, 20),
                            Location = new Point(0, 5),
                            BackColor = isRunning ? Color.Green : Color.Red
                        };
                        Label label = new Label
                        {
                            Text = service.ServiceName,
                            Location = new Point(30, 5),
                            AutoSize = true
                        };

                        entryPanel.Controls.Add(icon);
                        entryPanel.Controls.Add(label);

                        this.Invoke(() => uiPanel.Controls.Add(entryPanel));
                        statusIcons[service.ServiceName] = icon;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lecture état initial : {serviceInfo.Name} - {ex.Message}");
                }
            }

            // Étape 2 : redémarrage réel + mise à jour dynamique
            foreach (var serviceInfo in servicesToRestart)
            {
                try
                {
                    using (ServiceController service = new ServiceController(serviceInfo.Name))
                    {
                        PictureBox icon = statusIcons[service.ServiceName];

                        if (service.Status == ServiceControllerStatus.Running)
                        {
                            service.Stop();
                            icon.Invoke(() => icon.BackColor = Color.Red); // Rouge : arrêt en cours
                            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
                        }

                        await Task.Delay(1000); // Petite pause
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
                        icon.Invoke(() => icon.BackColor = Color.Green); // Vert : redémarré
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur redémarrage : {serviceInfo.Name} - {ex.Message}");
                    if (statusIcons.TryGetValue(serviceInfo.Name, out var icon))
                        icon.Invoke(() => icon.BackColor = Color.Red);
                }
            }
        }

    }
}
