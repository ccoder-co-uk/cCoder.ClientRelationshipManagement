# CRM Bolt-On Requirements

Date: 11 June 2026
Purpose: Define the lightweight CRM package needed to manage outreach through signed contract, then hand off into the existing client onboarding process.

## 1. Scope

This bolt-on covers the pre-contract commercial process:

1. Lead capture
2. Research and qualification
3. Outreach
4. Response handling
5. Discovery
6. Proposal / commercial review
7. Contracting
8. Signed-contract handoff to onboarding

Out of scope:

- Post-contract onboarding execution
- Client portal implementation
- Operational support workflows
- Supplier onboarding workflows after project launch

The handoff point is a signed client contract plus a complete onboarding handoff pack.

## 2. Core Objects

### Account

The company or organisation being pursued.

Examples:

- Stannah
- Vitacress
- Aster Group
- Carnival UK

### Contact

A person linked to an account.

Examples:

- Purchasing Manager
- Group Finance Director
- General Manager
- Procurement Director

### Opportunity

A specific commercial opportunity with an account.

Examples:

- Supplier payment review
- Early discounting feasibility
- Portal / SCF platform discussion
- Financial review / third-party audit

### Activity

A dated interaction or action.

Examples:

- Email sent
- Follow-up due
- Call completed
- Document sent
- Referral received

### Material

A document or resource sent to, received from, or prepared for a client.

Examples:

- First outreach email
- One-page overview
- Proposal
- NDA
- Contract

### Handoff Pack

The structured data and documents needed to move a signed client into onboarding.

## 3. Pipeline Stages

| Stage | Meaning | Exit Criteria |
|---|---|---|
| 00 Target | Company identified but not yet researched | Basic account record created |
| 01 Researched | Account has enough context to decide whether to pursue | Fit score and opening angle recorded |
| 02 Contact Identified | Named person or route identified | Contact record exists |
| 03 Outreach Ready | Message and route prepared | Outreach approved / ready to send |
| 04 Outreach Sent | First contact sent | Follow-up date set |
| 05 Engaged | Prospect has replied or been referred | Next conversation or route agreed |
| 06 Discovery | First substantive conversation in progress or complete | Pain, fit, owner, and next step known |
| 07 Qualified Opportunity | There is a plausible commercial opportunity | Opportunity type, stakeholders, and value hypothesis recorded |
| 08 Proposal / Review | Proposal, diagnostic, or review scope being shaped | Commercial next step sent or agreed |
| 09 Commercial Negotiation | Pricing, scope, legal, or terms under discussion | Contract pack ready or abandoned |
| 10 Contract Sent | Contract issued for signature | Signed, rejected, or stalled |
| 11 Signed - Handoff Ready | Contract signed and onboarding handoff pack complete | Onboarding process created |
| 12 Handed To Onboarding | Existing onboarding process owns delivery | CRM opportunity closed-won / handed off |
| Closed Lost | Opportunity ended | Lost reason recorded |
| Nurture | Not active now, but worth revisiting | Revisit date and reason recorded |

## 4. Required Account Fields

These should map directly to `Client.md` front matter initially.

| Field | Required | Notes |
|---|---|---|
| client_name | Yes | Display name |
| folder_name | Yes | File/folder-safe account name |
| created_date | Yes | ISO date |
| last_updated | Yes | ISO date |
| relationship_status | Yes | Human-readable status |
| pipeline_stage | Yes | Use controlled stage values |
| lead_source | Yes | Where the lead came from |
| initial_route | Yes | Cold, warm, referral, LinkedIn, website, etc. |
| next_action | Yes | One clear next action |
| next_action_date | Yes | ISO date |
| account_owner | Yes | Internal owner |
| priority | Yes | A / B / C or descriptive value |
| legal_entity | If known | Legal contracting entity |
| trading_name | If known | Public-facing name |
| company_number | If known | Useful for contracting and validation |
| vat_number | If known | Useful later in onboarding |
| registered_office | If known | Useful later in onboarding |
| website | Yes | Primary public website |
| fit_score | Recommended | 0-18 score from qualification model |
| opportunity_type | Recommended | Review, audit, early discounting, portal, unknown |

## 5. Required Contact Fields

| Field | Required | Notes |
|---|---|---|
| account_name | Yes | Parent account |
| contact_name | Yes | Person name |
| role | If known | Job title |
| email | If known | Direct email |
| phone | If known | Direct phone |
| linkedin | Optional | Useful for relationship building |
| source | Yes | Where contact data came from |
| status | Yes | Not contacted, contacted, replied, referred, invalid |
| relationship_route | Recommended | Cold, referred by, warm intro, public contact |
| consent_notes | Optional | Useful for warm intro permissions |

## 6. Required Activity Fields

| Field | Required | Notes |
|---|---|---|
| activity_date | Yes | ISO date |
| account_name | Yes | Parent account |
| contact_name | If applicable | Person involved |
| activity_type | Yes | Email, call, LinkedIn, meeting, document sent, note |
| direction | Yes | Outbound, inbound, internal |
| summary | Yes | Short plain-English summary |
| next_action | If applicable | Follow-up action |
| next_action_date | If applicable | ISO date |
| material_reference | If applicable | File path or document ID |

## 7. Automation Rules

### Lead Creation

When a new client folder is created:

- Create `Client.md` from the template.
- Set `pipeline_stage` to `00 Target` or `01 Researched`.
- Set `next_action` and `next_action_date`.
- Add the account to the tracker or CRM index.

### Outreach Sent

When first outreach is logged:

- Set stage to `04 Outreach Sent`.
- Record contact, message type, date, and material sent.
- Create follow-up activity for 3 to 5 business days later.

### No Reply Follow-Up

When follow-up date arrives and there is no reply:

- Surface account in daily/weekly action list.
- Use the relevant follow-up template.
- After follow-up, either schedule final check, move to nurture, or leave active if there is a reason.

### Reply Received

When a prospect replies:

- Set stage to `05 Engaged`.
- Capture reply summary.
- Classify response: interested, send info, referral, not relevant, not now, unsubscribe / do not contact.
- Create next action immediately.

### Discovery Completed

When a discovery call is completed:

- Set stage to `06 Discovery` or `07 Qualified Opportunity`.
- Record pain, stakeholders, fit, next step, and objections.
- Create opportunity record if fit is plausible.

### Contract Signed

When contract is signed:

- Set stage to `11 Signed - Handoff Ready`.
- Generate onboarding handoff pack.
- Validate mandatory handoff fields.
- Create or trigger onboarding process.

### Onboarding Accepted

When onboarding owns the client:

- Set stage to `12 Handed To Onboarding`.
- Mark opportunity closed-won.
- Keep sales relationship history available to delivery team.

## 8. Onboarding Handoff Pack

The handoff pack should include:

- Account legal entity
- Trading name
- Registered office
- Company number
- VAT number
- Primary commercial contact
- Primary operational contact
- Primary technical contact
- Contract summary
- Agreed service / product scope
- Commercial terms summary
- Key pain points and promised outcomes
- Known stakeholders
- Important relationship notes
- Documents sent / received
- Proposal and signed contract
- Delivery risks or sensitivities
- Target launch / review dates

## 9. First Implementation Shape

Phase 1 can stay file-based:

- Continue using `Clients/<Client Name>/Client.md`.
- Use YAML front matter as the system-readable source.
- Use Markdown sections as human-readable notes.
- Use `Prospect Tracker - Initial.csv` as a simple index until replaced.

Phase 2 can add a simple internal UI:

- Account list
- Next-action dashboard
- Stage board
- Client detail page
- Activity log
- Material register
- Handoff pack generator

Phase 3 can integrate with onboarding:

- Convert signed opportunity into onboarding record.
- Push company/contact/legal data into onboarding system.
- Attach proposal/contract/materials.
- Create delivery checklist from agreed opportunity type.

## 10. Design Principles

- Every active account must have one next action.
- Every next action must have a date.
- Do not separate sales notes from sent/received materials.
- Keep reusable templates separate from client-specific records.
- Prefer structured fields where the system may need to filter, sort, or trigger automation.
- Preserve human-readable notes because relationship context matters.
