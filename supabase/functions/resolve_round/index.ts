import { jsonResponse } from "../_shared/http.ts";

Deno.serve((_request) => {
  return jsonResponse({
    error: "resolve_round is not used in the 5v5 launch flow. The server resolves the full auto-battle after blind bans and formation lock.",
  }, 410);
});
