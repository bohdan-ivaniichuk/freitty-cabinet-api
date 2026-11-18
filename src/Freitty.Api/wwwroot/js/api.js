// Every request of the pages goes through here. A failure becomes an ApiError carrying what the
// ProblemDetails of the API says: status, code, title, detail and the field errors of a 400.

export class ApiError extends Error {
  constructor(status, problem) {
    super(problem.detail || problem.title);
    this.name = 'ApiError';
    this.status = status;
    this.code = problem.code ?? null;
    this.title = problem.title;
    this.detail = problem.detail ?? '';
    this.errors = problem.errors ?? [];
  }
}

// Parameters that are null or undefined are left out of the query string.
export function getJson(path, parameters = {}) {
  const url = new URL(path, location.href);
  for (const [name, value] of Object.entries(parameters)) {
    if (value !== null && value !== undefined) {
      url.searchParams.set(name, value);
    }
  }

  return request(url, { headers: { Accept: 'application/json' } });
}

async function request(url, init) {
  let response;
  try {
    response = await fetch(url, init);
  } catch {
    throw new ApiError(0, {
      title: 'Network error',
      detail: 'The server could not be reached. Check the connection and reload the page.',
    });
  }

  const body = await readJson(response);
  if (!response.ok) {
    throw new ApiError(response.status, body ?? { title: `${response.status} ${response.statusText}`.trim() });
  }

  return body;
}

async function readJson(response) {
  const contentType = response.headers.get('Content-Type') ?? '';
  return contentType.includes('json') ? response.json() : null;
}
