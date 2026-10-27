package com.talent.shared.security;

import java.util.stream.Collectors;

import org.springframework.security.authentication.AbstractAuthenticationToken;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.oauth2.jwt.Jwt;

public class TalentAuthentication extends AbstractAuthenticationToken {

    private final CurrentUser user;
    private final Jwt jwt;

    public TalentAuthentication(CurrentUser user, Jwt jwt) {
        super(user.roles().stream().map(r -> new SimpleGrantedAuthority("ROLE_" + r)).collect(Collectors.toSet()));
        this.user = user;
        this.jwt = jwt;
        setAuthenticated(true);
    }

    @Override
    public Object getCredentials() {
        return jwt;
    }

    @Override
    public CurrentUser getPrincipal() {
        return user;
    }

    @Override
    public String getName() {
        return user.username();
    }
}
