const BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080/api';

async function request<T>(endpoint: string, method: string = 'GET', body?: any): Promise<T> {
  const token = typeof window !== 'undefined' ? localStorage.getItem('token') : null;
  
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const config: RequestInit = {
    method,
    headers,
  };

  if (body) {
    config.body = JSON.stringify(body);
  }

  const response = await fetch(`${BASE_URL}/${endpoint}`, config);

  if (response.status === 401) {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('token');
      window.location.href = '/';
    }
    throw new Error('Unauthorized');
  }

  if (!response.ok) {
    const errorMsg = await response.text();
    throw new Error(errorMsg || 'API Request failed');
  }

  return response.json();
}

export const api = {
  // Authentication
  login: (data: any) => request<any>('auth/login', 'POST', data),
  register: (data: any) => request<any>('auth/register', 'POST', data),
  getProfile: () => request<any>('auth/profile'),

  // Activities
  logActivity: (data: any) => request<any>('activity/log', 'POST', data),
  getHistory: () => request<any>('activity/history'),

  // Coach
  getTips: () => request<any>('coach/tips'),
  chatWithCoach: (message: string) => request<any>('coach/chat', 'POST', { message }),

  // Carbon Twin
  getForecast: () => request<any>('twin/forecast'),

  // What-If
  simulate: (data: any) => request<any>('whatif/simulate', 'POST', data),

  // Missions
  getMissions: () => request<any>('mission'),
  completeMission: (id: number) => request<any>(`mission/complete/${id}`, 'POST'),

  // Leaderboard
  getLeaderboard: () => request<any>('leaderboard'),

  // Impact Story
  getMonthlyStory: () => request<any>('story/monthly')
};
