import { apiClient } from './apiClient';

export interface Notification {
  id: string;
  userId: string;
  type: 'retake_request' | 'retake_approved' | 'retake_denied' | 'grade_released' | 'exam_available' | 'system';
  title: string;
  message: string;
  link?: string;
  read: boolean;
  createdAt: Date;
  metadata?: Record<string, any>;
}

class NotificationServiceClass {
  /**
   * Get user's notifications
   */
  async getNotifications(limit: number = 50): Promise<Notification[]> {
    const response = await apiClient.get(`/api/notifications?limit=${limit}`);
    return response.data.map((notification: any) => ({
      ...notification,
      createdAt: new Date(notification.createdAt),
    }));
  }

  /**
   * Get unread notification count
   */
  async getUnreadCount(): Promise<number> {
    const response = await apiClient.get('/api/notifications/unread-count');
    return response.data.count;
  }

  /**
   * Mark notification as read
   */
  async markAsRead(notificationId: string): Promise<void> {
    await apiClient.put(`/api/notifications/${notificationId}/read`);
  }

  /**
   * Mark all notifications as read
   */
  async markAllAsRead(): Promise<void> {
    await apiClient.put('/api/notifications/read-all');
  }

  /**
   * Delete a notification
   */
  async deleteNotification(notificationId: string): Promise<void> {
    await apiClient.delete(`/api/notifications/${notificationId}`);
  }

  /**
   * Delete all read notifications
   */
  async deleteAllRead(): Promise<void> {
    await apiClient.delete('/api/notifications/read/all');
  }

  /**
   * Format notification time for display
   */
  formatNotificationTime(date: Date): string {
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    
    return date.toLocaleDateString();
  }

  /**
   * Get notification icon based on type
   */
  getNotificationIcon(type: Notification['type']): string {
    const icons: Record<Notification['type'], string> = {
      retake_request: '🔄',
      retake_approved: '✅',
      retake_denied: '/icons/status/incorrect.png',
      grade_released: '/icons/status/total-attempts.png',
      exam_available: '/icons/nav/exams.png',
      system: 'ℹ️',
    };
    return icons[type] || 'ℹ️';
  }
}

export const NotificationService = new NotificationServiceClass();
