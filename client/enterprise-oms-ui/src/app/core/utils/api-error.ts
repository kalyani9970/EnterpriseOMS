import { HttpErrorResponse } from '@angular/common/http';

export interface ApiError {
  status: number;
  /** One human-readable sentence suitable for an alert banner. */
  message: string;
  /** Per-field messages from the API's validation errors, keyed by camelCase field name. */
  fieldErrors: Record<string, string[]>;
}

/** Shape of the ProblemDetails / ValidationProblemDetails JSON the API returns for errors. */
interface ProblemDetailsBody {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/**
 * Turns any error thrown by HttpClient into one predictable shape, so components never
 * have to know about status codes or the server's JSON format.
 */
export function parseApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, message: 'Something went wrong. Please try again.', fieldErrors: {} };
  }

  if (error.status === 0) {
    return {
      status: 0,
      message: 'Cannot reach the server. Check your connection and try again.',
      fieldErrors: {},
    };
  }

  const body: ProblemDetailsBody =
    typeof error.error === 'object' && error.error !== null ? (error.error as ProblemDetailsBody) : {};

  const fieldErrors = camelCaseKeys(body.errors);
  const firstFieldMessage = Object.values(fieldErrors).flat()[0];

  switch (error.status) {
    case 400:
      return {
        status: 400,
        message: firstFieldMessage ?? body.title ?? 'The request was not valid.',
        fieldErrors,
      };
    case 401:
      return { status: 401, message: 'Your session has expired. Please sign in again.', fieldErrors };
    case 403:
      return { status: 403, message: 'You do not have permission to do that.', fieldErrors };
    case 404:
      return { status: 404, message: body.detail ?? 'The requested item was not found.', fieldErrors };
    case 409:
    case 422:
      return {
        status: error.status,
        message: body.detail ?? body.title ?? 'The request conflicts with the current data.',
        fieldErrors,
      };
    default:
      return {
        status: error.status,
        message:
          error.status >= 500
            ? 'The server ran into a problem. Please try again later.'
            : (body.title ?? 'The request failed.'),
        fieldErrors,
      };
  }
}

/**
 * The API names fields the .NET way ("Email", "Address.CountryCode"). The client uses camelCase
 * ("email", "address.countryCode"), so every dot-separated part gets a lower-case first letter.
 */
function camelCaseKeys(errors: Record<string, string[]> | undefined): Record<string, string[]> {
  const result: Record<string, string[]> = {};

  for (const [key, messages] of Object.entries(errors ?? {})) {
    const camelKey = key
      .split('.')
      .map((part) => part.charAt(0).toLowerCase() + part.slice(1))
      .join('.');

    result[camelKey] = messages;
  }

  return result;
}