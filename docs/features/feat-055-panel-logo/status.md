# feat-055-panel-logo

- type: feature
- size: S
- branch: feat/055-panel-logo
- stage: 06-frontend
- epic: platform/ui
- deps: -
- gates: gate-3

## Gates

gate-3: pending

## Plan

1. `app/shell/ProductMark.tsx`: the mark (ring, disc with a check, amber dot) and the wordmark «mus[t]ask», both decorative.
2. `TopBar.tsx`: the home link shows `ProductMark`, its accessible name stays `PRODUCT_NAME`.
3. Logo colours in `theme/tokens.ts` `shellPalette` (logoRing, logoMust, logoAccent); the disc and check reuse primary.
4. `public/favicon.svg` + the icon link in `index.html`.

## Log

- 2026-11-10 [new-task] task started, worktree C:/Users/khamb/Workspace/devex/internal_products/mustask/.claude/worktrees/feat-055-panel-logo
- 2026-11-10 [orchestrator] route confirmed by the human in chat: feature, S, Dev → gate-3 → PR, panel only; the Keycloak login logo split into feat-056. The human asked for a fast cycle ("5 minutes"): no separate QA/review sessions were run, the review is the human at gate-3.

## Notes

Summary (from the queue line or an argument): Product logo in the panel: the T2 mark (light-blue ring, blue disc with a white check, amber dot on the ring at 45 degrees) and the wordmark mus[t]ask (mus #3A96DD, shared t #F2A93B, ask in the text colour) as SVG assets, the favicon, and the mark plus wordmark in TopBar; light and dark themes
