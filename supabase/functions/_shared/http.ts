export async function readJson<T>(request: Request): Promise<T> {
  return await request.json() as T;
}

export function jsonResponse(payload: unknown, status = 200): Response {
  return new Response(JSON.stringify(payload, null, 2), {
    status,
    headers: {
      "Content-Type": "application/json",
    },
  });
}
