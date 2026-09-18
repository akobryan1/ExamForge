# ExamForge

A modern, web-based exam management platform: instructors author exams and question banks, students take them under proctoring rules, and submitted work flows into an automated and AI-assisted grading queue.

> **Status:** beta. [`BETA_TESTER_CHECKLIST.md`](./BETA_TESTER_CHECKLIST.md) tracks the manual test pass and [`IMPLEMENTATION_SUMMARY.md`](./IMPLEMENTATION_SUMMARY.md) records what has shipped.

## 🏗️ Architecture

| Layer | Technology |
| --- | --- |
| Front end | React 18, TypeScript, Vite, React Query, Zustand, Framer Motion |
| Back end | Node.js, Express, TypeScript |
| Database | Google Cloud Firestore |
| Auth | Firebase Authentication + JWT (access/refresh tokens) |
| Real-time | SignalR hub (separate Render service) |
| AI | DeepSeek chat completions for essay grading (placeholder key — see below) |
| Edge guard | Cloudflare Worker for AI-grading rate limits and keep-alive pings |
| Hosting | Render.com |

### Applications in this repository

| Path | Purpose |
| --- | --- |
| `frontend/` | Instructor/admin SPA — dashboard, exam builder, question bank, grading queue, analytics, settings |
| `backend/` | REST API shared by every client |
| `exam-portal/` | Standalone portal for previewing and taking an exam from a direct link |
| `student-portal/` | Public examinee portal — self-registration, exam preview, taking, results |
| `cloudflare-worker/` | Edge guard for the AI grading endpoint |

## 🚀 Getting Started

### Prerequisites

- Node.js 18+ and npm
- A Firebase project with Firestore and Authentication enabled
- A Supabase project (optional for local development; used for auxiliary data)
- A DeepSeek API key — only needed if you want AI essay grading to actually run (see [AI grading configuration](#ai-grading-configuration))

### Local Development

#### 1. Clone the repository

```bash
git clone https://github.com/akobryan1/ExamForge.git
cd ExamForge
```

#### 2. Set up the backend

```bash
cd backend
npm install
cp .env.example .env      # Windows: copy .env.example .env
# Fill in your own credentials — see Environment Variables below
npm run dev
```

The API listens on `http://localhost:5000`; health check at `GET /health`.

#### 3. Set up the instructor front end

```bash
cd frontend
npm install
cp .env.example .env
npm run dev
```

The app runs on `http://localhost:3000` and proxies `/api/*` to `http://localhost:5000`.

#### 4. Set up the portals (optional)

`exam-portal/` and `student-portal/` are independent Vite apps using the same workflow — `npm install`, copy `.env.example` to `.env`, then `npm run dev`. Point their `VITE_API_BASE_URL` at your local API to exercise them against a local backend.

| App | Dev URL |
| --- | --- |
| `frontend/` | `http://localhost:3000` |
| `student-portal/` | `http://localhost:3001` |
| `exam-portal/` | `http://localhost:3002` |

### Environment Variables

Every service ships a committed `.env.example` holding placeholders. Copy it to `.env` and substitute your own values. Real credentials are never committed — see [🔐 Security](#-security).

#### Frontend (`frontend/.env`)

```env
VITE_API_BASE_URL=http://localhost:5000
VITE_FIREBASE_API_KEY=your_firebase_api_key
VITE_FIREBASE_AUTH_DOMAIN=your-project.firebaseapp.com
VITE_FIREBASE_PROJECT_ID=your-project-id
VITE_FIREBASE_STORAGE_BUCKET=your-project.appspot.com
VITE_FIREBASE_MESSAGING_SENDER_ID=your_sender_id
VITE_FIREBASE_APP_ID=your_app_id
VITE_SUPABASE_URL=https://your-project.supabase.co
VITE_SUPABASE_ANON_KEY=your_supabase_anon_key
VITE_SIGNALR_HUB_URL=https://your-signalr-service.onrender.com/sessionHub
VITE_EXAM_PORTAL_URL=http://localhost:3002
VITE_STUDENT_PORTAL_URL=http://localhost:3001
```

#### Backend (`backend/.env`)

```env
NODE_ENV=development
PORT=5000
CORS_ORIGIN=http://localhost:3000

# JWT
JWT_SECRET=your-super-secret-jwt-key-change-this-in-production
JWT_EXPIRES_IN=7d
JWT_REFRESH_SECRET=your-super-secret-refresh-key-change-this
JWT_REFRESH_EXPIRES_IN=30d

# Firebase / Firestore — provide ONE of: the full service-account JSON on a
# single line, the individual fields, or a path to a downloaded key file
GOOGLE_APPLICATION_CREDENTIALS=./firebase-adminsdk.json
GOOGLE_APPLICATION_CREDENTIALS_JSON={"type":"service_account",...}
FIRESTORE_PROJECT_ID=your-project-id
# FIREBASE_PROJECT_ID=your-project-id
# FIREBASE_CLIENT_EMAIL=firebase-adminsdk-xxxxx@your-project-id.iam.gserviceaccount.com
# FIREBASE_PRIVATE_KEY="-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----\n"

# Supabase
SUPABASE_URL=https://your-project.supabase.co
SUPABASE_ANON_KEY=your_supabase_anon_key
SUPABASE_SERVICE_ROLE_KEY=your_supabase_service_role_key

# SignalR + companion services
SIGNALR_HUB_URL=https://your-signalr-service.onrender.com/sessionHub
ESSAY_GRADING_API_URL=https://your-signalr-service.onrender.com/api/essay/grade
PUBLISHING_SERVER_URL=https://your-publisher-service.onrender.com

# AI (see "AI grading configuration")
DEEPSEEK_API_KEY=your_deepseek_api_key
```

### AI grading configuration

Essay grading calls DeepSeek's chat-completions API. **The repository currently ships with a placeholder key, so no live credential is baked into the code or used at runtime:**

- `backend/src/config/ai.ts` exports `PLACEHOLDER_AI_API_KEY = 'sk-placeholder-key-replace-in-production'`.
- `backend/src/routes/exams.ts` (essay grading, plus the anti-drift verification pass) and `backend/src/routes/settings.ts` (settings read-out) use that constant instead of reading `process.env.DEEPSEEK_API_KEY`.
- The `DEEPSEEK_API_KEY` entry in `render.yaml` is declared with `sync: false` and is **not read** while the placeholder is in place, so `POST /api/exams/grading/ai-grade` fails until a real key is configured.

To enable real AI grading:

1. Set `DEEPSEEK_API_KEY` in your environment (on Render: service → **Environment**).
2. In `backend/src/routes/exams.ts` and `backend/src/routes/settings.ts`, switch the key source back to `process.env.DEEPSEEK_API_KEY || ''`.
3. Delete `backend/src/config/ai.ts` (or repoint it at the env var).

## 📦 Deployment

### Render.com (recommended)

[`render.yaml`](./render.yaml) is a Render Blueprint that defines these services:

| Service | Type | Root directory |
| --- | --- | --- |
| `examforge-backend` | Web service (Node) | `backend` |
| `examforge-frontend` | Static site | `frontend` |
| `examforge-exam-portal` | Static site | `exam-portal` |

1. Connect this GitHub repository to Render.
2. Render detects `render.yaml` — apply the Blueprint.
3. Fill in the values marked `sync: false` (Firebase, Supabase, `CORS_ORIGIN`, and `DEEPSEEK_API_KEY` if you want AI grading) in each service's **Environment** tab.

`JWT_SECRET` and `JWT_REFRESH_SECRET` are generated by Render (`generateValue: true`), and the front end derives `VITE_API_BASE_URL` from the backend service's URL. `student-portal/` is deployed as its own service and is not part of the Blueprint.

### Cloudflare Worker (optional)

`cloudflare-worker/` reverse-proxies `POST /api/exams/grading/ai-grade`, rate-limits it per JWT subject (5 requests / 60 s), and runs a cron keep-alive ping against the API's `/health` endpoint. Configure it with:

- `RENDER_BACKEND_URL` — the API's public URL
- `JWT_SECRET` — must match the backend's `JWT_SECRET`
- A KV namespace bound as `examforge_rate_limit`

```bash
cd cloudflare-worker
npx wrangler deploy
```

### Manual build

```bash
# Instructor app
cd frontend && npm run build      # output: frontend/dist

# API
cd backend && npm run build && npm start
```

## 🎨 Design System

The UI uses a **ledger / grade-book** aesthetic — paper and ink surfaces with kraft and gold accents, evoking ruled paper, stamps, and index tabs.

- **Display**: Fraunces — **Body**: Inter — **Mono**: JetBrains Mono
- **Ledger tokens**: `--ledger-paper`, `--ledger-card`, `--ledger-ink`, `--ledger-kraft`, `--ledger-gold`, `--font-display-ledger`, `--font-mono-ledger` in [`frontend/src/styles/design-system.css`](./frontend/src/styles/design-system.css)
- **Page styles**: `frontend/src/styles/pages/*.css`
- **Motion**: Framer Motion

## 📚 Project Structure

```
Exam Forge/
├── backend/                  # Express + TypeScript API
│   ├── database/             # Firestore-related scratch space (currently empty)
│   └── src/
│       ├── config/           # Firebase, Supabase, AI key
│       ├── middleware/       # Auth and role guards
│       ├── routes/           # API route modules
│       ├── services/         # Business logic + AI grading pipeline
│       ├── types/            # Shared TypeScript types
│       └── utils/            # JWT, cache, activity logging
├── frontend/                 # Instructor/admin SPA
│   └── src/
│       ├── components/       # Reusable UI
│       ├── pages/            # Route-level screens
│       ├── hooks/            # React Query hooks
│       ├── contexts/         # Auth context
│       ├── services/         # API clients
│       └── styles/           # Design system + page CSS
├── exam-portal/              # Exam preview/taking portal (port 3002)
├── student-portal/           # Public examinee portal (port 3001)
├── cloudflare-worker/        # Edge guard for the AI grading route
├── agents/                   # Design/agent documentation
└── render.yaml               # Render Blueprint
```

### API routes

Everything is mounted under `/api` in `backend/src/server.ts`:

| Base path | Purpose |
| --- | --- |
| `/api/auth` | Registration, login, refresh, Google OAuth |
| `/api/exams` | Exam CRUD, attempts, submission, results, AI grading |
| `/api/students` | Student records and sections |
| `/api/notifications` | In-app notifications |
| `/api/activity` | Activity feed |
| `/api/files` | File upload/download |
| `/api/settings` | Per-user settings (model + key status) |

`GET /health` is used by Render's health check and the Cloudflare keep-alive cron.

## 🔧 Development Scripts

| Location | Command | Purpose |
| --- | --- | --- |
| `frontend/` | `npm run dev` | Vite dev server on port 3000 |
| `frontend/` | `npm run build` | Type check + production build to `dist/` |
| `frontend/` | `npm run preview` | Preview the production build |
| `frontend/` | `npm run lint` | ESLint |
| `backend/` | `npm run dev` | `tsx watch` hot reload |
| `backend/` | `npm run build` | Compile with `tsc` to `dist/` |
| `backend/` | `npm start` | Run `dist/server.js` |
| `backend/` | `npm run lint` | ESLint |

### Tests

There is no automated test suite in the repository yet — `npm test` is not implemented for the front end, and the backend's `jest` script has no specs to run. Verification is currently manual: follow [`BETA_TESTER_CHECKLIST.md`](./BETA_TESTER_CHECKLIST.md).

## 📖 Documentation

- [Frontend design guidelines](./agents/FRONTEND_DESIGN.md)
- [Implementation summary](./IMPLEMENTATION_SUMMARY.md)
- [Beta tester checklist](./BETA_TESTER_CHECKLIST.md)
- API reference (OpenAPI/Postman): not yet published

## 🔐 Security

- No API keys, JWT secrets, or service-account credentials are committed. Everything sensitive is read from environment variables at runtime.
- The DeepSeek key is intentionally stubbed by `backend/src/config/ai.ts` — see [AI grading configuration](#ai-grading-configuration).
- `.env`, `.env.local`, and `.env.production` are ignored by `backend/.gitignore` and `frontend/.gitignore`. Never commit a populated env file or a `firebase-adminsdk*.json` key.
- JWT access/refresh tokens are signed with `JWT_SECRET` and `JWT_REFRESH_SECRET`; rotate both if they are ever exposed.
- Found a vulnerability? Please use GitHub's private security advisory instead of opening a public issue.

## 🤝 Contributing

1. Branch off `main` (`git checkout -b feature/your-feature`).
2. Keep changes focused, and type check before pushing: `npx tsc --noEmit` in `backend/` and `frontend/`.
3. Open a pull request explaining the change and how you verified it.
4. Update [`BETA_TESTER_CHECKLIST.md`](./BETA_TESTER_CHECKLIST.md) when you touch user-facing flows.

## 📄 License

No license has been selected, so all rights are reserved by default. Add a `LICENSE` file before distributing the project or accepting outside contributions.

## 🛠️ Tech Stack

- **Front end**: React 18, TypeScript, Vite, React Query, Zustand, Framer Motion, React Router
- **Back end**: Node.js, Express, TypeScript, express-validator, JWT
- **Data**: Google Cloud Firestore
- **Auth**: Firebase Authentication, Supabase
- **Real-time**: SignalR
- **AI**: DeepSeek chat completions (essay grading, key-point verification, question generation)
- **Edge**: Cloudflare Workers + KV
- **Hosting**: Render.com

## 🚧 Status & Roadmap

Implemented: authentication (email/password + Google OAuth, refresh tokens), instructor dashboard, exam builder (access control, timing, proctoring, retakes, instructions), question bank, examinee registration, exam preview and delivery, auto-grading for objective question types, grading queue with AI-assisted essay grading and review, analytics, incident reports, notifications, and per-user settings.

Not yet in place: an automated test suite, published API documentation, and a chosen license.

## 📜 History

This project began as a progressive migration from a WPF desktop application; the web app is now the primary implementation.
