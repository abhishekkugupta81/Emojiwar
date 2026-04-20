import { jsonResponse } from "../_shared/http.ts";

Deno.serve((_request) => {
  return jsonResponse({
    error: "submit_pick is not used in the 5v5 launch flow. Ranked and bot matches resolve a single auto-battle after bans and formation lock.",
  }, 410);
});
