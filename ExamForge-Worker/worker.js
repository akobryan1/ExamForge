// Cloudflare Worker for ExamForge API - SECURED VERSION

const allowedOrigins = new Set([
  'https://examforge-201e8.web.app',
  'https://examforge-publisher.onrender.com'
]);

function buildCorsHeaders(origin) {
  const safeOrigin = allowedOrigins.has(origin) ? origin : 'https://examforge-201e8.web.app';
  return {
    'Access-Control-Allow-Origin': safeOrigin,
    'Access-Control-Allow-Methods': 'POST, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type',
  };
}

// Handle OPTIONS request for CORS
function handleOptions(request) {
  const origin = request.headers.get('Origin') || '';
  return new Response(null, {
    headers: buildCorsHeaders(origin)
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
    const origin = request.headers.get('Origin') || '';
    const corsHeaders = buildCorsHeaders(origin);
    const submission = await request.json();

    if (!submission.examId || !submission.ownerUserId || !submission.answers) {
      return new Response(JSON.stringify({ error: 'Missing required fields' }), {
        status: 400,
        headers: { ...corsHeaders, 'Content-Type': 'application/json' }
      });
    }

    const serviceAccount = JSON.parse(env.SERVICE_ACCOUNT_JSON);
    const accessToken = await getAccessToken(serviceAccount);

    const studentInfo = submission.studentInfo || {};
    const studentName = studentInfo.name || submission.studentName || 'Anonymous';
    const studentEmail = studentInfo.email || submission.studentEmail || '';
    const studentId = studentInfo.studentId || submission.studentId || '';
    const yearSection = studentInfo.yearSection || submission.yearSection || '';

    const responseEntries = Object.entries(submission.answers || {});
    const responses = responseEntries.map(([key, value], index) => {
      const match = key.match(/question(\d+)/i);
      const questionNumber = match ? Number.parseInt(match[1], 10) : index + 1;

      return {
        QuestionId: key,
        QuestionNumber: Number.isFinite(questionNumber) ? questionNumber : index + 1,
        Answer: value == null ? '' : String(value),
        PointsEarned: 0,
        PointsPossible: 0,
        IsCorrect: false
      };
    });

    const submissionData = {
      id: crypto.randomUUID(),
      examId: submission.examId,
      submittedAt: new Date().toISOString(),
      timeSpent: submission.timeSpent || 0,
      studentEmail,
      studentName,
      studentId,
      yearSection,
      responses,
      totalScore: 0,
      totalPossiblePoints: 0,
      status: 'Submitted'
    };

    const firestoreUrl = `https://firestore.googleapis.com/v1/projects/${env.PROJECT_ID}/databases/(default)/documents/examforge_users/${submission.ownerUserId}/examinee_data?documentId=${submissionData.id}`;
    
    const firestoreResponse = await fetch(firestoreUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${accessToken}`
      },
      body: JSON.stringify({
        fields: {
          Id: { stringValue: submissionData.id },
          ExamId: { stringValue: submissionData.examId },
          StudentName: { stringValue: submissionData.studentName },
          StudentEmail: { stringValue: submissionData.studentEmail },
          StudentId: { stringValue: submissionData.studentId },
          YearSection: { stringValue: submissionData.yearSection },
          SubmittedAt: { timestampValue: submissionData.submittedAt },
          Responses: {
            arrayValue: {
              values: submissionData.responses.map(r => ({
                mapValue: {
                  fields: {
                    QuestionId: { stringValue: r.QuestionId },
                    QuestionNumber: { integerValue: r.QuestionNumber.toString() },
                    Answer: { stringValue: r.Answer },
                    PointsEarned: { doubleValue: r.PointsEarned },
                    PointsPossible: { doubleValue: r.PointsPossible },
                    IsCorrect: { booleanValue: r.IsCorrect }
                  }
                }
              }))
            }
          },
          TotalScore: { doubleValue: submissionData.totalScore },
          TotalPossiblePoints: { doubleValue: submissionData.totalPossiblePoints },
          Status: { stringValue: submissionData.status }
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
    const message = error instanceof Error ? error.message : 'Unknown error';
    const fallbackOrigin = request.headers.get('Origin') || '';
    const fallbackCorsHeaders = buildCorsHeaders(fallbackOrigin);
    return new Response(JSON.stringify({ 
      error: 'Internal server error',
      message
    }), {
      status: 500,
      headers: { ...fallbackCorsHeaders, 'Content-Type': 'application/json' }
    });
  }
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const origin = request.headers.get('Origin') || '';
    const corsHeaders = buildCorsHeaders(origin);

    // ✅ SECURITY: Verify origin
    if (origin && !allowedOrigins.has(origin)) {
      return new Response(JSON.stringify({ error: 'Forbidden' }), {
        status: 403,
        headers: { ...corsHeaders, 'Content-Type': 'application/json' }
      });
    }

    if (request.method === 'OPTIONS') {
      return handleOptions(request);
    }

    if ((url.pathname === '/api/submit' || url.pathname === '/') && request.method === 'POST') {
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