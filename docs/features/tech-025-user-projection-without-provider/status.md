# tech-025-user-projection-without-provider

- type: tech
- size: M
- branch: tech/025-user-projection-without-provider
- stage: 04-backend
- epic: core/org-access
- deps: mod-016, tech-024
- gates: gate-3

## Gates

gate-3: pending

## Plan

<!-- filled by the dev stage -->

## Log

- 2026-11-02 [orchestrator] task started; queue line taken by PR #46 (sync/queue-2026-11-02-take-tech-025). The branch is stacked on tech/024-shared-realm-client-roles (tech-024 is in review and not merged: the same session works both; the PR of this task carries tech-024's commits until that one merges)
- 2026-11-02 [orchestrator] size divergence: the queue line says size:L, the human confirmed M (chat 2026-10-20, relayed by the launching session, and again by the form answer «M, как в чате» on 2026-11-02). Route: Dev -> QA -> Review -> gate-3 -> PR, no spec, no design; one Opus review covers tech-024 and tech-025 together. routing.md would make it L (an ADR amending ADR-0025 and a change of the authentication rules); the human chose M knowingly. Economy mode: one agent per stage
- 2026-11-02 [orchestrator] session binding: this session cannot relocate; it switched the pool-made checkout onto the task branch (as for tech-024)

## Decisions

## Notes

Summary: Users reach MUSTASK without our own Keycloak provider (the company realm takes no JAR): upsert the user on first sign-in from token claims instead of the ADR-0025 event-listener; member search reads that projection, so only users who signed in once can be added to an organization. Amends ADR-0025.
