# Copilot Instructions

## General Guidelines
- First general instruction
- Second general instruction

## Code Style
- Use specific formatting rules
- Follow naming conventions
- Apply the "Premium Gold" design style consistently across UI components, which includes:
  - Background color: #1c192a (deep purple/navy)
  - Surface/cards color: #282538 (lighter purple)
  - Gold gradient accents: #d4b896 to #c09860, fading from top to bottom
  - On hover, cards and interactive containers should show a gold-beige gradient border (from #d4b896 to #c09860) that animates in (fade/width) and then fades out on mouse leave. The left border is not permanent and should only appear during hover. The hover overlay must render on top of cards (Panel.ZIndex higher) and animate uniform border thickness on all sides so the top border matches the bottom. Use BorderThickness animation to a uniform value (e.g., "2,2,2,2").
  - 3px left borders with gold gradients fading to transparent

## UserControl Layout Rules
- Apply the 'no margin' rule exclusively to UserControls; panels within MainWindow may retain margins as necessary. Ensure UserControls avoid internal margins while respecting the layout of the host panel.

## Resource Management
- Icons are stored under `Documents/ICONS`; adjust WPF image sources accordingly (pack URI or add to project).