-- Access control (ADR-07): global role assignments maintained in Talent.

create table role_assignment (
    id          bigserial primary key,
    employee_id bigint not null references employee (id),
    role        varchar(40) not null,
    source      varchar(20) not null,
    valid_from  date,
    valid_to    date,
    unique (employee_id, role)
);
