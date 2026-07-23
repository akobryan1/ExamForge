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

    // Check for env var first (overrides Firestore)
    const envKey = process.env.OPENROUTER_API_KEY || '';
    if (envKey) {
      console.log(`[Settings GET] Using env var key, length=${envKey.length}`);
      const masked = envKey.slice(0, 8) + '••••' + envKey.slice(-4);
      res.json({ apiKey: masked, model: process.env.AI_MODEL || 'deepseek/deepseek-chat' });
      return;
    }

    // Prevent any caching of settings response
    res.set('Cache-Control', 'no-store, no-cache, must-revalidate, proxy-revalidate');
    res.set('Pragma', 'no-cache');
    res.set('Expires', '0');

    const doc = await db.collection('examforge_users').doc(userId).get();
    const rawData = doc.data();
    const settings = rawData?.settings;
    const rawKey = settings?.apiKey || '';
    console.log(`[Settings GET] keyFound=${!!rawKey} keyLength=${rawKey.length}, first 8: "${rawKey.slice(0, 8)}"`);
    const masked = rawKey
      ? rawKey.slice(0, 8) + '••••' + rawKey.slice(-4)
      : '';
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
    console.log('[Settings PUT] RAW wire body:', (req as any).rawBody || 'N/A');
    console.log('[Settings PUT] Parsed req.body:', JSON.stringify(req.body));
    const { apiKey, model } = req.body;
    const { getFirestore } = await import('firebase-admin/firestore');
    const db = getFirestore();
    const userId = req.user!.userId;

    console.log(`[Settings PUT] userId=${userId} apiKeyReceived=${!!apiKey} apiKeyLength=${apiKey?.length || 0} model=${model}`);
    if (apiKey) {
      console.log(`[Settings PUT] first 8 chars: "${apiKey.slice(0, 8)}" last 4: "${apiKey.slice(-4)}"`);
    }

    const settingsData: Record<string, unknown> = {};
    if (apiKey !== undefined && typeof apiKey === 'string') {
      settingsData.apiKey = apiKey;
    }
    if (model !== undefined) {
      settingsData.model = model;
    }

    await db.collection('examforge_users').doc(userId).set(
      { settings: settingsData, updatedAt: new Date() },
      { merge: true }
    );

    console.log('[Settings PUT] Save successful');
    // Verify by reading back immediately
    const verify = await db.collection('examforge_users').doc(userId).get();
    const savedKey = verify.data()?.settings?.apiKey || '';
    console.log(`[Settings PUT] Verification read: keyLength=${savedKey.length}, first 8: "${savedKey.slice(0, 8)}"`);

    res.json({ success: true, savedKeyPrefix: savedKey.slice(0, 8), savedKeyLength: savedKey.length });
  } catch (error) {
    console.error('[Settings PUT] Error:', error);
    res.status(500).json({ error: 'Failed to save settings' });
  }
});

/**
 * GET /debug/api-key — Debug: show current saved API key info
 */
router.get('/debug/api-key', authenticate, async (req: Request, res: Response) => {
  try {
    const { getFirestore } = await import('firebase-admin/firestore');
    const db = getFirestore();
    const doc = await db.collection('examforge_users').doc(req.user!.userId).get();
    const rawKey = doc.data()?.settings?.apiKey || '';
    res.json({
      exists: !!rawKey,
      length: rawKey.length,
      prefix: rawKey.slice(0, 10),
      suffix: rawKey.slice(-6),
    });
  } catch (error) {
    res.status(500).json({ error: 'Debug error' });
  }
});

export default router;
