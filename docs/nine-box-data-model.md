# Nine-box evaluation: data model

BRD "Nine-Box Talent Evaluation" §6. Schema `assessment`, migration `20261112183314_InitialAssessment`.

```
assessment.nine_box_evaluations              (insert-only history)
  id                uuid  PK  (UUID v7, time-ordered)
  employee_id       uuid      Talent employee id (Integration module)
  grid_version_id   uuid      Config grid version the labels came from
  performance_level smallint  CHECK 1..3
  potential_level   smallint  CHECK 1..3
  box_number        smallint  CHECK = (potential_level - 1) * 3 + performance_level
  box_label         varchar(100)
  talent_score      smallint  CHECK = performance_level * potential_level
  band              varchar(20)
  reviewer_id       varchar(200)  stable user id of the reviewer
  evaluated_at      timestamptz
  UNIQUE (id, employee_id)
  INDEX  (employee_id, evaluated_at DESC)       history, newest first

assessment.latest_nine_box_placements        (one row per evaluated employee)
  employee_id       uuid  PK
  evaluation_id     uuid
  evaluated_at      timestamptz
  FK (evaluation_id, employee_id) -> nine_box_evaluations (id, employee_id)
```

## Note for the PR

Each evaluation is an immutable row in `assessment.nine_box_evaluations` holding the inputs (performance, potential), the employee, the reviewer, the time and the grid config version it used, so re-evaluating never loses history. The derived box, label, score and band are stored on purpose: they are a snapshot of what the reviewer saw under that pinned grid version (ADR-02), so history stays correct after the client publishes new labels. The cost is redundancy, which CHECK constraints control: the database rejects any row whose box or score does not match its inputs. The latest placement lives in a small pointer table keyed by employee (`latest_nine_box_placements`), so "latest for these employees" is a primary-key lookup whose cost does not grow with history; the composite foreign key `(evaluation_id, employee_id)` guarantees a pointer can only name an evaluation of the same employee. The pointer is updated in the same transaction with one `INSERT … ON CONFLICT … WHERE evaluated_at <= EXCLUDED.evaluated_at`, so concurrent or late saves cannot leave an older placement as "latest". The alternative, `DISTINCT ON (employee_id)` over the history index, needs no extra table but scans more rows as history grows. Employees are not copied: rows store the Talent employee id from the Integration module, and the service checks it exists through `IEmployeeReader`. A cross-schema foreign key to `integration.employees` would break the module boundary rule (ADR-01), so that integrity is enforced in the service layer instead. Labels, colours, bands and rubrics are versioned Config data (`TALENT_NINE_BOX`, `TALENT_PERFORMANCE_3`, `TALENT_POTENTIAL_3`), not code. The BRD formulas for box and score stay in a pure function.

## Query cost of the list screen

The employee list makes 4 queries per page, whatever the page size: employees, their positions, their units, and their latest evaluations (pointer joined to history). There is no query per row.
