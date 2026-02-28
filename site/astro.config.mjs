import { defineConfig } from 'astro/config';

// ── GitHub Pages URL configuration ──────────────────────────────────────────
//
// Project repo  →  https://<username>.github.io/<repo>/
//   site: 'https://<username>.github.io'
//   base: '/<repo>'                 ← set to your exact repo name
//
// User / org site  →  https://<username>.github.io/
//   site: 'https://<username>.github.io'
//   base: undefined                  ← remove the base line below
//
// Custom domain  →  https://verdant.example.com/
//   site: 'https://verdant.example.com'
//   base: undefined                  ← remove the base line below
//
// Update both values below before the first deployment, then commit.
// ────────────────────────────────────────────────────────────────────────────

export default defineConfig({
  site: 'https://username.github.io',   // ← replace with your GitHub username
  base: '/verdant',                     // ← replace with your repo name (or remove)
});
