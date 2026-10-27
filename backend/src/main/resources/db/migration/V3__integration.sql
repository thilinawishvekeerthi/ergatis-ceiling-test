-- Integration layer (ADR-08): local read tables of the canonical model, External ID Map, sync log, dead letters.

create table org_unit (
    id               bigserial primary key,
    code             varchar(80) not null unique,
    name             varchar(200) not null,
    type             varchar(40),
    parent_id        bigint references org_unit (id),
    head_employee_id bigint,
    cost_centre      varchar(80),
    location         varchar(120),
    country          varchar(80),
    synced_at        timestamptz
);

create table job (
    id         bigserial primary key,
    code       varchar(80) not null unique,
    title      varchar(200) not null,
    grade_code varchar(40),
    synced_at  timestamptz
);

create table position (
    id                    bigserial primary key,
    code                  varchar(80) not null unique,
    title                 varchar(200) not null,
    position_type         varchar(60),
    org_unit_id           bigint not null references org_unit (id),
    job_id                bigint references job (id),
    incumbent_employee_id bigint,
    cost_centre           varchar(80),
    location              varchar(120),
    country               varchar(80),
    work_station          varchar(120),
    synced_at             timestamptz
);

create table employee (
    id                  bigserial primary key,
    employee_no         varchar(40) not null unique,
    first_name          varchar(100),
    last_name           varchar(100),
    display_name        varchar(200) not null,
    email               varchar(200),
    picture_url         varchar(500),
    grade_code          varchar(40),
    job_id              bigint references job (id),
    position_id         bigint references position (id),
    org_unit_id         bigint references org_unit (id),
    supervisor_id       bigint references employee (id),
    status              varchar(20) not null,
    join_date           date,
    employment_category varchar(40),
    location            varchar(120),
    work_station        varchar(120),
    country             varchar(80),
    cost_centre         varchar(80),
    synced_at           timestamptz
);
create index ix_employee_supervisor on employee (supervisor_id);
create index ix_employee_org on employee (org_unit_id);
create index ix_employee_email on employee (lower(email));

alter table org_unit add constraint fk_org_head foreign key (head_employee_id) references employee (id);
alter table position add constraint fk_position_incumbent foreign key (incumbent_employee_id) references employee (id);

create table competency (
    id          bigserial primary key,
    code        varchar(80) not null unique,
    name        varchar(200) not null,
    category    varchar(80),
    description text,
    synced_at   timestamptz
);

create table appraisal_result (
    id                bigserial primary key,
    employee_id       bigint not null references employee (id),
    cycle_code        varchar(80) not null,
    period_end        date,
    final_score       double precision,
    rating_band       varchar(40),
    finalised         boolean not null default false,
    competency_scores jsonb,
    synced_at         timestamptz
);
create index ix_appraisal_employee on appraisal_result (employee_id, period_end desc);

create table qualification (
    id             bigserial primary key,
    employee_id    bigint not null references employee (id),
    type           varchar(60),
    title          varchar(200) not null,
    institution    varchar(200),
    level          int,
    year_completed int,
    synced_at      timestamptz
);
create index ix_qualification_employee on qualification (employee_id);

create table experience (
    id          bigserial primary key,
    employee_id bigint not null references employee (id),
    employer    varchar(200),
    title       varchar(200) not null,
    job_code    varchar(80),
    start_date  date,
    end_date    date,
    internal    boolean not null default false,
    synced_at   timestamptz
);
create index ix_experience_employee on experience (employee_id);

create table expertise (
    id              bigserial primary key,
    employee_id     bigint not null references employee (id),
    area            varchar(200) not null,
    competency_code varchar(80),
    proficiency     int,
    synced_at       timestamptz
);
create index ix_expertise_employee on expertise (employee_id);

create table course (
    id             bigserial primary key,
    external_id    varchar(120) not null unique,
    title          varchar(300) not null,
    url            varchar(500),
    provider       varchar(120),
    duration_hours double precision,
    synced_at      timestamptz
);

create table course_mapping (
    id           bigserial primary key,
    course_id    bigint not null references course (id) on delete cascade,
    mapping_type varchar(20) not null check (mapping_type in ('ROLE', 'COMPETENCY')),
    code         varchar(80) not null
);
create index ix_course_mapping on course_mapping (mapping_type, code);

create table course_enrolment (
    id               bigserial primary key,
    employee_id      bigint not null references employee (id),
    course_id        bigint not null references course (id),
    status           varchar(20) not null,
    assigned_on      date,
    completed_on     date,
    progress_percent int,
    score            double precision,
    synced_at        timestamptz
);
create index ix_enrolment_employee on course_enrolment (employee_id);

create table external_id_map (
    id          bigserial primary key,
    system      varchar(40) not null,
    entity      varchar(40) not null,
    external_id varchar(200) not null,
    internal_id bigint not null,
    unique (system, entity, external_id)
);

create table sync_run (
    id             bigserial primary key,
    system         varchar(40) not null,
    trigger        varchar(20) not null,
    started_at     timestamptz not null,
    finished_at    timestamptz,
    status         varchar(20) not null,
    read_count     bigint,
    upserted_count bigint,
    failed_count   bigint,
    message        text
);

create table sync_dead_letter (
    id              bigserial primary key,
    system          varchar(40) not null,
    entity          varchar(40) not null,
    external_id     varchar(200) not null,
    payload         jsonb not null,
    error           text not null,
    attempts        int not null,
    status          varchar(20) not null,
    first_failed_at timestamptz not null,
    last_failed_at  timestamptz not null,
    resolved_at     timestamptz
);
create unique index ux_dead_letter_open on sync_dead_letter (system, entity, external_id) where status = 'OPEN';
