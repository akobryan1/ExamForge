import admin from 'firebase-admin';
import { Firestore } from '@google-cloud/firestore';
import dotenv from 'dotenv';

dotenv.config();

let firebaseApp: admin.app.App | null = null;
let firestoreDb: Firestore | null = null;

/**
 * Build a Firebase ServiceAccount from either the JSON env var (recommended for
 * deployments) or the individual FIREBASE_* fields. Returns null when none are set.
 */
function loadServiceAccount(): admin.ServiceAccount | null {
  const credentialsJsonEnv = process.env.GOOGLE_APPLICATION_CREDENTIALS_JSON;
  if (credentialsJsonEnv) {
    return parseServiceAccountJson(credentialsJsonEnv);
  }

  const projectId = process.env.FIREBASE_PROJECT_ID;
  const clientEmail = process.env.FIREBASE_CLIENT_EMAIL;
  const privateKey = process.env.FIREBASE_PRIVATE_KEY;
  if (projectId && clientEmail && privateKey) {
    return {
      projectId,
      clientEmail,
      privateKey: privateKey.replace(/\\n/g, '\n'),
    };
  }

  return null;
}

/**
 * Parse and validate a service-account JSON string. Fails with an actionable message
 * instead of letting an invalid credential surface later as an opaque gRPC error.
 */
function parseServiceAccountJson(raw: string): admin.ServiceAccount {
  let parsed: any;
  try {
    parsed = JSON.parse(raw);
  } catch {
    throw new Error(
      'GOOGLE_APPLICATION_CREDENTIALS_JSON is not valid JSON. ' +
      'Re-paste the entire service-account key on a single line.'
    );
  }

  const missing = ['project_id', 'client_email', 'private_key'].filter((key) => !parsed?.[key]);
  if (missing.length > 0) {
    throw new Error(
      `GOOGLE_APPLICATION_CREDENTIALS_JSON is missing required field(s): ${missing.join(', ')}`
    );
  }

  return {
    projectId: parsed.project_id,
    clientEmail: parsed.client_email,
    privateKey: String(parsed.private_key).replace(/\\n/g, '\n'),
  };
}

/**
 * Initialize Firebase Admin SDK
 */
export function initializeFirebase(): admin.app.App {
  if (firebaseApp) {
    return firebaseApp;
  }

  try {
    // Check for credentials in environment variable (JSON string for Render deployment)
    const serviceAccount = loadServiceAccount();
    
    if (serviceAccount) {
      firebaseApp = admin.initializeApp({
        credential: admin.credential.cert(serviceAccount),
        projectId: process.env.FIRESTORE_PROJECT_ID || serviceAccount.projectId || 'examforge-201e8',
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
      throw new Error(
        'No Firebase credentials found. Set GOOGLE_APPLICATION_CREDENTIALS_JSON (recommended), ' +
        'GOOGLE_APPLICATION_CREDENTIALS, or FIREBASE_PROJECT_ID + FIREBASE_CLIENT_EMAIL + FIREBASE_PRIVATE_KEY'
      );
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
