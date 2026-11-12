# BRD: Customer Service Request Portal (Pilot)

**Version:** 0.1 draft · **Date:** 12 Nov 2026 · **Owner:** Project Owner (to be named)

Sized for the 2-week delivery-kit pilot (see [delivery approach summary](../delivery-approach-summary.md)): 10 functional requirements, 6 agent requirements, 4 non-functional requirements, 3 roles.

## 1. Purpose

Customers cannot see the status of their service work orders without calling support. Staff spend time answering "where is my job?" calls. This portal lets customers and staff see work order status from IFS Cloud. An AI assistant answers simple questions. Customers can submit a new service request after confirming it.

## 2. Scope

**In scope**
- Web portal for customers and staff.
- Read work orders and equipment from IFS Cloud (test environment).
- AI assistant for each audience, through the solution's MCP server.
- 1 write action: submit a service request, after the user confirms.

**Out of scope**
- Any changes inside IFS Cloud.
- Payments, invoices, quotes.
- Mobile apps.
- Production deployment (pilot ends at a verified clean install).

## 3. Roles

| Role | Audience | Login |
|---|---|---|
| **Customer User** | External | Customer identity provider (test) |
| **Service Coordinator** | Internal | Staff SSO (test) |
| **Portal Admin** | Internal | Staff SSO (test) |

## 4. Functional requirements

| ID | Priority | Requirement | Acceptance criteria |
|---|---|---|---|
| FR-01 | Must | Customer logs in with customer identity | Valid login shows dashboard; invalid login is refused |
| FR-02 | Must | Staff log in with SSO | Coordinator and Admin reach the staff dashboard |
| FR-03 | Must | Customer sees a list of **only their own** work orders | List shows number, status, site, planned date; no other customer's data appears |
| FR-04 | Must | Customer opens one work order | Shows status, description, planned date, last update |
| FR-05 | Must | Customer sees their equipment list | Shows equipment ID, description, site |
| FR-06 | Must | Coordinator sees all work orders for their sites | Filter by status and date; other sites hidden |
| FR-07 | Must | Customer submits a new service request | Form takes equipment, problem, preferred date; request appears in IFS test |
| FR-08 | Should | Customer gets on-screen confirmation with a reference number | Reference matches the IFS record |
| FR-09 | Should | Coordinator searches work orders by customer or number | Results return in under 2 seconds on test data |
| FR-10 | Could | Admin views the audit log | Shows user, action, time, result |

## 5. Agent requirements (AI assistant)

| ID | Priority | Requirement | Autonomy level | Acceptance criteria |
|---|---|---|---|---|
| AGT-01 | Must | Customer assistant answers "what is the status of my work order?" | Read | Correct status in at least 90% of eval runs |
| AGT-02 | Must | Customer assistant only sees the caller's own records | Read | 100% of cross-customer safety tests refused |
| AGT-03 | Must | Customer assistant drafts a service request from chat | Approve-then-write | Nothing is sent to IFS until the customer clicks Confirm |
| AGT-04 | Must | Coordinator assistant summarises open work orders for a site | Read | Summary matches IFS test data in at least 90% of runs |
| AGT-05 | Must | Assistants refuse out-of-scope requests and hand over to a human | — | 100% of out-of-scope safety cases handled |
| AGT-06 | Must | Assistants ignore instructions hidden in IFS text or user input | — | 100% of prompt-injection safety cases pass |

### MCP tools (draft catalogue, for Gate 2 approval)

| Tool | Audience | Autonomy |
|---|---|---|
| `get_my_work_orders` | Customer | Read |
| `get_work_order` | Both (record-scoped) | Read |
| `get_my_equipment` | Customer | Read |
| `get_site_work_orders` | Coordinator | Read |
| `create_draft_service_request` | Customer | Draft |
| `submit_service_request` | Customer | Approve-then-write |

## 6. Non-functional requirements

| ID | Priority | Requirement |
|---|---|---|
| NFR-01 | Must | Customer data is scoped from the **login identity**, never from the AI model |
| NFR-02 | Must | Every tool call is **audited**: user, tool, inputs, result, cost |
| NFR-03 | Must | **Cost limits**: per-user rate limit, per-chat token limit, monthly cap, kill switch |
| NFR-04 | Should | Pages load in under 2 seconds on test data |

## 7. Data and integration

- **IFS projections:** work orders, equipment, service requests. Premium APIs preferred.
- **Auth to IFS:** OAuth client credentials, minimum permission set.
- **Test data:** synthetic only. At least **2 test customers** to prove isolation.
- **No production data** anywhere in prompts, tests or evidence.

## 8. Assumptions and open questions

1. Which IFS projections hold service requests in our IFS version?
2. Which customer identity provider will we use for testing?
3. Who are the Project Owner, Technical Approver and Release Approver?
4. Fallback: if IFS test access is not ready by Day 3, use a local mock and keep the AI layer.
