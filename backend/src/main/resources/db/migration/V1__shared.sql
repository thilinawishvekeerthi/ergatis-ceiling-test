-- Shared kernel: append-only audit log (ADR-07) and Spring Modulith event publication registry = outbox (ADR-10).

create table audit_log (
    id                  bigserial primary key,
    occurred_at         timestamptz not null,
    actor_employee_id   bigint,
    action              varchar(80) not null,
    object_type         varchar(60) not null,
    object_id           varchar(200),
    subject_employee_id bigint,
    before_value        jsonb,
    after_value         jsonb,
    reason              text
);
create index ix_audit_object on audit_log (object_type, object_id);
create index ix_audit_subject on audit_log (subject_employee_id);

create function audit_log_append_only() returns trigger language plpgsql as $$
begin
    raise exception 'audit_log is append-only';
end $$;

create trigger trg_audit_log_append_only before update or delete on audit_log
    for each row execute function audit_log_append_only();

-- Outbox (ADR-10): Spring Modulith event publication registry. A row is written in the same transaction as the
-- business change and completed after each listener succeeds; incomplete rows are resubmitted on restart.
create table event_publication (
    id               uuid primary key,
    listener_id      text not null,
    event_type       text not null,
    serialized_event text not null,
    publication_date timestamptz not null,
    completion_date  timestamptz
);
create index ix_event_publication_incomplete on event_publication (completion_date) where completion_date is null;

create table event_publication_archive (
    id               uuid primary key,
    listener_id      text not null,
    event_type       text not null,
    serialized_event text not null,
    publication_date timestamptz not null,
    completion_date  timestamptz
);
