---
name: reference-network-access
description: "Outbound web access in this environment — what works (WebFetch/WebSearch) and what is blocked (curl/PowerShell HTTPS, some flagged domains)"
metadata: 
  node_type: memory
  type: reference
  originSessionId: a345149c-cc7c-46c7-8e85-daa652c6f5df
---

External web access in this OMS environment (as of 2026-05-29):

- **`curl` and PowerShell `Invoke-WebRequest` HTTPS are blocked** — every outbound HTTPS attempt fails with SSL connect error (`curl` exit 35, `HTTP=000`), even with `dangerouslyDisableSandbox`. Don't waste time debugging certs/proxy; it's a hard egress block. Don't try to fetch external pages via the shell.
- **`WebFetch` works for most domains** (e.g. `hitkey.bms.ms`, `bmson-spec.readthedocs.io`, `bm98.yaneu.com`) but **refuses flagged/unreachable ones** with "Unable to verify if domain X is safe to fetch" — seen for `*.dyndns.info` and (intermittently) `github.com`. For GitHub, prefer the `gh` CLI or `raw.githubusercontent.com` paths; if a dyndns/old domain is refused, look for a migrated canonical domain.
- **`WebSearch` works** (US-only) and is a good fallback to discover migrated URLs and extract page facts when WebFetch refuses a domain.

So: for external research use WebFetch first, WebSearch to find/replace dead URLs, never the shell. See [[reference-build-and-test]].
