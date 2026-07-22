import { Router, Request, Response } from 'express';
import { authenticate } from '../middleware/auth';

const router = Router();

/**
 * GET /api/settings — Retrieve user settings
 */
router.get('/', authenticate, async (req: Request, res: Response) => {
  try {
    const { getFirestore } = await import('firebase-admin/firestore');
    const db = getFirestore();
    const doc = await db.collection('examforge_users').doc(req.user!.userId).get();
    const settings = doc.data()?.settings || {
      apiKey: '',
      model: 'openai/gpt-oss-120b:free',
    };
    // Never send the full key back — mask it
    const masked = settings.apiKey
      ? settings.apiKey.slice(0, 8) + '••••' + settings.apiKey.slice(-4)
      : '';
    res.json({ apiKey: masked, model: settings.model || 'openai/gpt-oss-120b:free' });
  } catch (error) {
    res.status(500).json({ error: 'Failed to load settings' });
  }
});

/**
 * PUT /api/settings — Save user settings
 */
router.put('/', authenticate, async (req: Request, res: Response) => {
  try {
    const { apiKey, model } = req.body;
    const { getFirestore } = await import('firebase-admin/firestore');
    const db = getFirestore();

    const settings: Record<string, unknown> = {};
    // Only save apiKey if it's a real key (not the masked placeholder length)
    if (apiKey !== undefined && typeof apiKey === 'string' && apiKey.length > 10) {
      settings.apiKey = apiKey;
    }
    if (model !== undefined) settings.model = model;

    await db.collection('examforge_users').doc(req.user!.userId).set(
      { settings, updatedAt: new Date() },
      { merge: true }
    );

    res.json({ success: true });
  } catch (error) {
    console.error('[Settings] PUT error:', error instanceof Error ? error.message : error);
    console.error('[Settings] PUT error stack:', error instanceof Error ? error.stack : '');
    res.status(500).json({ error: 'Failed to save settings' });
  }
});

export default router;
