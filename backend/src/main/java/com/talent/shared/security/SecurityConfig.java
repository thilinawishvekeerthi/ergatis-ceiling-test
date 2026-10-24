package com.talent.shared.security;

import java.time.Clock;
import java.util.Collection;
import java.util.List;
import java.util.Set;
import java.util.stream.Collectors;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.core.convert.converter.Converter;
import org.springframework.security.authentication.AbstractAuthenticationToken;
import org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.oauth2.jose.jws.MacAlgorithm;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.security.oauth2.jwt.JwtDecoders;
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder;
import org.springframework.security.oauth2.server.resource.InvalidBearerTokenException;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.util.matcher.RegexRequestMatcher;
import org.springframework.http.HttpMethod;
import org.springframework.boot.autoconfigure.security.oauth2.resource.OAuth2ResourceServerProperties;

/** Authentication (SSO via OIDC bearer tokens). Authorisation is done in the service layer (ADR-07). */
@Configuration
@EnableMethodSecurity
class SecurityConfig {

    @Bean
    SecurityFilterChain filterChain(HttpSecurity http, Converter<Jwt, AbstractAuthenticationToken> converter) throws Exception {
        http.csrf(c -> c.disable())
                .sessionManagement(s -> s.sessionCreationPolicy(SessionCreationPolicy.STATELESS))
                .headers(h -> h.contentSecurityPolicy(csp -> csp.policyDirectives(
                        "default-src 'self'; img-src 'self' data: https:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'")))
                .authorizeHttpRequests(a -> a
                        .requestMatchers("/api/auth/**", "/actuator/health", "/actuator/health/**", "/v3/api-docs/**", "/swagger-ui/**", "/swagger-ui.html").permitAll()
                        // Static web UI only; everything else is denied unless listed here.
                        .requestMatchers("/", "/index.html", "/assets/**", "/favicon.ico", "/favicon.svg", "/error").permitAll()
                        // Client-side routes of the web UI (forwarded to index.html); no data is served on these paths.
                        .requestMatchers(RegexRequestMatcher.regexMatcher(HttpMethod.GET, "^/(?!api/|actuator|v3/|swagger-ui)[^.]*$")).permitAll()
                        .requestMatchers("/api/**").authenticated()
                        .anyRequest().denyAll())
                .oauth2ResourceServer(o -> o.jwt(j -> j.jwtAuthenticationConverter(converter)));
        return http.build();
    }

    @Bean
    JwtDecoder jwtDecoder(SecurityProperties props, OAuth2ResourceServerProperties rs) {
        if (props.devMode()) {
            return NimbusJwtDecoder.withSecretKey(DevTokenIssuer.key(props.devSecret())).macAlgorithm(MacAlgorithm.HS256).build();
        }
        return JwtDecoders.fromIssuerLocation(rs.getJwt().getIssuerUri());
    }

    @Bean
    DevTokenIssuer devTokenIssuer(SecurityProperties props, Clock clock) {
        return props.devMode() ? new DevTokenIssuer(props.devSecret(), clock) : null;
    }

    @Bean
    Converter<Jwt, AbstractAuthenticationToken> jwtConverter(SecurityProperties props, EmployeeIdentityResolver resolver) {
        return jwt -> {
            Set<String> roles = rolesOf(jwt.getClaim(props.rolesClaim()));
            Object employeeId = jwt.getClaim("employee_id");
            EmployeeIdentityResolver.ResolvedIdentity id;
            if (props.devMode() && employeeId != null) {
                id = resolver.byEmployeeId(((Number) employeeId).longValue())
                        .orElseThrow(() -> new InvalidBearerTokenException("Unknown employee"));
            } else {
                String login = jwt.getClaimAsString(props.loginClaim());
                id = resolver.resolve(login).orElseThrow(() -> new InvalidBearerTokenException("No employee record for " + login));
            }
            return new TalentAuthentication(new CurrentUser(id.employeeId(), id.username(), id.displayName(), roles), jwt);
        };
    }

    private static Set<String> rolesOf(Object claim) {
        if (claim instanceof Collection<?> c) {
            return c.stream().map(Object::toString).map(String::toUpperCase).collect(Collectors.toUnmodifiableSet());
        }
        if (claim instanceof String s && !s.isBlank()) {
            return Set.copyOf(List.of(s.toUpperCase().split("[ ,]+")));
        }
        return Set.of();
    }
}
