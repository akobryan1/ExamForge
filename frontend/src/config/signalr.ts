import * as signalR from '@microsoft/signalr';

const SIGNALR_HUB_URL = import.meta.env.VITE_SIGNALR_HUB_URL;

if (!SIGNALR_HUB_URL) {
  throw new Error('Missing VITE_SIGNALR_HUB_URL environment variable');
}

/**
 * Create SignalR connection
 */
export function createSignalRConnection(): signalR.HubConnection {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(SIGNALR_HUB_URL)
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        // Exponential backoff: 0s, 2s, 10s, 30s, then 30s
        if (retryContext.previousRetryCount === 0) return 0;
        if (retryContext.previousRetryCount === 1) return 2000;
        if (retryContext.previousRetryCount === 2) return 10000;
        return 30000;
      },
    })
    .configureLogging(signalR.LogLevel.Information)
    .build();

  // Connection event handlers
  connection.onreconnecting((error) => {
    console.warn('⚠️ SignalR reconnecting...', error);
  });

  connection.onreconnected((connectionId) => {
    console.log('✅ SignalR reconnected:', connectionId);
  });

  connection.onclose((error) => {
    console.error('❌ SignalR connection closed:', error);
  });

  return connection;
}

export { signalR };
