# Client Folder Standard

Date: 11 June 2026
Purpose: Keep every client folder consistent enough for human use now and CRM automation later.

## Folder Structure

Each client folder should use:

```text
Clients/
  Client Name/
    Client.md
    Handoff.md
    Sent/
    Received/
    Drafts/
    Notes/
```

For now, only `Client.md` is required.

Create the other folders when needed:

- `Sent`: material actually sent to the client
- `Received`: material received from the client
- `Drafts`: working material not yet sent
- `Notes`: call notes, meeting notes, internal research
- `Handoff.md`: created when a signed client is ready to move into onboarding

## Client.md Rule

Every client folder must include a `Client.md` file.

`Client.md` must include:

- YAML front matter
- The standard 13 numbered sections
- One clear next action
- One next action date

## Standard Sections

Use these headings exactly:

1. Account Snapshot
2. Company Details
3. Public Links
4. Products And Services
5. Relationship Summary
6. Contacts
7. Fit Hypothesis
8. Preferred Opening Angle
9. Engagement History
10. Materials
11. Next Step Plan
12. Open Questions
13. Source Notes

## Material Handling

Reusable material stays in the main `Templates` folder or root marketing folder.

Client-specific material belongs in the client folder.

When a reusable material is actually sent to a client:

1. Copy it into `Clients/<Client Name>/Sent`.
2. Rename it with the sent date if useful.
3. Log it in `Client.md` under Engagement History and Materials.

Suggested sent filename pattern:

```text
YYYY-MM-DD - Material Name.ext
```

Example:

```text
2026-06-10 - Introduction Email - Personal Local First Contact.docx
```

## Stage Updates

When a meaningful event happens, update:

- YAML front matter
- Relationship Summary
- Engagement History
- Materials, if a file was sent or received
- Next Step Plan

Meaningful events include:

- Outreach sent
- Reply received
- Referral received
- Call booked
- Call completed
- Document sent
- Proposal sent
- Contract sent
- Contract signed
- Handoff completed

## Automation Notes

The YAML front matter should be treated as the structured source for dashboards and automation.

The Markdown body should be treated as the human-readable account briefing.
