import { getFirestore } from '../config/firebase';
import { Timestamp } from '@google-cloud/firestore';

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

export class NotificationService {
  /**
   * Create a notification for a user
   */
  static async createNotification(
    userId: string,
    type: Notification['type'],
    title: string,
    message: string,
    link?: string,
    metadata?: Record<string, any>
  ): Promise<Notification> {
    const db = getFirestore();

    const notificationData = {
      userId,
      type,
      title,
      message,
      link,
      read: false,
      createdAt: Timestamp.now(),
      metadata: metadata || {},
    };

    const docRef = await db.collection(`examforge_users/${userId}/notifications`).add(notificationData);

    return {
      id: docRef.id,
      ...notificationData,
      createdAt: new Date(),
    };
  }

  /**
   * Get user's notifications
   */
  static async getUserNotifications(userId: string, limit: number = 50): Promise<Notification[]> {
    const db = getFirestore();

    const snapshot = await db
      .collection(`examforge_users/${userId}/notifications`)
      .orderBy('createdAt', 'desc')
      .limit(limit)
      .get();

    return snapshot.docs.map(doc => ({
      id: doc.id,
      ...doc.data(),
      createdAt: doc.data().createdAt.toDate(),
    })) as Notification[];
  }

  /**
   * Get unread notification count
   */
  static async getUnreadCount(userId: string): Promise<number> {
    const db = getFirestore();

    const snapshot = await db
      .collection(`examforge_users/${userId}/notifications`)
      .where('read', '==', false)
      .get();

    return snapshot.size;
  }

  /**
   * Mark notification as read
   */
  static async markAsRead(userId: string, notificationId: string): Promise<void> {
    const db = getFirestore();

    await db.doc(`examforge_users/${userId}/notifications/${notificationId}`).update({
      read: true,
    });
  }

  /**
   * Mark all notifications as read
   */
  static async markAllAsRead(userId: string): Promise<void> {
    const db = getFirestore();
    const batch = db.batch();

    const snapshot = await db
      .collection(`examforge_users/${userId}/notifications`)
      .where('read', '==', false)
      .get();

    snapshot.docs.forEach(doc => {
      batch.update(doc.ref, { read: true });
    });

    await batch.commit();
  }

  /**
   * Delete a notification
   */
  static async deleteNotification(userId: string, notificationId: string): Promise<void> {
    const db = getFirestore();

    await db.doc(`examforge_users/${userId}/notifications/${notificationId}`).delete();
  }

  /**
   * Delete all read notifications
   */
  static async deleteAllRead(userId: string): Promise<void> {
    const db = getFirestore();
    const batch = db.batch();

    const snapshot = await db
      .collection(`examforge_users/${userId}/notifications`)
      .where('read', '==', true)
      .get();

    snapshot.docs.forEach(doc => {
      batch.delete(doc.ref);
    });

    await batch.commit();
  }

  /**
   * Send retake request notification to instructor
   */
  static async notifyRetakeRequest(
    instructorId: string,
    studentName: string,
    examTitle: string,
    examId: string,
    attemptId: string
  ): Promise<void> {
    await this.createNotification(
      instructorId,
      'retake_request',
      'Retake Request',
      `${studentName} has requested a retake for "${examTitle}"`,
      `/exams/${examId}/retake-requests`,
      { examId, attemptId, studentName }
    );
  }

  /**
   * Send retake decision notification to student
   */
  static async notifyRetakeDecision(
    studentId: string,
    examTitle: string,
    approved: boolean,
    examId: string
  ): Promise<void> {
    await this.createNotification(
      studentId,
      approved ? 'retake_approved' : 'retake_denied',
      approved ? 'Retake Approved' : 'Retake Denied',
      approved
        ? `Your retake request for "${examTitle}" has been approved`
        : `Your retake request for "${examTitle}" has been denied`,
      `/exams/${examId}`,
      { examId, approved }
    );
  }

  /**
   * Send grade release notification to student
   */
  static async notifyGradeReleased(
    studentId: string,
    examTitle: string,
    score: number,
    examId: string,
    attemptId: string
  ): Promise<void> {
    await this.createNotification(
      studentId,
      'grade_released',
      'Grade Released',
      `Your grade for "${examTitle}" is now available: ${score}%`,
      `/attempts/${attemptId}/results`,
      { examId, attemptId, score }
    );
  }
}
