-- Workflow engine (ADR-05).

create table workflow_instance (
    id                    bigserial primary key,
    definition_code       varchar(80) not null,
    definition_version_id bigint not null references config_version (id),
    object_type           varchar(40) not null,
    object_id             bigint not null,
    subject_employee_id   bigint references employee (id),
    context_id            bigint,
    state                 varchar(60) not null,
    status                varchar(20) not null check (status in ('ACTIVE', 'ENDED', 'CANCELLED')),
    started_at            timestamptz not null,
    updated_at            timestamptz not null,
    row_version           bigint not null default 0
);
create unique index ux_workflow_object on workflow_instance (object_type, object_id) where status <> 'CANCELLED';
create index ix_workflow_context on workflow_instance (context_id);

create table workflow_task (
    id                   bigserial primary key,
    instance_id          bigint not null references workflow_instance (id),
    state                varchar(60) not null,
    assignee_employee_id bigint not null references employee (id),
    original_assignee_id bigint references employee (id),
    status               varchar(20) not null check (status in ('OPEN', 'DONE', 'CANCELLED')),
    created_at           timestamptz not null,
    due_at               timestamptz,
    completed_at         timestamptz,
    completed_by         bigint,
    action               varchar(60),
    reminder_sent        boolean not null default false,
    escalated            boolean not null default false
);
create index ix_task_assignee on workflow_task (assignee_employee_id, status);
create index ix_task_instance on workflow_task (instance_id, status);

create table workflow_history (
    id                bigserial primary key,
    instance_id       bigint not null references workflow_instance (id),
    from_state        varchar(60),
    action            varchar(60) not null,
    to_state          varchar(60),
    actor_employee_id bigint,
    comment           text,
    at                timestamptz not null
);
create index ix_history_instance on workflow_history (instance_id);

create table delegation (
    id           bigserial primary key,
    delegator_id bigint not null references employee (id),
    delegate_id  bigint not null references employee (id),
    valid_from   date not null,
    valid_to     date not null,
    reason       text,
    created_at   timestamptz not null,
    check (delegator_id <> delegate_id)
);
create index ix_delegation_delegate on delegation (delegate_id, valid_from, valid_to);
