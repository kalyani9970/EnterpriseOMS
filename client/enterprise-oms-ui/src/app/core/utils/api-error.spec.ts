import { HttpErrorResponse } from '@angular/common/http';
import { parseApiError } from './api-error';

const httpError = (status: number, body: unknown = null) =>
  new HttpErrorResponse({ status, statusText: 'x', error: body });

describe('parseApiError', () => {
  it('handles errors that did not come from HTTP', () => {
    const result = parseApiError(new Error('boom'));

    expect(result.status).toBe(0);
    expect(result.message).toBe('Something went wrong. Please try again.');
  });

  it('explains a network failure (status 0)', () => {
    expect(parseApiError(httpError(0)).message).toBe(
      'Cannot reach the server. Check your connection and try again.',
    );
  });

  it('uses the first field error as the 400 message and camelCases the keys', () => {
    const result = parseApiError(
      httpError(400, {
        title: 'One or more validation errors occurred.',
        errors: { Email: ['This email is already registered.'], Password: ['Too weak.'] },
      }),
    );

    expect(result.message).toBe('This email is already registered.');
    expect(result.fieldErrors).toEqual({
      email: ['This email is already registered.'],
      password: ['Too weak.'],
    });
  });

  it('falls back to the title for a 400 without field errors', () => {
    expect(parseApiError(httpError(400, { title: 'Bad thing.' })).message).toBe('Bad thing.');
  });

  it('maps 401, 403 and 404 to friendly messages', () => {
    expect(parseApiError(httpError(401)).message).toBe('Your session has expired. Please sign in again.');
    expect(parseApiError(httpError(403)).message).toBe('You do not have permission to do that.');
    expect(parseApiError(httpError(404, { detail: 'Role 5 was not found.' })).message).toBe('Role 5 was not found.');
    expect(parseApiError(httpError(404)).message).toBe('The requested item was not found.');
  });

  it('uses the detail for 409 and 422 business-rule errors', () => {
    expect(parseApiError(httpError(422, { detail: 'Stock cannot go negative.' })).message).toBe(
      'Stock cannot go negative.',
    );
    expect(parseApiError(httpError(409, { detail: 'Modified by another user.' })).message).toBe(
      'Modified by another user.',
    );
  });

  it('never leaks server internals for 5xx', () => {
    const result = parseApiError(httpError(500, { detail: 'System.NullReferenceException at ...' }));

    expect(result.message).toBe('The server ran into a problem. Please try again later.');
  });

  it('copes with a non-JSON error body', () => {
    expect(parseApiError(httpError(400, '<html>oops</html>')).message).toBe('The request was not valid.');
  });
});