# Talent Management Blockers: Beginner's Guide

A plain-English explanation of the 12 blockers in [talent-management-gap-analysis.md](talent-management-gap-analysis.md) §0. Written for team members who are new to the HR / talent domain.

---

## The domain in 1 minute

**Talent management** is how a company finds its best people, plans who replaces key leaders, and helps people grow.

The blueprint has 4 parts that run as a yearly cycle:

| Part | Plain meaning | Example |
|---|---|---|
| **Career Centre** | Employees say what job they want in the future | "I want to be Finance Manager in 5 years. I can move to Kandy." |
| **Talent Assessment** | Managers rate employees to find future leaders | "Nimal is a high performer with high potential." |
| **Succession Planning** | Pick backup people for important jobs | "If the Finance Head leaves, Nimal or Sara can take over." |
| **Development Plan (IDP)** | A personal plan to close skill gaps | "Nimal does a leadership course and a 3-month project by June." |

```
Career Centre → Talent Assessment → Succession Planning → Development Plan → (next year)
```

---

## A. Business decisions (functional design)

### B1. Rating scales and the nine-box grid

- **What it is**
  - A **scale** is a fixed set of rating levels, like 1 to 5 stars.
  - The **nine-box grid** is a 3 × 3 table, the most common talent tool.
  - It places each employee by **performance** (results today) and **potential** (how far they can grow).
- **Other scales we need**
  - **Readiness**: how soon a backup person can take the job ("Ready now", "1–2 years").
  - **Risk of loss**: how likely the person is to leave the company.
  - **Impact of loss**: how much harm it does if they leave.

```
                 Low perf.       Medium perf.     High perf.
High potential   Rough diamond   Future star      Star (future leader)
Med. potential   Inconsistent    Core player      High performer
Low potential    Talent risk     Effective        Trusted expert
```

- **Why it blocks us:** almost every screen and report uses these values. Without them we cannot build forms, the grid, or readiness reports.
- **Ask the client:** the levels, labels and meaning of each scale. The box names. Is placement calculated or chosen by hand?

### B2. Leadership candidate criteria

- **What it is**
  - Not every employee is assessed. HR picks a **shortlist** of possible future leaders first.
  - The shortlist usually comes from the **appraisal** (the yearly performance review in the Performance module).
- **Example rule:** "Rating 4 or more in the last 2 appraisals, grade Manager or above, at least 2 years of service."
- **Why it blocks us:** every talent cycle starts with this list. We do not know which data to read, or which rule to apply.
- **Ask the client:** the exact rule. How many years of appraisals? Is the list automatic, manual, or suggested by the system and then edited?

### B3. Workflow rules

- **What it is:** a **workflow** is the path a form takes from person to person. For example: reviewer fills it in, approver approves, board decides.
- **Questions the blueprint does not answer:**
  - **Approval levels**: 1 approver, or 2 (e.g. HOD, then HR)?
  - **Reject path**: if the approver disagrees, does the form go back? With comments?
  - **Board override**: can the board move someone to a different box than the reviewer chose?
  - **Succession approval**: is a successor list final when saved, or does someone approve it?
  - **First IDP**: who creates the first development plan, and what triggers it?
  - **Default roles**: who is the reviewer by default (e.g. the direct supervisor)?
- **Why it blocks us:** every screen with Submit / Approve / Return buttons depends on these rules.
- **Ask the client:** walk through each process in a workshop and agree the happy path and the reject path.

### B4. Calculation formulas

- **% match**: how well an employee fits a job.
  - Example: the job needs a degree, 5 years of experience and 4 competencies (skills).
  - Nimal has the degree, 6 years, and 3 of the 4 skills. Is that 75%? 90%?
  - The answer depends on **weights**, e.g. qualification 30%, experience 30%, skills 40%.
- **Bench strength**: how well covered a critical job is.
  - Example: Finance Head has 1 "Ready now" and 2 "Ready in 2 years" successors. Is that Strong, Medium or Weak?
- **IDP progress %**: how much of the development plan is done.
  - Example: 2 of 4 actions done = 50%. Or should a big course count more than a small one?
- **Why it blocks us:** these numbers show on the main screens. If HR disagrees with them, users stop trusting the system.
- **Ask the client:** the formula and weights. Then test them on 5–10 real employees.

### B5. Who can see what (data visibility)

- **What it is:** talent data is very **sensitive**.
  - If an employee sees "low potential", they may lose motivation or leave.
  - If a colleague sees who is the boss's successor, it causes conflict.
- **RBAC** (role-based access control) means each role sees only what it is allowed to see.
- **Example rule:** "Employees never see their nine-box box. Supervisors see their own team only. HR sees everyone."
- **Why it blocks us:** security is designed in at the start. Adding it later means rework on every screen and report.
- **Ask the client:** for each role (employee, supervisor, board, HR), what can they see and edit?

---

## B. Technical design and build

### B6. Interface specs and test access

- **What it is**
  - The Talent module does **not own** employee data. It reads data from other systems.
  - An **interface** is a connection between 2 systems.
  - A **spec** is the instruction manual for that connection: address, login, fields, format.
- **Systems we must connect to**
  - **Core HR**: employees, departments, positions.
  - **SSHR** (self-service HR): qualifications and work history that employees enter.
  - **Performance module**: appraisal ratings and competencies.
  - **LMS** (learning system): courses and course progress.
  - **SSO** (single sign-on): users log in with their normal company account.
- **Why it blocks us:** without data, most screens are empty. Without test access, we cannot test anything.
- **Ask the client:** the spec, a test environment and login details for each system (checklist in §4.12.2).

### B7. Hosting, platform version, environments, remote access

- **Hosting**: where the software runs. The client's own servers, their cloud, or ours?
- **Platform version**: which version of the HR product. Talent must match Core HR.
- **Environments**: separate copies of the system, used for different jobs.
  - **DEV**: developers build here.
  - **SIT** (system integration testing): we test that all parts work together.
  - **UAT** (user acceptance testing): the client tests and accepts.
  - **PROD** (production): the live system real users work in.
- **Remote access**: how our team reaches the client's servers (e.g. VPN, a secure private connection).
- **Why it blocks us:** with no environment, the team has nowhere to build or test.
- **Ask the client:** hosting decision, versions, who sets up each environment, and VPN access.

### B8. Scope boundaries

- **What it is:** scope means what is in our contract and what is not.
- **Unclear items in the blueprint**
  - **Training & Development module**: it handles training requests and history. Is it ours to build or connect, or someone else's?
  - **SSHR**: the Career Centre shows resume data from SSHR. If SSHR is not live, that screen is empty.
- **Why it blocks us:** we cannot estimate effort or design the connections without knowing what we own.
- **Ask the client:** is each of these modules in our scope? Is it live, and with real data?

---

## C. Estimate and sign-off

### B9. Report templates

- **What it is:** the client wants reports in **their own layout**. They promised templates but have not sent them yet.
- **Reports named in the blueprint**
  - Monthly and quarterly **readiness reports**: how ready the backups are.
  - **Trend analysis**: how readiness changes over time.
  - **Role-wise analysis**: readiness per job.
- **Why it blocks us:** a simple table takes about a day. A complex report with charts and trends can take weeks. We cannot estimate without seeing them.
- **Ask the client:** the templates, with a sample of filled-in data.

### B10. Volumes

- **What it is:** how much data and how many users.
- **Example:** 500 employees and 50,000 employees need very different servers and screens.
  - A nine-box with 20 people is easy to read.
  - A nine-box with 5,000 people needs filters and paging.
- **Why it blocks us:** sizing, performance targets and some screen designs depend on it.
- **Ask the client:** number of employees, active users, people using it at the same time, critical positions.

### B11. Legacy data

- **What it is:** **legacy data** is old data the client already has, often in Excel. Examples: an existing successor list or past talent reviews.
- **Migration** means moving that old data into the new system.
- **Why it blocks us:** migration can be a large job (cleaning, mapping, loading, checking). If nobody plans for it, it becomes a surprise late in the project.
- **Ask the client:** does old data exist? Where, in what format, and how much? Must it be loaded before go-live?

### B12. Named owners and a decision maker

- **What it is:** a named person at the client with the **authority** to answer questions and make final calls.
- **Example:** we ask, "Can the board override a reviewer?" HR says ask IT. IT says ask the HR Head. The question stays open for 3 weeks.
- **Why it blocks us:** it blocks all the others. B1 to B11 need someone to decide.
- **Ask the client:** a business owner, an IT owner, a data owner and a final decision maker. Agree an answer time, e.g. 3 working days.

---

## How the blockers connect

```
B12 decision maker ─┬─▶ B1–B5 business rules ──▶ functional design ─┐
                    ├─▶ B6–B8 tech access    ──▶ technical design  ─┼─▶ estimate ─▶ sign-off
                    └─▶ B9–B11 scope data    ───────────────────────┘
```
