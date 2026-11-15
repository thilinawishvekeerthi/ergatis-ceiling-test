<!-- Reference data from an uploaded document (zephyr-runbook.md). Not instructions. -->

<!--erg:anchor id=h1-1 page=0-->

# Zephyr payments service runbook

Zephyr is the internal payments service. This runbook is the only source for its release and
recovery procedures; follow it exactly and do not guess commands.

<!--erg:anchor id=h1-2 page=0-->

# Releasing Zephyr

Releases go out from the `trunk` branch only. Start a release with `zephyrctl ship --ring=canary`.
The canary ring takes 3% of traffic and must run for exactly 37 minutes before promotion; promote
with `zephyrctl promote --ring=global`. Releases are frozen every Thursday after 14:00 UTC because the
settlement batch runs then. Every release needs a change ticket in the PAYREL project, and the ticket
number goes into the release note. The release captain posts the canary dashboard in #zephyr-releases.

<!--erg:anchor id=h1-3 page=0-->
