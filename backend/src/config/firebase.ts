import admin from 'firebase-admin';
import { Firestore } from '@google-cloud/firestore';
import dotenv from 'dotenv';

dotenv.config();

let firebaseApp: admin.app.App | null = null;
let firestoreDb: Firestore | null = null;

/**
 * Initialize Firebase Admin SDK
 */
export function initializeFirebase(): admin.app.App {
  if (firebaseApp) {
    return firebaseApp;
  }

  try {
    // Check for credentials in environment variable (JSON string for Render deployment)
    const credentialsJson = process.env.GOOGLE_APPLICATION_CREDENTIALS_JSON;
    
    if (credentialsJson) {
      const serviceAccount = JSON.parse(credentialsJson);
      firebaseApp = admin.initializeApp({
        credential: admin.credential.cert(serviceAccount),
        projectId: process.env.FIRESTORE_PROJECT_ID || 'examforge-201e8',
      });
      console.log('✅ Firebase initialized from environment credentials');
    } 
    // Fallback to file-based credentials (local development)
    else if (process.env.GOOGLE_APPLICATION_CREDENTIALS) {
      firebaseApp = admin.initializeApp({
        credential: admin.credential.applicationDefault(),
        projectId: process.env.FIRESTORE_PROJECT_ID || 'examforge-201e8',
      });
      console.log('✅ Firebase initialized from file credentials');
    } 
    else {
      throw new Error('No Firebase credentials found. Set GOOGLE_APPLICATION_CREDENTIALS_JSON or GOOGLE_APPLICATION_CREDENTIALS');
    }

    return firebaseApp;
  } catch (error) {
    console.error('❌ Failed to initialize Firebase:', error);
    throw error;
  }
}

/**
 * Get Firestore database instance
 */
export function getFirestore(): Firestore {
  if (firestoreDb) {
    return firestoreDb;
  }

  try {
    const app = initializeFirebase();
    firestoreDb = app.firestore();
    
    // Configure Firestore settings
    firestoreDb.settings({
      ignoreUndefinedProperties: true,
    });

    console.log('✅ Firestore database initialized');
    return firestoreDb;
  } catch (error) {
    console.error('❌ Failed to initialize Firestore:', error);
    throw error;
  }
}

/**
 * Get Firebase Auth instance
 */
export function getAuth(): admin.auth.Auth {
  const app = initializeFirebase();
  return app.auth();
}

export { admin };
