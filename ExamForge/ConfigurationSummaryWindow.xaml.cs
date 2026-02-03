using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ExamForge
{
    public partial class ConfigurationSummaryWindow : Window
    {
        public bool ProceedConfirmed { get; private set; } = false;

        public ConfigurationSummaryWindow(ExamConfiguration config)
        {
            InitializeComponent();
            DataContext = config;
            LoadSummary(config);
        }

        private void LoadSummary(ExamConfiguration config)
        {
            // Exam Structure Summary
            if (config.DataGridState is DataGridState dataGridState && dataGridState.Rows.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Total Rows Configured: {dataGridState.Rows.Count}");
                sb.AppendLine();
                
                int validRows = 0;
                foreach (var row in dataGridState.Rows)
                {
                    bool isComplete = !string.IsNullOrWhiteSpace(row.TestGroup) &&
                                     !string.IsNullOrWhiteSpace(row.TestType) &&
                                     !string.IsNullOrWhiteSpace(row.Start) &&
                                     !string.IsNullOrWhiteSpace(row.End) &&
                                     !string.IsNullOrWhiteSpace(row.Points);
                    
                    if (isComplete)
                    {
                        validRows++;
                        sb.AppendLine($"• Test {row.TestGroup}: {row.TestType} (Items {row.Start}-{row.End}, {row.Points} points each)");
                    }
                    else
                    {
                        sb.AppendLine($"• Test {row.TestGroup}: ⚠️ Incomplete configuration");
                    }
                }
                
                ExamStructureText.Text = sb.ToString();
                
                if (validRows == 0)
                {
                    ExamStructureText.Foreground = (Brush)FindResource("ErrorRed");
                }
                else if (validRows < dataGridState.Rows.Count)
                {
                    ExamStructureText.Foreground = (Brush)FindResource("AccentGold");
                }
                else
                {
                    ExamStructureText.Foreground = (Brush)FindResource("TextPrimary");
                }
            }
            else
            {
                ExamStructureText.Text = "⚠️ Not configured";
                ExamStructureText.Foreground = (Brush)FindResource("ErrorRed");
            }

            // Timing Configuration Summary
            TimingText.Text = config.TimingState != null ? "✓ Configured" : "⚠️ Not configured (optional)";
            TimingText.Foreground = (Brush)FindResource(config.TimingState != null ? "TextPrimary" : "TextSecondary");

            // Anti-Cheat Settings Summary
            AntiCheatText.Text = config.AntiCheatState != null ? "✓ Configured" : "⚠️ Not configured (optional)";
            AntiCheatText.Foreground = (Brush)FindResource(config.AntiCheatState != null ? "TextPrimary" : "TextSecondary");

            // Login Configuration Summary
            LoginConfigText.Text = config.LoginConfigState != null ? "✓ Configured" : "⚠️ Not configured (optional)";
            LoginConfigText.Foreground = (Brush)FindResource(config.LoginConfigState != null ? "TextPrimary" : "TextSecondary");
        }

        private void GoBack_Click(object sender, RoutedEventArgs e)
        {
            ProceedConfirmed = false;
            Close();
        }

        private void Proceed_Click(object sender, RoutedEventArgs e)
        {
            ProceedConfirmed = true;
            Close();
        }
    }
}