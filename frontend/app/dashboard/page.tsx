'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { api } from '../../utils/api';

interface UserStats {
  id: number;
  name: string;
  email: string;
  level: number;
  xp: number;
  points: number;
  streak: number;
}

export default function Dashboard() {
  const router = useRouter();
  const [user, setUser] = useState<UserStats | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  const [loading, setLoading] = useState(true);
  const [toastMessage, setToastMessage] = useState('');

  // Daily Logger States
  const [logDate, setLogDate] = useState(new Date().toISOString().split('T')[0]);
  const [carKm, setCarKm] = useState(15);
  const [bikeKm, setBikeKm] = useState(0);
  const [transitHours, setTransitHours] = useState(0);
  const [fuelType, setFuelType] = useState('Petrol');
  const [electricity, setElectricity] = useState(8);
  const [acHours, setAcHours] = useState(2);
  const [appliances, setAppliances] = useState(4);
  const [meatServings, setMeatServings] = useState(1);
  const [vegServings, setVegServings] = useState(2);
  const [dairyServings, setDairyServings] = useState(1);
  const [clothingCount, setClothingCount] = useState(0);
  const [electronicsCount, setElectronicsCount] = useState(0);
  const [householdSpent, setHouseholdSpent] = useState(10);
  const [plasticKg, setPlasticKg] = useState(0.4);
  const [organicKg, setOrganicKg] = useState(0.5);
  const [recycledKg, setRecycledKg] = useState(0.2);

  // Computed / Response States
  const [currentScore, setCurrentScore] = useState(78);
  const [currentDailyCO2, setCurrentDailyCO2] = useState(14.2);
  const [history, setHistory] = useState<any[]>([]);
  const [missions, setMissions] = useState<any[]>([]);
  const [leaderboard, setLeaderboard] = useState<any>({ scoreLeaderboard: [], improvementLeaderboard: [] });
  const [coachTips, setCoachTips] = useState<string>('Loading personalized recommendations...');
  const [twinForecast, setTwinForecast] = useState<any>({ current: { score: 78, co2: 14.2 }, projection3m: { score: 82, co2: 12.1 }, projection6m: { score: 86, co2: 10.3 }, projection12m: { score: 92, co2: 7.5 }, explanation: 'Loading predictions...' });
  const [monthlyStory, setMonthlyStory] = useState<any>(null);

  // What-If Simulator Selection States
  const [simEV, setSimEV] = useState(false);
  const [simWFH, setSimWFH] = useState(false);
  const [simSolar, setSimSolar] = useState(false);
  const [simBike, setSimBike] = useState(false);
  const [simMeat, setSimMeat] = useState(false);
  const [simResult, setSimResult] = useState<any>(null);
  const [simLoading, setSimLoading] = useState(false);

  // Theme State
  const [theme, setTheme] = useState<'dark' | 'light'>('dark');

  // Onboarding Wizard States
  const [showOnboarding, setShowOnboarding] = useState(false);
  const [onboardStep, setOnboardStep] = useState(1);
  const [onboardCommute, setOnboardCommute] = useState('Petrol');
  const [onboardKm, setOnboardKm] = useState(20);
  const [onboardAC, setOnboardAC] = useState(3);
  const [onboardDiet, setOnboardDiet] = useState('Meat');

  // Interactive AI Coach Chat States
  const [chatInput, setChatInput] = useState('');
  const [chatMessages, setChatMessages] = useState<{ sender: 'user' | 'coach'; text: string }[]>([]);
  const [chatLoading, setChatLoading] = useState(false);

  // Sync theme class
  useEffect(() => {
    if (theme === 'light') {
      document.body.classList.add('light-theme');
    } else {
      document.body.classList.remove('light-theme');
    }
  }, [theme]);

  // Fetch all dashboard data
  const fetchData = async () => {
    try {
      const historyData = await api.getHistory();
      setHistory(historyData);

      if (historyData.length > 0) {
        const latest = historyData[0];
        setCurrentDailyCO2(latest.dailyCO2Kg);
        setCurrentScore(latest.score);
      } else {
        // If history is empty, trigger onboarding modal
        setShowOnboarding(true);
      }

      const missionsData = await api.getMissions();
      setMissions(missionsData);

      const coachData = await api.getTips();
      setCoachTips(coachData.tips);

      const twinData = await api.getForecast();
      setTwinForecast(twinData);

      const leaderboardData = await api.getLeaderboard();
      setLeaderboard(leaderboardData);

      const storyData = await api.getMonthlyStory();
      setMonthlyStory(storyData);
    } catch (err) {
      console.error('Error fetching dashboard stats:', err);
    }
  };

  useEffect(() => {
    const token = localStorage.getItem('token');
    const storedUser = localStorage.getItem('user');

    if (!token || !storedUser) {
      router.push('/');
      return;
    }

    const parsedUser = JSON.parse(storedUser);
    setUser(parsedUser);
    setChatMessages([
      { sender: 'coach', text: `Hi ${parsedUser.name}! I am your EcoPilot AI Carbon Coach. Ask me any questions about reducing your carbon footprint, green options, or how to improve your level!` }
    ]);
    setLoading(false);
    fetchData();
  }, [router]);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(''), 4000);
  };

  // Send message to AI Coach
  const handleSendMessage = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!chatInput.trim() || chatLoading) return;

    const userMsg = chatInput;
    setChatInput('');
    setChatMessages(prev => [...prev, { sender: 'user', text: userMsg }]);
    setChatLoading(true);

    try {
      const res = await api.chatWithCoach(userMsg);
      setChatMessages(prev => [...prev, { sender: 'coach', text: res.response }]);
    } catch (err: any) {
      setChatMessages(prev => [...prev, { sender: 'coach', text: `Sorry, I encountered an error: ${err.message || 'API request failed'}.` }]);
    } finally {
      setChatLoading(false);
    }
  };

  // Submit onboarding wizard logs
  const handleOnboardSubmit = async () => {
    try {
      const payload = {
        logDate: new Date(),
        carKm: onboardCommute === 'PublicTransit' || onboardCommute === 'Bicycle' ? 0 : onboardKm,
        bikeKm: onboardCommute === 'Bicycle' ? onboardKm : 0,
        publicTransitHours: onboardCommute === 'PublicTransit' ? 1.5 : 0,
        carFuelType: onboardCommute !== 'PublicTransit' && onboardCommute !== 'Bicycle' ? onboardCommute : 'None',
        electricityKwh: 8.0,
        acHours: onboardAC,
        appliancesUsedCount: 4,
        meatServings: onboardDiet === 'Meat' ? 2.0 : onboardDiet === 'Flexitarian' ? 1.0 : 0.0,
        vegetarianServings: onboardDiet === 'Vegetarian' || onboardDiet === 'Vegan' ? 3.0 : 1.0,
        dairyServings: onboardDiet === 'Vegan' ? 0.0 : 1.5,
        clothingItemsBought: 0,
        electronicsBought: 0,
        householdSpent: 10.0,
        plasticWasteKg: 0.4,
        organicWasteKg: 0.5,
        recycledWasteKg: 0.2
      };

      const res = await api.logActivity(payload);
      setCurrentDailyCO2(res.dailyCO2Kg);
      setCurrentScore(res.score);

      if (user) {
        const updated = {
          ...user,
          level: res.userStats.level,
          xp: res.userStats.xp,
          points: res.userStats.points,
          streak: res.userStats.streak
        };
        setUser(updated);
        localStorage.setItem('user', JSON.stringify(updated));
      }

      setShowOnboarding(false);
      showToast("🎉 Onboarding complete! Welcome to EcoPilot AI.");
      fetchData();
    } catch (err: any) {
      alert(err.message || 'Onboarding submission failed.');
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    document.body.classList.remove('light-theme');
    router.push('/');
  };

  // Submit Logger Form
  const handleLogSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const payload = {
        logDate: new Date(logDate),
        carKm,
        bikeKm,
        publicTransitHours: transitHours,
        carFuelType: fuelType,
        electricityKwh: electricity,
        acHours,
        appliancesUsedCount: appliances,
        meatServings,
        vegetarianServings: vegServings,
        dairyServings,
        clothingItemsBought: clothingCount,
        electronicsBought: electronicsCount,
        householdSpent,
        plasticWasteKg: plasticKg,
        organicWasteKg: organicKg,
        recycledWasteKg: recycledKg
      };

      const res = await api.logActivity(payload);
      
      setCurrentDailyCO2(res.dailyCO2Kg);
      setCurrentScore(res.score);
      
      // Update local storage user stats (xp, level, points)
      if (user) {
        const updated = {
          ...user,
          level: res.userStats.level,
          xp: res.userStats.xp,
          points: res.userStats.points,
          streak: res.userStats.streak
        };
        setUser(updated);
        localStorage.setItem('user', JSON.stringify(updated));
      }

      showToast(`Logged activity successfully! Calculated Footprint: ${res.dailyCO2Kg} kg CO₂.`);
      if (res.leveledUp) {
        showToast("⚡ LEVEL UP! You reached a new level!");
      }
      
      // Refresh calculations and predictions
      fetchData();
      setActiveTab('overview');
    } catch (err: any) {
      alert(err.message || 'Failed to log daily activity.');
    }
  };

  // Trigger What-If Simulation
  const handleSimulate = async () => {
    setSimLoading(true);
    try {
      const res = await api.simulate({
        switchToEV: simEV,
        workFromHome: simWFH,
        installSolar: simSolar,
        useBicycle: simBike,
        reduceMeat: simMeat
      });
      setSimResult(res);
    } catch (err: any) {
      alert(err.message || 'Simulation failed.');
    } finally {
      setSimLoading(false);
    }
  };

  useEffect(() => {
    if (activeTab === 'simulator') {
      handleSimulate();
    }
  }, [simEV, simWFH, simSolar, simBike, simMeat, activeTab]);

  // Complete Quest/Mission
  const handleCompleteMission = async (id: number) => {
    try {
      const res = await api.completeMission(id);
      showToast(res.message);
      
      if (user) {
        const updated = {
          ...user,
          level: res.userStats.level,
          xp: res.userStats.xp,
          points: res.userStats.points,
          streak: res.userStats.streak
        };
        setUser(updated);
        localStorage.setItem('user', JSON.stringify(updated));
      }

      fetchData();
    } catch (err: any) {
      alert(err.message || 'Failed to complete mission.');
    }
  };

  if (loading || !user) {
    return (
      <div style={{ display: 'flex', minHeight: '100vh', alignItems: 'center', justifyContent: 'center' }}>
        <p style={{ fontStyle: 'italic', color: 'hsl(var(--text-muted))' }}>Initializing EcoPilot Dashboard...</p>
      </div>
    );
  }

  // Visual calculations for xp meter
  const xpPercent = Math.min(100, user.xp);

  // SVG Gauge variables
  // Score is 0-100, radius is 50, circumference is 2 * PI * 50 = 314
  // We want a semi circle gauge so circumference strokeDasharray = 314, strokeDashoffset represents progress (scaled to half circle 157)
  const scorePercent = currentScore / 100;
  const strokeDashoffset = 314 - (scorePercent * 157); // 314 is full, 157 is half

  return (
    <div className="console-container">
      {/* Toast popup */}
      {toastMessage && (
        <div style={{ position: 'fixed', top: '20px', right: '20px', zIndex: 999 }}>
          <div className="toast-success">{toastMessage}</div>
        </div>
      )}

      {/* Sidebar Navigation */}
      <aside className="console-sidebar">
        <div className="console-nav-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
          <span>🍀 EcoPilot AI</span>
          <button 
            onClick={() => setTheme(t => t === 'dark' ? 'light' : 'dark')}
            style={{
              background: 'rgba(255,255,255,0.06)',
              border: '1px solid hsl(var(--border-glass))',
              borderRadius: '8px',
              width: '32px',
              height: '32px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontSize: '0.95rem',
              cursor: 'pointer',
              outline: 'none',
              transition: 'var(--transition-smooth)'
            }}
            title={theme === 'dark' ? 'Switch to Light Mode' : 'Switch to Dark Mode'}
          >
            {theme === 'dark' ? '☀️' : '🌙'}
          </button>
        </div>

        <nav className="console-nav-list">
          <button onClick={() => setActiveTab('overview')} className={`console-nav-item ${activeTab === 'overview' ? 'active' : ''}`}>
            📊 Console Hub
          </button>
          <button onClick={() => setActiveTab('logger')} className={`console-nav-item ${activeTab === 'logger' ? 'active' : ''}`}>
            ✏️ Daily Logger
          </button>
          <button onClick={() => setActiveTab('twin')} className={`console-nav-item ${activeTab === 'twin' ? 'active' : ''}`}>
            👥 Carbon Twin
          </button>
          <button onClick={() => setActiveTab('simulator')} className={`console-nav-item ${activeTab === 'simulator' ? 'active' : ''}`}>
            🎛️ Simulator
          </button>
          <button onClick={() => setActiveTab('coach')} className={`console-nav-item ${activeTab === 'coach' ? 'active' : ''}`}>
            🤖 AI Coach
          </button>
          <button onClick={() => setActiveTab('missions')} className={`console-nav-item ${activeTab === 'missions' ? 'active' : ''}`}>
            ⚔️ Green Missions
          </button>
          <button onClick={() => setActiveTab('leaderboard')} className={`console-nav-item ${activeTab === 'leaderboard' ? 'active' : ''}`}>
            🏆 Leaderboard
          </button>
          <button onClick={() => setActiveTab('story')} className={`console-nav-item ${activeTab === 'story' ? 'active' : ''}`}>
            📖 Impact Story
          </button>
        </nav>

        {/* User stats widget in sidebar */}
        <div className="sidebar-user">
          <h4>{user.name}</h4>
          <p>{user.email}</p>
          <div style={{ marginTop: '0.75rem', fontSize: '0.8rem', display: 'flex', justifyContent: 'space-between', fontWeight: 600 }}>
            <span style={{ color: '#ff9f43' }}>🔥 {user.streak} Streak</span>
            <span style={{ color: 'hsl(var(--accent))' }}>⚡ Lvl {user.level}</span>
          </div>
          {/* XP Progress Bar */}
          <div style={{ width: '100%', height: '6px', background: 'rgba(255,255,255,0.06)', borderRadius: '3px', marginTop: '0.5rem', overflow: 'hidden' }}>
            <div style={{ width: `${xpPercent}%`, height: '100%', background: 'hsl(var(--accent))' }} />
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.7rem', color: 'hsl(var(--text-muted))', marginTop: '0.2rem' }}>
            <span>{user.xp} XP</span>
            <span>100 XP</span>
          </div>
          
          <button 
            onClick={handleLogout}
            style={{ 
              width: '100%', 
              background: 'rgba(239, 68, 68, 0.1)', 
              color: 'hsl(var(--danger))', 
              border: '1px solid rgba(239, 68, 68, 0.2)',
              marginTop: '1rem',
              padding: '0.4rem',
              borderRadius: '6px',
              fontSize: '0.8rem',
              fontWeight: 600,
              cursor: 'pointer'
            }}
          >
            Sign Out
          </button>
        </div>
      </aside>

      {/* Main Panel Content */}
      <main className="console-main">
        
        {/* OVERVIEW PANEL */}
        {activeTab === 'overview' && (
          <div className="animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
            <h2>Console Overview</h2>
            
            <div className="dashboard-grid">
              {/* Carbon Score Gauge Card */}
              <div className="col-6 glass-card" style={{ display: 'flex', alignItems: 'center', gap: '2rem' }}>
                <div style={{ position: 'relative', width: '140px', height: '100px', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <svg width="120" height="120" viewBox="0 0 120 120" style={{ transform: 'rotate(-180deg)' }}>
                    <circle cx="60" cy="60" r="50" fill="transparent" stroke="rgba(255,255,255,0.04)" strokeWidth="8" strokeDasharray="157 314" />
                    <circle 
                      cx="60" 
                      cy="60" 
                      r="50" 
                      fill="transparent" 
                      stroke="url(#gaugeGradient)" 
                      strokeWidth="8" 
                      strokeDasharray="157 314" 
                      strokeDashoffset={strokeDashoffset} 
                      strokeLinecap="round"
                      style={{ transition: 'stroke-dashoffset 0.8s ease' }}
                    />
                    <defs>
                      <linearGradient id="gaugeGradient" x1="0%" y1="0%" x2="100%" y2="0%">
                        <stop offset="0%" stopColor="#EF4444" />
                        <stop offset="50%" stopColor="#F59E0B" />
                        <stop offset="100%" stopColor="#10B981" />
                      </linearGradient>
                    </defs>
                  </svg>
                  <div style={{ position: 'absolute', bottom: '15px', textAlign: 'center' }}>
                    <span style={{ fontSize: '2rem', fontWeight: 800 }}>{currentScore}</span>
                    <p style={{ fontSize: '0.7rem', color: 'hsl(var(--text-muted))', textTransform: 'uppercase' }}>Score</p>
                  </div>
                </div>
                
                <div>
                  <h3>Carbon Rating</h3>
                  <p style={{ fontSize: '0.9rem', marginTop: '0.5rem' }}>
                    Your current score is <strong>{currentScore}/100</strong>. This indicates your emissions are 
                    {currentScore >= 90 ? ' extremely sustainable!' : currentScore >= 70 ? ' moderately stable.' : ' higher than baseline climate safe levels.'}
                  </p>
                  <button onClick={() => setActiveTab('logger')} className="btn btn-primary" style={{ padding: '0.5rem 1rem', fontSize: '0.8rem', marginTop: '1rem' }}>
                    ✏️ Log Activity
                  </button>
                </div>
              </div>

              {/* Emissions Summary Card */}
              <div className="col-6 glass-card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
                <span style={{ fontSize: '0.8rem', color: 'hsl(var(--text-muted))', textTransform: 'uppercase' }}>Latest Daily Emissions</span>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: '0.5rem', margin: '0.5rem 0' }}>
                  <h1 style={{ fontSize: '3rem', color: 'hsl(var(--accent))' }}>{currentDailyCO2.toFixed(1)}</h1>
                  <span style={{ fontSize: '0.95rem', fontWeight: 600 }}>kg CO₂ / day</span>
                </div>
                <p style={{ fontSize: '0.85rem' }}>
                  Global Target: <strong>5.5 kg CO₂e</strong>. {currentDailyCO2 <= 5.5 ? '🎉 You are inside the safe budget!' : `⚠️ You exceed the safe target by ${(currentDailyCO2 - 5.5).toFixed(1)} kg.`}
                </p>
              </div>
            </div>

            <div className="dashboard-grid">
              {/* AI twin projection preview */}
              <div className="col-6 glass-card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
                <div>
                  <h3 style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem' }}>👥 Carbon Twin</h3>
                  <p style={{ fontSize: '0.85rem', color: 'hsl(var(--text-secondary))' }}>
                    Projections show that under your current trend, your 12-month footprint is expected to be 
                    <strong> {twinForecast.projection12m.co2.toFixed(1)} kg CO₂/day</strong>.
                  </p>
                  <div style={{ background: 'rgba(0,0,0,0.15)', padding: '0.75rem', borderRadius: '8px', border: '1px solid hsl(var(--border-glass))', fontSize: '0.8rem', marginTop: '1rem', fontStyle: 'italic' }}>
                    {twinForecast.explanation.length > 120 ? twinForecast.explanation.substring(0, 120) + "..." : twinForecast.explanation}
                  </div>
                </div>
                <button onClick={() => setActiveTab('twin')} className="btn btn-outline" style={{ padding: '0.5rem', width: '100%', fontSize: '0.8rem', marginTop: '1rem' }}>
                  Open Twin Panel
                </button>
              </div>

              {/* Weekly Challenges snippet */}
              <div className="col-6 glass-card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
                <div>
                  <h3 style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem' }}>⚔️ Green Missions</h3>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', marginTop: '1rem' }}>
                    {missions.slice(0, 2).map((m) => (
                      <div key={m.id} style={{ display: 'flex', justifyContent: 'space-between', background: 'rgba(255,255,255,0.02)', padding: '0.5rem', borderRadius: '6px', fontSize: '0.8rem' }}>
                        <span>⚔️ {m.title}</span>
                        <span style={{ color: 'hsl(var(--primary))', fontWeight: 600 }}>+{m.rewardXP} XP</span>
                      </div>
                    ))}
                    {missions.length === 0 && <p style={{ fontSize: '0.8rem', fontStyle: 'italic' }}>No active challenges. Claim a new set!</p>}
                  </div>
                </div>
                <button onClick={() => setActiveTab('missions')} className="btn btn-outline" style={{ padding: '0.5rem', width: '100%', fontSize: '0.8rem', marginTop: '1rem' }}>
                  Manage Missions
                </button>
              </div>
            </div>

            {/* AI Coach tips overview block */}
            <div className="glass-card">
              <h3 style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>🤖 AI Carbon Coach Recommendation</h3>
              <div style={{ whiteSpace: 'pre-wrap', fontSize: '0.9rem', lineHeight: '1.5', padding: '1rem', background: 'rgba(255, 255, 255, 0.02)', borderRadius: '12px', border: '1px solid hsl(var(--border-glass))' }}>
                {coachTips}
              </div>
            </div>
          </div>
        )}

        {/* LOGGERS PANEL */}
        {activeTab === 'logger' && (
          <form onSubmit={handleLogSubmit} className="glass-card animate-fade-in" style={{ maxWidth: '800px', margin: '0 auto' }}>
            <h2 style={{ marginBottom: '0.5rem' }}>✏️ Daily Carbon Logger</h2>
            <p style={{ fontSize: '0.85rem', color: 'hsl(var(--text-muted))', marginBottom: '2rem' }}>
              Input your activity variables for today to compute your daily carbon rating.
            </p>

            <div style={{ display: 'flex', gap: '1rem', marginBottom: '1.5rem' }}>
              <div className="form-group" style={{ flex: 1 }}>
                <label>Log Date</label>
                <input type="date" value={logDate} onChange={(e) => setLogDate(e.target.value)} className="form-input" required />
              </div>
              <div className="form-group" style={{ flex: 1 }}>
                <label>Car Fuel Type</label>
                <select value={fuelType} onChange={(e) => setFuelType(e.target.value)} className="form-select">
                  <option value="Petrol">Petrol</option>
                  <option value="Diesel">Diesel</option>
                  <option value="Hybrid">Hybrid</option>
                  <option value="EV">Electric (EV)</option>
                  <option value="None">No driving / None</option>
                </select>
              </div>
            </div>

            {/* Transport Category */}
            <h3 style={{ borderBottom: '1px solid hsl(var(--border-glass))', paddingBottom: '0.5rem', marginBottom: '1rem', color: 'hsl(var(--accent))' }}>🚗 Transportation</h3>
            <div className="dashboard-grid">
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>Car Driving (km)</label>
                  <strong>{carKm} km</strong>
                </div>
                <input type="range" min="0" max="200" step="5" value={carKm} onChange={(e) => setCarKm(parseInt(e.target.value))} className="custom-slider" />
              </div>
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>Bicycle distance (km)</label>
                  <strong>{bikeKm} km</strong>
                </div>
                <input type="range" min="0" max="40" step="1" value={bikeKm} onChange={(e) => setBikeKm(parseInt(e.target.value))} className="custom-slider" />
              </div>
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>Public Transit (hours)</label>
                  <strong>{transitHours} hrs</strong>
                </div>
                <input type="range" min="0" max="8" step="0.5" value={transitHours} onChange={(e) => setTransitHours(parseFloat(e.target.value))} className="custom-slider" />
              </div>
            </div>

            {/* Energy Category */}
            <h3 style={{ borderBottom: '1px solid hsl(var(--border-glass))', paddingBottom: '0.5rem', marginBottom: '1rem', color: 'hsl(var(--accent))', marginTop: '1.5rem' }}>⚡ Energy Usage</h3>
            <div className="dashboard-grid">
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>Electricity (kWh)</label>
                  <strong>{electricity} kWh</strong>
                </div>
                <input type="range" min="0" max="50" step="1" value={electricity} onChange={(e) => setElectricity(parseInt(e.target.value))} className="custom-slider" />
              </div>
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>AC Operation (hours)</label>
                  <strong>{acHours} hrs</strong>
                </div>
                <input type="range" min="0" max="24" step="0.5" value={acHours} onChange={(e) => setAcHours(parseFloat(e.target.value))} className="custom-slider" />
              </div>
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>Active Appliances</label>
                  <strong>{appliances} items</strong>
                </div>
                <input type="range" min="0" max="15" step="1" value={appliances} onChange={(e) => setAppliances(parseInt(e.target.value))} className="custom-slider" />
              </div>
            </div>

            {/* Food Category */}
            <h3 style={{ borderBottom: '1px solid hsl(var(--border-glass))', paddingBottom: '0.5rem', marginBottom: '1rem', color: 'hsl(var(--accent))', marginTop: '1.5rem' }}>🥗 Food Consumption</h3>
            <div className="dashboard-grid">
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>Meat portions</label>
                  <strong>{meatServings} servings</strong>
                </div>
                <input type="range" min="0" max="6" step="1" value={meatServings} onChange={(e) => setMeatServings(parseInt(e.target.value))} className="custom-slider" />
              </div>
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>Vegetarian portions</label>
                  <strong>{vegServings} servings</strong>
                </div>
                <input type="range" min="0" max="6" step="1" value={vegServings} onChange={(e) => setVegServings(parseInt(e.target.value))} className="custom-slider" />
              </div>
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>Dairy portions</label>
                  <strong>{dairyServings} servings</strong>
                </div>
                <input type="range" min="0" max="6" step="1" value={dairyServings} onChange={(e) => setDairyServings(parseInt(e.target.value))} className="custom-slider" />
              </div>
            </div>

            {/* Waste Category */}
            <h3 style={{ borderBottom: '1px solid hsl(var(--border-glass))', paddingBottom: '0.5rem', marginBottom: '1rem', color: 'hsl(var(--accent))', marginTop: '1.5rem' }}>🗑️ Waste & Recycling</h3>
            <div className="dashboard-grid">
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>Recycled Waste (kg)</label>
                  <strong>{recycledKg} kg</strong>
                </div>
                <input type="range" min="0" max="5" step="0.1" value={recycledKg} onChange={(e) => setRecycledKg(parseFloat(e.target.value))} className="custom-slider" />
              </div>
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>Plastic Waste (kg)</label>
                  <strong>{plasticKg} kg</strong>
                </div>
                <input type="range" min="0" max="5" step="0.1" value={plasticKg} onChange={(e) => setPlasticKg(parseFloat(e.target.value))} className="custom-slider" />
              </div>
              <div className="col-4 slider-container">
                <div className="slider-header">
                  <label>Organic Waste (kg)</label>
                  <strong>{organicKg} kg</strong>
                </div>
                <input type="range" min="0" max="5" step="0.1" value={organicKg} onChange={(e) => setOrganicKg(parseFloat(e.target.value))} className="custom-slider" />
              </div>
            </div>

            <button type="submit" className="btn btn-primary" style={{ width: '100%', marginTop: '2rem' }}>
              💾 Calculate & Save Daily Logs (+20 XP)
            </button>
          </form>
        )}

        {/* CARBON TWIN PANEL */}
        {activeTab === 'twin' && (
          <div className="glass-card animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: '2rem' }}>
            <div>
              <h2>👥 User Carbon Twin</h2>
              <p style={{ fontSize: '0.85rem', color: 'hsl(var(--text-muted))' }}>
                A digital replication of your current lifestyle habits. It forecasts emission indexes into the future.
              </p>
            </div>

            <div className="dashboard-grid">
              <div className="col-3 glass-card" style={{ textAlign: 'center', border: '1px solid rgba(255,255,255,0.05)' }}>
                <span style={{ fontSize: '0.8rem', color: 'hsl(var(--text-muted))', textTransform: 'uppercase' }}>Current You</span>
                <h1 style={{ color: 'hsl(var(--accent))', margin: '0.5rem 0' }}>{twinForecast.current.co2.toFixed(1)}</h1>
                <p style={{ fontSize: '0.85rem', fontWeight: 700 }}>Score: {twinForecast.current.score}</p>
              </div>
              <div className="col-3 glass-card" style={{ textAlign: 'center', border: '1px solid rgba(16, 185, 129, 0.1)' }}>
                <span style={{ fontSize: '0.8rem', color: 'hsl(var(--text-muted))', textTransform: 'uppercase' }}>Future You (3m)</span>
                <h1 style={{ color: '#10B981', margin: '0.5rem 0' }}>{twinForecast.projection3m.co2.toFixed(1)}</h1>
                <p style={{ fontSize: '0.85rem', fontWeight: 700 }}>Score: {twinForecast.projection3m.score}</p>
              </div>
              <div className="col-3 glass-card" style={{ textAlign: 'center', border: '1px solid rgba(16, 185, 129, 0.2)' }}>
                <span style={{ fontSize: '0.8rem', color: 'hsl(var(--text-muted))', textTransform: 'uppercase' }}>Future You (6m)</span>
                <h1 style={{ color: '#10B981', margin: '0.5rem 0' }}>{twinForecast.projection6m.co2.toFixed(1)}</h1>
                <p style={{ fontSize: '0.85rem', fontWeight: 700 }}>Score: {twinForecast.projection6m.score}</p>
              </div>
              <div className="col-3 glass-card" style={{ textAlign: 'center', border: '1px solid rgba(16, 185, 129, 0.35)' }}>
                <span style={{ fontSize: '0.8rem', color: 'hsl(var(--text-muted))', textTransform: 'uppercase' }}>Future You (12m)</span>
                <h1 style={{ color: '#10B981', margin: '0.5rem 0' }}>{twinForecast.projection12m.co2.toFixed(1)}</h1>
                <p style={{ fontSize: '0.85rem', fontWeight: 700 }}>Score: {twinForecast.projection12m.score}</p>
              </div>
            </div>

            {/* Custom SVG Trend Graph */}
            <div className="glass-card" style={{ padding: '2rem' }}>
              <h4 style={{ marginBottom: '1.5rem' }}>📈 Emissions Trend Graph (12-Month Twin Projection)</h4>
              
              <div style={{ height: '180px', width: '100%', position: 'relative', display: 'flex', alignItems: 'flex-end', borderBottom: '1px solid rgba(255,255,255,0.1)', borderLeft: '1px solid rgba(255,255,255,0.1)', paddingBottom: '0.5rem' }}>
                
                {/* Horizontal scale helper lines */}
                <div style={{ position: 'absolute', left: 0, right: 0, bottom: '25%', height: '1px', borderBottom: '1px dashed rgba(255,255,255,0.03)' }} />
                <div style={{ position: 'absolute', left: 0, right: 0, bottom: '50%', height: '1px', borderBottom: '1px dashed rgba(255,255,255,0.03)' }} />
                <div style={{ position: 'absolute', left: 0, right: 0, bottom: '75%', height: '1px', borderBottom: '1px dashed rgba(255,255,255,0.03)' }} />

                {/* Graph bars representing predictions */}
                <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0.5rem' }}>
                  <div style={{ width: '40px', height: `${(twinForecast.current.co2 / 30) * 150}px`, background: 'hsl(var(--accent))', borderRadius: '4px 4px 0 0', minHeight: '10px', boxShadow: '0 0 10px rgba(6, 182, 212, 0.2)' }} />
                  <span style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>Current</span>
                </div>
                <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0.5rem' }}>
                  <div style={{ width: '40px', height: `${(twinForecast.projection3m.co2 / 30) * 150}px`, background: '#10B981', borderRadius: '4px 4px 0 0', minHeight: '10px' }} />
                  <span style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>3 Months</span>
                </div>
                <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0.5rem' }}>
                  <div style={{ width: '40px', height: `${(twinForecast.projection6m.co2 / 30) * 150}px`, background: '#10B981', borderRadius: '4px 4px 0 0', minHeight: '10px' }} />
                  <span style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>6 Months</span>
                </div>
                <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0.5rem' }}>
                  <div style={{ width: '40px', height: `${(twinForecast.projection12m.co2 / 30) * 150}px`, background: '#059669', borderRadius: '4px 4px 0 0', minHeight: '10px', boxShadow: '0 0 10px rgba(16, 185, 129, 0.2)' }} />
                  <span style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>12 Months</span>
                </div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', color: 'hsl(var(--text-muted))', marginTop: '0.5rem' }}>
                <span>0 kg CO₂</span>
                <span>Max: 30 kg CO₂ / day</span>
              </div>
            </div>

            {/* AI Predictions explanation box */}
            <div style={{
              background: 'rgba(6, 182, 212, 0.05)',
              border: '1px solid rgba(6, 182, 212, 0.2)',
              padding: '1.25rem',
              borderRadius: '16px',
              display: 'flex',
              flexDirection: 'column',
              gap: '0.5rem'
            }}>
              <h4 style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: 'hsl(var(--accent))' }}>🤖 AI Twin Insights</h4>
              <p style={{ fontSize: '0.9rem', lineHeight: '1.5', fontStyle: 'italic' }}>
                {twinForecast.explanation}
              </p>
            </div>
          </div>
        )}

        {/* SIMULATOR PANEL */}
        {activeTab === 'simulator' && (
          <div className="glass-card animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: '2rem' }}>
            <div>
              <h2>🎛️ AI What-If Simulator</h2>
              <p style={{ fontSize: '0.85rem', color: 'hsl(var(--text-muted))' }}>
                Simulate potential lifestyle decisions. Toggle options below to view predicted footprint cuts and get chemical AI explanations.
              </p>
            </div>

            <div className="dashboard-grid">
              {/* Checkboxes column */}
              <div className="col-5 glass-card" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                <h4 style={{ borderBottom: '1px solid hsl(var(--border-glass))', paddingBottom: '0.5rem', marginBottom: '0.5rem' }}>Select Scenarios</h4>
                
                <label style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', cursor: 'pointer', padding: '0.5rem 0' }}>
                  <input type="checkbox" checked={simEV} onChange={(e) => setSimEV(e.target.checked)} style={{ width: '18px', height: '18px' }} />
                  <div>
                    <span style={{ fontWeight: 600 }}>Switch to Electric Vehicle (EV)</span>
                    <p style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>Eliminates tailpipe emissions</p>
                  </div>
                </label>

                <label style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', cursor: 'pointer', padding: '0.5rem 0' }}>
                  <input type="checkbox" checked={simWFH} onChange={(e) => setSimWFH(e.target.checked)} style={{ width: '18px', height: '18px' }} />
                  <div>
                    <span style={{ fontWeight: 600 }}>Work From Home (WFH)</span>
                    <p style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>Avoid weekly road commuting</p>
                  </div>
                </label>

                <label style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', cursor: 'pointer', padding: '0.5rem 0' }}>
                  <input type="checkbox" checked={simSolar} onChange={(e) => setSimSolar(e.target.checked)} style={{ width: '18px', height: '18px' }} />
                  <div>
                    <span style={{ fontWeight: 600 }}>Install Solar Panels</span>
                    <p style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>Photovoltaic zero-carbon electricity</p>
                  </div>
                </label>

                <label style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', cursor: 'pointer', padding: '0.5rem 0' }}>
                  <input type="checkbox" checked={simBike} onChange={(e) => setSimBike(e.target.checked)} style={{ width: '18px', height: '18px' }} />
                  <div>
                    <span style={{ fontWeight: 600 }}>Commute by Bicycle</span>
                    <p style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>Zero-emissions muscle mobility</p>
                  </div>
                </label>

                <label style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', cursor: 'pointer', padding: '0.5rem 0' }}>
                  <input type="checkbox" checked={simMeat} onChange={(e) => setSimMeat(e.target.checked)} style={{ width: '18px', height: '18px' }} />
                  <div>
                    <span style={{ fontWeight: 600 }}>Adopt Plant-Based / Vegetarian</span>
                    <p style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>Slash food processing carbon</p>
                  </div>
                </label>
              </div>

              {/* Simulation Result Output */}
              <div className="col-7 glass-card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
                <h4 style={{ borderBottom: '1px solid hsl(var(--border-glass))', paddingBottom: '0.5rem', marginBottom: '0.5rem' }}>Simulated Footprint Output</h4>
                
                {simLoading ? (
                  <p style={{ color: 'hsl(var(--text-muted))', fontStyle: 'italic', margin: 'auto' }}>Calculating simulated values...</p>
                ) : simResult ? (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', marginTop: '0.5rem' }}>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
                      <div style={{ background: 'rgba(255,255,255,0.02)', padding: '1rem', borderRadius: '8px' }}>
                        <span style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>Original Footprint</span>
                        <h2>{simResult.currentEmissions} kg</h2>
                      </div>
                      <div style={{ background: 'rgba(16, 185, 129, 0.05)', padding: '1rem', borderRadius: '8px', border: '1px solid rgba(16, 185, 129, 0.1)' }}>
                        <span style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>Simulated Footprint</span>
                        <h2 style={{ color: '#10B981' }}>{simResult.predictedEmissions} kg</h2>
                      </div>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
                      <div style={{ background: 'rgba(6, 182, 212, 0.05)', padding: '1rem', borderRadius: '8px', border: '1px solid rgba(6, 182, 212, 0.1)' }}>
                        <span style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>Annual Carbon Saved</span>
                        <h3 style={{ color: 'hsl(var(--accent))' }}>{simResult.annualCO2Reduction} kg</h3>
                      </div>
                      <div style={{ background: 'rgba(255, 159, 67, 0.05)', padding: '1rem', borderRadius: '8px', border: '1px solid rgba(255, 159, 67, 0.1)' }}>
                        <span style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>Estimated Annual Cash Saved</span>
                        <h3 style={{ color: '#ff9f43' }}>${simResult.annualSavings.toFixed(0)}</h3>
                      </div>
                    </div>

                    {/* AI explanation block */}
                    <div style={{ background: 'rgba(0,0,0,0.15)', padding: '1rem', borderRadius: '12px', border: '1px solid hsl(var(--border-glass))', fontSize: '0.85rem', lineHeight: '1.4' }}>
                      <strong style={{ color: 'hsl(var(--primary))', display: 'block', marginBottom: '0.25rem' }}>🔬 Gemini Explanation:</strong>
                      <span style={{ fontStyle: 'italic' }}>{simResult.explanation}</span>
                    </div>
                  </div>
                ) : (
                  <p style={{ color: 'hsl(var(--text-muted))', fontStyle: 'italic', margin: 'auto' }}>Please select one or more scenarios to simulate.</p>
                )}
              </div>
            </div>
          </div>
        )}

        {/* AI COACH tips panel */}
        {activeTab === 'coach' && (
          <div className="animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
            <div>
              <h2>🤖 AI Carbon Coach Console</h2>
              <p style={{ fontSize: '0.85rem', color: 'hsl(var(--text-muted))' }}>
                Seek immediate green advice, ask questions, or read your custom carbon reduction recommendations.
              </p>
            </div>

            <div className="dashboard-grid">
              {/* Left Column: Recommendations Feed */}
              <div className="col-5 glass-card" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                <h3 style={{ borderBottom: '1px solid hsl(var(--border-glass))', paddingBottom: '0.5rem', marginBottom: '0.5rem', color: 'hsl(var(--accent))' }}>📋 Suggested Actions</h3>
                <div style={{
                  lineHeight: '1.6',
                  fontSize: '0.85rem',
                  whiteSpace: 'pre-wrap',
                  color: 'hsl(var(--text-secondary))'
                }}>
                  {coachTips}
                </div>
                <div style={{
                  background: 'rgba(16, 185, 129, 0.04)',
                  border: '1px solid rgba(16, 185, 129, 0.15)',
                  padding: '0.75rem',
                  borderRadius: '8px',
                  fontSize: '0.75rem',
                  color: '#10B981',
                  marginTop: 'auto'
                }}>
                  💡 Averages from the last 7 days are processed by our regression model to tailor these items.
                </div>
              </div>

              {/* Right Column: Live Chat Interface */}
              <div className="col-7 glass-card" style={{ display: 'flex', flexDirection: 'column', height: '500px', padding: '1.25rem' }}>
                <h3 style={{ borderBottom: '1px solid hsl(var(--border-glass))', paddingBottom: '0.5rem', marginBottom: '0.75rem', color: 'hsl(var(--primary))' }}>💬 Chat with EcoPilot AI</h3>
                
                {/* Chat Message Window */}
                <div style={{ 
                  flex: 1, 
                  overflowY: 'auto', 
                  display: 'flex', 
                  flexDirection: 'column', 
                  gap: '0.75rem', 
                  paddingRight: '0.5rem', 
                  marginBottom: '1rem' 
                }}>
                  {chatMessages.map((msg, idx) => (
                    <div 
                      key={idx} 
                      style={{ 
                        alignSelf: msg.sender === 'user' ? 'flex-end' : 'flex-start',
                        maxWidth: '85%',
                        background: msg.sender === 'user' ? 'hsl(var(--primary-glow))' : 'rgba(255,255,255,0.04)',
                        border: `1px solid ${msg.sender === 'user' ? 'hsl(var(--primary) / 0.25)' : 'hsl(var(--border-glass))'}`,
                        borderRadius: msg.sender === 'user' ? '12px 12px 0 12px' : '12px 12px 12px 0',
                        padding: '0.75rem 1rem',
                        fontSize: '0.85rem',
                        lineHeight: '1.4',
                        whiteSpace: 'pre-wrap'
                      }}
                    >
                      <strong style={{ display: 'block', fontSize: '0.75rem', marginBottom: '0.2rem', color: msg.sender === 'user' ? 'hsl(var(--primary))' : 'hsl(var(--accent))' }}>
                        {msg.sender === 'user' ? 'You' : 'EcoPilot AI'}
                      </strong>
                      {msg.text}
                    </div>
                  ))}
                  
                  {chatLoading && (
                    <div style={{ alignSelf: 'flex-start', background: 'rgba(255,255,255,0.02)', border: '1px solid hsl(var(--border-glass))', borderRadius: '12px 12px 12px 0', padding: '0.75rem 1rem', fontSize: '0.85rem', color: 'hsl(var(--text-muted))', fontStyle: 'italic' }}>
                      EcoPilot AI is thinking...
                    </div>
                  )}
                </div>

                {/* Chat Input form */}
                <form 
                  onSubmit={handleSendMessage}
                  style={{ display: 'flex', gap: '0.5rem', borderTop: '1px solid hsl(var(--border-glass))', paddingTop: '0.75rem' }}
                >
                  <input 
                    type="text" 
                    value={chatInput}
                    onChange={(e) => setChatInput(e.target.value)}
                    placeholder="Ask about reducing vehicle emissions, vegan diet tips, home power savers..."
                    className="form-input"
                    style={{ flex: 1, fontSize: '0.85rem' }}
                    disabled={chatLoading}
                    required
                  />
                  <button type="submit" className="btn btn-primary" style={{ padding: '0.5rem 1.25rem', fontSize: '0.85rem' }} disabled={chatLoading}>
                    Send
                  </button>
                </form>
              </div>
            </div>
          </div>
        )}

        {/* GREEN MISSIONS PANEL */}
        {activeTab === 'missions' && (
          <div className="glass-card animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: '2rem' }}>
            <div>
              <h2>⚔️ Personalized Green Missions</h2>
              <p style={{ fontSize: '0.85rem', color: 'hsl(var(--text-muted))' }}>
                AI-generated weekly challenges matching your emission categories. Complete challenges to earn XP and level up.
              </p>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
              {missions.map((mission) => (
                <div key={mission.id} style={{
                  padding: '1.25rem',
                  background: 'rgba(0,0,0,0.15)',
                  border: '1px solid hsl(var(--border-glass))',
                  borderRadius: '16px',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  flexWrap: 'wrap',
                  gap: '1rem'
                }}>
                  <div style={{ flex: 1, minWidth: '280px' }}>
                    <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.5rem' }}>
                      <span style={{ fontSize: '0.7rem', fontWeight: 700, padding: '0.15rem 0.4rem', border: '1px solid hsl(var(--accent))', borderRadius: '4px', color: 'hsl(var(--accent))', textTransform: 'uppercase' }}>
                        {mission.difficulty}
                      </span>
                      <span style={{ fontSize: '0.75rem', color: 'hsl(var(--text-muted))' }}>{mission.category}</span>
                    </div>
                    <h4>{mission.title}</h4>
                    <p style={{ fontSize: '0.85rem', color: 'hsl(var(--text-secondary))', marginTop: '0.25rem' }}>{mission.description}</p>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                    <div style={{ textAlign: 'right' }}>
                      <span style={{ fontSize: '0.8rem', background: 'rgba(16,185,129,0.1)', color: '#10B981', padding: '0.2rem 0.5rem', borderRadius: '6px', fontWeight: 700, marginRight: '0.4rem' }}>
                        +{mission.rewardXP} XP
                      </span>
                      <span style={{ fontSize: '0.8rem', background: 'rgba(6,182,212,0.1)', color: 'hsl(var(--accent))', padding: '0.2rem 0.5rem', borderRadius: '6px', fontWeight: 700 }}>
                        +{mission.rewardPoints} Pts
                      </span>
                    </div>
                    <button 
                      onClick={() => handleCompleteMission(mission.id)}
                      className="btn btn-primary"
                      style={{ padding: '0.5rem 1rem', fontSize: '0.85rem' }}
                    >
                      ✓ Complete
                    </button>
                  </div>
                </div>
              ))}

              {missions.length === 0 && (
                <p style={{ color: 'hsl(var(--text-muted))', fontStyle: 'italic', textAlign: 'center' }}>No active challenges. Complete daily logging to fetch personalized missions.</p>
              )}
            </div>
          </div>
        )}

        {/* LEADERBOARD PANEL */}
        {activeTab === 'leaderboard' && (
          <div className="glass-card animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: '2rem' }}>
            <div>
              <h2>🏆 Global Leaderboard (Anonymous)</h2>
              <p style={{ fontSize: '0.85rem', color: 'hsl(var(--text-muted))' }}>
                Compare your scores anonymously against other players. User profile specifics are hidden to protect privacy.
              </p>
            </div>

            <div className="dashboard-grid">
              {/* Leaderboard Left: Score rankings */}
              <div className="col-6 glass-card">
                <h3 style={{ marginBottom: '1.25rem', borderBottom: '1px solid hsl(var(--border-glass))', paddingBottom: '0.5rem', color: 'hsl(var(--accent))' }}>🌟 Carbon Score Rank</h3>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                  {leaderboard.scoreLeaderboard.map((entry: any) => (
                    <div 
                      key={entry.anonymousName} 
                      style={{ 
                        display: 'flex', 
                        justifyContent: 'space-between', 
                        padding: '0.75rem 1rem', 
                        background: entry.isCurrentUser ? 'hsl(var(--primary-glow))' : 'rgba(255,255,255,0.02)', 
                        border: `1px solid ${entry.isCurrentUser ? 'hsl(var(--primary) / 0.3)' : 'hsl(var(--border-glass))'}`,
                        borderRadius: '8px',
                        fontSize: '0.85rem'
                      }}
                    >
                      <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
                        <span style={{ fontWeight: 800, color: entry.rank <= 3 ? '#ff9f43' : 'hsl(var(--text-muted))' }}>#{entry.rank}</span>
                        <span style={{ fontWeight: entry.isCurrentUser ? 700 : 500 }}>{entry.anonymousName}</span>
                      </div>
                      <div style={{ fontWeight: 700, color: '#10B981' }}>{entry.carbonScore} pts</div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Leaderboard Right: Improvement rankings */}
              <div className="col-6 glass-card">
                <h3 style={{ marginBottom: '1.25rem', borderBottom: '1px solid hsl(var(--border-glass))', paddingBottom: '0.5rem', color: 'hsl(var(--accent))' }}>🔥 Weekly Improvement</h3>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                  {leaderboard.improvementLeaderboard.map((entry: any) => (
                    <div 
                      key={entry.anonymousName} 
                      style={{ 
                        display: 'flex', 
                        justifyContent: 'space-between', 
                        padding: '0.75rem 1rem', 
                        background: entry.isCurrentUser ? 'hsl(var(--primary-glow))' : 'rgba(255,255,255,0.02)', 
                        border: `1px solid ${entry.isCurrentUser ? 'hsl(var(--primary) / 0.3)' : 'hsl(var(--border-glass))'}`,
                        borderRadius: '8px',
                        fontSize: '0.85rem'
                      }}
                    >
                      <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
                        <span style={{ fontWeight: 800, color: entry.rank <= 3 ? '#ff9f43' : 'hsl(var(--text-muted))' }}>#{entry.rank}</span>
                        <span style={{ fontWeight: entry.isCurrentUser ? 700 : 500 }}>{entry.anonymousName}</span>
                      </div>
                      <div style={{ fontWeight: 700, color: 'hsl(var(--accent))' }}>
                        {entry.improvementRate >= 0 ? `-${entry.improvementRate} kg CO₂` : `+${Math.abs(entry.improvementRate)} kg CO₂`}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* IMPACT STORY PANEL */}
        {activeTab === 'story' && (
          <div className="glass-card animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: '2rem', maxWidth: '800px', margin: '0 auto' }}>
            <div>
              <h2>📖 Monthly Eco Impact Story</h2>
              <p style={{ fontSize: '0.85rem', color: 'hsl(var(--text-muted))' }}>
                A dynamic summary report transforming abstract carbon statistics into meaningful biological equivalents.
              </p>
            </div>

            {monthlyStory ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                {/* Visual Report Card */}
                <div style={{
                  background: 'linear-gradient(135deg, rgba(16, 185, 129, 0.1), rgba(6, 182, 212, 0.1))',
                  border: '1px solid rgba(16, 185, 129, 0.25)',
                  padding: '2rem',
                  borderRadius: '20px',
                  boxShadow: 'var(--shadow-premium)'
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px dashed rgba(255,255,255,0.1)', paddingBottom: '1rem', marginBottom: '1.5rem' }}>
                    <div>
                      <h3 style={{ color: 'white' }}>EcoPilot Report Card</h3>
                      <span style={{ fontSize: '0.8rem', color: 'hsl(var(--text-muted))' }}>{monthlyStory.monthName}</span>
                    </div>
                    <div style={{ fontSize: '2.5rem' }}>🏆</div>
                  </div>

                  <div className="dashboard-grid" style={{ marginBottom: '1.5rem' }}>
                    <div className="col-6">
                      <span style={{ fontSize: '0.8rem', color: 'hsl(var(--text-muted))' }}>Carbon Avoided</span>
                      <h2 style={{ color: '#10B981', margin: '0.2rem 0' }}>{monthlyStory.carbonReductionKg.toFixed(1)} kg CO₂</h2>
                    </div>
                    <div className="col-6">
                      <span style={{ fontSize: '0.8rem', color: 'hsl(var(--text-muted))' }}>Fuel Saved</span>
                      <h2 style={{ color: 'hsl(var(--accent))', margin: '0.2rem 0' }}>{monthlyStory.fuelSavedLiters} Liters</h2>
                    </div>
                  </div>

                  <div className="dashboard-grid" style={{ borderBottom: '1px dashed rgba(255,255,255,0.1)', paddingBottom: '1.5rem', marginBottom: '1.5rem' }}>
                    <div className="col-6">
                      <span style={{ fontSize: '0.8rem', color: 'hsl(var(--text-muted))' }}>Electricity Conservation</span>
                      <h2 style={{ color: '#ff9f43', margin: '0.2rem 0' }}>{monthlyStory.electricityCutPercentage}% Cut</h2>
                    </div>
                    <div className="col-6">
                      <span style={{ fontSize: '0.8rem', color: 'hsl(var(--text-muted))' }}>Forest Equivalent</span>
                      <h2 style={{ color: '#10B981', margin: '0.2rem 0' }}>{monthlyStory.environmentalEquivalents.virtualTreesPlanted} Trees</h2>
                    </div>
                  </div>

                  {/* Gemini story summary */}
                  <div style={{ fontStyle: 'italic', fontSize: '0.95rem', lineHeight: '1.5', whiteSpace: 'pre-wrap', color: 'hsl(var(--text-secondary))' }}>
                    {monthlyStory.storySummary}
                  </div>
                </div>

                <div style={{ display: 'flex', gap: '1rem' }}>
                  <button 
                    onClick={() => {
                      navigator.clipboard.writeText(`This month I saved ${monthlyStory.carbonReductionKg} kg of CO2 emissions and ${monthlyStory.fuelSavedLiters} liters of fuel with EcoPilot AI! Equivalent to planting ${monthlyStory.environmentalEquivalents.virtualTreesPlanted} trees. Join me!`);
                      showToast("Copied card statistics to clipboard!");
                    }}
                    className="btn btn-accent"
                    style={{ flex: 1 }}
                  >
                    🔗 Share Report Card
                  </button>
                  <button onClick={() => setActiveTab('overview')} className="btn btn-outline" style={{ flex: 1 }}>
                    Back to Console
                  </button>
                </div>
              </div>
            ) : (
              <p style={{ color: 'hsl(var(--text-muted))', fontStyle: 'italic', textAlign: 'center' }}>Logging daily activities is required to build your monthly story card.</p>
            )}
          </div>
        )}
      </main>
      {/* Onboarding Wizard Modal overlay */}
      {showOnboarding && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          width: '100vw',
          height: '100vh',
          background: 'rgba(5, 8, 22, 0.85)',
          backdropFilter: 'blur(12px)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 9999,
          padding: '1.5rem'
        }}>
          <div className="glass-card" style={{ maxWidth: '500px', width: '100%', padding: '2.5rem', background: 'rgba(15, 23, 42, 0.95)', border: '1px solid hsl(var(--primary) / 0.2)', animation: 'fadeInUp 0.3s ease' }}>
            <div style={{ textAlign: 'center', marginBottom: '1.5rem' }}>
              <span style={{ fontSize: '2.5rem', animation: 'float 4s ease-in-out infinite', display: 'inline-block' }}>🚀</span>
              <h2 style={{ background: 'linear-gradient(135deg, hsl(var(--primary)), hsl(var(--accent)))', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent', marginTop: '0.5rem' }}>Welcome to EcoPilot AI</h2>
              <p style={{ fontSize: '0.85rem', color: 'hsl(var(--text-muted))', marginTop: '0.25rem' }}>Let's configure your baseline sustainability profile in 3 fast questions.</p>
            </div>

            {/* Step Indicators */}
            <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '2rem', justifyContent: 'center' }}>
              <div style={{ width: '40px', height: '4px', background: onboardStep >= 1 ? 'hsl(var(--primary))' : 'rgba(255,255,255,0.1)', borderRadius: '2px' }} />
              <div style={{ width: '40px', height: '4px', background: onboardStep >= 2 ? 'hsl(var(--primary))' : 'rgba(255,255,255,0.1)', borderRadius: '2px' }} />
              <div style={{ width: '40px', height: '4px', background: onboardStep >= 3 ? 'hsl(var(--primary))' : 'rgba(255,255,255,0.1)', borderRadius: '2px' }} />
            </div>

            {/* STEP 1: Commuting */}
            {onboardStep === 1 && (
              <div className="animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
                <h3>Step 1: Your Commute Method</h3>
                <p style={{ fontSize: '0.85rem' }}>How do you primarily travel on a typical day?</p>
                <div className="form-group">
                  <label>Primary Transit Mode</label>
                  <select value={onboardCommute} onChange={(e) => setOnboardCommute(e.target.value)} className="form-select">
                    <option value="Petrol">Petrol Car</option>
                    <option value="Diesel">Diesel Car</option>
                    <option value="Hybrid">Hybrid Car</option>
                    <option value="EV">Electric Vehicle (EV)</option>
                    <option value="PublicTransit">Public Transit (Bus/Train)</option>
                    <option value="Bicycle">Bicycle / Walking</option>
                  </select>
                </div>
                {onboardCommute !== 'Bicycle' && onboardCommute !== 'PublicTransit' && (
                  <div className="form-group">
                    <label>Average Daily Distance (km)</label>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.3rem', fontSize: '0.8rem', fontWeight: 600 }}>
                      <span>Estimated distance</span>
                      <span style={{ color: 'hsl(var(--primary))' }}>{onboardKm} km</span>
                    </div>
                    <input type="range" min="5" max="100" step="5" value={onboardKm} onChange={(e) => setOnboardKm(parseInt(e.target.value))} className="custom-slider" />
                  </div>
                )}
                <button onClick={() => setOnboardStep(2)} className="btn btn-primary" style={{ width: '100%', marginTop: '1rem' }}>
                  Next Question
                </button>
              </div>
            )}

            {/* STEP 2: Home Cooling/Heating */}
            {onboardStep === 2 && (
              <div className="animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
                <h3>Step 2: Home Air Conditioning</h3>
                <p style={{ fontSize: '0.85rem' }}>On average, how many hours per day do you run air conditioning?</p>
                <div className="form-group">
                  <label>AC Operation Duration</label>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.3rem', fontSize: '0.8rem', fontWeight: 600 }}>
                    <span>Daily run time</span>
                    <span style={{ color: 'hsl(var(--accent))' }}>{onboardAC} Hours</span>
                  </div>
                  <input type="range" min="0" max="12" step="1" value={onboardAC} onChange={(e) => setOnboardAC(parseInt(e.target.value))} className="custom-slider" />
                </div>
                <div style={{ display: 'flex', gap: '1rem', marginTop: '1rem' }}>
                  <button onClick={() => setOnboardStep(1)} className="btn btn-outline" style={{ flex: 1 }}>
                    Back
                  </button>
                  <button onClick={() => setOnboardStep(3)} className="btn btn-primary" style={{ flex: 1 }}>
                    Next Question
                  </button>
                </div>
              </div>
            )}

            {/* STEP 3: Diet */}
            {onboardStep === 3 && (
              <div className="animate-fade-in" style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
                <h3>Step 3: Food & Diet Profile</h3>
                <p style={{ fontSize: '0.85rem' }}>Select the diet profile that closest matches your eating habits:</p>
                <div className="form-group">
                  <label>Daily Diet Type</label>
                  <select value={onboardDiet} onChange={(e) => setOnboardDiet(e.target.value)} className="form-select">
                    <option value="Meat">Regular Meat Consumer (Heavy poultry/beef)</option>
                    <option value="Flexitarian">Flexitarian (Minimal meat / fish only)</option>
                    <option value="Vegetarian">Vegetarian (No meat, eggs/dairy allowed)</option>
                    <option value="Vegan">Vegan (100% plant-based diet)</option>
                  </select>
                </div>
                <div style={{ display: 'flex', gap: '1rem', marginTop: '1rem' }}>
                  <button onClick={() => setOnboardStep(2)} className="btn btn-outline" style={{ flex: 1 }}>
                    Back
                  </button>
                  <button onClick={handleOnboardSubmit} className="btn btn-accent" style={{ flex: 1 }}>
                    Finish Setup 🚀
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
