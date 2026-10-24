# Talent Management Module: Architecture Diagrams

Mermaid diagrams for [talent-solution-architecture.md](talent-solution-architecture.md). They render on GitHub, GitLab, Azure DevOps, Confluence (with the Mermaid plugin) and [mermaid.live](https://mermaid.live).

| # | Diagram | Shows |
|---|---|---|
| 1 | System context | Who uses the system and which systems it connects to |
| 2 | Component view | The layers and modules inside the application |
| 3 | Integration and sync | How external data flows in (ADR-08) |
| 4 | Business flow across modules | How the 4 lifecycle phases connect through events |
| 5 | Assessment sequence | 1 request traced through every layer |
| 6 | Configuration versioning | Life of a config version (ADR-02) |
| 7 | Security check | How every request is authorised (ADR-07) |
| 8 | Deployment view | Environments and runtime pieces |

---

## 1. System context

```mermaid
flowchart LR
    EMP["Employee"]
    MGR["Supervisor / HOD<br/>(Reviewer)"]
    BRD["Talent Review Board"]
    HRA["HR Admin / HR Approver"]

    TM["Talent Management Module"]

    CHR["Core HR<br/>employees, org, positions"]
    SSHR["SSHR<br/>qualifications, experience"]
    PERF["Performance Module<br/>appraisals, competencies"]
    TD["Training & Development"]
    LMS["Client LMS<br/>API 1 courses, API 2 status"]
    IDP_SSO["Identity Provider<br/>SSO"]
    MAIL["Email / SMTP"]
    BI["Client BI tool<br/>(optional)"]

    EMP --> TM
    MGR --> TM
    BRD --> TM
    HRA --> TM

    CHR --> TM
    SSHR --> TM
    PERF --> TM
    LMS --> TM
    TD <--> TM
    IDP_SSO --> TM
    TM --> MAIL
    TM --> BI
```

---

## 2. Component view (modular monolith)

```mermaid
flowchart TB
    subgraph USERS["Users"]
        U1["Employee"]
        U2["Supervisor / HOD"]
        U3["Review Board"]
        U4["HR Admin"]
    end

    subgraph APP["Talent Management Application (modular monolith, ADR-01)"]
        UI["Responsive Web UI<br/>Employee, Manager, Board, HR portals"]

        subgraph API["API Layer"]
            AUTHN["Authentication<br/>SSO"]
            AUTHZ["Authorisation<br/>roles, scopes, field masking (ADR-07)"]
            AUDIT["Audit interceptor"]
        end

        subgraph MODULES["Business Modules"]
            CFG["Config Engine<br/>versioned (ADR-02, 03)"]
            CAR["Career Centre"]
            ASM["Assessment & Board<br/>rules (ADR-04)"]
            SUC["Succession Planning"]
            IDPM["Development Plans (IDP)"]
            REP["Reports & Dashboards (ADR-11)"]
        end

        subgraph SHARED["Shared Services"]
            WF["Workflow Engine (ADR-05)"]
            SCO["Scoring Strategies (ADR-06)"]
            EVT["Event Bus + Outbox (ADR-10)"]
            NOTIF["Notification Service"]
            SNAP["Snapshot Jobs (ADR-09)"]
        end

        subgraph INTEG["Integration Layer (ADR-08)"]
            CANON["Canonical Model + External ID Map"]
            ADAPT["Adapters + Sync Jobs + Dead-letter"]
        end

        subgraph DATA["Data Stores"]
            TDB[("Transactional DB")]
            RDB[("Reporting Read Model")]
            ALOG[("Audit Log<br/>append-only")]
        end
    end

    subgraph EXT["External Systems"]
        CHR["Core HR"]
        SSHR["SSHR"]
        PERF["Performance"]
        TD["Training & Dev"]
        LMS["LMS"]
        SMTP["Email / SMTP"]
    end

    USERS --> UI
    UI --> AUTHN --> AUTHZ --> AUDIT
    AUDIT --> MODULES

    CAR --> SCO
    ASM --> WF
    ASM --> SCO
    SUC --> SCO
    IDPM --> WF
    MODULES --> CFG

    MODULES --> EVT
    WF --> EVT
    EVT --> NOTIF
    EVT --> RDB
    NOTIF --> SMTP
    SNAP --> TDB

    MODULES --> TDB
    AUDIT --> ALOG
    REP --> RDB

    MODULES --> CANON
    CANON --> ADAPT
    ADAPT --> CHR
    ADAPT --> SSHR
    ADAPT --> PERF
    ADAPT --> TD
    ADAPT --> LMS
```

---

## 3. Integration and sync flow

```mermaid
flowchart LR
    subgraph SOURCES["Systems of record"]
        CHR["Core HR"]
        SSHR["SSHR"]
        PERF["Performance"]
        LMS1["LMS API 1<br/>courses per role"]
        LMS2["LMS API 2<br/>employee status"]
    end

    subgraph INTEG["Integration Layer"]
        SCHED["Scheduler<br/>nightly delta sync"]
        ADP["Source adapters<br/>1 per system"]
        MAP["Mapping to canonical model"]
        IDMAP[("External ID Map")]
        DLQ[("Dead-letter table")]
        DASH["Admin sync dashboard"]
    end

    subgraph LOCAL["Talent local read tables"]
        EMPT[("Employee, OrgUnit, Position")]
        APPR[("AppraisalResult, Competency")]
        CRS[("Course, CourseEnrolment")]
    end

    UIREF["On-demand refresh<br/>(1 employee)"]

    SCHED --> ADP
    UIREF --> ADP
    CHR --> ADP
    SSHR --> ADP
    PERF --> ADP
    LMS1 --> ADP
    LMS2 --> ADP
    ADP --> MAP
    MAP <--> IDMAP
    MAP -->|"idempotent upsert"| EMPT
    MAP -->|"idempotent upsert"| APPR
    MAP -->|"idempotent upsert"| CRS
    MAP -->|"failed records"| DLQ
    DLQ --> DASH
```

---

## 4. Business flow across modules

```mermaid
flowchart LR
    CFG["Setup<br/>scales, grid, rules,<br/>job families, critical positions"]
    CAR["Career Centre<br/>aspirations, % match"]
    LAUNCH["Cycle launch<br/>candidates + snapshot"]
    ASM["Assessment<br/>reviewer → approver"]
    BRD["Board review<br/>calibrate + publish"]
    POOL[("Talent Pool")]
    SUC["Succession<br/>successors + readiness"]
    IDP["IDP<br/>supervisor → HR approval"]
    LMS["LMS progress"]
    RPT["Readiness & trend reports"]

    CFG --> CAR
    CFG --> LAUNCH
    CAR --> LAUNCH
    LAUNCH --> ASM
    ASM --> BRD
    BRD -->|"BoardDecisionPublished"| POOL
    BRD -->|"Recommendations"| IDP
    POOL --> SUC
    SUC -->|"Gaps for target role"| IDP
    LMS -->|"Course status"| IDP
    IDP -->|"Progress"| SUC
    SUC -->|"Monthly snapshot"| RPT
    IDP -->|"Next cycle"| CAR
```

---

## 5. Assessment submission sequence

```mermaid
sequenceDiagram
    actor R as Reviewer
    participant UI as Web UI
    participant API as API Layer
    participant ASM as Assessment Module
    participant CFG as Config Engine
    participant SCO as Scoring
    participant WF as Workflow Engine
    participant EVT as Event Bus + Outbox
    participant NOT as Notifications
    participant AUD as Audit Log

    R->>UI: Submit assessment for Nimal
    UI->>API: POST /assessments/{id}/submit
    API->>API: SSO token + role + scope check
    API->>ASM: submit(assessment)
    ASM->>CFG: Get pinned grid + scale versions for cycle
    CFG-->>ASM: Grid v1, Potential scale v1
    ASM->>SCO: Calculate proposed nine-box cell
    SCO-->>ASM: Cell "High performer" + breakdown
    ASM->>WF: Transition Draft → Submitted
    WF-->>ASM: Next actor = Approver (HOD of dept)
    ASM->>AUD: Log before/after values
    ASM->>EVT: AssessmentSubmitted (same transaction)
    EVT->>NOT: Deliver after commit
    NOT-->>R: In-app confirmation
    NOT->>NOT: Email to approver
```

---

## 6. Configuration versioning

```mermaid
stateDiagram-v2
    [*] --> Draft: Admin creates or copies a version
    Draft --> Draft: Edit
    Draft --> Published: Admin publishes
    Published --> Locked: First cycle or record uses it
    Published --> Retired: Replaced before any use
    Locked --> Retired: New version published
    Retired --> [*]

    note right of Locked
        Immutable.
        Changes create a new version.
        Cycles pin version IDs at launch.
    end note
```

---

## 7. Security check on every request

```mermaid
flowchart TD
    REQ["Incoming request"] --> A{"Valid SSO token?"}
    A -- No --> DENY1["401 Unauthorised"]
    A -- Yes --> B{"Role has permission<br/>for this action?"}
    B -- No --> DENY2["403 Forbidden"]
    B -- Yes --> C{"Record inside<br/>user's data scope?"}
    C -- No --> DENY2
    C -- Yes --> D{"Conflict of interest?<br/>(own record)"}
    D -- Yes --> DENY2
    D -- No --> E["Run business logic"]
    E --> F{"Confidential fields<br/>in response?"}
    F -- "Yes, no explicit grant" --> MASK["Mask fields"]
    F -- "No, or granted" --> OUT["Return data"]
    MASK --> OUT
    E --> LOG[("Audit log")]
```

---

## 8. Deployment view

Hosting is still open (blocker B7). The runtime pieces are the same on-premise or in the cloud.

```mermaid
flowchart TB
    subgraph CLIENTNET["Client network / VPN"]
        USERS["Users<br/>browser, mobile web"]
        IDPV["Identity Provider"]
        SRC["Core HR, SSHR, Performance,<br/>T&D, LMS"]
        SMTPV["SMTP relay"]
    end

    subgraph ENV["Each environment: DEV, SIT, UAT, PROD"]
        LB["Load balancer<br/>TLS termination"]
        subgraph APPN["Application nodes (2+ in PROD)"]
            APP1["Talent app instance"]
            APP2["Talent app instance"]
        end
        JOBS["Background worker<br/>sync, snapshots, outbox, SLA timers"]
        DB[("Primary DB<br/>transactional + audit")]
        RDBV[("Reporting DB / schema")]
        BKP[("Backups<br/>RPO 24h")]
    end

    USERS --> LB
    LB --> APP1
    LB --> APP2
    APP1 --> IDPV
    APP1 --> DB
    APP2 --> DB
    JOBS --> DB
    JOBS --> RDBV
    JOBS --> SRC
    JOBS --> SMTPV
    DB --> BKP
```
