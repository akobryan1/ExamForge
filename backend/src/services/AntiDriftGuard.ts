/**
 * AntiDriftGuard — Post-generation mechanical validation layers for AI essay grading.
 *
 * Layer 1: Structural & range validation (parse, required keys, score bounds)
 * Layer 2: Key-point matching via TF-IDF (false-positive override)
 * Layer 3: Chain-of-verification (secondary DeepSeek call on 10% sample)
 * Layer 4: Circuit breaker (store failures in Firestore, return graceful fallback)
 */

import { computeSimilarity } from './TfIdfMatcher';
import { getFirestore, FieldValue } from 'firebase-admin/firestore';

/* ------------------------------------------------------------------ */
/*  Types                                                              */
/* ------------------------------------------------------------------ */

export interface EvaluationResult {
  score: number;
  feedback: string;
  justification?: string;
  matched_keypoints?: string[];
}

export interface ValidationResult {
  valid: boolean;
  result?: EvaluationResult;
  error?: 'parse' | 'structural' | 'keypoint' | 'verification';
  message?: string;
}

interface FailedEvaluationRecord {
  userId: string;
  timestamp: FirebaseFirestore.FieldValue;
  rawInput: {
    questionText: string;
    studentAnswer: string;
    maxPoints: number;
    modelAnswer?: string;
    keyPoints?: string;
  };
  rawLLMOutput: string;
  failureReason: string;
}

/* ------------------------------------------------------------------ */
/*  Layer 1 — Structural & Range Validation                           */
/* ------------------------------------------------------------------ */

const ALLOWED_KEYS = new Set(['score', 'feedback', 'justification', 'matched_keypoints']);

export function validateStructure(raw: string): ValidationResult {
  let parsed: Record<string, unknown>;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return { valid: false, error: 'parse', message: 'AI response is not valid JSON' };
  }

  // Reject if unknown keys exist
  for (const key of Object.keys(parsed)) {
    if (!ALLOWED_KEYS.has(key)) {
      return {
        valid: false,
        error: 'structural',
        message: `Unexpected key "${key}" in AI response`,
      };
    }
  }

  // Validate score
  if (typeof parsed.score !== 'number') {
    return { valid: false, error: 'structural', message: '"score" must be a number' };
  }
  if (!Number.isInteger(parsed.score)) {
    return { valid: false, error: 'structural', message: '"score" must be an integer' };
  }
  if (parsed.score < 0 || parsed.score > 100) {
    return { valid: false, error: 'structural', message: '"score" must be between 0 and 100' };
  }

  // Validate feedback
  if (typeof parsed.feedback !== 'string' || parsed.feedback.trim().length === 0) {
    return { valid: false, error: 'structural', message: '"feedback" must be a non-empty string' };
  }

  // Validate matched_keypoints
  if (parsed.matched_keypoints !== undefined) {
    if (!Array.isArray(parsed.matched_keypoints)) {
      return { valid: false, error: 'structural', message: '"matched_keypoints" must be an array' };
    }
    for (const kp of parsed.matched_keypoints) {
      if (typeof kp !== 'string') {
        return { valid: false, error: 'structural', message: 'All "matched_keypoints" entries must be strings' };
      }
    }
  }

  return {
    valid: true,
    result: {
      score: parsed.score as number,
      feedback: parsed.feedback as string,
      justification: (parsed.justification as string) || undefined,
      matched_keypoints: parsed.matched_keypoints as string[] | undefined,
    },
  };
}

export function buildRetryPrompt(originalPrompt: string): string {
  return `Your previous response was malformed. You must return ONLY a JSON object with exactly the following keys: score (integer 0-100), feedback (string), justification (string, optional), and matched_keypoints (array of strings, optional). No other text, no markdown, no explanation outside the JSON.\n\n${originalPrompt}`;
}

/* ------------------------------------------------------------------ */
/*  Layer 2 — Key-Point Matching (TF-IDF Override)                    */
/* ------------------------------------------------------------------ */

const KEYPOINT_SIMILARITY_THRESHOLD = 0.45;

export interface KeypointOverrideResult {
  score: number;
  feedback: string;
  overrides: string[];
}

export function validateKeypoints(
  essayText: string,
  keyPoints: string,
  llmMatchedKeypoints: string[] | undefined,
  llmScore: number,
  maxPoints: number
): KeypointOverrideResult {
  const overrides: string[] = [];
  let adjustedScore = llmScore;

  // Split key points by newline, semicolon, or period
  const officialKeypoints = keyPoints
    .split(/[\n;.]+/)
    .map(kp => kp.trim())
    .filter(kp => kp.length > 3);

  if (officialKeypoints.length === 0) {
    return { score: adjustedScore, feedback: '', overrides };
  }

  const pointsPerKeypoint = maxPoints / officialKeypoints.length;

  for (const officialKp of officialKeypoints) {
    const similarity = computeSimilarity(officialKp, essayText);

    // The LLM claims it matched this key point, but TF-IDF says otherwise
    if (similarity < KEYPOINT_SIMILARITY_THRESHOLD && llmMatchedKeypoints) {
      const llmClaimedMatch = llmMatchedKeypoints.some(
        (mk: string) => computeSimilarity(officialKp, mk) > 0.6
      );

      if (llmClaimedMatch) {
        adjustedScore = Math.max(0, adjustedScore - pointsPerKeypoint);
        const msg = `System override: Key point "${officialKp}" was not found in the essay.`;
        overrides.push(msg);
      }
    }
  }

  return {
    score: Math.round(adjustedScore),
    feedback: overrides.join('\n'),
    overrides,
  };
}

/* ------------------------------------------------------------------ */
/*  Layer 3 — Chain-of-Verification (10% Sampling)                    */
/* ------------------------------------------------------------------ */

const VERIFICATION_SAMPLE_RATE = 0.1;
const VERIFICATION_SCORE_DISCREPANCY_THRESHOLD = 5;

export interface VerificationResult {
  originalResult: EvaluationResult;
  verifiedResult?: EvaluationResult;
  discrepancyDetected: boolean;
  verificationNote: string;
}

export async function verifyWithSecondPass(
  essayText: string,
  rubricContext: string,
  firstEvaluation: EvaluationResult,
  apiKey: string,
  model: string
): Promise<VerificationResult> {
  // Only verify a random sample
  if (Math.random() >= VERIFICATION_SAMPLE_RATE) {
    return {
      originalResult: firstEvaluation,
      discrepancyDetected: false,
      verificationNote: '',
    };
  }

  console.log('[AntiDrift] Triggering chain-of-verification for sample');

  const verifyPrompt = `You are a strict evaluation verifier. You will receive:
1. The original essay
2. The grading rubric/model answer/key points
3. The first evaluation's JSON output

Your task: Strictly verify the first evaluation against the rubric. Return ONLY a JSON object with:
- "verdict": either "Adhere" (if the first evaluation is correct) or "Drift" (if it deviates from the rubric)
- "corrected_score": an integer 0-100 representing what the score should actually be
- "explanation": a brief string explaining your verdict

Do not add any text outside the JSON.`;

  const verifyUserContent = `Rubric:\n${rubricContext}\n\nEssay:\n${essayText}\n\nFirst Evaluation:\n${JSON.stringify(firstEvaluation)}`;

  try {
    const response = await fetch('https://api.deepseek.com/v1/chat/completions', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${apiKey}`,
      },
      body: JSON.stringify({
        model,
        messages: [
          { role: 'system', content: verifyPrompt },
          { role: 'user', content: verifyUserContent },
        ],
        temperature: 0.0,
        max_tokens: 300,
      }),
    });

    if (!response.ok) {
      console.warn('[AntiDrift] Verification call failed:', response.status);
      return {
        originalResult: firstEvaluation,
        discrepancyDetected: false,
        verificationNote: '',
      };
    }

    const data = await response.json() as { choices?: { message?: { content?: string } }[] };
    const content = data.choices?.[0]?.message?.content || '{}';

    let verifyResult: { verdict: string; corrected_score: number; explanation?: string };
    try {
      verifyResult = JSON.parse(content);
    } catch {
      console.warn('[AntiDrift] Failed to parse verification response');
      return {
        originalResult: firstEvaluation,
        discrepancyDetected: false,
        verificationNote: '',
      };
    }

    const scoreDiff = Math.abs(firstEvaluation.score - (verifyResult.corrected_score ?? firstEvaluation.score));

    if (verifyResult.verdict === 'Drift' && scoreDiff > VERIFICATION_SCORE_DISCREPANCY_THRESHOLD) {
      console.log(`[AntiDrift] Drift detected! Original: ${firstEvaluation.score}, Corrected: ${verifyResult.corrected_score}`);
      return {
        originalResult: firstEvaluation,
        verifiedResult: {
          ...firstEvaluation,
          score: verifyResult.corrected_score,
          feedback: `[Verified by secondary review] ${verifyResult.explanation || ''}\n\n${firstEvaluation.feedback}`,
        },
        discrepancyDetected: true,
        verificationNote: `Score corrected from ${firstEvaluation.score} to ${verifyResult.corrected_score} by secondary review.`,
      };
    }

    return {
      originalResult: firstEvaluation,
      discrepancyDetected: false,
      verificationNote: 'Verified by secondary review — score consistent.',
    };
  } catch (err) {
    console.warn('[AntiDrift] Verification error:', (err as Error).message);
    return {
      originalResult: firstEvaluation,
      discrepancyDetected: false,
      verificationNote: '',
    };
  }
}

/* ------------------------------------------------------------------ */
/*  Layer 4 — Circuit Breaker                                         */
/* ------------------------------------------------------------------ */

export async function circuitBreak(
  failedAttempt: {
    userId: string;
    rawInput: { questionText: string; studentAnswer: string; maxPoints: number; modelAnswer?: string; keyPoints?: string };
    rawLLMOutput: string;
    failureReason: string;
  }
): Promise<void> {
  try {
    const db = getFirestore();
    await db.collection('failed_evaluations').add({
      userId: failedAttempt.userId,
      timestamp: FieldValue.serverTimestamp(),
      rawInput: failedAttempt.rawInput,
      rawLLMOutput: failedAttempt.rawLLMOutput,
      failureReason: failedAttempt.failureReason,
    } as FailedEvaluationRecord);
    console.log('[AntiDrift] Stored failed evaluation in Firestore:', failedAttempt.failureReason);
  } catch (err) {
    console.error('[AntiDrift] Failed to store circuit-break record:', (err as Error).message);
    // Swallow — we don't want the circuit breaker itself to crash
  }
}
