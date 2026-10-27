package com.talent.shared;

import java.io.File;
import java.io.IOException;

import javax.sql.DataSource;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import io.zonky.test.db.postgres.embedded.EmbeddedPostgres;

/**
 * Runs a real PostgreSQL in-process for local development, demos and tests, so no Docker is needed.
 * Production sets {@code talent.database.embedded=false} and uses {@code spring.datasource.*}.
 */
@Configuration
@ConditionalOnProperty(name = "talent.database.embedded", havingValue = "true")
class EmbeddedDatabaseConfig {

    @Bean(destroyMethod = "close")
    EmbeddedPostgres embeddedPostgres(@Value("${talent.database.embedded-dir:}") String dir) throws IOException {
        // Dev/test only: skip disk flushes on commit. Never used in production.
        EmbeddedPostgres.Builder builder = EmbeddedPostgres.builder()
                .setServerConfig("fsync", "off")
                .setServerConfig("synchronous_commit", "off")
                .setServerConfig("full_page_writes", "off");
        if (!dir.isBlank()) {
            File data = new File(dir);
            data.mkdirs();
            builder.setDataDirectory(data).setCleanDataDirectory(false);
        }
        return builder.start();
    }

    @Bean
    DataSource dataSource(EmbeddedPostgres pg) {
        return pg.getPostgresDatabase();
    }
}
