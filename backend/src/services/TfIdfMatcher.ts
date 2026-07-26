/**
 * TfIdfMatcher — Lightweight TF-IDF + cosine similarity for key-point matching.
 * Zero external dependencies. Runs in <50ms for typical essay lengths (500–2000 words).
 */

const STOP_WORDS = new Set([
  'a', 'an', 'the', 'and', 'or', 'but', 'in', 'on', 'at', 'to', 'for',
  'of', 'by', 'with', 'from', 'as', 'is', 'was', 'were', 'be', 'been',
  'being', 'have', 'has', 'had', 'do', 'does', 'did', 'will', 'would',
  'can', 'could', 'shall', 'should', 'may', 'might', 'not', 'no', 'nor',
  'so', 'if', 'than', 'that', 'this', 'these', 'those', 'it', 'its',
  'he', 'she', 'they', 'them', 'their', 'we', 'you', 'your', 'my', 'me',
  'our', 'us', 'all', 'each', 'every', 'some', 'any', 'both', 'few',
  'more', 'most', 'other', 'such', 'only', 'own', 'same', 'here', 'there',
  'about', 'into', 'over', 'after', 'before', 'between', 'under', 'again',
  'further', 'then', 'once', 'just', 'also', 'very', 'too', 'really',
]);

function tokenize(text: string): string[] {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9\s-]/g, ' ')
    .split(/\s+/)
    .filter(t => t.length > 1 && !STOP_WORDS.has(t));
}

function computeTF(tokens: string[]): Map<string, number> {
  const tf = new Map<string, number>();
  const len = tokens.length || 1;
  for (const t of tokens) {
    tf.set(t, (tf.get(t) || 0) + 1 / len);
  }
  return tf;
}

function computeIDF(documents: string[][]): Map<string, number> {
  const idf = new Map<string, number>();
  const N = documents.length;
  const df = new Map<string, number>();
  for (const doc of documents) {
    const seen = new Set(doc);
    for (const term of seen) {
      df.set(term, (df.get(term) || 0) + 1);
    }
  }
  for (const [term, count] of df) {
    idf.set(term, Math.log((N + 1) / (count + 1)) + 1);
  }
  return idf;
}

function cosineSimilarity(
  vecA: Map<string, number>,
  vecB: Map<string, number>
): number {
  let dot = 0, normA = 0, normB = 0;
  for (const [term, val] of vecA) {
    normA += val * val;
    const bVal = vecB.get(term) || 0;
    dot += val * bVal;
  }
  for (const [, val] of vecB) {
    normB += val * val;
  }
  const denom = Math.sqrt(normA) * Math.sqrt(normB);
  return denom === 0 ? 0 : dot / denom;
}

/**
 * Compute TF-IDF cosine similarity between two text strings.
 * Returns a value between 0 (completely dissimilar) and 1 (identical).
 */
export function computeSimilarity(textA: string, textB: string): number {
  const tokensA = tokenize(textA);
  const tokensB = tokenize(textB);
  if (tokensA.length === 0 || tokensB.length === 0) return 0;

  const idf = computeIDF([tokensA, tokensB]);
  const tfA = computeTF(tokensA);
  const tfB = computeTF(tokensB);

  // Convert TF to TF-IDF
  const tfidfA = new Map<string, number>();
  const tfidfB = new Map<string, number>();
  for (const [term, tf] of tfA) {
    tfidfA.set(term, tf * (idf.get(term) || 1));
  }
  for (const [term, tf] of tfB) {
    tfidfB.set(term, tf * (idf.get(term) || 1));
  }

  return cosineSimilarity(tfidfA, tfidfB);
}
