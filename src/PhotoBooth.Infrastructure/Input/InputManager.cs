using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.Input;

/// <summary>
/// Background service that manages input providers and routes triggers to the capture workflow.
/// </summary>
public class InputManager : BackgroundService
{
    private readonly IEnumerable<IInputProvider> _inputProviders;
    private readonly ICaptureWorkflowService _captureWorkflow;
    private readonly ILogger<InputManager> _logger;

    public InputManager(
        IEnumerable<IInputProvider> inputProviders,
        ICaptureWorkflowService captureWorkflow,
        ILogger<InputManager> logger)
    {
        _inputProviders = inputProviders;
        _captureWorkflow = captureWorkflow;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var provider in _inputProviders)
        {
            provider.CaptureTriggered += OnCaptureTriggered;

            try
            {
                await provider.StartAsync(stoppingToken);
                _logger.LogInformation("Input provider '{Name}' started", provider.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start input provider '{Name}'", provider.Name);
            }
        }

        // Keep running until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        // Stop all providers
        foreach (var provider in _inputProviders)
        {
            provider.CaptureTriggered -= OnCaptureTriggered;

            try
            {
                await provider.StopAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping input provider '{Name}'", provider.Name);
            }
        }
    }

    private async void OnCaptureTriggered(object? sender, CaptureTriggeredEventArgs e)
    {
        try
        {
            await _captureWorkflow.TriggerCaptureAsync(e.Source);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling capture trigger from {Source}", e.Source);
        }
    }
}
