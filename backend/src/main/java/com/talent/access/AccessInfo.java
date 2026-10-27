package com.talent.access;

import java.util.Map;
import java.util.Set;

/** What the UI needs to show or hide menus. The server still checks every request. */
public record AccessInfo(Long employeeId, String displayName, Set<String> roles, Map<String, Set<String>> permissions) {
}
