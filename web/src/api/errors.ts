export class ApiError extends Error {
  readonly status: number;
  /** Field messages from a 400 ValidationProblemDetails, keyed by field name. */
  readonly fieldErrors?: Record<string, string[]>;
  constructor(status: number, message: string, fieldErrors?: Record<string, string[]>) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
  get isUnauthenticated() {
    return this.status === 401;
  }
  get isForbidden() {
    return this.status === 403;
  }
  /** Someone else changed the record (optimistic concurrency). */
  get isConflict() {
    return this.status === 409;
  }
}
