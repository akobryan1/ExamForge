import express, { Router, Request, Response } from 'express';
import { param, query } from 'express-validator';
import { NotificationService } from '../services/NotificationService';
import { authenticate } from '../middleware/auth';

const router: Router = express.Router();

// All notification routes require authentication
router.use(authenticate);

/**
 * GET /api/notifications - Get user's notifications
 */
router.get('/', async (req: Request, res: Response) => {
  try {
    const limit = req.query.limit ? parseInt(req.query.limit as string) : 50;
    const notifications = await NotificationService.getUserNotifications(req.user!.userId, limit);
    return res.json(notifications);
  } catch (error: any) {
    return res.status(500).json({ error: error.message });
  }
});

/**
 * GET /api/notifications/unread-count - Get unread notification count
 */
router.get('/unread-count', async (req: Request, res: Response) => {
  try {
    const count = await NotificationService.getUnreadCount(req.user!.userId);
    return res.json({ count });
  } catch (error: any) {
    return res.status(500).json({ error: error.message });
  }
});

/**
 * PUT /api/notifications/:id/read - Mark notification as read
 */
router.put('/:id/read', async (req: Request, res: Response) => {
  try {
    await NotificationService.markAsRead(req.user!.userId, req.params.id);
    return res.json({ message: 'Notification marked as read' });
  } catch (error: any) {
    return res.status(500).json({ error: error.message });
  }
});

/**
 * PUT /api/notifications/read-all - Mark all notifications as read
 */
router.put('/read-all', async (req: Request, res: Response) => {
  try {
    await NotificationService.markAllAsRead(req.user!.userId);
    return res.json({ message: 'All notifications marked as read' });
  } catch (error: any) {
    return res.status(500).json({ error: error.message });
  }
});

/**
 * DELETE /api/notifications/:id - Delete a notification
 */
router.delete('/:id', async (req: Request, res: Response) => {
  try {
    await NotificationService.deleteNotification(req.user!.userId, req.params.id);
    return res.json({ message: 'Notification deleted' });
  } catch (error: any) {
    return res.status(500).json({ error: error.message });
  }
});

/**
 * DELETE /api/notifications/read - Delete all read notifications
 */
router.delete('/read/all', async (req: Request, res: Response) => {
  try {
    await NotificationService.deleteAllRead(req.user!.userId);
    return res.json({ message: 'Read notifications deleted' });
  } catch (error: any) {
    return res.status(500).json({ error: error.message });
  }
});

export default router;
