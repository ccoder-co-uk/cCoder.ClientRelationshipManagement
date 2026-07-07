# Pipeline Stage Definitions

Date: 11 June 2026
Purpose: Provide controlled stage definitions for the sales CRM bolt-on.

## Stage List

| Stage ID | Stage Name | Description | Owner Focus |
|---|---|---|---|
| 00 | Target | Company has been identified but not yet researched. | Decide whether to research |
| 01 | Researched | Basic account research and fit hypothesis exist. | Score and prioritise |
| 02 | Contact Identified | A named contact or credible route exists. | Confirm route |
| 03 | Outreach Ready | Message, route, and material decision are ready. | Send first contact |
| 04 | Outreach Sent | First contact has been sent. | Wait / follow up |
| 05 | Engaged | Prospect replied, referred, or otherwise interacted. | Create next conversation |
| 06 | Discovery | Discovery conversation is in progress or complete. | Understand pain and fit |
| 07 | Qualified Opportunity | There is a plausible commercial opportunity. | Shape value and stakeholders |
| 08 | Proposal / Review | Diagnostic, review, or proposal is being shaped or has been sent. | Move to commercial decision |
| 09 | Commercial Negotiation | Scope, price, terms, or legal points are being discussed. | Get contract ready |
| 10 | Contract Sent | Contract has been issued for signature. | Close signature |
| 11 | Signed - Handoff Ready | Contract is signed and sales must prepare onboarding handoff. | Complete handoff pack |
| 12 | Handed To Onboarding | Existing onboarding process owns delivery. | Support delivery if needed |
| CL | Closed Lost | No active opportunity remains. | Record lost reason |
| NU | Nurture | Not active now but worth revisiting. | Schedule revisit |

## Stage Entry And Exit Criteria

### 00 Target

Entry:

- Company name identified.

Exit:

- Basic research completed and `Client.md` exists.

### 01 Researched

Entry:

- Account snapshot, links, and fit hypothesis recorded.

Exit:

- Contact or route identified, or account rejected / moved to nurture.

### 02 Contact Identified

Entry:

- Named contact, warm route, public department route, or credible contact path exists.

Exit:

- First message and opening angle drafted.

### 03 Outreach Ready

Entry:

- Outreach message, route, and material decision are prepared.

Exit:

- First contact sent and logged.

### 04 Outreach Sent

Entry:

- Initial email, call, LinkedIn message, or referral request sent.

Exit:

- Prospect replies, route fails, follow-up sent, or account moved to nurture.

### 05 Engaged

Entry:

- Prospect replies, refers, asks for information, accepts connection, or otherwise interacts.

Exit:

- Discovery call booked/completed, no-fit confirmed, or new stakeholder identified.

### 06 Discovery

Entry:

- Substantive conversation occurs or is scheduled.

Exit:

- Pain, fit, stakeholders, and next step are known.

### 07 Qualified Opportunity

Entry:

- There is a plausible need, owner, and next commercial action.

Exit:

- Proposal/review scope sent, or opportunity disqualified.

### 08 Proposal / Review

Entry:

- Diagnostic, review, proposal, or scoped next step is being prepared or has been sent.

Exit:

- Commercial negotiation begins, proposal rejected, or account moved to nurture.

### 09 Commercial Negotiation

Entry:

- Scope, price, terms, legal, or procurement process is active.

Exit:

- Contract sent, opportunity lost, or stalled.

### 10 Contract Sent

Entry:

- Contract issued for signature.

Exit:

- Contract signed, rejected, or stalled.

### 11 Signed - Handoff Ready

Entry:

- Signed contract received.

Exit:

- Handoff pack complete and onboarding process created.

### 12 Handed To Onboarding

Entry:

- Onboarding owner accepts the client.

Exit:

- Sales opportunity complete.

## Lost Reasons

Use consistent reasons:

- No response
- Not relevant
- Wrong timing
- No budget
- No pain identified
- Existing provider
- Not enough scale
- Could not reach decision-maker
- Competitor selected
- Internal project delayed
- Other

## Nurture Reasons

Use consistent reasons:

- Timing not right
- Waiting for procurement cycle
- Relationship-building only
- Need better route
- Need stronger proof
- Future system/process review
- Seasonal / operational timing
