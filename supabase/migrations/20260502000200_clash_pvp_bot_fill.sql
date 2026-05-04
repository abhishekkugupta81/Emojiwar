create table if not exists public.clash_bot_turns (
  id uuid primary key default gen_random_uuid(),
  match_id uuid not null references public.matches(id) on delete cascade,
  round_number integer not null check (round_number > 0),
  bot_profile_id text not null,
  emoji_id text not null,
  locked_at timestamptz not null default timezone('utc', now()),
  unique (match_id, round_number)
);

alter table public.clash_bot_turns enable row level security;

create index if not exists clash_bot_turns_match_turn_idx
  on public.clash_bot_turns (match_id, round_number);

create index if not exists matches_clash_bot_fill_active_idx
  on public.matches (player_a, status, updated_at desc)
  where mode = 'emoji_clash_pvp' and bot_profile_id is not null;
