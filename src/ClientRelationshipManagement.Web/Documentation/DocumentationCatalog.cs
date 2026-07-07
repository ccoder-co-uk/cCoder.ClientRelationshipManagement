using ClientRelationshipManagement.Web.Models.Documentation;

namespace ClientRelationshipManagement.Web.Documentation;

public static class DocumentationCatalog
{
    public static DocumentationPageDefinition Root { get; } = BuildRoot();

    public static DocumentationPageDefinition GetPage(string slug)
    {
        string normalizedSlug = NormalizeSlug(slug);

        DocumentationPageDefinition page = Flatten(Root)
            .FirstOrDefault(item => item.Slug == normalizedSlug);

        return page ?? Root;
    }

    public static IReadOnlyList<DocumentationPageDefinition> FlattenedPages { get; } =
        Flatten(BuildRoot()).ToList();

    static DocumentationPageDefinition BuildRoot()
    {
        DocumentationPageDefinition homePage = new()
        {
            Slug = "Pages/Home-Page",
            Title = "Home Page",
            Eyebrow = "Pages",
            Lead = "The dashboard is the operator's daily command board: it surfaces workflow-owned tasks, portfolio stats, and the quickest route into the right workspace.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "overview",
                    Title = "What the page is for",
                    Paragraphs =
                    [
                        "The home page is the starting point for users working the workflow. It is intentionally focused on what must happen next rather than on broad navigation or reporting alone.",
                        "Every task shown here is generated from the process engine and tied back to a lead, opportunity, or live client account."
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "portfolio-state-model",
                    Title = "Portfolio state model",
                    Paragraphs =
                    [
                        "Relationship states describe where the commercial journey sits after a lead has been qualified into a tenant relationship. They are intended to drive action, not decorate the UI."
                    ],
                    Bullets =
                    [
                        .. ClientStateGuide.Entries.Select(entry =>
                            $"{Utilities.DisplayText.Humanize(entry.Status)}: {entry.Summary} {entry.ProgressionHint}")
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "actions",
                    Title = "Top actions",
                    Paragraphs =
                    [
                        "The top five scheduled actions are drawn from pending process tasks ordered by due date.",
                        "When the action is an email step, the card carries the generated draft and the user can review, approve, and confirm sending without leaving the dashboard."
                    ]
                }
            ]
        };

        DocumentationPageDefinition leadsPage = new()
        {
            Slug = "Pages/Leads-Page",
            Title = "Leads Page",
            Eyebrow = "Pages",
            Lead = "The leads page is the intake and qualification surface for raw company data before it becomes trusted master data and a live opportunity.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "intake",
                    Title = "Lead intake",
                    Paragraphs =
                    [
                        "Users can create a single lead manually or bulk import a CSV file of raw companies and contacts from focused dialogs while the main page stays centred on the lead queue.",
                        "Leads are intentionally provisional. They can contain incomplete or untrusted data until qualification improves the record."
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "qualification",
                    Title = "Qualification flow",
                    Paragraphs =
                    [
                        "Workflow steps move the lead through research, verification, and qualification.",
                        "When the lead is qualified, automation matches or creates master company data, creates the tenant relationship, and opens the first opportunity."
                    ]
                }
            ]
        };

        DocumentationPageDefinition opportunitiesPage = new()
        {
            Slug = "Pages/Opportunities-Page",
            Title = "Opportunities Page",
            Eyebrow = "Pages",
            Lead = "The opportunities page gives a stage-focused view of live commercial work so the team can see the pipeline separately from the broader relationship index.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "grid",
                    Title = "Opportunity grid",
                    Paragraphs =
                    [
                        "The grid shows company, relationship state, pipeline stage, opportunity type, primary contact, expected value, and the next workflow action.",
                        "This lets users work the opportunity pipeline directly while still linking back into the full client workspace for detailed editing."
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "workflow",
                    Title = "Workflow alignment",
                    Paragraphs =
                    [
                        "Next actions on this page come from pending process tasks rather than old free-text next-action fields.",
                        "That keeps the operational queue aligned with the workflow engine, generated drafts, and approval steps."
                    ]
                }
            ]
        };

        DocumentationPageDefinition clientListPage = new()
        {
            Slug = "Pages/Clients-Page",
            Title = "Clients Page",
            Eyebrow = "Pages",
            Lead = "The clients page is the tenant relationship index for qualified companies, active opportunities, and live client accounts.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "grid",
                    Title = "Grid view",
                    Paragraphs =
                    [
                        "The grid shows the company, current relationship status, sales stage, ownership, and the next workflow action for each tenant relationship.",
                        "Use it to filter the book by state, sort it by operational priority, and jump into a focused workspace for the selected relationship."
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "workflow",
                    Title = "Filtering and navigation",
                    Paragraphs =
                    [
                        "Dashboard status cards can link into this page with a pre-applied state filter so users can work a specific slice of the pipeline immediately.",
                        "Each row routes into the client details page, where the tenant relationship, child opportunities, communications, and activity history are managed in context."
                    ]
                }
            ]
        };

        DocumentationPageDefinition clientWorkspacePage = new()
        {
            Slug = "Pages/Client-Details-Page",
            Title = "Client Details Page",
            Eyebrow = "Pages",
            Lead = "The client details page is the operational workspace for one tenant relationship, showing the root relationship record alongside opportunities, communications, activities, and scheduled work.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "record",
                    Title = "Relationship record",
                    Paragraphs =
                    [
                        "The top form edits the tenant-specific relationship to the company, including ownership, status, stage, priority, research notes, and data quality notes.",
                        "This keeps the operator-facing commercial view separate from the shared master company record while still letting trusted company fields be corrected."
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "pipeline",
                    Title = "Collections and history",
                    Paragraphs =
                    [
                        "Tabbed collections expose child opportunities, communication history, and recorded activities so the page mirrors the underlying data graph more accurately.",
                        "Workflow-owned email drafts and scheduled tasks appear in the same workspace, which means progress and approvals stay tied to the record they belong to."
                    ]
                }
            ]
        };

        DocumentationPageDefinition emailsPage = new()
        {
            Slug = "Pages/Emails-Page",
            Title = "Emails Page",
            Eyebrow = "Pages",
            Lead = "The emails page exposes the strongly typed CRM email queue so draft, approved, failed, and sent messages can be reviewed independently of the relationship workspace.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "queue",
                    Title = "Queue visibility",
                    Paragraphs =
                    [
                        "This page sits directly on top of the CRM `Email` entity rather than inferring mail state from generic materials.",
                        "Use it to review recipient details, scheduled send timing, failures, and overall queue health."
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "approval",
                    Title = "Approval workflow",
                    Paragraphs =
                    [
                        "Draft emails can be approved for sending from the emails page, the dashboard scheduled-action flow, or the client communications tab.",
                        "Approval prepares the message for scheduled sending, but the related workflow task is only completed once the email is confirmed as sent."
                    ]
                }
            ]
        };

        DocumentationPageDefinition importsPage = new()
        {
            Slug = "Pages/Imports-Page",
            Title = "Imports Page",
            Eyebrow = "Pages",
            Lead = "The imports page manages large authority and third-party files from draft upload through mapping review and hosted processing.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "workflow",
                    Title = "Workflow",
                    Paragraphs =
                    [
                        "CRM creates the import record and stores the source, instructions, mapping snapshot, and processing status.",
                        "The browser uploads directly to Hosted Services in resumable chunks. Hosted Services owns the only import workspace on disk and processes only imports explicitly marked ready."
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "processing",
                    Title = "Processing",
                    Paragraphs =
                    [
                        "Hosted Services canonicalizes the uploaded file into internal company/contact files, then merges data into master companies, addresses, contacts, leads, and provenance links.",
                        "Authoritative source records can update exact matches, while non-authoritative imports preserve verified data and fill only safe blanks."
                    ]
                }
            ]
        };

        DocumentationPageDefinition processPage = new()
        {
            Slug = "Pages/Process-Page",
            Title = "Process Page",
            Eyebrow = "Pages",
            Lead = "The process page defines the operational playbook for lead qualification, opportunity progression, and client onboarding.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "engine",
                    Title = "What the page controls",
                    Paragraphs =
                    [
                        "The process list page focuses on workflow definitions as a grid, with create, edit, and delete operations available at the definition level.",
                        "Editing a process opens a dedicated detail page with the scalar process definition at the top and the ordered step grid below it."
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "templates",
                    Title = "Templates and scripts",
                    Paragraphs =
                    [
                        "Step editing happens in dialogs so the detail page stays centred on the ordered step list rather than a permanently expanded form surface.",
                        "Email steps can generate draft messages from templates with lead, company, contact, relationship, opportunity, and client-account placeholders rendered into the final content.",
                        "Call and review steps can carry scripts, instructions, and key questions so the operator can follow a consistent route through qualification, outreach, negotiation, and onboarding."
                    ]
                }
            ]
        };

        DocumentationPageDefinition securityPage = new()
        {
            Slug = "API/Security",
            Title = "Security",
            Eyebrow = "API",
            Lead = "Authentication for the CRM app is handled by the SSO-backed account endpoints in the CRM web layer.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "login",
                    Title = "Authenticate and issue a token",
                    Paragraphs =
                    [
                        "Submit credentials to `/Api/Account/Login` as JSON. The endpoint both creates the authenticated session cookie used by MVC pages and returns a token payload for API callers."
                    ],
                    Bullets =
                    [
                        "POST `/Api/Account/Login` with `{ \"user\": \"name\", \"pass\": \"password\" }`",
                        "POST `/Api/Account/Logout` to clear the current session"
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "usage",
                    Title = "Using the result",
                    Paragraphs =
                    [
                        "Browser-based CRM pages rely on the authenticated session cookie after login.",
                        "API clients can also retain the returned token and submit it as a bearer token on subsequent requests if they are not using the session cookie path."
                    ]
                }
            ]
        };

        DocumentationPageDefinition currentSurfacePage = new()
        {
            Slug = "API/Current-Surface",
            Title = "Current Surface",
            Eyebrow = "API",
            Lead = "The rebuilt CRM currently exposes session and authentication endpoints in the API layer while operational data management is routed through MVC workflows.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "today",
                    Title = "What exists today",
                    Paragraphs =
                    [
                        "At present the JSON API surface is intentionally narrow and centred on account/session handling.",
                        "Lead creation, qualification, relationship management, workflow progression, and draft-email approvals currently flow through the MVC routes so the user experience and workflow engine stay aligned."
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "next",
                    Title = "Future direction",
                    Paragraphs =
                    [
                        "When operational JSON endpoints are introduced, they should mirror the rebuilt platform model rather than the retired client-centric API.",
                        "That means any future API work should speak in terms of leads, companies, company contacts, tenant relationships, opportunities, client accounts, workflow instances, tasks, materials, and emails."
                    ]
                }
            ]
        };

        DocumentationPageDefinition importsApiPage = new()
        {
            Slug = "API/Imports",
            Title = "Imports",
            Eyebrow = "API",
            Lead = "Import APIs coordinate draft job creation, Hosted Services upload-session brokering, mapping review, and ready-to-process transitions.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "crm-api",
                    Title = "CRM endpoints",
                    Bullets =
                    [
                        "POST `/Api/Imports` creates a draft import and source binding.",
                        "POST `/Api/Imports/{id}/upload-session` requests a one-time Hosted Services upload session.",
                        "POST `/Api/Imports/{id}/analyse` asks Hosted Services to sample the uploaded file and propose mapping.",
                        "POST `/Api/Imports/{id}/mapping` stores user-reviewed mapping and instructions.",
                        "POST `/Api/Imports/{id}/mark-ready` releases the import for Hosted Services processing.",
                        "GET `/Api/Imports/{id}` and GET `/Api/Imports` return status and progress.",
                        "POST `/Api/Imports/{id}/cancel` and DELETE `/Api/Imports/{id}` stop or remove eligible imports."
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "hosted-services-api",
                    Title = "Hosted Services endpoints",
                    Bullets =
                    [
                        "POST `/internal/imports/upload-session` creates a resumable chunked upload session.",
                        "PUT `/internal/imports/{id}/chunks/{chunkIndex}` stores a chunk in the Hosted Services import workspace.",
                        "POST `/internal/imports/{id}/complete-upload` assembles chunks into the raw import file.",
                        "GET `/internal/imports/{id}/upload-status` reports upload and processing status.",
                        "DELETE `/internal/imports/{id}/files` removes staged files when an import is deleted."
                    ]
                }
            ]
        };

        DocumentationPageDefinition pagesFolder = new()
        {
            Slug = "Pages",
            Title = "Pages",
            Eyebrow = "Documentation",
            Lead = "Page documentation explains how each user-facing area of the CRM is intended to behave and what operational decisions it supports.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "scope",
                    Title = "Purpose",
                    Paragraphs =
                    [
                        "As new MVC pages are added to the CRM app, they should be documented here so the product structure stays understandable and discoverable."
                    ]
                }
            ],
            Children =
            [
                homePage,
                leadsPage,
                opportunitiesPage,
                clientListPage,
                clientWorkspacePage,
                emailsPage,
                importsPage,
                processPage
            ]
        };

        DocumentationPageDefinition apiFolder = new()
        {
            Slug = "API",
            Title = "API",
            Eyebrow = "Documentation",
            Lead = "The API documentation describes the current authenticated surface and the model principles future endpoints should follow.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "scope",
                    Title = "Purpose",
                    Paragraphs =
                    [
                        "The documentation should describe the API that actually exists today, while also making the intended future direction clear enough that later integration work has a stable target."
                    ]
                }
            ],
            Children =
            [
                securityPage,
                currentSurfacePage,
                importsApiPage
            ]
        };

        return new DocumentationPageDefinition
        {
            Slug = string.Empty,
            Title = "Documentation",
            Eyebrow = "Corporate LinX CRM",
            Lead = "Welcome to the CRM documentation workspace. Use the tree on the left to move between product pages, API notes, and the operational guidance we are building alongside the system.",
            Sections =
            [
                new DocumentationSectionDefinition
                {
                    Id = "welcome",
                    Title = "Welcome",
                    Paragraphs =
                    [
                        "This area is intentionally structured like a documentation portal rather than a one-page help sheet.",
                        "It gives the CRM app a growing knowledge base for both UI behavior and API contracts, which means product changes can be explained at the same time they are shipped."
                    ]
                },
                new DocumentationSectionDefinition
                {
                    Id = "structure",
                    Title = "Initial structure",
                    Bullets =
                    [
                        "Pages: user-facing MVC areas such as dashboard, leads, clients, emails, and process design",
                        "API: current security/session behavior plus notes on the future operational surface"
                    ]
                }
            ],
            Children =
            [
                pagesFolder,
                apiFolder
            ]
        };
    }

    static IEnumerable<DocumentationPageDefinition> Flatten(DocumentationPageDefinition page)
    {
        yield return page;

        foreach (DocumentationPageDefinition child in page.Children)
        foreach (DocumentationPageDefinition descendant in Flatten(child))
            yield return descendant;
    }

    static string NormalizeSlug(string slug) =>
        string.IsNullOrWhiteSpace(slug)
            ? string.Empty
            : slug.Trim().Trim('/');
}
