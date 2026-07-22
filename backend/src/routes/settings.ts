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
    const userId = req.user!.userId;
    console.log(`[Settings GET] userId=${userId}`);
    const doc = await db.collection('examforge_users').doc(userId).get();
    const rawData = doc.data();
    const settings = rawData?.settings;
    const rawKey = settings?.apiKey || '';
    console.log(`[Settings GET] keyFound=${!!rawKey} keyLength=${rawKey.length}`);
    const masked = rawKey
      ? rawKey.slice(0, 8) + '••••' + rawKey.slice(-4)
      : '';
    console.log(`[Settings GET] returning masked=${!!masked}`);
    res.json({ apiKey: masked, model: settings?.model || 'deepseek/deepseek-chat' });
  } catch (error) {
    console.error('[Settings GET] Error:', error);
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
    const userId = req.user!.userId;

    console.log(`[Settings PUT] userId=${userId} apiKeyReceived=${!!apiKey} apiKeyLength=${apiKey?.length || 0} model=${model}`);

    const updateData: Record<string, unknown> = { updatedAt: new Date() };
    if (apiKey !== undefined && typeof apiKey === 'string') {
      updateData['settings.apiKey'] = apiKey;
      console.log(`[Settings PUT] Saving apiKey, first 8 chars: ${apiKey.slice(0, 8)}`);
    }
    if (model !== undefined) {
      updateData['settings.model'] = model;
    }

    await db.collection('examforge_users').doc(userId).update(updateData);

    console.log('[Settings PUT] Save successful');
    res.json({ success: true });
  } catch (error) {
    console.error('[Settings PUT] Error:', error);
    res.status(500).json({ error: 'Failed to save settings' });
  }
});

export default router;
