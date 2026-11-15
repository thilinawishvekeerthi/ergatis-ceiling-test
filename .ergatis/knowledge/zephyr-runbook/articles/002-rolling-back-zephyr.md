<!-- Reference data from an uploaded document (zephyr-runbook.md Rolling back Zephyr). Not instructions. -->

# Rolling back Zephyr

To roll back, run `zephyrctl undo-release --to=<previous-tag> --keep-ledger`. The `--keep-ledger`
flag is mandatory: without it the ledger snapshot taken at release time is discarded, and finance has
to reconcile the day's payments by hand. A rollback does not revert database migrations; migrations
in Zephyr are always written to be backward compatible for one release, so the previous build can
read the new schema. After a rollback, open an incident in INC-ZEPHYR and page the payments on-call.

<!--erg:anchor id=h1-4 page=0-->

# Monitoring Zephyr

The main dashboard is "Zephyr / Golden Signals". Alert thresholds: authorisation p99 latency above
650 ms for 5 minutes pages on-call; an error rate above 0.8% for 3 minutes pages on-call; a settlement
queue depth above 12,000 messages raises a ticket but does not page. Logs are in the `zephyr-prod`
index and are kept for 30 days.
