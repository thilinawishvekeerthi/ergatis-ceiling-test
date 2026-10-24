package com.talent.integration.internal;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.context.event.ApplicationReadyEvent;
import org.springframework.context.event.EventListener;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

@Component
class SyncScheduler {

    private static final Logger log = LoggerFactory.getLogger(SyncScheduler.class);

    private final SyncService sync;
    private final IntegrationProperties props;

    SyncScheduler(SyncService sync, IntegrationProperties props) {
        this.sync = sync;
        this.props = props;
    }

    @Scheduled(cron = "${talent.integration.sync-cron:0 0 1 * * *}")
    void nightly() {
        sync.syncAll("SCHEDULED").forEach(r -> log.info("Sync {}: {} read, {} loaded, {} failed ({})",
                r.system(), r.read(), r.upserted(), r.failed(), r.status()));
    }

    @EventListener(ApplicationReadyEvent.class)
    void onStartup() {
        if (props.syncOnStartup()) {
            nightly();
        }
    }
}
