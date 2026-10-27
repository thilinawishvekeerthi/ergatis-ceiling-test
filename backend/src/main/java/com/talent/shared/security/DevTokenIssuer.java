package com.talent.shared.security;

import java.nio.charset.StandardCharsets;
import java.time.Clock;
import java.time.Duration;
import java.util.Collection;

import javax.crypto.spec.SecretKeySpec;

import org.springframework.security.oauth2.jose.jws.MacAlgorithm;
import org.springframework.security.oauth2.jwt.JwsHeader;
import org.springframework.security.oauth2.jwt.JwtClaimsSet;
import org.springframework.security.oauth2.jwt.JwtEncoderParameters;
import org.springframework.security.oauth2.jwt.NimbusJwtEncoder;

import com.nimbusds.jose.jwk.source.ImmutableSecret;

/** Signs tokens in dev mode only. Never used when {@code talent.security.mode=oidc}. */
public class DevTokenIssuer {

    private final NimbusJwtEncoder encoder;
    private final Clock clock;

    public DevTokenIssuer(String secret, Clock clock) {
        this.encoder = new NimbusJwtEncoder(new ImmutableSecret<>(key(secret)));
        this.clock = clock;
    }

    static SecretKeySpec key(String secret) {
        if (secret == null || secret.length() < 32) {
            throw new IllegalStateException("talent.security.dev-secret must be at least 32 characters");
        }
        return new SecretKeySpec(secret.getBytes(StandardCharsets.UTF_8), "HmacSHA256");
    }

    public String issue(Long employeeId, String username, String displayName, Collection<String> roles) {
        var now = clock.instant();
        JwtClaimsSet claims = JwtClaimsSet.builder()
                .issuer("talent-dev")
                .subject(username)
                .issuedAt(now)
                .expiresAt(now.plus(Duration.ofHours(12)))
                .claim("employee_id", employeeId)
                .claim("name", displayName)
                .claim("roles", roles)
                .build();
        return encoder.encode(JwtEncoderParameters.from(JwsHeader.with(MacAlgorithm.HS256).build(), claims)).getTokenValue();
    }
}
