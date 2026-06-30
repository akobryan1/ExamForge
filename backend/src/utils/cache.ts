import NodeCache from 'node-cache';

// Singleton cache instance for Firestore query results
// maxKeys=200 avoids unbounded memory growth on Render's 512MB free tier
// useClones=false saves memory by storing references
const cache = new NodeCache({
  stdTTL: 120,          // default TTL: 2 minutes
  checkperiod: 60,      // clean expired keys every 60 seconds
  maxKeys: 200,         // cap at 200 cached entries (~10-20MB max)
  useClones: false,     // store references to save memory
});

/**
 * Get a cached value, or compute and store it if missing.
 * @param key   Cache key (typically `{endpoint}_{userId}`)
 * @param ttl   Time-to-live in seconds (override default)
 * @param fn    Async function to compute the value if cache miss
 */
export async function getOrSet<T>(
  key: string,
  ttl: number,
  fn: () => Promise<T>
): Promise<T> {
  const cached = cache.get<T>(key);
  if (cached !== undefined) return cached;

  const value = await fn();
  cache.set(key, value, ttl);
  return value;
}

/**
 * Invalidate a single cache key.
 */
export function invalidateKey(key: string): void {
  cache.del(key);
}

/**
 * Invalidate all keys matching a prefix.
 * Example: invalidatePrefix('exams_list_') clears all exam lists.
 */
export function invalidatePrefix(prefix: string): void {
  const keys = cache.keys().filter(k => k.startsWith(prefix));
  if (keys.length > 0) cache.del(keys);
}

/**
 * Invalidate multiple known keys at once.
 */
export function invalidateKeys(keys: string[]): void {
  cache.del(keys);
}

/**
 * Get cache stats (for debugging / health-check).
 */
export function getCacheStats(): { keys: number; hits: number; misses: number } {
  return {
    keys: cache.keys().length,
    hits: cache.getStats().hits,
    misses: cache.getStats().misses,
  };
}

/**
 * Flush entire cache (useful after major mutations or testing).
 */
export function flushCache(): void {
  cache.flushAll();
}

export { cache };
