# LMS Integration Implementation Guide

## Overview
Integrate with popular Learning Management Systems (Canvas, Blackboard, Moodle) using LTI 1.3.

## Step 1: Install LTI Library

```bash
dotnet add package LtiAdvantage.IdentityServer4
```

## Step 2: Create LMS Integration Service

Add to `ExamForge/Services/LmsIntegrationService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExamForge.Services
{
    public class LmsIntegrationService
    {
        private readonly HttpClient _httpClient;

        public LmsIntegrationService()
        {
            _httpClient = new HttpClient();
        }

        // Canvas LMS Integration
        public async Task<bool> SyncToCanvasAsync(
            string canvasApiUrl,
            string accessToken,
            string courseId,
            ExamSubmission submission)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", accessToken);

                // Create assignment submission
                var submission Payload = new
                {
                    submission = new
                    {
                        user_id = submission.StudentId,
                        submission_type = "online_text_entry",
                        body = $"ExamForge Submission - Score: {submission.TotalScore}/{submission.TotalPossiblePoints}",
                        score = submission.TotalScore,
                        grade = CalculateLetterGrade(submission)
                    }
                };

                var json = JsonSerializer.Serialize(submissionPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{canvasApiUrl}/api/v1/courses/{courseId}/assignments/{submission.ExamId}/submissions",
                    content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Canvas sync failed: {ex.Message}");
                return false;
            }
        }

        // Blackboard Learn Integration
        public async Task<bool> SyncToBlackboardAsync(
            string blackboardUrl,
            string accessToken,
            string courseId,
            ExamSubmission submission)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var gradePayload = new
                {
                    text = CalculateLetterGrade(submission),
                    score = submission.TotalScore,
                    notes = "Grade synced from ExamForge"
                };

                var json = JsonSerializer.Serialize(gradePayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync(
                    $"{blackboardUrl}/learn/api/public/v1/courses/{courseId}/gradebook/columns/{submission.ExamId}/users/{submission.StudentId}",
                    content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blackboard sync failed: {ex.Message}");
                return false;
            }
        }

        // Moodle Integration
        public async Task<bool> SyncToMoodleAsync(
            string moodleUrl,
            string token,
            string courseId,
            ExamSubmission submission)
        {
            try
            {
                var requestUrl = $"{moodleUrl}/webservice/rest/server.php?wstoken={token}" +
                    $"&wsfunction=mod_assign_save_grade" +
                    $"&assignmentid={submission.ExamId}" +
                    $"&userid={submission.StudentId}" +
                    $"&grade={submission.TotalScore}" +
                    $"&moodlewsrestformat=json";

                var response = await _httpClient.PostAsync(requestUrl, null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Moodle sync failed: {ex.Message}");
                return false;
            }
        }

        // Export gradebook in Common Cartridge format
        public async Task<string> ExportCommonCartridgeAsync(
            string examId,
            string examTitle,
            List<ExamSubmission> submissions)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"CommonCartridge_{examTitle}_{timestamp}.xml";
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var exportPath = System.IO.Path.Combine(documentsPath, "ExamForge_Exports");
                System.IO.Directory.CreateDirectory(exportPath);
                var filePath = System.IO.Path.Combine(exportPath, filename);

                // Generate Common Cartridge XML
                var xml = GenerateCommonCartridgeXml(examId, examTitle, submissions);
                await System.IO.File.WriteAllTextAsync(filePath, xml);

                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export Common Cartridge: {ex.Message}", ex);
            }
        }

        // Import roster from LMS
        public async Task<List<StudentRoster>> ImportRosterFromCanvasAsync(
            string canvasApiUrl,
            string accessToken,
            string courseId)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.GetAsync(
                    $"{canvasApiUrl}/api/v1/courses/{courseId}/students");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var students = JsonSerializer.Deserialize<List<CanvasStudent>>(json);
                    
                    return students?.Select(s => new StudentRoster
                    {
                        StudentId = s.id.ToString(),
                        Name = s.name,
                        Email = s.email,
                        SisId = s.sis_user_id
                    }).ToList() ?? new List<StudentRoster>();
                }

                return new List<StudentRoster>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Roster import failed: {ex.Message}");
                return new List<StudentRoster>();
            }
        }

        // SSO Integration
        public async Task<SsoAuthResult> AuthenticateViaSsoAsync(
            string provider,
            string authCode)
        {
            try
            {
                // Implement OAuth2 flow for LMS SSO
                // This is a placeholder - actual implementation depends on LMS
                return new SsoAuthResult
                {
                    Success = false,
                    Message = "SSO not yet configured"
                };
            }
            catch (Exception ex)
            {
                return new SsoAuthResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        private string CalculateLetterGrade(ExamSubmission submission)
        {
            if (submission.TotalPossiblePoints == 0) return "F";
            
            var percentage = (submission.TotalScore / submission.TotalPossiblePoints) * 100;
            
            return percentage switch
            {
                >= 90 => "A",
                >= 80 => "B",
                >= 70 => "C",
                >= 60 => "D",
                _ => "F"
            };
        }

        private string GenerateCommonCartridgeXml(
            string examId,
            string examTitle,
            List<ExamSubmission> submissions)
        {
            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<manifest identifier=\"ExamForge_" + examId + "\">");
            xml.AppendLine("  <metadata>");
            xml.AppendLine($"    <schema>IMS Common Cartridge</schema>");
            xml.AppendLine($"    <schemaversion>1.3.0</schemaversion>");
            xml.AppendLine($"  </metadata>");
            xml.AppendLine("  <organizations>");
            xml.AppendLine($"    <organization identifier=\"{examId}\">");
            xml.AppendLine($"      <item identifier=\"{examId}_item\" title=\"{examTitle}\">");
            
            foreach (var submission in submissions)
            {
                xml.AppendLine($"        <item identifier=\"{submission.Id}\">");
                xml.AppendLine($"          <title>{submission.StudentName}</title>");
                xml.AppendLine($"          <gradebook>");
                xml.AppendLine($"            <score>{submission.TotalScore}</score>");
                xml.AppendLine($"            <possible>{submission.TotalPossiblePoints}</possible>");
                xml.AppendLine($"          </gradebook>");
                xml.AppendLine($"        </item>");
            }
            
            xml.AppendLine("      </item>");
            xml.AppendLine("    </organization>");
            xml.AppendLine("  </organizations>");
            xml.AppendLine("</manifest>");
            
            return xml.ToString();
        }
    }

    // Supporting classes
    public class StudentRoster
    {
        public string StudentId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string SisId { get; set; } = "";
    }

    public class CanvasStudent
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public string email { get; set; } = "";
        public string sis_user_id { get; set; } = "";
    }

    public class SsoAuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string UserId { get; set; } = "";
        public string AccessToken { get; set; } = "";
    }
}
```

## Step 3: Create LMS Settings Dialog

Create `ExamForge/Views/LmsSettingsDialog.xaml`:

```xaml
<Window x:Class="ExamForge.Views.LmsSettingsDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LMS Integration Settings"
        Height="600" Width="700"
        WindowStartupLocation="CenterOwner"
        Background="{StaticResource AppBackgroundBrush}">
    
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <Border Grid.Row="0" Background="{StaticResource CardBackgroundBrush}" 
                Padding="25,20" BorderBrush="{StaticResource BorderBrushLight}" 
                BorderThickness="0,0,0,1">
            <StackPanel>
                <TextBlock Text="LMS Integration Settings" FontSize="20" 
                           FontWeight="SemiBold" Foreground="{StaticResource TextPrimaryBrush}"/>
                <TextBlock Text="Configure integration with your Learning Management System" 
                           FontSize="13" Foreground="{StaticResource TextSecondaryBrush}" 
                           Margin="0,5,0,0"/>
            </StackPanel>
        </Border>

        <!-- Content -->
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" Padding="25,20">
            <StackPanel>
                <!-- LMS Provider Selection -->
                <TextBlock Text="LMS Provider" FontSize="14" FontWeight="SemiBold" 
                           Margin="0,0,0,10"/>
                <ComboBox x:Name="LmsProviderCombo" Height="35" 
                          SelectionChanged="LmsProvider_Changed">
                    <ComboBoxItem Content="Canvas LMS" IsSelected="True"/>
                    <ComboBoxItem Content="Blackboard Learn"/>
                    <ComboBoxItem Content="Moodle"/>
                    <ComboBoxItem Content="Google Classroom"/>
                    <ComboBoxItem Content="Microsoft Teams"/>
                </ComboBox>

                <!-- API Configuration -->
                <TextBlock Text="API Configuration" FontSize="14" FontWeight="SemiBold" 
                           Margin="0,20,0,10"/>
                
                <TextBlock Text="API URL:" FontSize="12" Margin="0,0,0,5"/>
                <TextBox x:Name="ApiUrlTextBox" Height="35" Padding="10,8"/>
                
                <TextBlock Text="Access Token:" FontSize="12" Margin="0,10,0,5"/>
                <PasswordBox x:Name="AccessTokenBox" Height="35" Padding="10,8"/>
                
                <TextBlock Text="Course ID:" FontSize="12" Margin="0,10,0,5"/>
                <TextBox x:Name="CourseIdTextBox" Height="35" Padding="10,8"/>

                <!-- Sync Options -->
                <TextBlock Text="Sync Options" FontSize="14" FontWeight="SemiBold" 
                           Margin="0,20,0,10"/>
                
                <CheckBox x:Name="AutoSyncCheckBox" Content="Auto-sync grades after submission" 
                          IsChecked="True" Margin="0,0,0,10"/>
                <CheckBox x:Name="ImportRosterCheckBox" Content="Import student roster from LMS" 
                          Margin="0,0,0,10"/>
                <CheckBox x:Name="SsoEnabledCheckBox" Content="Enable SSO authentication" 
                          Margin="0,0,0,10"/>

                <!-- Test Connection -->
                <Button Content="Test Connection" Width="150" Height="35" Margin="0,20,0,0"
                        HorizontalAlignment="Left" Click="TestConnection_Click"
                        Style="{StaticResource SecondaryButtonStyle}"/>
                
                <TextBlock x:Name="ConnectionStatusText" FontSize="12" 
                           Margin="0,10,0,0" Foreground="{StaticResource TextMutedBrush}"/>
            </StackPanel>
        </ScrollViewer>

        <!-- Footer -->
        <Border Grid.Row="2" Background="{StaticResource CardBackgroundBrush}" 
                BorderBrush="{StaticResource BorderBrushLight}" 
                BorderThickness="0,1,0,0" Padding="25,15">
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                <Button Content="Cancel" Width="100" Height="35" Margin="0,0,10,0"
                        Style="{StaticResource SecondaryButtonStyle}" 
                        IsCancel="True" Click="Cancel_Click"/>
                <Button Content="Save Settings" Width="130" Height="35" 
                        Style="{StaticResource PrimaryButtonStyle}" 
                        Click="SaveSettings_Click"/>
            </StackPanel>
        </Border>
    </Grid>
</Window>
```

## Step 4: Add Sync Buttons

In `published_exams_usercontrol.xaml`, add sync buttons:

```xaml
<Button Content="?? Sync to LMS" Margin="16,0,0,0" Padding="12,6" 
        Background="{StaticResource AccentBlue}" 
        Foreground="White" BorderThickness="0" 
        Style="{StaticResource RoundedButtonStyle}" 
        Click="SyncToLms_Click"/>
```

## Implementation Time: 8-12 hours
## Complexity: Very High
## Benefits: Seamless integration with existing LMS platforms
## Dependencies: LMS API access, OAuth tokens
## Note: Each LMS has different API specifications - implement adapters as needed
