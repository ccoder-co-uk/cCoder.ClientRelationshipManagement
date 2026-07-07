# CRM Data Model - Draft

Date: 11 June 2026
Purpose: Define the first structured data model for a Corporate LinX sales pipeline bolt-on.

## Implementation Note

The original CRM language used `Account` for the root record. For the cCoder domain package, the root entity is `Client`.

`Client` represents Corporate LinX's relationship with a company inside a tenant. `Company` and `Address` remain supporting masterdata-style entities based on the CLX B2B model.

## 1. Client

| Field | Type | Required | Example |
|---|---|---|---|
| client_id | string | Yes | cli_stannah |
| tenant_id | string | Yes | corporate_linx |
| client_name | string | Yes | Stannah |
| folder_name | string | Yes | Stannah |
| legal_entity | string | No | Carnival UK Ltd |
| trading_name | string | No | Carnival UK |
| company_number | string | No | 04039524 |
| vat_number | string | No | GB761 4300 58 |
| registered_office | string | No | Carnival House, Southampton |
| website | string | Yes | https://example.com |
| public_phone | string | No | 0333 400 8222 |
| sector | string | No | Manufacturing |
| location_context | string | No | Andover, Hampshire |
| relationship_status | string | Yes | Outreach sent |
| pipeline_stage | enum | Yes | 04 Outreach Sent |
| priority | enum/string | Yes | A |
| fit_score | integer | No | 14 |
| lead_source | string | Yes | Ashley referral |
| initial_route | string | Yes | Warm intro via Liam |
| account_owner | string | Yes | Paul Ward |
| next_action | string | Yes | Follow up |
| next_action_date | date | Yes | 2026-06-15 |
| created_date | date | Yes | 2026-06-10 |
| last_updated | date | Yes | 2026-06-11 |

## 2. Contact

Represents a person associated with a client.

| Field | Type | Required | Example |
|---|---|---|---|
| contact_id | string | Yes | con_tiffany_howell |
| client_id | string | Yes | cli_stannah |
| name | string | Yes | Tiffany Howell |
| role | string | No | Purchasing Manager |
| email | string | No | tiffany.howell@stannah.co.uk |
| phone | string | No | 01264 386750 |
| linkedin | string | No |  |
| source | string | Yes | Public purchasing page |
| status | enum | Yes | Contacted |
| relationship_route | string | No | Public contact |
| is_primary | boolean | Yes | true |
| notes | text | No | Likely purchasing route |

## 3. Opportunity

Represents a possible commercial sale or review.

| Field | Type | Required | Example |
|---|---|---|---|
| opportunity_id | string | Yes | opp_stannah_supplier_review |
| client_id | string | Yes | cli_stannah |
| opportunity_type | enum | Yes | Supplier Payment Review |
| stage | enum | Yes | 04 Outreach Sent |
| value_estimate | decimal | No |  |
| probability | integer | No | 10 |
| pain_summary | text | No | Unknown until discovery |
| value_hypothesis | text | Yes | Supplier payment visibility may matter |
| primary_contact_id | string | No | con_tiffany_howell |
| next_action | string | Yes | Follow up |
| next_action_date | date | Yes | 2026-06-15 |
| created_date | date | Yes | 2026-06-10 |
| last_updated | date | Yes | 2026-06-11 |

## 4. Activity

Represents a dated interaction or task.

| Field | Type | Required | Example |
|---|---|---|---|
| activity_id | string | Yes | act_20260610_stannah_email |
| client_id | string | Yes | cli_stannah |
| contact_id | string | No | con_tiffany_howell |
| opportunity_id | string | No | opp_stannah_supplier_review |
| activity_date | date | Yes | 2026-06-10 |
| activity_type | enum | Yes | Email |
| direction | enum | Yes | Outbound |
| summary | text | Yes | Initial local intro email sent |
| outcome | string | No | Awaiting reply |
| next_action | string | No | Follow up |
| next_action_date | date | No | 2026-06-15 |
| material_id | string | No | mat_intro_email |

## 5. Material

Represents a document or email asset.

| Field | Type | Required | Example |
|---|---|---|---|
| material_id | string | Yes | mat_supplier_review_overview |
| client_id | string | No | cli_stannah |
| name | string | Yes | Supplier Payment Review - One Page Overview.pdf |
| file_path | string | Yes | E:/Documentation/... |
| material_type | enum | Yes | PDF |
| status | enum | Yes | Reusable / Sent / Received / Draft |
| sent_date | date | No |  |
| sent_to_contact_id | string | No |  |
| notes | text | No | Send only if requested |

## 6. Handoff Pack

Represents the signed-contract handoff into onboarding.

| Field | Type | Required | Example |
|---|---|---|---|
| handoff_id | string | Yes | handoff_stannah_001 |
| client_id | string | Yes | cli_stannah |
| opportunity_id | string | Yes | opp_stannah_supplier_review |
| signed_contract_path | string | Yes |  |
| legal_entity | string | Yes |  |
| primary_commercial_contact | string | Yes |  |
| primary_operational_contact | string | Yes |  |
| primary_technical_contact | string | If applicable |  |
| agreed_scope | text | Yes |  |
| commercial_terms_summary | text | Yes |  |
| promised_outcomes | text | Yes |  |
| known_risks | text | No |  |
| onboarding_owner | string | Yes |  |
| handoff_status | enum | Yes | Draft / Ready / Accepted |
| handoff_date | date | No |  |

## 7. Controlled Values

### Contact Status

- Not contacted
- Contacted
- Replied
- Referred
- Invalid
- Do not contact

### Activity Type

- Email
- Call
- Meeting
- LinkedIn
- Document sent
- Document received
- Internal note
- Follow-up task

### Direction

- Outbound
- Inbound
- Internal

### Opportunity Type

- Supplier Payment Review
- Financial Review / Third-Party Audit
- Early Discounting Feasibility
- Supplier Portal / SCF Platform
- Unknown

## 8. File-Based MVP Mapping

For the current file-based system:

- `Client.md` front matter maps to Client.
- `Client.md` Contacts section maps to Contact.
- `Client.md` Engagement History maps to Activity.
- `Client.md` Materials section maps to Material.
- A future `Handoff.md` inside each client folder can map to Handoff Pack.
