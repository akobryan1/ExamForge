# Copilot Instructions

## General Guidelines
- First general instruction
- Second general instruction
- Avoid using command prompts in troubleshooting or implementation guidance.

## Code Style
- Use specific formatting rules
- Follow naming conventions
- Apply the "Light Modern" design style consistently across UI components, which includes:
  - Main background color: #f3f5f7 (light gray)
  - Panels/cards color: #ffffff (white)
  - Primary accent color: #3aae9e (teal)
  - Primary hover color: #2e9c8d (darker teal)
  - Soft accent background: #e6f4f1 (light teal)
  - On hover, buttons and interactive elements should show a soft teal background (#e6f4f1) with a 3px left border in the primary teal color (#3aae9e) that animates in and out smoothly
  - Sidebar buttons use left border hover animation with background highlight
  - Text colors: primary #2e2f33 (dark), secondary #6b7280 (gray), muted #9aa3af (light gray)
  - Exam Forge branding text uses the primary teal color (#3aae9e)

## UserControl Layout Rules
- Apply the 'no margin' rule exclusively to UserControls; panels within MainWindow may retain margins as necessary. Ensure UserControls avoid internal margins while respecting the layout of the host panel.

## Resource Management
- Icons are stored under `Documents/ICONS`; adjust WPF image sources accordingly (pack URI or add to project).

## Firestore Configuration
- On user sign-up, Firestore should create per-user collections named `exam_sessions`, `published_exams`, `examinee_data`, `incident_reports`, and `session_events`. Adjust system paths accordingly, using `examforge_users` as the root user collection. Avoid using `question_bank`, `grading_queue`, or `integrity_incidents`.
- Store integrity incidents under the user-scoped subcollection named `incident_reports` (not top-level `integrity_incidents`), and ensure `session_events` are also user-scoped under `examforge_users/{userId}`. Use user-scoped Firestore paths; SessionHub should not read/write top-level collections.