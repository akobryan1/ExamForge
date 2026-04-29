# ExamForge Web Application

A modern web-based exam management platform rebuilt from WPF using React and Node.js, leveraging cloud infrastructure for scalability.

## 🏗️ Architecture

- **Frontend**: React 18 + TypeScript + Vite
- **Backend**: Node.js + Express + TypeScript
- **Database**: Google Cloud Firestore
- **Authentication**: Supabase + Firebase Auth
- **Real-time**: SignalR (existing Render.com deployment)
- **Deployment**: Render.com (Frontend + Backend)

## 🚀 Getting Started

### Prerequisites

- Node.js 18+ and npm
- Firebase project credentials
- Supabase account
- Render.com account (for deployment)

### Local Development

#### 1. Clone the repository

```bash
git clone <repository-url>
cd "Exam Forge"
```

#### 2. Setup Frontend

```bash
cd frontend
npm install
cp .env.example .env
# Edit .env with your credentials
npm run dev
```

Frontend will run on `http://localhost:3000`

#### 3. Setup Backend

```bash
cd backend
npm install
cp .env.example .env
# Edit .env with your credentials
npm run dev
```

Backend will run on `http://localhost:5000`

### Environment Variables

#### Frontend (.env)

```env
VITE_API_BASE_URL=http://localhost:5000
VITE_FIREBASE_API_KEY=your_firebase_api_key
VITE_FIREBASE_AUTH_DOMAIN=examforge-201e8.firebaseapp.com
VITE_FIREBASE_PROJECT_ID=examforge-201e8
VITE_SUPABASE_URL=https://fachmhcsfxjtgifexbyd.supabase.co
VITE_SUPABASE_ANON_KEY=your_supabase_anon_key
VITE_SIGNALR_HUB_URL=https://examforge-signalr.onrender.com/sessionHub
```

#### Backend (.env)

```env
NODE_ENV=development
PORT=5000
CORS_ORIGIN=http://localhost:3000
JWT_SECRET=your-secret-key
FIRESTORE_PROJECT_ID=examforge-201e8
GOOGLE_APPLICATION_CREDENTIALS=./firebase-adminsdk.json
SUPABASE_URL=https://fachmhcsfxjtgifexbyd.supabase.co
SUPABASE_SERVICE_ROLE_KEY=your_service_role_key
```

## 📦 Deployment

### Render.com (Recommended)

1. Connect your GitHub repository to Render
2. Render will automatically detect `render.yaml`
3. Configure environment variables in Render dashboard
4. Deploy both frontend and backend services

### Manual Deployment

#### Frontend (Static Site)

```bash
cd frontend
npm run build
# Deploy ./dist folder to any static hosting
```

#### Backend (Node.js Service)

```bash
cd backend
npm run build
npm start
```

## 🎨 Design System

ExamForge uses a custom Editorial/Magazine design aesthetic:

- **Typography**: Playfair Display + Crimson Pro + Source Sans 3
- **Colors**: Deep indigo primary, warm amber accents, sophisticated neutrals
- **Motion**: Framer Motion for refined animations
- **Components**: Custom-built for distinctive UI

See `/frontend/src/styles/design-system.css` for full design tokens.

## 📚 Project Structure

```
Exam Forge/
├── frontend/              # React frontend
│   ├── src/
│   │   ├── components/    # Reusable components
│   │   ├── pages/         # Page components
│   │   ├── contexts/      # React contexts
│   │   ├── hooks/         # Custom hooks
│   │   ├── services/      # API services
│   │   ├── config/        # Configuration files
│   │   ├── styles/        # CSS and design system
│   │   ├── types/         # TypeScript types
│   │   └── utils/         # Utility functions
│   └── package.json
├── backend/               # Node.js backend
│   ├── src/
│   │   ├── routes/        # API routes
│   │   ├── services/      # Business logic
│   │   ├── middleware/    # Express middleware
│   │   ├── config/        # Configuration
│   │   └── types/         # TypeScript types
│   └── package.json
└── render.yaml            # Render.com deployment config
```

## 🔧 Development Scripts

### Frontend

- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run preview` - Preview production build
- `npm run lint` - Lint code

### Backend

- `npm run dev` - Start development server with hot reload
- `npm run build` - Compile TypeScript
- `npm start` - Run production server
- `npm run lint` - Lint code

## 🧪 Testing

```bash
# Frontend tests
cd frontend
npm test

# Backend tests
cd backend
npm test
```

## 📖 Documentation

- [Frontend Design Guidelines](./agents/FRONTEND_DESIGN.md)
- [API Documentation](./DOCUMENTATION/03_API_Documentation.md)
- [User Manual](./DOCUMENTATION/02_User_Manual.md)
- [Deployment Guide](./DOCUMENTATION/01_Deployment_Guide.md)

## 🤝 Contributing

1. Create a feature branch
2. Make your changes
3. Test thoroughly
4. Submit a pull request

## 📄 License

[License information]

## 🛠️ Tech Stack

- **Frontend**: React, TypeScript, Vite, Framer Motion, React Query, Zustand
- **Backend**: Node.js, Express, TypeScript
- **Database**: Google Cloud Firestore
- **Auth**: Supabase, Firebase Auth
- **Real-time**: SignalR
- **Deployment**: Render.com
- **AI**: DeepSeek API for essay grading

## 🚧 Migration Status

This is a progressive migration from the WPF desktop application. Current implementation status:

- [x] Phase 1: Foundation & Infrastructure
- [x] Phase 2: Authentication System
- [x] Phase 3: Core Data Services (Firestore + Exam Management)
- [x] Phase 4: Exam Management Interface
- [ ] Phase 5: Publishing & Student Interface
- [ ] Phase 6: Real-Time Monitoring & Anti-Cheat
- [ ] Phase 7: Grading & Analytics
- [ ] Phase 8: Advanced Features
- [ ] Phase 9: Polish & Deployment

See `/memories/session/plan.md` for the complete migration roadmap.
