// Cloudflare Worker for ExamForge API - SECURED VERSION

const corsHeaders = {
  'Access-Control-Allow-Origin': 'https://examforge-201e8.web.app',
  'Access-Control-Allow-Methods': 'POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type',
};

// Handle OPTIONS request for CORS
function handleOptions(request) {
  return new Response(null, {
    headers: corsHeaders
  });
}

// Get OAuth2 access token from service account
async function getAccessToken(serviceAccount) {
  const jwtHeader = btoa(JSON.stringify({ alg: 'RS256', typ: 'JWT' }));
  
  const now = Math.floor(Date.now() / 1000);
  const jwtClaimSet = {
    iss: serviceAccount.client_email,
    scope: 'https://www.googleapis.com/auth/datastore',
    aud: 'https://oauth2.googleapis.com/token',
    exp: now + 3600,
    iat: now
  };
  
  const jwtClaimSetEncoded = btoa(JSON.stringify(jwtClaimSet));
  const signatureInput = `${jwtHeader}.${jwtClaimSetEncoded}`;
  
  const privateKey = await crypto.subtle.importKey(
    'pkcs8',
    pemToArrayBuffer(serviceAccount.private_key),
    {
      name: 'RSASSA-PKCS1-v1_5',
      hash: 'SHA-256',
    },
    false,
    ['sign']
  );
  
  const signature = await crypto.subtle.sign(
    'RSASSA-PKCS1-v1_5',
    privateKey,
    new TextEncoder().encode(signatureInput)
  );
  
  const signatureEncoded = btoa(String.fromCharCode(...new Uint8Array(signature)))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=/g, '');
  
  const jwt = `${signatureInput}.${signatureEncoded}`;
  
  const tokenResponse = await fetch('https://oauth2.googleapis.com/token', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
    },
    body: `grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer&assertion=${jwt}`
  });
  
  const tokenData = await tokenResponse.json();
  return tokenData.access_token;
}

function pemToArrayBuffer(pem) {
  const b64 = pem
    .replace(/-----BEGIN PRIVATE KEY-----/, '')
    .replace(/-----END PRIVATE KEY-----/, '')
    .replace(/\s/g, '');
  const binary = atob(b64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes.buffer;
}

async function submitExam(request, env) {
  try {
    const submission = await request.json();
    
    if (!submission.examId || !submission.answers || !submission.studentEmail) {
      return new Response(JSON.stringify({ error: 'Missing required fields' }), {
        status: 400,
        headers: { ...corsHeaders, 'Content-Type': 'application/json' }
      });
    }

    const serviceAccount = JSON.parse(env.SERVICE_ACCOUNT_JSON);
    const accessToken = await getAccessToken(serviceAccount);

    const submissionData = {
      id: crypto.randomUUID(),
      examId: submission.examId,
      answers: submission.answers,
      submittedAt: new Date().toISOString(),
      timeSpent: submission.timeSpent || 0,
      studentEmail: submission.studentEmail,
      studentName: submission.studentName || 'Anonymous'
    };

    // ✅ FIXED: Now uses examinee_data collection
    const firestoreUrl = `https://firestore.googleapis.com/v1/projects/${env.PROJECT_ID}/databases/(default)/documents/examinee_data?documentId=${submissionData.id}`;
    
    const firestoreResponse = await fetch(firestoreUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${accessToken}`
      },
      body: JSON.stringify({
        fields: {
          id: { stringValue: submissionData.id },
          examId: { stringValue: submissionData.examId },
          answers: { mapValue: { fields: convertToFirestoreMap(submissionData.answers) } },
          submittedAt: { timestampValue: submissionData.submittedAt },
          timeSpent: { integerValue: submissionData.timeSpent.toString() },
          studentEmail: { stringValue: submissionData.studentEmail },
          studentName: { stringValue: submissionData.studentName }
        }
      })
    });

    if (!firestoreResponse.ok) {
      const error = await firestoreResponse.text();
      console.error('Firestore error:', error);
      throw new Error('Failed to save submission');
    }

    return new Response(JSON.stringify({ 
      success: true, 
      submissionId: submissionData.id 
    }), {
      status: 200,
      headers: { ...corsHeaders, 'Content-Type': 'application/json' }
    });

  } catch (error) {
    console.error('Submission error:', error);
    return new Response(JSON.stringify({ 
      error: 'Internal server error',
      message: error.message 
    }), {
      status: 500,
      headers: { ...corsHeaders, 'Content-Type': 'application/json' }
    });
  }
}

function convertToFirestoreMap(obj) {
  const fields = {};
  for (const [key, value] of Object.entries(obj)) {
    fields[key] = { stringValue: value.toString() };
  }
  return fields;
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    // ✅ SECURITY: Verify origin
    const origin = request.headers.get('Origin');
    if (origin && origin !== 'https://examforge-201e8.web.app') {
      return new Response(JSON.stringify({ error: 'Forbidden' }), {
        status: 403,
        headers: { 'Content-Type': 'application/json' }
      });
    }

    if (request.method === 'OPTIONS') {
      return handleOptions(request);
    }

    if (url.pathname === '/api/submit' && request.method === 'POST') {
      return submitExam(request, env);
    }

    return new Response(JSON.stringify({ 
      status: 'ok',
      message: 'ExamForge API - Secured'
    }), {
      status: 200,
      headers: { ...corsHeaders, 'Content-Type': 'application/json' }
    });
  }
};