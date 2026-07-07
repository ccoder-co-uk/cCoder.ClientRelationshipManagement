import fsSync from 'node:fs';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const activityTypes = {
  Note: 0,
  Research: 10,
  Email: 20,
  PhoneCall: 30,
  Meeting: 40,
  Demo: 50,
  Proposal: 60,
  Contract: 70,
  Handoff: 80,
  FollowUp: 90,
};

const activityDirections = {
  Internal: 0,
  Outbound: 10,
  Inbound: 20,
};

const contactStatuses = {
  Unknown: 0,
  Suggested: 10,
  Verified: 20,
  Contacted: 30,
  Engaged: 40,
  DoNotContact: 50,
  LeftCompany: 60,
};

const opportunityTypes = {
  Unknown: 0,
  PortalServices: 10,
  SupplierPaymentReview: 20,
  ThirdPartyAudit: 30,
  ProcessImprovement: 40,
  EarlyPaymentDiscounting: 50,
  Blended: 60,
};

const relationshipStatuses = {
  Prospect: 0,
  ActiveOpportunity: 10,
  Contracted: 20,
  Onboarding: 30,
  Client: 40,
  Dormant: 50,
  Disqualified: 60,
};

const pipelineStages = {
  Unqualified: 0,
  Researched: 10,
  ContactIdentified: 20,
  OutreachReady: 30,
  OutreachSent: 40,
  Responded: 50,
  DiscoveryBooked: 60,
  DiscoveryCompleted: 70,
  ProposalSent: 80,
  Negotiation: 90,
  ContractSent: 100,
  Won: 110,
  Lost: 120,
  Nurture: 130,
};

const priorities = {
  Unknown: 0,
  Low: 10,
  Medium: 20,
  High: 30,
  Strategic: 40,
};

const materialTypes = {
  Unknown: 0,
  Email: 10,
  Overview: 20,
  Proposal: 30,
  DiscoveryNotes: 40,
  Contract: 50,
  SupportingEvidence: 60,
  HandoffPack: 70,
};

const materialStatuses = {
  Draft: 0,
  Ready: 10,
  Sent: 20,
  Received: 30,
  Superseded: 40,
  Archived: 50,
};

const args = parseArgs(process.argv.slice(2));
const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const marketingRoot = path.resolve(repoRoot, '..');
const clientsRoot = path.resolve(args['clients-root'] ?? path.join(marketingRoot, 'Clients'));
const apiBase = (args['api-base'] ?? process.env.CRM_API_BASE ?? 'http://localhost:5294').replace(/\/$/, '');
const bearerToken = args.token ?? process.env.CRM_IMPORT_TOKEN ?? 'crm-import-paul-ward';
const tenantId = args['tenant-id'] ?? process.env.CRM_TENANT_ID ?? 'default';
const onlyFolder = args.only ?? process.env.CRM_IMPORT_ONLY ?? null;
const dryRun = hasFlag(args, 'dry-run');
const allowNonEmpty = hasFlag(args, 'allow-nonempty');

if (typeof fetch !== 'function')
  throw new Error('This script requires a Node.js runtime with global fetch support.');

const clientDirectories = await fs.readdir(clientsRoot, { withFileTypes: true });
const folders = clientDirectories
  .filter(entry => entry.isDirectory())
  .map(entry => entry.name)
  .filter(name => !onlyFolder || name.localeCompare(onlyFolder, undefined, { sensitivity: 'accent' }) === 0)
  .sort((a, b) => a.localeCompare(b));

const existingClients = await apiGet('/Api/Client');

if (!allowNonEmpty && Array.isArray(existingClients) && existingClients.length > 0)
{
  throw new Error(
    `CRM already contains ${existingClients.length} client record(s). ` +
    'Pass --allow-nonempty if you want to import into a non-empty database.');
}

const dataset = [];

for (const folder of folders)
{
  const clientFile = path.join(clientsRoot, folder, 'Client.md');
  const markdown = await fs.readFile(clientFile, 'utf8');
  const parsed = parseClientMarkdown(markdown);
  dataset.push(buildImportModel({
    folder,
    clientFile,
    marketingRoot,
    clientsRoot,
    tenantId,
    parsed
  }));
}

const summary = dataset.map(item => ({
  name: item.companyPayload.Name,
  contacts: item.contactPayloads.length,
  activities: item.activityPayloads.length,
  materials: item.materialPayloads.length,
  opportunityType: item.opportunityPayload.Type,
  stage: item.clientPayload.CurrentStage
}));

if (dryRun)
{
  console.log(JSON.stringify({
    apiBase,
    clientsRoot,
    tenantId,
    records: summary
  }, null, 2));
  process.exit(0);
}

for (const item of dataset)
{
  const createdClient = await apiPost('/Api/Client', item.clientPayload);
  const clientId = createdClient.id ?? createdClient.Id;

  const createdCompany = await apiPost('/Api/Company', {
    ...item.companyPayload,
    ClientId: clientId
  });

  const contactIds = new Map();

  for (const payload of item.contactPayloads)
  {
    const createdContact = await apiPost('/Api/ClientContact', {
      ...payload,
      ClientId: clientId
    });

    const title = payload.__title;
    const contactId = createdContact.id ?? createdContact.Id;
    if (title)
      contactIds.set(title, contactId);
    if (payload.IsPrimary)
      contactIds.set('__primary__', contactId);
  }

  const createdOpportunity = await apiPost('/Api/ClientOpportunity', {
    ...item.opportunityPayload,
    ClientId: clientId,
    PrimaryContactId: contactIds.get('__primary__') ?? null
  });

  const opportunityId = createdOpportunity.id ?? createdOpportunity.Id;
  const materialIds = new Map();

  for (const payload of item.materialPayloads)
  {
    const createdMaterial = await apiPost('/Api/ClientMaterial', {
      ...payload,
      ClientId: clientId,
      SentToContactId: payload.SentToContactId === '__primary__'
        ? (contactIds.get('__primary__') ?? null)
        : payload.SentToContactId
    });

    materialIds.set(payload.Name, createdMaterial.id ?? createdMaterial.Id);
  }

  for (const payload of item.activityPayloads)
  {
    await apiPost('/Api/ClientActivity', {
      ...payload,
      ClientId: clientId,
      ClientOpportunityId: payload.ClientOpportunityId === '__default__'
        ? opportunityId
        : payload.ClientOpportunityId,
      ClientContactId: payload.ClientContactId === '__primary__'
        ? (contactIds.get('__primary__') ?? null)
        : payload.ClientContactId,
      ClientMaterialId: payload.__materialName
        ? (materialIds.get(payload.__materialName) ?? null)
        : payload.ClientMaterialId
    });
  }

  console.log(`Imported ${item.companyPayload.Name}`);
}

async function apiGet(route)
{
  const response = await fetch(`${apiBase}${route}`, {
    headers: {
      Authorization: `Bearer ${bearerToken}`
    }
  });

  return handleJsonResponse(response, 'GET', route);
}

async function apiPost(route, body)
{
  const cleanBody = JSON.parse(JSON.stringify(body, (_, value) =>
    value === undefined ? null : value));

  delete cleanBody.__title;
  delete cleanBody.__materialName;

  const response = await fetch(`${apiBase}${route}`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${bearerToken}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(cleanBody)
  });

  return handleJsonResponse(response, 'POST', route);
}

async function handleJsonResponse(response, method, route)
{
  const text = await response.text();
  const data = text ? tryParseJson(text) : null;

  if (!response.ok)
  {
    throw new Error(
      `${method} ${route} failed with ${response.status}: ${text || response.statusText}`);
  }

  return data;
}

function tryParseJson(text)
{
  try
  {
    return JSON.parse(text);
  }
  catch
  {
    return text;
  }
}

function buildImportModel({ folder, clientFile, marketingRoot, clientsRoot, tenantId, parsed })
{
  const companyDetails = parsed.companyDetails;
  const relationshipSummary = parsed.relationshipSummary;
  const primaryContact = parsed.contacts.find(contact => contact.isPrimary);
  const primaryTitle = primaryContact?.title;
  const accountSnapshot = normalizeWhitespace(parsed.accountSnapshot);
  const fitHypothesis = listToText(parsed.fitHypothesis);
  const preferredOpeningAngle = formatPreferredOpeningAngle(parsed.preferredOpeningAngle);
  const nextStepPlan = listToText(parsed.nextStepPlan, '1. ');
  const openQuestions = listToText(parsed.openQuestions);
  const sourceNotes = listToText(parsed.sourceNotes);
  const publicLinks = parsed.publicLinks.map(link => `${link.Label}: ${link.URL}`);
  const products = parsed.productsAndServices;
  const publicPhone = normalizeValue(companyDetails['Public phone']);
  const companyName = pickFirstMeaningful([
    normalizeValue(parsed.frontmatter.trading_name),
    normalizeValue(parsed.frontmatter.client_name),
    normalizeValue(companyDetails['Trading name'])
  ]);

  const fitScore = parseFitScore(parsed.frontmatter.fit_score);
  const createdOn = parseDate(parsed.frontmatter.created_date) ?? new Date().toISOString();
  const updatedOn = parseDate(parsed.frontmatter.last_updated) ?? createdOn;
  const nextActionDueOn = parseDate(parsed.frontmatter.next_action_date);

  const clientPayload = {
    TenantId: tenantId,
    AccountOwner: normalizeValue(parsed.frontmatter.account_owner),
    Status: mapRelationshipStatus(parsed.frontmatter.relationship_status, parsed.frontmatter.pipeline_stage),
    CurrentStage: mapPipelineStage(parsed.frontmatter.pipeline_stage),
    Priority: mapPriority(parsed.frontmatter.priority),
    LeadSource: normalizeValue(parsed.frontmatter.lead_source),
    InitialRoute: normalizeValue(parsed.frontmatter.initial_route),
    FitScore: fitScore,
    OpportunitySummary: clamp(
      joinBlocks([
        accountSnapshot,
        publicLinks.length ? `Public links:\n${publicLinks.map(item => `- ${item}`).join('\n')}` : null,
        products.length ? `Products and services:\n${products.map(item => `- ${item}`).join('\n')}` : null
      ]),
      2048),
    PreferredOpeningAngle: clamp(preferredOpeningAngle, 2048),
    NextAction: clamp(normalizeValue(parsed.frontmatter.next_action), 1024),
    NextActionDueOn: nextActionDueOn,
    CreatedOn: createdOn,
    LastUpdated: updatedOn,
    IsArchived: false
  };

  const companyPayload = {
    Name: companyName,
    LegalEntityName: normalizeValue(parsed.frontmatter.legal_entity),
    TradingName: normalizeValue(parsed.frontmatter.trading_name),
    CompanyNumber: normalizeUnknown(parsed.frontmatter.company_number),
    VatNumber: normalizeUnknown(parsed.frontmatter.vat_number),
    ContactEmailAddress: null,
    ContactPhoneNumber: normalizeUnknown(publicPhone),
    WebsiteUrl: normalizeValue(parsed.frontmatter.website),
    RegisteredOfficeText: normalizeUnknown(parsed.frontmatter.registered_office),
    CreatedOn: createdOn,
    LastUpdated: updatedOn,
    IsActive: true,
    IsVerified: hasStructuredCompanyIdentity(parsed.frontmatter),
    RegisteredAddressId: null
  };

  const contactPayloads = parsed.contacts.map(contact =>
  {
    const notes = [
      contact.notes.length ? `Notes:\n${contact.notes.map(note => `- ${note}`).join('\n')}` : null,
      contact.source ? `Source: ${contact.source}` : null,
      contact.statusText ? `Raw status: ${contact.statusText}` : null
    ].filter(Boolean).join('\n\n');

    return {
      __title: contact.title,
      Name: normalizeValue(contact.fields.Name),
      Position: normalizeValue(contact.fields.Role),
      EmailAddress: normalizeUnknown(contact.fields.Email),
      PhoneNumber: normalizeUnknown(contact.fields.Phone),
      LinkedInUrl: null,
      Source: clamp(contact.source, 256),
      RelationshipRoute: clamp(contact.title, 512),
      Status: mapContactStatus(contact.statusText),
      IsPrimary: contact.isPrimary,
      Notes: clamp(notes || null, 2048),
      CreatedOn: createdOn,
      LastUpdated: updatedOn
    };
  }).filter(payload => payload.Name);

  const opportunityPayload = {
    Type: mapOpportunityType(parsed.frontmatter.opportunity_type),
    Stage: mapPipelineStage(parsed.frontmatter.pipeline_stage),
    EstimatedAnnualValue: null,
    Probability: inferProbability(parsed.frontmatter.pipeline_stage),
    PainSummary: clamp(fitHypothesis, 2048),
    ValueHypothesis: clamp(accountSnapshot, 2048),
    DecisionProcess: clamp(joinBlocks([
      nextStepPlan ? `Next step plan:\n${nextStepPlan}` : null,
      openQuestions ? `Open questions:\n${openQuestions}` : null
    ]), 2048),
    NextAction: clamp(normalizeValue(parsed.frontmatter.next_action), 1024),
    NextActionDueOn: nextActionDueOn,
    CreatedOn: createdOn,
    LastUpdated: updatedOn,
    PrimaryContactId: primaryTitle ?? null
  };

  const materialPayloads = parsed.materials.flatMap(group =>
    group.items
      .filter(item => !isBlankLike(item) && !/^none/i.test(item))
      .map(item =>
      {
        const resolvedPath = resolveMaterialPath(item, {
          folder,
          clientFile,
          marketingRoot,
          clientsRoot
        });

        return {
          Name: clamp(extractMaterialName(item), 256),
          FilePath: clamp(resolvedPath?.filePath ?? null, 1024),
          Type: inferMaterialType(item),
          Status: group.kind === 'sent' ? materialStatuses.Sent : materialStatuses.Ready,
          SentOn: group.kind === 'sent'
            ? inferMaterialSentOn(item, parsed.engagementHistory)
            : null,
          Notes: clamp(joinBlocks([
            resolvedPath?.note,
            `Imported from ${group.label}`
          ]), 2048),
          SentToContactId: group.kind === 'sent' && primaryTitle ? '__primary__' : null,
          CreatedOn: createdOn,
          LastUpdated: updatedOn
        };
      }));

  const activityPayloads = [
    ...parsed.engagementHistory.map(row => ({
      ActivityOn: parseDate(row.Date) ?? createdOn,
      Type: mapActivityType(row.Type),
      Direction: mapActivityDirection(row.Type, row.Summary),
      Summary: clamp(normalizeWhitespace(row.Summary), 2048),
      Outcome: clamp(formatActivityOutcome(row.Material, row.Type), 2048),
      NextAction: null,
      NextActionDueOn: null,
      CreatedOn: parseDate(row.Date) ?? createdOn,
      ClientContactId: shouldLinkPrimaryContact(row.Type, row.Summary) ? '__primary__' : null,
      ClientOpportunityId: '__default__',
      ClientMaterialId: null,
      __materialName: normalizeMaterialReference(row.Material)
    })),
    createNoteActivity(
      createdOn,
      'Imported research summary',
      joinBlocks([
        companyDetails['Main location context']
          ? `Main location context: ${companyDetails['Main location context']}`
          : null,
        products.length
          ? `Products and services:\n${products.map(item => `- ${item}`).join('\n')}`
          : null,
        publicLinks.length
          ? `Public links:\n${publicLinks.map(item => `- ${item}`).join('\n')}`
          : null
      ])),
    createNoteActivity(
      createdOn,
      'Imported strategy summary',
      joinBlocks([
        fitHypothesis ? `Fit hypothesis:\n${fitHypothesis}` : null,
        preferredOpeningAngle ? `Preferred opening angle:\n${preferredOpeningAngle}` : null,
        nextStepPlan ? `Next step plan:\n${nextStepPlan}` : null,
        openQuestions ? `Open questions:\n${openQuestions}` : null
      ])),
    createNoteActivity(
      updatedOn,
      'Imported source notes',
      sourceNotes || null)
  ].filter(Boolean);

  return {
    folder,
    clientPayload,
    companyPayload,
    contactPayloads,
    opportunityPayload,
    materialPayloads,
    activityPayloads
  };
}

function createNoteActivity(activityOn, summary, outcome)
{
  if (isBlankLike(summary) && isBlankLike(outcome))
    return null;

  return {
    ActivityOn: activityOn,
    Type: activityTypes.Note,
    Direction: activityDirections.Internal,
    Summary: clamp(summary, 2048),
    Outcome: clamp(outcome, 2048),
    NextAction: null,
    NextActionDueOn: null,
    CreatedOn: activityOn,
    ClientContactId: null,
    ClientOpportunityId: '__default__',
    ClientMaterialId: null
  };
}

function parseClientMarkdown(markdown)
{
  return {
    frontmatter: parseFrontmatter(markdown),
    accountSnapshot: getSection(markdown, '1\\. Account Snapshot'),
    companyDetails: parseKeyValueTable(getSection(markdown, '2\\. Company Details')),
    publicLinks: parseMarkdownTable(getSection(markdown, '3\\. Public Links')),
    productsAndServices: parseBulletList(getSection(markdown, '4\\. Products And Services')),
    relationshipSummary: parseKeyValueTable(getSection(markdown, '5\\. Relationship Summary')),
    contacts: parseContacts(getSection(markdown, '6\\. Contacts')),
    fitHypothesis: parseBulletList(getSection(markdown, '7\\. Fit Hypothesis')),
    preferredOpeningAngle: parsePreferredOpeningAngle(getSection(markdown, '8\\. Preferred Opening Angle')),
    engagementHistory: parseMarkdownTable(getSection(markdown, '9\\. Engagement History')),
    materials: parseMaterialGroups(getSection(markdown, '10\\. Materials')),
    nextStepPlan: parseNumberedList(getSection(markdown, '11\\. Next Step Plan')),
    openQuestions: parseBulletList(getSection(markdown, '12\\. Open Questions')),
    sourceNotes: parseBulletList(getSection(markdown, '13\\. Source Notes'))
  };
}

function parseFrontmatter(markdown)
{
  const match = markdown.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n/);
  if (!match)
    return {};

  const result = {};
  for (const line of match[1].split(/\r?\n/))
  {
    const index = line.indexOf(':');
    if (index < 0)
      continue;

    const key = line.slice(0, index).trim();
    let value = line.slice(index + 1).trim();
    value = value.replace(/^"(.*)"$/, '$1');
    result[key] = value;
  }

  return result;
}

function getSection(markdown, headingPattern)
{
  const expression = new RegExp(
    String.raw`##\s+${headingPattern}\r?\n([\s\S]*?)(?=\r?\n##\s+\d+\.|\s*$)`);
  const match = markdown.match(expression);
  return match ? match[1].trim() : '';
}

function parseKeyValueTable(section)
{
  const rows = parseMarkdownTable(section);
  const result = {};

  for (const row of rows)
  {
    const key = pickFirstMeaningful(Object.values(row).slice(0, 1));
    const value = pickFirstMeaningful(Object.values(row).slice(1));
    if (key)
      result[key] = value;
  }

  return result;
}

function parseMarkdownTable(section)
{
  const lines = section
    .split(/\r?\n/)
    .filter(line => line.trim().startsWith('|'));

  if (lines.length < 2)
    return [];

  const header = parseTableLine(lines[0]);
  const rows = [];

  for (const line of lines.slice(2))
  {
    const values = parseTableLine(line);
    const row = {};

    for (let index = 0; index < header.length; index += 1)
      row[header[index]] = values[index] ?? '';

    rows.push(row);
  }

  return rows;
}

function parseTableLine(line)
{
  return line
    .split('|')
    .map(value => value.trim())
    .filter(Boolean);
}

function parseBulletList(section)
{
  return section
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(line => line.startsWith('- '))
    .map(line => line.slice(2).trim())
    .filter(Boolean);
}

function parseNumberedList(section)
{
  return section
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(line => /^\d+\.\s+/.test(line))
    .map(line => line.replace(/^\d+\.\s+/, '').trim())
    .filter(Boolean);
}

function parseContacts(section)
{
  const blocks = [];

  for (const block of splitSubsections(section))
  {
    const title = block.title;
    const body = block.body;
    const [tablePart, notesPart = ''] = body.split(/\r?\nNotes:\r?\n/i);
    const fields = parseKeyValueTable(tablePart);
    const notes = parseBulletList(notesPart);

    blocks.push({
      title,
      isPrimary: /primary/i.test(title),
      fields,
      source: normalizeValue(fields.Source),
      statusText: normalizeValue(fields.Status),
      notes
    });
  }

  return blocks;
}

function parsePreferredOpeningAngle(section)
{
  const leadWith = [];
  const avoidLeadingWith = [];
  let target = null;

  for (const rawLine of section.split(/\r?\n/))
  {
    const line = rawLine.trim();

    if (/^Lead with:/i.test(line))
    {
      target = leadWith;
      continue;
    }

    if (/^Avoid leading with:/i.test(line))
    {
      target = avoidLeadingWith;
      continue;
    }

    if (target && line.startsWith('- '))
      target.push(line.slice(2).trim());
  }

  return { leadWith, avoidLeadingWith };
}

function parseMaterialGroups(section)
{
  const groups = [];

  for (const block of splitSubsections(section))
  {
    const label = block.title;
    const items = parseBulletList(block.body);
    groups.push({
      label,
      kind: /^sent\b/i.test(label) ? 'sent' : 'reusable',
      items
    });
  }

  return groups;
}

function splitSubsections(section)
{
  const lines = section.split(/\r?\n/);
  const blocks = [];
  let currentTitle = null;
  let currentLines = [];

  const flush = () =>
  {
    if (!currentTitle)
      return;

    blocks.push({
      title: currentTitle,
      body: currentLines.join('\n').trim()
    });
  };

  for (const line of lines)
  {
    const match = line.match(/^###\s+(.+)$/);
    if (match)
    {
      flush();
      currentTitle = match[1].trim();
      currentLines = [];
      continue;
    }

    if (currentTitle)
      currentLines.push(line);
  }

  flush();
  return blocks;
}

function mapRelationshipStatus(rawStatus, rawStage)
{
  const status = `${rawStatus ?? ''} ${rawStage ?? ''}`.toLowerCase();

  if (status.includes('won') || status.includes('client'))
    return relationshipStatuses.Client;
  if (status.includes('contracted'))
    return relationshipStatuses.Contracted;
  if (status.includes('onboarding'))
    return relationshipStatuses.Onboarding;
  if (status.includes('dormant') || status.includes('nurture'))
    return relationshipStatuses.Dormant;
  if (status.includes('lost') || status.includes('disqualified'))
    return relationshipStatuses.Disqualified;
  if (
    status.includes('responded')
    || status.includes('discovery')
    || status.includes('proposal')
    || status.includes('negotiation')
    || status.includes('contract sent'))
    return relationshipStatuses.ActiveOpportunity;

  return relationshipStatuses.Prospect;
}

function mapPipelineStage(rawStage)
{
  const stage = (rawStage ?? '').toLowerCase();

  if (stage.includes('unqualified'))
    return pipelineStages.Unqualified;
  if (stage.includes('research'))
    return pipelineStages.Researched;
  if (stage.includes('contact identified'))
    return pipelineStages.ContactIdentified;
  if (stage.includes('outreach ready'))
    return pipelineStages.OutreachReady;
  if (stage.includes('outreach sent') || stage.includes('follow-up sent'))
    return pipelineStages.OutreachSent;
  if (stage.includes('responded'))
    return pipelineStages.Responded;
  if (stage.includes('discovery booked'))
    return pipelineStages.DiscoveryBooked;
  if (stage.includes('discovery completed'))
    return pipelineStages.DiscoveryCompleted;
  if (stage.includes('proposal sent'))
    return pipelineStages.ProposalSent;
  if (stage.includes('negotiation'))
    return pipelineStages.Negotiation;
  if (stage.includes('contract sent'))
    return pipelineStages.ContractSent;
  if (stage.includes('won'))
    return pipelineStages.Won;
  if (stage.includes('lost'))
    return pipelineStages.Lost;
  if (stage.includes('nurture'))
    return pipelineStages.Nurture;

  return pipelineStages.Researched;
}

function mapPriority(rawPriority)
{
  const priority = (rawPriority ?? '').toLowerCase();

  if (priority.includes('priority a'))
    return priorities.Strategic;
  if (priority.includes('active') || priority.includes('awaiting response'))
    return priorities.High;
  if (priority.includes('initial outreach sent'))
    return priorities.Medium;

  return priorities.Unknown;
}

function mapOpportunityType(rawType)
{
  const type = (rawType ?? '').toLowerCase();

  if (type.includes('supplier payment review'))
    return opportunityTypes.SupplierPaymentReview;
  if (type.includes('third-party audit'))
    return type.includes('financial review')
      ? opportunityTypes.Blended
      : opportunityTypes.ThirdPartyAudit;
  if (type.includes('process improvement'))
    return opportunityTypes.ProcessImprovement;
  if (type.includes('early payment'))
    return opportunityTypes.EarlyPaymentDiscounting;
  if (type.includes('portal'))
    return opportunityTypes.PortalServices;
  if (type.includes('blended'))
    return opportunityTypes.Blended;

  return opportunityTypes.Unknown;
}

function mapContactStatus(rawStatus)
{
  const status = (rawStatus ?? '').toLowerCase();

  if (status.includes('do not contact'))
    return contactStatuses.DoNotContact;
  if (status.includes('left company'))
    return contactStatuses.LeftCompany;
  if (status.includes('follow-up sent') || status.includes('email sent') || status.includes('outreach sent'))
    return contactStatuses.Contacted;
  if (status.includes('verified public direct email') || status.includes('best named public route'))
    return contactStatuses.Verified;
  if (status.includes('high-confidence pattern') || status.includes('pattern inference') || status.includes('inferred'))
    return contactStatuses.Suggested;
  if (status.includes('engaged'))
    return contactStatuses.Engaged;

  return contactStatuses.Unknown;
}

function mapActivityType(rawType)
{
  const type = (rawType ?? '').toLowerCase();

  if (type.includes('follow-up'))
    return activityTypes.FollowUp;
  if (type.includes('email'))
    return activityTypes.Email;
  if (type.includes('lead researched') || type.includes('contact identified') || type.includes('contact research') || type.includes('lead created'))
    return activityTypes.Research;
  if (type.includes('contact detail update'))
    return activityTypes.Note;
  if (type.includes('messaging developed'))
    return activityTypes.Note;

  return activityTypes.Note;
}

function mapActivityDirection(rawType, rawSummary)
{
  const text = `${rawType ?? ''} ${rawSummary ?? ''}`.toLowerCase();

  if (text.includes('email sent') || text.includes('follow-up sent') || /^email$/i.test(rawType ?? ''))
    return activityDirections.Outbound;

  return activityDirections.Internal;
}

function shouldLinkPrimaryContact(rawType, rawSummary)
{
  const text = `${rawType ?? ''} ${rawSummary ?? ''}`.toLowerCase();
  return text.includes('email') || text.includes('follow-up');
}

function inferMaterialType(value)
{
  const text = (value ?? '').toLowerCase();

  if (text.includes('email'))
    return materialTypes.Email;
  if (text.includes('overview'))
    return materialTypes.Overview;
  if (text.includes('proposal'))
    return materialTypes.Proposal;
  if (text.includes('discovery'))
    return materialTypes.DiscoveryNotes;
  if (text.includes('contract'))
    return materialTypes.Contract;
  if (text.includes('handoff'))
    return materialTypes.HandoffPack;

  return materialTypes.SupportingEvidence;
}

function inferProbability(rawStage)
{
  const stage = mapPipelineStage(rawStage);

  switch (stage)
  {
    case pipelineStages.OutreachReady: return 10;
    case pipelineStages.OutreachSent: return 15;
    case pipelineStages.Responded: return 25;
    case pipelineStages.DiscoveryBooked: return 35;
    case pipelineStages.DiscoveryCompleted: return 45;
    case pipelineStages.ProposalSent: return 60;
    case pipelineStages.Negotiation: return 75;
    case pipelineStages.ContractSent: return 90;
    case pipelineStages.Won: return 100;
    case pipelineStages.Lost: return 0;
    default: return 5;
  }
}

function parseFitScore(rawScore)
{
  if (isBlankLike(rawScore) || /^unknown$/i.test(rawScore))
    return null;

  const ratioMatch = String(rawScore).match(/(\d+(?:\.\d+)?)\s*\/\s*(\d+(?:\.\d+)?)/);
  if (ratioMatch)
    return Number.parseFloat(ratioMatch[1]);

  const numeric = Number.parseFloat(String(rawScore));
  return Number.isFinite(numeric) ? numeric : null;
}

function parseDate(value)
{
  if (isBlankLike(value))
    return null;

  const raw = String(value).trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(raw))
    return `${raw}T00:00:00.000Z`;

  const parsed = new Date(raw);
  return Number.isNaN(parsed.valueOf()) ? null : parsed.toISOString();
}

function formatPreferredOpeningAngle(preferred)
{
  return joinBlocks([
    preferred.leadWith.length
      ? `Lead with:\n${preferred.leadWith.map(item => `- ${item}`).join('\n')}`
      : null,
    preferred.avoidLeadingWith.length
      ? `Avoid leading with:\n${preferred.avoidLeadingWith.map(item => `- ${item}`).join('\n')}`
      : null
  ]);
}

function formatActivityOutcome(material, type)
{
  const parts = [];
  if (!isBlankLike(type))
    parts.push(`Imported type: ${normalizeWhitespace(type)}`);
  if (!isBlankLike(material) && !/^none$/i.test(material))
    parts.push(`Material: ${normalizeWhitespace(material)}`);
  return parts.join('\n');
}

function normalizeMaterialReference(material)
{
  if (isBlankLike(material) || /^none$/i.test(material))
    return null;

  return extractMaterialName(material);
}

function inferMaterialSentOn(material, engagementRows)
{
  const normalized = normalizeMaterialReference(material);
  if (!normalized)
    return null;

  const row = engagementRows.find(item =>
    normalizeMaterialReference(item.Material) === normalized);

  return parseDate(row?.Date);
}

function extractMaterialName(value)
{
  return normalizeWhitespace(
    String(value)
      .replace(/\s+in the main Marketing folder$/i, '')
      .replace(/^Sent\//i, '')
      .replace(/^Drafts\//i, '')
  );
}

function resolveMaterialPath(value, { folder, marketingRoot, clientsRoot })
{
  const normalized = String(value)
    .replace(/\s+in the main Marketing folder$/i, '')
    .trim();

  const clientRoot = path.join(clientsRoot, folder);
  const candidates = [
    path.join(clientRoot, normalized),
    path.join(marketingRoot, normalized),
    path.join(marketingRoot, 'Documentation', normalized),
    path.join(marketingRoot, extractMaterialName(normalized))
  ];

  for (const candidate of candidates)
  {
    if (fsSync.existsSync(candidate))
    {
      return {
        filePath: candidate,
        note: `Resolved from import source: ${candidate}`
      };
    }
  }

  return {
    filePath: null,
    note: `Source reference: ${normalized}`
  };
}

function hasStructuredCompanyIdentity(frontmatter)
{
  return !isBlankLike(normalizeUnknown(frontmatter.company_number))
    || !isBlankLike(normalizeUnknown(frontmatter.registered_office))
    || !isBlankLike(normalizeUnknown(frontmatter.vat_number));
}

function normalizeValue(value)
{
  if (value === undefined || value === null)
    return null;

  const normalized = normalizeWhitespace(String(value));
  return normalized.length === 0 ? null : normalized;
}

function normalizeUnknown(value)
{
  const normalized = normalizeValue(value);
  if (normalized && /^unknown$/i.test(normalized))
    return null;
  return normalized;
}

function normalizeWhitespace(value)
{
  return String(value ?? '')
    .replace(/\r/g, '')
    .replace(/\u00c2/g, '')
    .replace(/[ \t]+\n/g, '\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

function isBlankLike(value)
{
  return value === undefined || value === null || String(value).trim().length === 0;
}

function clamp(value, max)
{
  if (value === null || value === undefined)
    return null;

  const normalized = normalizeWhitespace(value);
  return normalized.length <= max
    ? normalized
    : normalized.slice(0, max - 1).trimEnd() + '…';
}

function listToText(items, prefix = '- ')
{
  return items.length
    ? items.map(item => `${prefix}${item}`).join('\n')
    : null;
}

function joinBlocks(blocks)
{
  return blocks.filter(block => !isBlankLike(block)).join('\n\n');
}

function pickFirstMeaningful(values)
{
  for (const value of values)
  {
    const normalized = normalizeValue(value);
    if (!isBlankLike(normalized))
      return normalized;
  }

  return null;
}

function parseArgs(argv)
{
  const parsed = {};

  for (let index = 0; index < argv.length; index += 1)
  {
    const arg = argv[index];
    if (!arg.startsWith('--'))
      continue;

    const key = arg.slice(2);
    const next = argv[index + 1];

    if (next && !next.startsWith('--'))
    {
      parsed[key] = next;
      index += 1;
    }
    else
    {
      parsed[key] = true;
    }
  }

  return parsed;
}

function hasFlag(parsedArgs, name)
{
  return parsedArgs[name] === true || parsedArgs[name] === 'true';
}
