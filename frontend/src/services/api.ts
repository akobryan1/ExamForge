import axios, { AxiosInstance, AxiosError } from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

console.log('[API] Initializing with base URL:', API_BASE_URL);
console.log('[API] Env vars:', {
  VITE_API_BASE_URL: import.meta.env.VITE_API_BASE_URL,
  VITE_SUPABASE_URL: import.meta.env.VITE_SUPABASE_URL ? 'set' : 'not set',
  VITE_FIREBASE_API_KEY: import.meta.env.VITE_FIREBASE_API_KEY ? 'set' : 'not set',
});

/**
 * Create axios instance with default config
 */
const apiClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  withCredentials: true, // Send cookies with requests
});

/**
 * Request interceptor - add auth token to requests
 */
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('accessToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

/**
 * Response interceptor - handle token refresh on 401
 */
apiClient.interceptors.response.use(
  (response) => {
    console.log(`[API] Response ${response.status}: ${response.config?.url}`);
    return response;
  },
  async (error: AxiosError) => {
    const originalRequest = error.config as any;
    const url = originalRequest?.url || 'unknown';
    console.log(`[API] Error on ${url}:`, error.message, 'status:', error.response?.status);

    // If 401 and not already retried
    if (error.response?.status === 401 && !originalRequest._retry) {
      const token = localStorage.getItem('accessToken');
      
      // Only attempt refresh if we have a token
      if (!token) {
        // No token at all - just reject silently (user needs to login)
        console.log('[API] 401 on', url, '- no token, not redirecting');
        return Promise.reject(error);
      }

      console.log('[API] 401 received, attempting token refresh...');
      originalRequest._retry = true;

      try {
        console.log('[API] Calling refresh endpoint...');
        const { data } = await axios.post(
          `${API_BASE_URL}/api/auth/refresh`,
          {},
          { withCredentials: true }
        );

        console.log('[API] Token refreshed successfully');
        localStorage.setItem('accessToken', data.accessToken);

        originalRequest.headers.Authorization = `Bearer ${data.accessToken}`;
        return apiClient(originalRequest);
      } catch (refreshError: any) {
        // Refresh failed - clear auth
        console.error('[API] Token refresh failed:', refreshError.message);
        localStorage.removeItem('accessToken');
        localStorage.removeItem('user');
        
        // Only redirect if not already on login/signup page
        const path = window.location.pathname;
        if (path !== '/login' && path !== '/signup' && path !== '/register/student') {
          console.log('[API] Redirecting to /');
          window.location.href = '/';
        } else {
          console.log('[API] Already on auth page, not redirecting');
        }
        return Promise.reject(refreshError);
      }
    }

    console.log(`[API] Not retrying ${url} (status: ${error.response?.status}, retry: ${originalRequest._retry})`);
    return Promise.reject(error);
  }
);

export default apiClient;
