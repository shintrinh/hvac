# HVAC Conversation Engine – Gap Analysis and Proposed Solution

## Requested flow
1. Issue
2. Address
3. Urgency
4. Appointment
5. Confirm

## Current implementation status

### 1) State machine
**Status: Not implemented.**

Inbound processing currently appends customer message, calls AI once, and sends the response. There is no explicit conversation stage persisted (Issue/Address/Urgency/Appointment/Confirm), no deterministic transition rules, and no guardrails to force next-step questioning in order.

### 2) Prompt orchestration
**Status: Partially implemented.**

A generic `SystemPrompt` exists and can be configured. However:
- The prompt is not stage-aware.
- There is no prompt template per state.
- The model is not constrained to ask only one missing field per turn.

### 3) Lead extraction
**Status: Heuristic only.**

Issue/address/time are inferred with simple keyword checks from customer text. There is no urgency field and no structured slot lifecycle.

### 4) Confirmation logic
**Status: Missing.**

There is no explicit confirmation step summarizing collected data and asking final yes/no confirmation.

---

## Proposed solution (before coding)

### A. Add explicit state + slots to `Conversation`
Introduce:
- `ConversationStage` enum: `Issue`, `Address`, `Urgency`, `Appointment`, `Confirm`, `Completed`
- Structured slots:
  - `Issue`
  - `Address`
  - `Urgency` (e.g., Emergency / Same-day / Flexible)
  - `Appointment` (preferred date/time window)
  - `IsConfirmed` (nullable bool)

Persist these fields with conversation so each inbound message continues from the correct stage.

### B. Deterministic transition engine
Add a small application service (e.g., `ConversationFlowEngine`) that:
1. Parses latest customer message into candidate slot values.
2. Fills missing slot(s) for current stage.
3. Advances stage only when current stage is complete.
4. Generates response instructions for AI limited to the next expected question.

Transition policy:
- If `Issue` empty => ask Issue.
- Else if `Address` empty => ask Address.
- Else if `Urgency` empty => ask Urgency.
- Else if `Appointment` empty => ask Appointment window.
- Else => enter `Confirm` and summarize.
- On confirmation “yes” => `Completed` and close with booking handoff.
- On “no/change” => reopen required field and loop.

### C. Stage-aware prompt design
Keep existing LLM but use strict prompt contract:
- System prompt defines receptionist role + policy.
- Developer/instruction message includes:
  - Current stage
  - Known slot values
  - Exactly one target field to collect
  - Style constraints (short SMS, one question max, bilingual option if needed)

This keeps conversational quality while enforcing flow.

### D. Structured extraction strategy
Preferred options:
1. **Function/tool calling JSON schema** (best): model returns structured fields + confidence.
2. Fallback regex/rules for obvious patterns.

Always validate before persisting (e.g., address-like pattern, appointment date/time parse).

### E. Domain + event updates
- Extend `Lead` with `Urgency` and `ConfirmedAt`.
- Emit events:
  - `stage_changed`
  - `slot_captured` (issue/address/urgency/appointment)
  - `booking_confirmed`

### F. Test plan
Add tests for:
1. Happy path through all 5 steps.
2. User provides multiple fields in one message.
3. User refuses/changes info at confirm.
4. Ambiguous answer triggers clarification but no stage skip.
5. Re-entry from existing conversation resumes correct stage.

---

## Recommended implementation order
1. Add stage + slot fields to domain/infrastructure models.
2. Build `ConversationFlowEngine` with deterministic transitions.
3. Update use case to call flow engine before AI generation.
4. Implement stage-aware prompts and structured extraction.
5. Expand tests for flow and edge cases.
