/**
 * Notifications (ADR-10): subscribes to domain events, renders versioned templates (config type
 * NOTIFICATION_TEMPLATE) and delivers by email and in-app inbox.
 */
@org.springframework.modulith.ApplicationModule(displayName = "Notifications")
package com.talent.notification;
