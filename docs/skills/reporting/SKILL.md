---
name: reporting
description: Reports and exports for the Talent module - the reporting read model, HTML report templates rendered with Razor and printed to PDF with Playwright, Excel with ClosedXML, Apache ECharts charts, row-level security and audited exports (ADR-11). Use when building a report, dashboard, export, scheduled report or BI view.
---

# Reporting (free, open-source stack)

Source: `docs/talent-solution-architecture.md` ADR-11; reports in the blueprint and gap analysis §4.8.

## Read model

- Reports read the **`reporting` schema**: denormalised tables refreshed by events and nightly Hangfire jobs.
- Reports **never read transactional tables**.
- **PostgreSQL row-level security** applies to the `reporting` schema.

## Tools (all free and open source)

| Need | Tool | Licence |
|---|---|---|
| Templates | Razor components rendered with the built-in `HtmlRenderer` | MIT |
| PDF | **Playwright for .NET** (Chromium `page.PdfAsync`) | Apache 2.0 |
| Excel | **ClosedXML** | MIT |
| Charts | **Apache ECharts**, the same library in the React app and in PDFs | Apache 2.0 |
| BI (optional) | Client's Power BI, or **Apache Superset**, on governed views | Apache 2.0 |

Do not add QuestPDF, DevExpress or Telerik. They need paid licences.
Renderers are a plug point (`IReportRenderer`, keyed by format: `Pdf`, `Excel`).

## How a report runs

1. A user request or schedule starts a **Hangfire job**. Never render a PDF inside the web request.
2. The job loads data from the `reporting` schema, **filtered by the user's data scope**.
3. Razor renders the template to HTML. Charts render in the page.
4. Playwright prints the PDF. ClosedXML builds the Excel file.
5. The job writes an **audit entry**, then stores or emails the file.

## Rules

- 1 Razor component per report. Template CSS lives with the template.
- Use **print CSS** (`@page`, page breaks, header/footer) to match client layouts.
- Reuse **1 Chromium instance** per worker. Create a new page per report.
- Run Chromium only in the **background worker** container, not the web nodes.
- Every export checks the `Export` permission and writes an audit entry.
- Confidential fields follow the same masking rules as the API.
- Trend reports use `ReadinessSnapshot` and the stable **reporting bands**.

## Tests (write first)

- Data query tests with Testcontainers: correct rows and correct scope filtering.
- Render tests: check the HTML for key values. Don't compare PDF bytes.
- 1 smoke test per report that produces a valid PDF and Excel file.

## Reports in scope

Monthly and quarterly readiness, trend analysis, and role-wise readiness.
Build the client templates (B9) once they arrive.
