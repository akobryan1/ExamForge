import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { NotificationService, Notification } from '../services/NotificationService';
import '../styles/components/notification-bell.css';

export function NotificationBell() {
  const navigate = useNavigate();
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [showDropdown, setShowDropdown] = useState(false);
  const [loading, setLoading] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // Only fetch for authenticated users
    const token = localStorage.getItem('accessToken');
    if (!token) return;
    
    loadUnreadCount();
    
    // Poll for new notifications every 30 seconds
    const interval = setInterval(loadUnreadCount, 30000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    // Close dropdown when clicking outside
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setShowDropdown(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const loadUnreadCount = async () => {
    try {
      const count = await NotificationService.getUnreadCount();
      setUnreadCount(count);
    } catch (err) {
      console.error('Failed to load unread count:', err);
    }
  };

  const loadNotifications = async () => {
    setLoading(true);
    try {
      const data = await NotificationService.getNotifications(10);
      setNotifications(data);
    } catch (err) {
      console.error('Failed to load notifications:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleBellClick = () => {
    if (!showDropdown) {
      loadNotifications();
    }
    setShowDropdown(!showDropdown);
  };

  const handleNotificationClick = async (notification: Notification) => {
    try {
      if (!notification.read) {
        await NotificationService.markAsRead(notification.id);
        await loadUnreadCount();
        setNotifications(prev =>
          prev.map(n => n.id === notification.id ? { ...n, read: true } : n)
        );
      }
      
      setShowDropdown(false);
      
      if (notification.link) {
        navigate(notification.link);
      }
    } catch (err) {
      console.error('Failed to mark notification as read:', err);
    }
  };

  const handleMarkAllRead = async () => {
    try {
      await NotificationService.markAllAsRead();
      await loadUnreadCount();
      await loadNotifications();
    } catch (err) {
      console.error('Failed to mark all as read:', err);
    }
  };

  const handleViewAll = () => {
    setShowDropdown(false);
    navigate('/notifications');
  };

  return (
    <div className="notification-bell-container" ref={dropdownRef}>
      <button className="notification-bell-button" onClick={handleBellClick}>
        <span className="bell-icon">🔔</span>
        {unreadCount > 0 && (
          <span className="notification-badge">{unreadCount > 9 ? '9+' : unreadCount}</span>
        )}
      </button>

      <AnimatePresence>
        {showDropdown && (
          <motion.div
            initial={{ opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
            transition={{ duration: 0.2 }}
            className="notification-dropdown"
          >
            <div className="dropdown-header">
              <h3>Notifications</h3>
              {unreadCount > 0 && (
                <button className="mark-all-read" onClick={handleMarkAllRead}>
                  Mark all read
                </button>
              )}
            </div>

            <div className="notifications-list">
              {loading ? (
                <div className="notification-loading">Loading...</div>
              ) : notifications.length === 0 ? (
                <div className="notification-empty">No notifications</div>
              ) : (
                notifications.map(notification => (
                  <div
                    key={notification.id}
                    className={`notification-item ${notification.read ? 'read' : 'unread'}`}
                    onClick={() => handleNotificationClick(notification)}
                  >
                    <div className="notification-icon">
                      {(() => {
                        const icon = NotificationService.getNotificationIcon(notification.type);
                        return icon.startsWith('/')
                          ? <img src={icon} alt="" className="inline-icon" />
                          : icon;
                      })()}
                    </div>
                    <div className="notification-content">
                      <div className="notification-title">{notification.title}</div>
                      <div className="notification-message">{notification.message}</div>
                      <div className="notification-time">
                        {NotificationService.formatNotificationTime(notification.createdAt)}
                      </div>
                    </div>
                    {!notification.read && <div className="unread-dot" />}
                  </div>
                ))
              )}
            </div>

            {notifications.length > 0 && (
              <div className="dropdown-footer">
                <button onClick={handleViewAll}>View all notifications</button>
              </div>
            )}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
