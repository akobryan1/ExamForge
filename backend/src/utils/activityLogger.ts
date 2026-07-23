import { getFirestore } from '../config/firebase';
import { Timestamp } from '@google-cloud/firestore';

export type ActivityAction =
  | 'exam.created'
  | 'exam.updated'
  | 'exam.published'
  | 'exam.completed'
  | 'exam.archived'
  | 'exam.deleted'
  | 'exam.cloned'
  | 'exam.republished'
  | 'exam.guest_access'
  | 'question.created'
  | 'question.updated'
  | 'question.deleted'
  | 'grade.submitted'
  | 'grade.ai_graded'
  | 'incident.archived'
  | 'incident.unarchived'
  | 'incident.deleted'
  | 'student.registered'
  | 'settings.updated';

interface ActivityEntry {
  action: ActivityAction;
  description: string;
  metadata?: Record<string, unknown>;
  createdAt: FirebaseFirestore.Timestamp;
}

/**
 * Log an activity entry for a user.
 * Stores in Firestore subcollection: examforge_users/{userId}/activity_log/{logId}
 * Automatically prunes to keep only the last 500 entries.
 */
export async function logActivity(
  userId: string,
  action: ActivityAction,
  description: string,
  metadata?: Record<string, unknown>
): Promise<void> {
  try {
    const db = getFirestore();
    const logsRef = db
      .collection('examforge_users')
      .doc(userId)
      .collection('activity_log');

    const entry: ActivityEntry = {
      action,
      description,
      metadata: metadata || {},
      createdAt: Timestamp.now(),
    };

    await logsRef.add(entry);

    // Best-effort pruning: keep only last 500 entries
    const countSnapshot = await logsRef.count().get();
    const total = countSnapshot.data().count;
    if (total > 500) {
      const toDelete = total - 500;
      const oldEntries = await logsRef
        .orderBy('createdAt', 'asc')
        .limit(toDelete)
        .get();
      const batch = db.batch();
      oldEntries.docs.forEach((doc) => batch.delete(doc.ref));
      await batch.commit();
    }
  } catch (error) {
    console.error('[ActivityLogger] Failed to log activity:', error);
    // Never throw — logging is best-effort
  }
}
