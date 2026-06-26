> **MAINTAINER STATUS (2026-06-22) — annotations for the next agent; skip the ✅ items.**
>
> - **#1 transport-neutral test composition — ✅ DONE & merged to `main`.** `Vostra.Result.Testing` is now
>   Core-only (chain + assert over any `Task<Result<T>>`); the HTTP verbs split into a new
>   `Vostra.Result.AspNetCore.Testing` package. Diagnostics extension point: `error.WithRequestContext(...)`.
>   Spec: `docs/superpowers/specs/2026-06-22-transport-neutral-testing-design.md`.
> - **#2 keep-going through every outcome — ✅ DONE & merged.** `SelectResultsAsync` — a non-collapsing
>   batch traverse preserving every per-item `Result<T>` (successes *and* failures), throttled, in order.
>   Spec: `docs/superpowers/specs/2026-06-22-batch-select-results-design.md`.
> - **#3 first-class distinct failure kinds (typed failure unions) — ⬜ OPEN.** Largest change; tensions with
>   OD-2 (single-error) + the terminal-union design. Needs its own brainstorm.
> - **#4 exact human message as load-bearing as the code — ⬜ OPEN.** `Message` exists; needs a
>   verbatim-preservation guarantee + tests (see hardening plan §5.1 round-trip fidelity).
> - **#5 exception-free early-exit ergonomics — ⬜ OPEN.** Still deferred (OD-3 / FR-8).

To the author of Vostra.Result,

  We're building a .NET service that sits between IBM Maximo and external contractors — it isn't a web API. Work arrives as messages on a queue (Azure Service Bus) and is processed by long-running per-work-order handlers, and our replies
  go back out as ACK/NACK messages (an "accept" or "reject, here's why" on the wire), not HTTP responses. We spent a good while weighing your library against this codebase, and we came away genuinely liking the core idea. A few things
  we'd wish for, roughly in the order they'd help us:

  1. ✅ **[ADDRESSED — see status note above]** A transport-neutral version of the test-composition style. The single most attractive thing you've built, for us, is the chained-and-assert test shape — run a step, .Assert() it, BindAsync into the next step only if the previous one
  succeeded, short-circuit on the first failure, assert inline. That style is perfect for our tests, which walk one work order through a sequence of steps. But it currently lives in the Testing package wired to HTTP verbs (Get/Post) and
  depends on the web package. We'd love that chaining-and-assertion layer factored out so it works over any operation that returns a Task<Result<T>> — so we could point it at "send a message and wait for the work to settle, then read the
  result" instead of at an HTTP call. The verbs are the replaceable part; the composition is the treasure. Don't bury the treasure inside the HTTP package.

  2. ✅ **[ADDRESSED — see status note above]** A way to keep going through several outcomes, not just stop at the first error. Our core loop processes a batch of actions and has to record every outcome — some are legitimate no-ops we skipped, some applied and advanced state, some
  were rejected by our rules, some refused by Maximo, some failed transiently — and only then decide one accept-or-reject for the whole batch. Combine accumulates errors, but it throws away the distinctions among the successes and the
  per-item detail we need to keep. A combinator that runs each item, preserves the full per-item outcome (success flavour included), and then lets us summarize, would turn a hand-written loop into something composable.

  3. ⬜ **[OPEN]** Make several distinct failure kinds as first-class as several distinct success kinds. Your multi-success union (Result<T1,T2,T3>) is lovely for "one of several good shapes," but on the failure side everything collapses into one error
  channel that we then have to pick apart by code. In our world the kind of "no" matters as much as the kind of "yes" — a business rejection, an external refusal, and a transient transport failure each lead to different behaviour.
  First-class support for "this success, that success, or one of these named failures" — and ideally being able to chain through it rather than only matching out at the end — would fit domains like ours much better.

  4. ⬜ **[OPEN]** Treat the exact human message as a first-class, guaranteed thing — not only the code. Your design leans on a stable error code surviving to the wire and into test assertions, which is great for HTTP APIs. But our contractors
  distinguish failures by the exact wording of the reason text, not by a code — the text is the contract. We'd wish for the human message to be as load-bearing and as easy to preserve verbatim as the code is, so a library built around
  code-identity doesn't quietly push us toward losing the wording.

  5. ⬜ **[OPEN]** The "early-exit" ergonomics, finished and exception-free if possible. Your own requirements floated a scoped early-return so imperative code can bail on a failure with a question-mark feel. For people coming from nested if/else, that's the bridge that makes the straight-line style click. If it can be done without a hidden control-flow exception, even better.

  And a thank-you worth saying plainly: the opinion baked into this — the happy path is implicit, failure is a typed value, and there's no property that hands you garbage when it failed — is exactly the discipline that stops code (whether
  written by a person or by an assistant) from drifting back into a pile of nested checks. Even where your packages don't fit our shape, that opinion travels. Make the good parts usable off the HTTP rails, and a lot more services like
  ours could adopt it.

  — The Maximo adapter team

 