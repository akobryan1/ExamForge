import { Router, Request, Response } from 'express';
import { authenticate } from '../middleware/auth';

const router = Router();

/**
 * GET /api/activity — Get paginated activity log for the current user
 * Query params:
 *   - limit (number, default 50, max 100)
 *   - cursor (string, optional — last document ID for pagination)
 */
router.get('/', authenticate, async (req: Request, res: Response) => {
  try {
    const { getFirestore } = await import('firebase-admin/firestore');
    const db = getFirestore();
    const userId = req.user!.userId;

    const limit = Math.min(Math.max(parseInt(req.query.limit as string) || 50, 1), 100);
    const cursor = req.query.cursor as string | undefined;

    const logsRef = db
      .collection('examforge_users')
      .doc(userId)
      .collection('activity_log')
      .orderBy('createdAt', 'desc')
      .limit(limit);

    let snapshot;
    if (cursor) {
      const cursorDoc = await db
        .collection('examforge_users')
        .doc(userId)
        .collection('activity_log')
        .doc(cursor)
        .get();
      if (cursorDoc.exists) {
        snapshot = await logsRef.startAfter(cursorDoc).get();
      } else {
        snapshot = await logsRef.get();
      }
    } else {
      snapshot = await logsRef.get();
    }

    const entries = snapshot.docs.map((doc) => {
      const data = doc.data();
      return {
        id: doc.id,
        action: data.action,
        description: data.description,
        metadata: data.metadata || {},
        createdAt: data.createdAt?.toDate?.()?.toISOString() || data.createdAt,
      };
    });

    const lastDoc = snapshot.docs[snapshot.docs.length - 1];

    res.json({
      entries,
      nextCursor: lastDoc?.id || null,
      hasMore: snapshot.docs.length === limit,
    });
  } catch (error) {
    console.error('[Activity] GET error:', error);
    res.status(500).json({ error: 'Failed to load activity log' });
  }
});

export default router;
