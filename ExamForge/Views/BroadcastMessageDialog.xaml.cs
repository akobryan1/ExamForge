using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ExamForge.Models;
using ExamForge.Services;

namespace ExamForge.Views;

public partial class BroadcastMessageDialog : Window
{
    private List<ExamSession> _sessions;
    private SignalRService? _signalRService;
    public bool MessageSent { get; private set; }

    public BroadcastMessageDialog(List<ExamSession> sessions, SignalRService? signalRService)
    {
        InitializeComponent();
        _sessions = sessions;
        _signalRService = signalRService;
        
        SessionInfoText.Text = $"Broadcasting to {sessions.Count} active session(s)";
        MessageTextBox.TextChanged += MessageTextBox_TextChanged;
    }

    private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        int remaining = 500 - MessageTextBox.Text.Length;
        CharCountText.Text = remaining.ToString();
    }

    private async void SendMessage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var message = MessageTextBox.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Please enter a message to broadcast.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Send this message to all {_sessions.Count} active session(s)?", 
                "Confirm Broadcast", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes) return;

            // Disable button and show progress
            var sendButton = sender as Button;
            if (sendButton != null)
            {
                sendButton.IsEnabled = false;
                sendButton.Content = "Sending...";
            }

            int successCount = 0;
            
            if (_signalRService != null)
            {
                if (!_signalRService.IsConnected)
                {
                    await _signalRService.ConnectAsync();
                }

                foreach (var session in _sessions)
                {
                    try
                    {
                        await _signalRService.BroadcastMessageAsync(session.Id, $"broadcast:{message}");
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to broadcast to session {session.Id}: {ex.Message}");
                    }
                }
            }

            MessageSent = true;
            MessageBox.Show(
                $"Message broadcasted successfully to {successCount} of {_sessions.Count} session(s)!", 
                "Broadcast Complete", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
            
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to broadcast message: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
