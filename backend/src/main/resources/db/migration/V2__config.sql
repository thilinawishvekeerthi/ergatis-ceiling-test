-- Config engine (ADR-02, ADR-03).

create table config_definition (
    id          bigserial primary key,
    type        varchar(40)  not null,
    code        varchar(80)  not null,
    name        varchar(200) not null,
    description text,
    unique (type, code)
);

create table config_version (
    id            bigserial primary key,
    definition_id bigint not null references config_definition (id),
    version_no    int    not null,
    status        varchar(20) not null check (status in ('DRAFT', 'PUBLISHED', 'LOCKED', 'RETIRED')),
    effective_from date,
    payload_json  jsonb  not null,
    checksum      varchar(64),
    created_by    bigint,
    created_at    timestamptz not null,
    published_by  bigint,
    published_at  timestamptz,
    locked_at     timestamptz,
    retired_at    timestamptz,
    row_version   bigint not null default 0,
    unique (definition_id, version_no)
);
-- At most one effective version and one draft per definition.
create unique index ux_config_effective on config_version (definition_id) where status in ('PUBLISHED', 'LOCKED');
create unique index ux_config_draft on config_version (definition_id) where status = 'DRAFT';

-- Locked versions are immutable, enforced in the database too.
create function config_version_immutable() returns trigger language plpgsql as $$
begin
    if old.status in ('LOCKED', 'RETIRED') and new.payload_json is distinct from old.payload_json then
        raise exception 'config version % is % and cannot change', old.id, old.status;
    end if;
    return new;
end $$;
create trigger trg_config_version_immutable before update on config_version
    for each row execute function config_version_immutable();

-- Typed copy of scale levels for reports (ADR-02 payload storage).
create table scale_level (
    id             bigserial primary key,
    version_id     bigint not null references config_version (id),
    scale_code     varchar(80) not null,
    level_value    int not null,
    code           varchar(80) not null,
    label          varchar(200) not null,
    colour         varchar(20),
    reporting_band varchar(80) not null,
    unique (version_id, level_value)
);
